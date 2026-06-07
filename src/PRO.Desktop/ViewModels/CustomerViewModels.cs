using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;
using PRO.Infrastructure.Common;
using PRO.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Windows;
using System.Net.Http;
using Microsoft.Win32;
using ClosedXML.Excel;
using Serilog;

namespace PRO.Desktop.ViewModels;

public partial class CustomerListViewModel : PagedViewModelBase
{
    private readonly ProDbContext _dbContext;
    private readonly ICustomerService _customerService;

    [ObservableProperty]
    private ObservableCollection<CustomerListItem> _customers = new();

    [ObservableProperty]
    private CustomerListItem? _selectedCustomer;

    [ObservableProperty]
    private bool _showMajorOnly;

    [ObservableProperty]
    private CustomerType? _filterCustomerType;

    [ObservableProperty]
    private ObservableCollection<CustomerListItem> _duplicateCustomers = new();

    [ObservableProperty]
    private bool _showDuplicateWarning;

    public CustomerListViewModel()
    {
        _dbContext = App.Services.GetService(typeof(ProDbContext)) as ProDbContext 
            ?? throw new InvalidOperationException("无法获取数据库上下文");
        _customerService = App.Services.GetService(typeof(ICustomerService)) as ICustomerService
            ?? throw new InvalidOperationException("无法获取客户服务");
        
        _ = LoadDataAsync();
    }

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
                Customers = new ObservableCollection<CustomerListItem>(result.Data.Items);
            }

            ShowDuplicateWarning = false;
            DuplicateCustomers.Clear();
        }
        catch (Exception ex)
        {
            ShowError($"加载数据失败: {ex.Message}");
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
        _ = LoadDataAsync();
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

        // 检查是否关联了企业微信 - 关联后不可删除
        var entity = await _dbContext.Customers.FindAsync(customer.Id);
        if (entity != null && !string.IsNullOrEmpty(entity.WeChatExternalUserId))
        {
            ShowError("该客户已关联企业微信，无法删除");
            return;
        }

        var result = MessageBox.Show($"确定要删除客户「{customer.Name}」吗？", "确认删除", 
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        
        if (result != MessageBoxResult.Yes) return;

        var deleteResult = await _customerService.DeleteAsync(customer.Id);
        if (deleteResult.Success)
        {
            ShowSuccess("删除成功");
            await LoadDataAsync();
        }
        else
        {
            ShowError(deleteResult.Message);
        }
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
            System.Diagnostics.Debug.WriteLine($"查重失败: {ex.Message}");
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

        var selected = await Views.CustomerPickerWindow.ShowAsync(
            owner: System.Windows.Application.Current.MainWindow);

        if (selected != null && selected.Id != sourceCustomer.Id)
        {
            var result = MessageBox.Show($"将「{sourceCustomer.Name}」合并至「{selected.Name}」？\n源客户订单将转移到目标客户。", "确认合并", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;
            
            var mergeResult = await _customerService.MergeCustomersAsync(new MergeCustomerRequest
            {
                MainCustomerId = selected.Id,
                MergedCustomerIds = new List<int> { sourceCustomer.Id }
            });
            
            if (mergeResult.Success)
            {
                ShowSuccess("合并成功");
                await LoadDataAsync();
            }
            else
            {
                ShowError($"合并失败: {mergeResult.Message}");
            }
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
                ShowSuccess($"导出成功，共 {customers.Count} 条记录");
            }
        }
        catch (Exception ex)
        {
            ShowError($"导出失败: {ex.Message}");
        }
    }
}

public partial class CustomerEditViewModel : ViewModelBase
{
    private readonly ProDbContext _dbContext;
    private readonly ICustomerService _customerService;
    private readonly DraftService _draftService;
    private CancellationTokenSource? _duplicateCheckCts;
    private System.Windows.Threading.DispatcherTimer? _autoSaveTimer;
    private int? _customerId;
    private Action? _onSaveCompleted;
    private const string DraftKeyPrefix = "customer_edit";
    private bool _isDirty;

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
    private ObservableCollection<string> _cities = new();

    [ObservableProperty]
    private ObservableCollection<string> _districts = new();

    [ObservableProperty]
    private ObservableCollection<BusinessDistrict> _businessDistricts = new();

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
    private ObservableCollection<CustomerListItem> _majorCustomers = new();

    [ObservableProperty]
    private CustomerListItem? _selectedMajorCustomer;

    [ObservableProperty]
    private bool _isEdit;

    [ObservableProperty]
    private string _windowTitle = "新增客户";

    [ObservableProperty]
    private ObservableCollection<CustomerDuplicateItem> _duplicateCandidates = new();

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
        _draftService = App.Services.GetService(typeof(DraftService)) as DraftService
            ?? throw new InvalidOperationException("无法获取草稿服务");
        
