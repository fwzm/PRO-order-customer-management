using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Common;
using PRO.Infrastructure.Persistence;
using PRO.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Net.Http;
using Microsoft.Win32;
using ClosedXML.Excel;
using Serilog;

namespace PRO.Desktop.ViewModels;

public partial class CustomerListViewModel : PagedViewModelBase
{
    private readonly ProDbContext _dbContext;
    private readonly ICustomerService _customerService;
    private readonly CustomerService _customerServiceImpl;
    private readonly DataMaskingService _maskingService;
    private readonly AuditService _auditService;

    protected override string EntityTypeName => "客户";

    [ObservableProperty]
    private ObservableCollection<CustomerListItem> _customers = [];

    [ObservableProperty]
    private CustomerListItem? _selectedCustomer;

    [ObservableProperty]
    private bool _showMajorOnly;

    [ObservableProperty]
    private CustomerType? _filterCustomerType;

    [ObservableProperty]
    private ObservableCollection<CustomerListItem> _duplicateCustomers = [];

    [ObservableProperty]
    private bool _showDuplicateWarning;

    // 批量选择
    [ObservableProperty]
    private ObservableCollection<object> _selectableCustomers = [];

    [ObservableProperty]
    private bool _isBatchMode;

    [ObservableProperty]
    private int _selectedCustomersCount;

    [ObservableProperty]
    private bool _isSelectAll;

    public CustomerListViewModel()
    {
        _dbContext = App.Services.GetService(typeof(ProDbContext)) as ProDbContext
            ?? throw new InvalidOperationException("无法获取数据库上下文");
        _customerService = App.Services.GetService(typeof(ICustomerService)) as ICustomerService
            ?? throw new InvalidOperationException("无法获取客户服务");
        _customerServiceImpl = App.Services.GetService(typeof(CustomerService)) as CustomerService
            ?? throw new InvalidOperationException("无法获取 CustomerService");
        _maskingService = App.Services.GetService(typeof(DataMaskingService)) as DataMaskingService
            ?? throw new InvalidOperationException("无法获取脱敏服务");
        _auditService = App.Services.GetService(typeof(AuditService)) as AuditService
            ?? throw new InvalidOperationException("无法获取审计服务");

        RunInBackground(LoadDataAsync(), "加载客户列表失败");
    }

    protected override bool HasActiveFilters() =>
        FilterCustomerType != null || ShowMajorOnly;

    [RelayCommand]
    private void ClearAllFilters()
    {
        FilterCustomerType = null;
        ShowMajorOnly = false;
        SearchKeyword = null;
        InvalidateCountCache();
        RunInBackground(ResetToFirstPageAndLoadAsync(), "清除筛选失败");
    }

    private ICommand? _clearFiltersCommand;
    protected override ICommand? ClearFiltersCommand => _clearFiltersCommand ??= new RelayCommand(ClearAllFilters);

    private ICommand? _createNewCommand;
    protected override ICommand? CreateNewCommand => _createNewCommand ??= new RelayCommand(NewCustomer);

