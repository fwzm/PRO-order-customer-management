using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRO.Application.DTOs;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;
using PRO.Infrastructure.Common;
using PRO.Infrastructure.WeChat;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Windows;
using System.IO;
using System.Net.Http;
using System.Text;
using Microsoft.Win32;
using ClosedXML.Excel;
using Serilog;

namespace PRO.Desktop.ViewModels;

public partial class CustomerListViewModel : PagedViewModelBase
{
    private readonly ProDbContext _dbContext;

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
        
        _ = LoadDataAsync();
    }

    protected override async Task LoadDataAsync()
    {
        IsLoading = true;
        try
        {
            var branchId = CurrentSession.CurrentBranchId;
            var query = _dbContext.Customers
                .Include(c => c.Branch)
                .Include(c => c.ParentCustomer)
                .Include(c => c.CustomerManager)
                .Include(c => c.Creator)
                .AsQueryable();

            // 数据隔离：只能查看本公司数据
            if (!CurrentSession.Current.IsHeadquartersAdmin)
            {
                query = query.Where(c => c.BranchId == branchId);
            }

            // 筛选大客户
            if (ShowMajorOnly)
            {
                query = query.Where(c => c.CustomerType == CustomerType.Major);
            }

            // 关键词搜索
            if (!string.IsNullOrWhiteSpace(SearchKeyword))
            {
                query = query.Where(c => c.Name.Contains(SearchKeyword) || 
                    (c.Phone != null && c.Phone.Contains(SearchKeyword)));
            }

            // 类型筛选
            if (FilterCustomerType.HasValue)
            {
                query = query.Where(c => c.CustomerType == FilterCustomerType.Value);
            }

            // 排除已删除
            query = query.Where(c => c.Status != CustomerStatus.Deleted);

            // 总数
            TotalCount = await query.CountAsync();

            // 分页
            var items = await query.OrderByDescending(c => c.CreatedAt)
                .Skip((PageIndex - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            // 计算订单统计
            var customerIds = items.Select(c => c.Id).ToList();
            var orderStats = await _dbContext.Orders
                .Where(o => customerIds.Contains(o.CustomerId))
                .GroupBy(o => o.CustomerId)
                .Select(g => new { CustomerId = g.Key, Count = g.Count(), Total = g.Sum(o => o.TotalAmount) })
                .ToDictionaryAsync(x => x.CustomerId, x => new { x.Count, x.Total });

            Customers = new ObservableCollection<CustomerListItem>(items.Select(c =>
            {
                var stats = orderStats.GetValueOrDefault(c.Id);
                return new CustomerListItem
                {
                    Id = c.Id,
                    Name = c.Name,
                    CustomerNo = c.CustomerNo,
                    CustomerType = c.CustomerType,
                    CustomerTypeName = c.CustomerType == CustomerType.Major ? "大客户" : "细分客户",
                    Phone = c.Phone,
                    Address = c.Address,
                    ParentCustomerId = c.ParentCustomerId,
                    ParentCustomerName = c.ParentCustomer?.Name,
                    BranchId = c.BranchId,
                    BranchName = c.Branch?.Name ?? "",
                    Status = c.Status,
                    OrderCount = stats?.Count ?? 0,
                    TotalOrderAmount = stats?.Total ?? 0m,
                    CreatedAt = c.CreatedAt,
                    CustomerManagerName = c.CustomerManager?.Name,
                    CreatorName = c.Creator?.Name
                };
            }));

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

        try
        {
            entity ??= await _dbContext.Customers.FindAsync(customer.Id);
            if (entity != null)
            {
                entity.Status = CustomerStatus.Deleted;
                entity.UpdatedAt = DateTime.Now;
                entity.LocalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                entity.SyncStatus = SyncStatus.Pending;
                await _dbContext.SaveChangesAsync();
            }
            
            ShowSuccess("删除成功");
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            ShowError($"删除失败: {ex.Message}");
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
            var branchId = CurrentSession.CurrentBranchId;
            var duplicates = await _dbContext.Customers
                .Where(c => c.BranchId == branchId && c.Status != CustomerStatus.Deleted)
                .Where(c => c.Name.Contains(SearchKeyword) || 
                    (c.Phone != null && c.Phone.Contains(SearchKeyword)))
                .Take(50)
                .ToListAsync();

            if (duplicates.Count > 1)
            {
                DuplicateCustomers = new ObservableCollection<CustomerListItem>(
                    duplicates.Select(c => new CustomerListItem
                    {
                        Id = c.Id,
                        Name = c.Name,
                        Phone = c.Phone,
                        Address = c.Address,
                        CreatedAt = c.CreatedAt
                    }));
                ShowDuplicateWarning = true;
                ShowError($"发现 {duplicates.Count} 个疑似重复客户");
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
            try
            {
                var entity = await _dbContext.Customers.FindAsync(sourceCustomer.Id);
                if (entity != null)
                {
                    var orders = await _dbContext.Orders.Where(o => o.CustomerId == sourceCustomer.Id).ToListAsync();
                    foreach (var o in orders) o.CustomerId = selected.Id;
                    entity.Status = CustomerStatus.Merged;
                    entity.Remark = $"已合并至 {selected.Name}";
                    entity.UpdatedAt = DateTime.Now;
                    entity.SyncStatus = SyncStatus.Pending;
                    await _dbContext.SaveChangesAsync();
                    ShowSuccess("合并成功");
                    await LoadDataAsync();
                }
            }
            catch (Exception ex) { ShowError($"合并失败: {ex.Message}"); }
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
            var customers = await _dbContext.Customers
                .Include(c => c.Branch)
                .Include(c => c.ParentCustomer)
                .Where(c => c.BranchId == branchId && c.Status != CustomerStatus.Deleted)
                .OrderBy(c => c.Id)
                .Take(5000)  // 最多导出5000条
                .ToListAsync();

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
                    var orderCount = await _dbContext.Orders.CountAsync(o => o.CustomerId == c.Id);
                    var totalAmount = await _dbContext.Orders.Where(o => o.CustomerId == c.Id).SumAsync(o => o.TotalAmount);

                    worksheet.Cell(row, 1).Value = c.Name;
                    worksheet.Cell(row, 2).Value = c.CustomerType == CustomerType.Major ? "大客户" : "细分客户";
                    worksheet.Cell(row, 3).Value = c.Phone;
                    worksheet.Cell(row, 4).Value = c.Address;
                    worksheet.Cell(row, 5).Value = c.Longitude;
                    worksheet.Cell(row, 6).Value = c.Latitude;
                    worksheet.Cell(row, 7).Value = c.LegalPerson;
                    worksheet.Cell(row, 8).Value = c.ParentCustomer?.Name;
                    worksheet.Cell(row, 9).Value = orderCount;
                    worksheet.Cell(row, 10).Value = totalAmount;
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
    private int? _customerId;
    private Action? _onSaveCompleted;

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

    public CustomerEditViewModel()
    {
        _dbContext = App.Services.GetService(typeof(ProDbContext)) as ProDbContext 
            ?? throw new InvalidOperationException("无法获取数据库上下文");
        
        BranchId = CurrentSession.CurrentBranchId;
        IsEdit = false;
        _ = LoadInitialDataAsync();
    }

    private async Task LoadInitialDataAsync()
    {
        await LoadMajorCustomersAsync();
        await LoadBusinessDistrictsAsync();
        
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
        var branch = await _dbContext.Branches.FindAsync(branchId);
        var branchCode = branch?.Code ?? "0000";
        if (branchCode.Length != 4)
            branchCode = branchCode.PadLeft(4, '0').Substring(0, 4);

        var datePart = DateTime.Now.ToString("yyyyMMdd");
        var prefix = $"K{datePart}{branchCode}";

        var maxNo = await _dbContext.Customers
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
            var configEntity = await _dbContext.WeChatConfigs.FirstOrDefaultAsync();
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