        BranchId = CurrentSession.CurrentBranchId;
        IsEdit = false;
        _ = LoadInitialDataAsync();
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
            var branch = await _dbContext.Branches.FindAsync(BranchId);
            if (branch != null)
            {
                var (defaultProvince, defaultCity) = ChinaDivisionData.GetDefaultProvinceCity(branch.Name);
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
        _ = LoadCustomerAsync();
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
            var list = await _dbContext.BusinessDistricts
                .AsNoTracking()
                .Where(b => b.Status == "Active" && (b.BranchId == null || b.BranchId == BranchId))
                .OrderBy(b => b.Name)
                .ToListAsync();
            BusinessDistricts = new ObservableCollection<BusinessDistrict>(list);
        }
        catch { }
    }

    private async Task LoadMajorCustomersAsync()
    {
        var customers = await _dbContext.Customers
            .AsNoTracking()
            .Where(c => c.BranchId == BranchId && c.CustomerType == CustomerType.Major && c.Status == CustomerStatus.Active)
            .ToListAsync();

        MajorCustomers = new ObservableCollection<CustomerListItem>(
            customers.Select(c => new CustomerListItem { Id = c.Id, Name = c.Name }));
    }

    private async Task LoadCustomerAsync()
    {
        if (!_customerId.HasValue) return;

        var customer = await _dbContext.Customers
            .Include(c => c.ParentCustomer)
            .FirstOrDefaultAsync(c => c.Id == _customerId.Value);
            
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
            ParentCustomerName = customer.ParentCustomer?.Name;
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
                var entity = await _dbContext.Customers.FindAsync(_customerId.Value);
                if (entity != null)
                {
                    entity.Name = Name;
                    entity.CustomerType = CustomerType;
                    entity.Phone = Phone;
                    entity.Province = Province;
                    entity.City = City;
                    entity.District = District;
                    entity.BusinessDistrictId = BusinessDistrictId;
                    entity.Address = Address;
                    entity.FullAddress = $"{Province ?? ""}{City ?? ""}{District ?? ""} {Address ?? ""}".Trim();
                    entity.Longitude = Longitude;
                    entity.Latitude = Latitude;
                    entity.LegalPerson = LegalPerson;
                    entity.RegisterAddress = RegisterAddress;
                    entity.ParentCustomerId = ParentCustomerId;
                    entity.Remark = Remark;
                    entity.UpdatedAt = DateTime.Now;
                    entity.LocalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    entity.SyncStatus = SyncStatus.Pending;
                }
            }
            else
            {
                var customerNo = await GenerateCustomerNoAsync(BranchId);
                var entity = new Customer
                {
                    Name = Name,
                    CustomerNo = customerNo,
                    CustomerType = CustomerType,
                    Phone = Phone,
                    Province = Province,
                    City = City,
                    District = District,
                    BusinessDistrictId = BusinessDistrictId,
                    Address = Address,
                    FullAddress = $"{Province ?? ""}{City ?? ""}{District ?? ""} {Address ?? ""}".Trim(),
                    Longitude = Longitude,
                    Latitude = Latitude,
                    LegalPerson = LegalPerson,
                    RegisterAddress = RegisterAddress,
                    ParentCustomerId = ParentCustomerId,
                    BranchId = BranchId,
                    CreatedById = CurrentSession.CurrentEmployeeId,
                    Remark = Remark,
                    Status = CustomerStatus.Active,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    LocalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    SyncStatus = SyncStatus.Pending
                };
                _dbContext.Customers.Add(entity);
            }

            await _dbContext.SaveChangesAsync();
            
            // 保存成功后删除草稿
            if (!IsEdit)
            {
                await DeleteDraftAsync();
            }
            
            ShowSuccess("保存成功");
            _onSaveCompleted?.Invoke();

            // 同步到企业微信（异步，不阻塞保存）
            _ = SyncToWeChatAsync(_customerId);
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
        _ = CheckDuplicatesDebouncedAsync(token);
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
        var branch = await _dbContext.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == branchId);
        var branchCode = branch?.Code ?? "0000";
        if (branchCode.Length != 4)
            branchCode = branchCode.PadLeft(4, '0').Substring(0, 4);

        var datePart = DateTime.Now.ToString("yyyyMMdd");
        var prefix = $"K{datePart}{branchCode}";

        var maxNo = await _dbContext.Customers
            .AsNoTracking()
            .Where(c => c.CustomerNo.StartsWith(prefix))
            .MaxAsync(c => (string?)c.CustomerNo) ?? "";

        var seq = 1;
        if (maxNo.Length >= prefix.Length + 4)
        {
            var lastSeqStr = maxNo.Substring(prefix.Length, 4);
            int.TryParse(lastSeqStr, out seq);
            seq++;
        }

        return $"{prefix}{seq:D4}";
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
            var entity = await _dbContext.Customers.FindAsync(customerId.Value);
            if (entity == null || string.IsNullOrEmpty(entity.WeChatExternalUserId)) return;

            // 找企业微信配置
            var configEntity = await _dbContext.WeChatConfigs.AsNoTracking().FirstOrDefaultAsync();
            if (configEntity == null || string.IsNullOrEmpty(configEntity.AppSecret)) return;

            var encryption = App.Services.GetService(typeof(PRO.Application.Interfaces.IEncryptionService))
                as PRO.Application.Interfaces.IEncryptionService;
            if (encryption == null) return;

            var secret = encryption.Decrypt(configEntity.AppSecret);
            using var httpClient = new HttpClient();

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