    protected override async Task LoadDataAsync()
    {
        IsLoading = true;
        try
        {
            int? branchId = CurrentSession.Current.IsHeadquartersAdmin ? null : CurrentSession.CurrentBranchId;
            var request = new PagedRequest
            {
                PageIndex = PageIndex,
                PageSize = PageSize,
                Keyword = SearchKeyword
            };

            var result = await _customerService.GetListAsync(request,
                branchId: branchId,
                customerType: FilterCustomerType,
                showMajorOnly: ShowMajorOnly);

            if (result.Success && result.Data != null)
            {
                TotalCount = result.Data.TotalCount;

                // 应用数据脱敏（非管理员角色对手机号脱敏）
                var items = result.Data.Items;
                if (!CurrentSession.Current.IsHeadquartersAdmin)
                {
                    foreach (var item in items)
                    {
                        item.Phone = _maskingService.MaskPhone(item.Phone ?? "");
                    }
                }

                Customers = new ObservableCollection<CustomerListItem>(items);
                UpdateEmptyState();
            }

            ShowDuplicateWarning = false;
            DuplicateCustomers.Clear();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载客户列表数据失败");
            ShowBusinessException(ex, "加载客户列表");
            ShowLoadFailedState();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Search()
    {
        PageIndex = 1;
        RunInBackground(LoadDataAsync(), "搜索客户失败");
    }

    [RelayCommand]
    private void NewCustomer()
    {
        var editVm = App.Services.GetService(typeof(CustomerEditViewModel)) as CustomerEditViewModel
            ?? throw new InvalidOperationException("无法创建编辑视图模型");

        editVm.OnSaveCompleted = async () => { await LoadDataAsync(); };

        var dialog = new Views.CustomerEditWindow(editVm) { Owner = System.Windows.Application.Current.MainWindow };
        dialog.ShowDialog();
    }

    [RelayCommand]
    private void EditCustomer(CustomerListItem? customer)
    {
        if (customer == null) return;

        var editVm = App.Services.GetService(typeof(CustomerEditViewModel)) as CustomerEditViewModel
            ?? throw new InvalidOperationException("无法创建编辑视图模型");

        editVm.LoadCustomer(customer.Id);
        editVm.OnSaveCompleted = async () => { await LoadDataAsync(); };

        var dialog = new Views.CustomerEditWindow(editVm) { Owner = System.Windows.Application.Current.MainWindow };
        dialog.ShowDialog();
    }

    [RelayCommand]
    private async Task DeleteCustomerAsync(CustomerListItem? customer)
    {
        if (customer == null) return;
        if (!CheckCustomerPermission("Delete")) return;

        // 检查是否关联了企业微信 - 关联后不可删除
        var entity = await _dbContext.Customers.FindAsync(customer.Id);
        if (entity != null && !string.IsNullOrEmpty(entity.WeChatExternalUserId))
        {
            ShowBusinessError(BusinessMessages.CustomerWeChatBound);
            return;
        }

        if (!ConfirmDangerousAction("删除客户", $"确定要删除客户「{customer.Name}」吗？")) return;

        ApiResponse<bool>? deleteResult = null;
        var executed = await ExecuteWithRetryAsync(async () =>
        {
            deleteResult = await _customerService.DeleteAsync(customer.Id);
            if (deleteResult == null || !deleteResult.Success)
                throw new InvalidOperationException(deleteResult?.Message ?? "删除客户失败");
        }, "删除客户", showSuccess: false);

        if (!executed || deleteResult == null) return;

        await _auditService.LogCustomerDeleteAsync(CurrentSession.CurrentEmployeeId, customer.Id, customer.Name);
        ShowSuccess("删除成功");
        await LoadDataAsync();
    }

    [RelayCommand]
    private void ViewCustomerDetail(CustomerListItem? customer)
    {
        if (customer == null) return;
        var vm = new CustomerDetailViewModel(customer.Id);
        vm.OnCustomerUpdated = async () => { await LoadDataAsync(); };
        var dialog = new Views.CustomerDetailWindow(vm) { Owner = System.Windows.Application.Current.MainWindow };
        dialog.ShowDialog();
    }

    [RelayCommand]
    private void ViewCustomerArchive(CustomerListItem? customer)
    {
        if (customer == null) return;

        var vm = App.Services.GetService(typeof(CustomerArchiveViewModel)) as CustomerArchiveViewModel
            ?? throw new InvalidOperationException("无法创建客户档案视图模型");
        vm.LoadCustomer(customer.Id);

        var view = new Views.CustomerArchiveView(vm);
        var dialog = new Window
        {
            Title = $"客户档案 - {customer.Name}",
            Content = view,
            Owner = System.Windows.Application.Current.MainWindow,
            Width = 1100,
            Height = 760,
            MinWidth = 960,
            MinHeight = 640,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        dialog.ShowDialog();
    }

    [RelayCommand]
    private async Task BatchUpdateAsync()
    {
        if (SelectedCustomer == null) { ShowError("请先选择客户"); return; }
        // 简化：直接编辑选中的客户
        EditCustomer(SelectedCustomer);
        await Task.CompletedTask;
    }

    [RelayCommand]
    private void ViewCustomer(CustomerListItem? customer)
    {
        if (customer == null) return;
        EditCustomer(customer);
    }

    [RelayCommand]
    private async Task CheckDuplicatesAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchKeyword))
        {
            ShowError("请输入搜索关键词进行查重");
            return;
        }

        try
        {
            var result = await _customerService.CheckDuplicatesAsync(
                phone: SearchKeyword,
                name: SearchKeyword,
                address: null,
                legalPerson: null);

            if (result.Success && result.Data != null && result.Data.HasDuplicates)
            {
                DuplicateCustomers = new ObservableCollection<CustomerListItem>(
                    result.Data.Duplicates.Select(d => new CustomerListItem
                    {
                        Id = d.Id,
                        Name = d.Name,
                        Phone = d.Phone,
                        Address = d.Address,
                    }));
                ShowDuplicateWarning = true;
                ShowError($"发现 {result.Data.Duplicates.Count} 个疑似重复客户");
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "客户查重失败");
            Log.Warning("查重过程中出现错误");
        }
    }

    [RelayCommand]
    private Task OnValidatingCustomerAsync()
    {
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task MergeCustomersAsync(CustomerListItem? sourceCustomer)
    {
        if (sourceCustomer == null) return;
        if (!CheckCustomerPermission("Merge")) return;

        var selected = await Views.CustomerPickerWindow.ShowAsync(
            owner: System.Windows.Application.Current.MainWindow);

        if (selected != null && selected.Id != sourceCustomer.Id)
        {
            if (!ConfirmDangerousAction("确认合并", $"将「{sourceCustomer.Name}」合并至「{selected.Name}」？\n源客户订单将转移到目标客户。")) return;

            ApiResponse<bool>? mergeResult = null;
            var executed = await ExecuteWithRetryAsync(async () =>
            {
                mergeResult = await _customerService.MergeCustomersAsync(new MergeCustomerRequest
                {
                    MainCustomerId = selected.Id,
                    MergedCustomerIds = [sourceCustomer.Id]
                });

                if (mergeResult == null || !mergeResult.Success)
                    throw new InvalidOperationException(mergeResult?.Message ?? "合并失败");
            }, "合并客户", showSuccess: false);

            if (executed && mergeResult != null)
            {
                await _auditService.LogCustomerMergeAsync(
                    CurrentSession.CurrentEmployeeId,
                    selected.Id,
                    selected.Name,
                    [sourceCustomer.Name]);
                ShowSuccess("合并成功");
                await LoadDataAsync();
            }
        }
    }

    partial void OnIsBatchModeChanged(bool value)
    {
        // 退出批量模式时清除所有选择
        if (!value)
        {
            foreach (var c in Customers)
                c.IsSelected = false;
            IsSelectAll = false;
            UpdateSelectedCount();
        }
    }

    partial void OnIsSelectAllChanged(bool value)
    {
        foreach (var c in Customers)
            c.IsSelected = value;
        UpdateSelectedCount();
    }

    private void UpdateSelectedCount()
    {
        SelectedCustomersCount = Customers.Count(c => c.IsSelected);
    }

    [RelayCommand]
    private async Task BatchAssignAsync()
    {
        if (SelectedCustomersCount == 0)
        {
            ShowError("请先在批量模式下勾选客户");
            return;
        }

        var selectedIds = Customers.Where(c => c.IsSelected).Select(c => c.Id).ToList();
        var selectedNames = Customers.Where(c => c.IsSelected).Take(5).Select(c => c.Name);
        var preview = string.Join("、", selectedNames);
        if (SelectedCustomersCount > 5) preview += $" 等{SelectedCustomersCount}个客户";

        if (!ConfirmDangerousAction("批量分配",
            $"确定要对以下 {SelectedCustomersCount} 个客户执行批量分配？\n{preview}"))
            return;

        try
        {
            await _customerService.BulkAssignCustomersAsync(new BulkAssignRequest
            {
                CustomerIds = selectedIds,
                BranchId = CurrentSession.CurrentBranchId
            });

            ShowSuccess($"成功分配 {SelectedCustomersCount} 个客户");
            IsBatchMode = false;
            UpdateSelectedCount();
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "批量分配客户失败");
            ShowError($"批量分配失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private void IgnoreDuplicate(CustomerListItem? customer)
    {
        if (customer != null)
        {
            DuplicateCustomers.Remove(customer);
            if (DuplicateCustomers.Count == 0)
            {
                ShowDuplicateWarning = false;
            }
        }
    }

    [RelayCommand]
    private async Task ExportToExcelAsync()
    {
        if (!CheckCustomerPermission("Export")) return;

        try
        {
            var branchId = CurrentSession.CurrentBranchId;
            var customers = await _dbContext.Customers.AsNoTracking()
                .Include(c => c.Branch)
                .Include(c => c.ParentCustomer)
                .Where(c => c.BranchId == branchId && c.Status != CustomerStatus.Deleted)
                .OrderBy(c => c.Id)
                .Take(5000)  // 最多导出5000条
                .ToListAsync();

            var customerIds = customers.Select(c => c.Id).ToList();
            var orderStatRows = await _dbContext.Orders.AsNoTracking()
                .Where(o => customerIds.Contains(o.CustomerId))
                .GroupBy(o => o.CustomerId)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    OrderCount = g.Count(),
                    TotalAmount = g.Sum(o => o.TotalAmount)
                })
                .ToListAsync();
            var orderStats = orderStatRows.ToDictionary(s => s.CustomerId);

            var dialog = new SaveFileDialog
            {
                Filter = "Excel文件|*.xlsx",
                FileName = $"客户数据_{DateTime.Now:yyyyMMdd}"
            };

            if (dialog.ShowDialog() == true)
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("客户列表");

                // 表头
                worksheet.Cell(1, 1).Value = "客户名称";
                worksheet.Cell(1, 2).Value = "类型";
                worksheet.Cell(1, 3).Value = "手机号";
                worksheet.Cell(1, 4).Value = "地址";
                worksheet.Cell(1, 5).Value = "经度";
                worksheet.Cell(1, 6).Value = "纬度";
                worksheet.Cell(1, 7).Value = "法人";
                worksheet.Cell(1, 8).Value = "所属大客户";
                worksheet.Cell(1, 9).Value = "订单数";
                worksheet.Cell(1, 10).Value = "订单总额";
                worksheet.Cell(1, 11).Value = "创建时间";

                var headerRange = worksheet.Range(1, 1, 1, 11);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                // 数据
                var row = 2;
                foreach (var c in customers)
                {
                    orderStats.TryGetValue(c.Id, out var stats);

                    worksheet.Cell(row, 1).Value = c.Name;
                    worksheet.Cell(row, 2).Value = c.CustomerType == CustomerType.Major ? "大客户" : "细分客户";
                    worksheet.Cell(row, 3).Value = c.Phone;
                    worksheet.Cell(row, 4).Value = c.Address;
                    worksheet.Cell(row, 5).Value = c.Longitude;
                    worksheet.Cell(row, 6).Value = c.Latitude;
                    worksheet.Cell(row, 7).Value = c.LegalPerson;
                    worksheet.Cell(row, 8).Value = c.ParentCustomer?.Name;
                    worksheet.Cell(row, 9).Value = stats?.OrderCount ?? 0;
                    worksheet.Cell(row, 10).Value = stats?.TotalAmount ?? 0m;
                    worksheet.Cell(row, 11).Value = c.CreatedAt.ToString("yyyy-MM-dd");
                    row++;
                }

                worksheet.Columns().AdjustToContents();
                workbook.SaveAs(dialog.FileName);
                await _auditService.LogExportAsync(CurrentSession.CurrentEmployeeId, "客户Excel", customers.Count);
                ShowSuccess($"导出成功，共 {customers.Count} 条记录");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "客户列表导出失败");
            ShowError($"导出失败: {ex.Message}");
        }
    }
}

public partial class CustomerEditViewModel : ViewModelBase
{
    private readonly ProDbContext _dbContext;
    private readonly ICustomerService _customerService;
    private readonly IBranchService _branchService;
    private readonly DraftService _draftService;
    private CancellationTokenSource? _duplicateCheckCts;
    private System.Windows.Threading.DispatcherTimer? _autoSaveTimer;
    private int? _customerId;
    private Action? _onSaveCompleted;
    private const string DraftKeyPrefix = "customer_edit";
    private bool _isDirty;

    /// <summary>是否有未保存的修改</summary>
    public bool IsDirty => _isDirty;

    public Action? OnSaveCompleted
    {
        get => _onSaveCompleted;
        set => _onSaveCompleted = value;
    }

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private CustomerType _customerType = CustomerType.Sub;

    [ObservableProperty]
    private string? _phone;

    // 省市区
    [ObservableProperty]
    private string? _province;

    [ObservableProperty]
    private string? _city;

    [ObservableProperty]
    private string? _district;

    // 商圈
    [ObservableProperty]
    private int? _businessDistrictId;

    [ObservableProperty]
    private string? _businessDistrictName;

    // 省市区下拉列表
    [ObservableProperty]
    private ObservableCollection<string> _provinces = new(ChinaDivisionData.Provinces);

    [ObservableProperty]
    private ObservableCollection<string> _cities = [];

    [ObservableProperty]
    private ObservableCollection<string> _districts = [];

    [ObservableProperty]
    private ObservableCollection<BusinessDistrict> _businessDistricts = [];

    [ObservableProperty]
    private BusinessDistrict? _selectedBusinessDistrict;

    [ObservableProperty]
    private string? _address;

    [ObservableProperty]
    private double? _longitude;

    [ObservableProperty]
    private double? _latitude;

    [ObservableProperty]
    private string? _legalPerson;

    [ObservableProperty]
    private string? _registerAddress;

    [ObservableProperty]
    private int? _parentCustomerId;

    [ObservableProperty]
    private string? _parentCustomerName;

    [ObservableProperty]
    private int _branchId;

    [ObservableProperty]
    private string? _remark;

    [ObservableProperty]
    private ObservableCollection<CustomerListItem> _majorCustomers = [];

    [ObservableProperty]
    private CustomerListItem? _selectedMajorCustomer;

    [ObservableProperty]
    private bool _isEdit;

    [ObservableProperty]
    private string _windowTitle = "新增客户";

    [ObservableProperty]
    private ObservableCollection<CustomerDuplicateItem> _duplicateCandidates = [];

    [ObservableProperty]
    private bool _showDuplicateWarning;

    [ObservableProperty]
    private string _duplicateWarningText = string.Empty;

    public CustomerEditViewModel()
    {
        _dbContext = App.Services.GetService(typeof(ProDbContext)) as ProDbContext
            ?? throw new InvalidOperationException("无法获取数据库上下文");
        _customerService = App.Services.GetService(typeof(ICustomerService)) as ICustomerService
            ?? throw new InvalidOperationException("无法获取客户服务");
        _branchService = App.Services.GetService(typeof(IBranchService)) as IBranchService
            ?? throw new InvalidOperationException("无法获取分公司服务");
        _draftService = App.Services.GetService(typeof(DraftService)) as DraftService
            ?? throw new InvalidOperationException("无法获取草稿服务");

        BranchId = CurrentSession.CurrentBranchId;
        IsEdit = false;
        RunInBackground(LoadInitialDataAsync(), "初始化客户编辑失败");
        InitializeAutoSave();
    }

    private void InitializeAutoSave()
    {
        _autoSaveTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        _autoSaveTimer.Tick += async (s, e) => await SaveDraftAsync();
        _autoSaveTimer.Start();

        // 监听属性变化标记为脏数据
        PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != nameof(IsLoading) && e.PropertyName != nameof(ErrorMessage)
                && e.PropertyName != nameof(SuccessMessage) && e.PropertyName != nameof(ShowDuplicateWarning))
            {
                _isDirty = true;
            }
        };
    }

    /// <summary>停止自动保存定时器，防止内存泄漏</summary>
    public void StopAutoSave()
    {
        _autoSaveTimer?.Stop();
        _autoSaveTimer = null;
    }

    private string GetDraftKey() => $"{DraftKeyPrefix}_{CurrentSession.CurrentEmployeeId}";

    private async Task SaveDraftAsync()
    {
        if (!_isDirty || IsEdit) return;

        try
        {
            var draftData = new CustomerDraftData
            {
                Name = Name,
                Phone = Phone,
                Province = Province,
                City = City,
                District = District,
                Address = Address,
                LegalPerson = LegalPerson,
                Remark = Remark,
                BusinessDistrictId = BusinessDistrictId,
                ParentCustomerId = ParentCustomerId,
                Longitude = Longitude,
                Latitude = Latitude
            };

            await _draftService.SaveDraftAsync(GetDraftKey(), draftData, CurrentSession.CurrentEmployeeId);
            _isDirty = false;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "自动保存草稿失败");
        }
    }

    private async Task<CustomerDraftData?> LoadDraftAsync()
    {
        return await _draftService.LoadDraftAsync<CustomerDraftData>(GetDraftKey(), CurrentSession.CurrentEmployeeId);
    }

    private async Task DeleteDraftAsync()
    {
        await _draftService.DeleteDraftAsync(GetDraftKey(), CurrentSession.CurrentEmployeeId);
    }

    private async Task LoadInitialDataAsync()
    {
        await LoadMajorCustomersAsync();
        await LoadBusinessDistrictsAsync();

        // 检查是否有未保存的草稿
        if (!IsEdit)
        {
            var draft = await LoadDraftAsync();
            if (draft != null)
            {
                var result = MessageBox.Show(
                    "检测到上次未保存的客户信息，是否恢复？",
                    "恢复草稿",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    Name = draft.Name ?? string.Empty;
                    Phone = draft.Phone;
                    Province = draft.Province;
                    City = draft.City;
                    District = draft.District;
                    Address = draft.Address;
                    LegalPerson = draft.LegalPerson;
                    Remark = draft.Remark;
                    BusinessDistrictId = draft.BusinessDistrictId;
                    ParentCustomerId = draft.ParentCustomerId;
                    Longitude = draft.Longitude;
                    Latitude = draft.Latitude;
                    _isDirty = false;
                    return;
                }
                else
                {
                    await DeleteDraftAsync();
                }
            }
        }

        // 根据分公司名称设置默认省市区
        if (BranchId > 0)
        {
            var branchResult = await _branchService.GetByIdAsync(BranchId);
            if (branchResult.Success && branchResult.Data != null)
            {
                var (defaultProvince, defaultCity) = ChinaDivisionData.GetDefaultProvinceCity(branchResult.Data.Name);
                if (!string.IsNullOrEmpty(defaultProvince))
                {
                    Province = defaultProvince;
                    City = defaultCity;
                }
            }
        }
    }

    public void LoadCustomer(int customerId)
    {
        _customerId = customerId;
        IsEdit = true;
        WindowTitle = "编辑客户";
        RunInBackground(LoadCustomerAsync(), "加载客户详情失败");
    }

    partial void OnSelectedMajorCustomerChanged(CustomerListItem? value)
    {
        if (value != null)
        {
            ParentCustomerId = value.Id;
            ParentCustomerName = value.Name;
        }
    }

    partial void OnNameChanged(string value) => QueueDuplicateCheck();
    partial void OnPhoneChanged(string? value) => QueueDuplicateCheck();
    partial void OnAddressChanged(string? value) => QueueDuplicateCheck();
    partial void OnLegalPersonChanged(string? value) => QueueDuplicateCheck();

    /// <summary>省份选择变动时更新城市列表</summary>
    partial void OnProvinceChanged(string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            Cities = new ObservableCollection<string>(ChinaDivisionData.GetCities(value));
            City = null;
            District = null;
        }
    }

    /// <summary>商圈选择变动</summary>
    partial void OnSelectedBusinessDistrictChanged(BusinessDistrict? value)
    {
        if (value != null)
        {
            BusinessDistrictId = value.Id;
            BusinessDistrictName = value.Name;
        }
    }

    private async Task LoadBusinessDistrictsAsync()
    {
        try
        {
            var list = await _customerService.GetBusinessDistrictsAsync(BranchId);
            BusinessDistricts = new ObservableCollection<BusinessDistrict>(list);
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "加载商圈列表失败"); }
    }

    private async Task LoadMajorCustomersAsync()
    {
        var customers = await _customerService.GetMajorCustomersAsync(BranchId);
        MajorCustomers = new ObservableCollection<CustomerListItem>(customers);
    }

    private async Task LoadCustomerAsync()
    {
        if (!_customerId.HasValue) return;

        var customer = await _customerService.GetEntityByIdAsync(_customerId.Value);

        if (customer != null)
        {
            _customerId = customer.Id;
            Name = customer.Name;
            CustomerType = customer.CustomerType;
            Phone = customer.Phone;
            Province = customer.Province;
            City = customer.City;
            District = customer.District;
            BusinessDistrictId = customer.BusinessDistrictId;
            Address = customer.Address;
            Longitude = customer.Longitude;
            Latitude = customer.Latitude;
            LegalPerson = customer.LegalPerson;
            RegisterAddress = customer.RegisterAddress;
            ParentCustomerId = customer.ParentCustomerId;
            Remark = customer.Remark;
            BranchId = customer.BranchId;

            // 重新设置省份触发城市列表更新
            if (!string.IsNullOrEmpty(customer.Province))
            {
                var savedProvince = customer.Province;
                Province = null;
                Province = savedProvince;
            }

            // 加载商圈列表并选中
            await LoadBusinessDistrictsAsync();
            if (customer.BusinessDistrictId.HasValue)
            {
                SelectedBusinessDistrict = BusinessDistricts.FirstOrDefault(b => b.Id == customer.BusinessDistrictId.Value);
            }

            if (customer.ParentCustomerId.HasValue)
            {
                SelectedMajorCustomer = MajorCustomers.FirstOrDefault(m => m.Id == customer.ParentCustomerId);
            }
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ShowError("请输入客户名称");
            return;
        }
        if (Name.Length > 20)
        {
            ShowError("客户名称最长20个字符（企业微信备注限制）");
            return;
        }

        // 客户编号会占用企业微信描述的部分长度（编号约20字符 + 1分隔符 = 21）
        // 企业微信描述总长150字符，剩余约129字符供Remark使用
        const int descriptionMaxLen = 150;
        const int customerNoReserved = 21; // 客户编号(~20字符) + 回车换行(1)
        const int remarkMaxLen = descriptionMaxLen - customerNoReserved;

        if (Remark != null && Remark.Length > remarkMaxLen)
        {
            ShowError($"客户描述最长{remarkMaxLen}个字符（客户编号占用{customerNoReserved}个字符，企业微信描述共{descriptionMaxLen}个字符）");
            return;
        }

        // 手机号格式校验（如果填写了手机号）
        if (!string.IsNullOrWhiteSpace(Phone) && Phone.Length != 11)
        {
            ShowError("手机号格式不正确，应为11位手机号");
            return;
        }

        // 经纬度范围校验（如果填写了经纬度）
        if (Longitude.HasValue && (Longitude < -180 || Longitude > 180))
        {
            ShowError("经度范围应在 -180 到 180 之间");
            return;
        }
        if (Latitude.HasValue && (Latitude < -90 || Latitude > 90))
        {
            ShowError("纬度范围应在 -90 到 90 之间");
            return;
        }

        try
        {
            if (!_customerId.HasValue && await ConfirmDuplicateRiskAsync())
            {
                return;
            }

            if (_customerId.HasValue)
            {
                var updateRequest = new UpdateCustomerRequest
                {
                    Id = _customerId.Value,
                    Name = Name,
                    CustomerType = CustomerType,
                    Phone = Phone,
                    Province = Province,
                    City = City,
                    District = District,
                    BusinessDistrictId = BusinessDistrictId,
                    Address = Address,
                    Longitude = Longitude,
                    Latitude = Latitude,
                    LegalPerson = LegalPerson,
                    RegisterAddress = RegisterAddress,
                    ParentCustomerId = ParentCustomerId,
                    BranchId = BranchId,
                    Remark = Remark
                };
                var result = await _customerService.UpdateAsync(updateRequest);
                if (!result.Success)
                {
                    ShowError(result.Message ?? "更新客户失败");
                    return;
                }
            }
            else
            {
                var createRequest = new CreateCustomerRequest
                {
                    Name = Name,
                    CustomerType = CustomerType,
                    Phone = Phone,
                    Province = Province,
                    City = City,
                    District = District,
                    BusinessDistrictId = BusinessDistrictId,
                    Address = Address,
                    Longitude = Longitude,
                    Latitude = Latitude,
                    LegalPerson = LegalPerson,
                    RegisterAddress = RegisterAddress,
                    ParentCustomerId = ParentCustomerId,
                    BranchId = BranchId,
                    Remark = Remark
                };
                var result = await _customerService.CreateAsync(createRequest);
                if (!result.Success)
                {
                    ShowError(result.Message ?? "创建客户失败");
                    return;
                }
                _customerId = result.Data;
            }

            // 保存成功后删除草稿
            if (!IsEdit)
            {
                await DeleteDraftAsync();
            }

            ShowSuccess("保存成功");
            _onSaveCompleted?.Invoke();

            // 同步到企业微信（异步，不阻塞保存）
            RunInBackground(SyncToWeChatAsync(_customerId), "同步客户到企业微信失败");
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "保存客户失败");
            ShowError($"保存失败: {ex.Message}");
        }
    }

    private void QueueDuplicateCheck()
    {
        if (IsEdit)
            return;

        _duplicateCheckCts?.Cancel();

        if (!HasDuplicateCheckInput())
        {
            ShowDuplicateWarning = false;
            DuplicateCandidates.Clear();
            DuplicateWarningText = string.Empty;
            return;
        }

        _duplicateCheckCts = new CancellationTokenSource();
        var token = _duplicateCheckCts.Token;
        RunInBackground(CheckDuplicatesDebouncedAsync(token), "检查重复客户失败");
    }

    private async Task CheckDuplicatesDebouncedAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(700, cancellationToken);
            await LoadDuplicateCandidatesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "客户自动查重失败");
        }
    }

    private async Task<bool> LoadDuplicateCandidatesAsync(CancellationToken cancellationToken = default)
    {
        if (!HasDuplicateCheckInput())
            return false;

        var result = await _customerService.CheckDuplicatesAsync(
            phone: Phone,
            name: Name,
            address: BuildFullAddress(),
            legalPerson: LegalPerson);

        cancellationToken.ThrowIfCancellationRequested();

        if (!result.Success || result.Data == null)
            return false;

        var candidates = result.Data.Duplicates
            .Where(d => !_customerId.HasValue || d.Id != _customerId.Value)
            .Take(5)
            .ToList();

        DuplicateCandidates = new ObservableCollection<CustomerDuplicateItem>(candidates);
        ShowDuplicateWarning = candidates.Count > 0;
        DuplicateWarningText = candidates.Count > 0
            ? $"发现 {candidates.Count} 个疑似重复客户，建议确认后再保存"
            : string.Empty;

        return candidates.Count > 0;
    }

    private async Task<bool> ConfirmDuplicateRiskAsync()
    {
        _duplicateCheckCts?.Cancel();

        var hasDuplicates = await LoadDuplicateCandidatesAsync();
        if (!hasDuplicates)
            return false;

        var preview = string.Join(Environment.NewLine, DuplicateCandidates
            .Take(5)
            .Select(c => $"- {c.Name} {c.Phone} 相似度 {c.Similarity:P0}"));

        var result = MessageBox.Show(
            $"系统发现疑似重复客户：{Environment.NewLine}{preview}{Environment.NewLine}{Environment.NewLine}仍然继续保存新客户吗？",
            "疑似重复客户",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        return result != MessageBoxResult.Yes;
    }

    private bool HasDuplicateCheckInput()
    {
        return (!string.IsNullOrWhiteSpace(Phone) && Phone.Trim().Length >= 4)
            || (!string.IsNullOrWhiteSpace(Name) && Name.Trim().Length >= 2)
            || (!string.IsNullOrWhiteSpace(Address) && Address.Trim().Length >= 3)
            || (!string.IsNullOrWhiteSpace(LegalPerson) && LegalPerson.Trim().Length >= 2);
    }

    private string BuildFullAddress()
    {
        return $"{Province ?? ""}{City ?? ""}{District ?? ""} {Address ?? ""}".Trim();
    }

    [RelayCommand]
    private void Cancel()
    {
        // 窗口关闭由View处理
    }

    /// <summary>
    /// 生成客户编号：K + 年月日 + 分公司4位编号 + 4位递增
    /// </summary>
    private async Task<string> GenerateCustomerNoAsync(int branchId)
    {
        return await _customerService.GenerateCustomerNoAsync(branchId);
    }

    /// <summary>
    /// 保存后异步同步到企业微信
    /// </summary>
    private async Task SyncToWeChatAsync(int? customerId)
    {
        if (!customerId.HasValue) return;
        try
        {
            // 用简单方式调用企业微信同步
            var entity = await _customerService.GetEntityByIdAsync(customerId.Value);
            if (entity == null || string.IsNullOrEmpty(entity.WeChatExternalUserId)) return;

            // 找企业微信配置
            var configEntity = await _dbContext.WeChatConfigs.AsNoTracking().FirstOrDefaultAsync();
            if (configEntity == null || string.IsNullOrEmpty(configEntity.AppSecret)) return;

            var encryption = App.Services.GetService(typeof(PRO.Application.Interfaces.IEncryptionService))
                as PRO.Application.Interfaces.IEncryptionService;
            if (encryption == null) return;

            var secret = encryption.Decrypt(configEntity.AppSecret);
            var httpClientFactory = App.Services.GetService(typeof(System.Net.Http.IHttpClientFactory)) as System.Net.Http.IHttpClientFactory;
            if (httpClientFactory == null) return;
            using var httpClient = httpClientFactory.CreateClient();

            // 获取token
            var tokenUrl = $"https://qyapi.weixin.qq.com/cgi-bin/gettoken?corpid={configEntity.CorpId}&corpsecret={secret}";
            var tokenResp = await httpClient.GetStringAsync(tokenUrl);
            var tokenJson = System.Text.Json.JsonDocument.Parse(tokenResp);
            var token = tokenJson.RootElement.GetProperty("access_token").GetString();
            if (string.IsNullOrEmpty(token)) return;

            // 找有企业微信的员工
            var employee = await _dbContext.Employees
                .AsNoTracking()
                .Where(e => !string.IsNullOrEmpty(e.WeChatUserId))
                .Select(e => e.WeChatUserId)
                .FirstOrDefaultAsync();
            if (string.IsNullOrEmpty(employee)) return;

            // 构建企业微信描述：客户编号 + 描述（Remark），总长度不超过150字符
            var descriptionParts = new List<string>();
            if (!string.IsNullOrEmpty(entity.CustomerNo))
                descriptionParts.Add($"编号：{entity.CustomerNo}");
            if (!string.IsNullOrEmpty(entity.Remark))
                descriptionParts.Add(entity.Remark);

            var description = string.Join("\n", descriptionParts);
            if (description.Length > 150)
                description = description.Substring(0, 150);

            // 更新备注和描述到企业微信
            var body = new
            {
                userid = employee,
                external_userid = entity.WeChatExternalUserId,
                remark = entity.Name.Length > 20 ? entity.Name.Substring(0, 20) : entity.Name,
                description = description,
                remark_mobiles = !string.IsNullOrEmpty(entity.Phone)
                    ? new[] { entity.Phone } : Array.Empty<string>()
            };
            var json = System.Text.Json.JsonSerializer.Serialize(body);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            await httpClient.PostAsync(
                $"https://qyapi.weixin.qq.com/cgi-bin/externalcontact/remark?access_token={token}",
                content);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "企业微信同步失败（不影响主流程）");
        }
    }
}

/// <summary>
/// 客户编辑草稿数据
/// </summary>
public class CustomerDraftData
{
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Province { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }
    public string? Address { get; set; }
    public string? LegalPerson { get; set; }
    public string? Remark { get; set; }
    public int? BusinessDistrictId { get; set; }
    public int? ParentCustomerId { get; set; }
    public double? Longitude { get; set; }
    public double? Latitude { get; set; }
}

