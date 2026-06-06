using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRO.Application.DTOs;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Collections.ObjectModel;

namespace PRO.Desktop.ViewModels;

// ==================== 产品编辑 ====================

public partial class ProductEditViewModel : ViewModelBase
{
    private readonly ProDbContext _dbContext;
    private int? _productId;
    private Action? _onSaveCompleted;

    public Action? OnSaveCompleted
    {
        get => _onSaveCompleted;
        set => _onSaveCompleted = value;
    }

    [ObservableProperty]
    private string _sku = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string? _specification;

    [ObservableProperty]
    private decimal _averageSalePrice;

    [ObservableProperty]
    private decimal _referencePrice;

    [ObservableProperty]
    private int _stock;

    [ObservableProperty]
    private string _unit = string.Empty;

    [ObservableProperty]
    private int? _categoryId;

    [ObservableProperty]
    private ProductStatus _status = ProductStatus.Active;

    [ObservableProperty]
    private ObservableCollection<ProductCategory> _categories = new();

    [ObservableProperty]
    private ObservableCollection<ProductStatusItem> _statusList = new()
    {
        new() { Name = "启用", Value = ProductStatus.Active },
        new() { Name = "停用", Value = ProductStatus.Inactive }
    };

    [ObservableProperty]
    private ProductStatusItem? _selectedStatus;

    partial void OnSelectedStatusChanged(ProductStatusItem? value)
    {
        if (value != null)
            Status = value.Value;
    }

    [ObservableProperty]
    private bool _isEdit;

    [ObservableProperty]
    private string _windowTitle = "新增产品";

    public ProductEditViewModel()
    {
        _dbContext = App.Services.GetService(typeof(ProDbContext)) as ProDbContext 
            ?? throw new InvalidOperationException("无法获取数据库上下文");
        
        _productId = null;
        IsEdit = false;
        WindowTitle = "新增产品";

        SelectedStatus = StatusList.FirstOrDefault(s => s.Value == ProductStatus.Active);
        _ = LoadCategoriesAsync();
    }

    public void LoadProduct(int productId)
    {
        _productId = productId;
        IsEdit = true;
        WindowTitle = "编辑产品";
        _ = LoadProductAsync();
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            var categories = await _dbContext.ProductCategories.ToListAsync();
            Categories = new ObservableCollection<ProductCategory>(categories);
        }
        catch (Exception ex) { Log.Error(ex, "产品分类加载失败"); }
    }

    private async Task LoadProductAsync()
    {
        if (!_productId.HasValue) return;

        var product = await _dbContext.Products.FindAsync(_productId.Value);
        if (product != null)
        {
            Sku = product.SKU;
            Name = product.Name;
            Specification = product.Specification;
            AverageSalePrice = product.AverageSalePrice;
            ReferencePrice = product.ReferencePrice ?? 0m;
            Stock = product.Stock;
            Unit = product.Unit ?? "";
            CategoryId = product.CategoryId;
            Status = product.Status;
            SelectedStatus = StatusList.FirstOrDefault(s => s.Value == product.Status);
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Sku) || string.IsNullOrWhiteSpace(Name))
        {
            ShowError("SKU和名称不能为空");
            return;
        }

        // SKU唯一性检查
        var existingProduct = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.SKU == Sku && (!_productId.HasValue || p.Id != _productId.Value));
        if (existingProduct != null)
        {
            ShowError($"SKU「{Sku}」已被产品「{existingProduct.Name}」使用，请更换");
            return;
        }

        try
        {
            if (IsEdit && _productId.HasValue)
            {
                var product = await _dbContext.Products.FindAsync(_productId.Value);
                if (product != null)
                {
                    product.SKU = Sku;
                    product.Name = Name;
                    product.Specification = Specification;
                    product.AverageSalePrice = AverageSalePrice;
                    product.ReferencePrice = ReferencePrice;
                    product.Stock = Stock;
                    product.Unit = Unit;
                    product.CategoryId = CategoryId;
                    product.Status = Status;
                    await _dbContext.SaveChangesAsync();
                }
            }
            else
            {
                var product = new Product
                {
                    SKU = Sku,
                    Name = Name,
                    Specification = Specification,
                    AverageSalePrice = AverageSalePrice,
                    ReferencePrice = ReferencePrice,
                    Stock = Stock,
                    Unit = Unit,
                    CategoryId = CategoryId,
                    Status = Status
                };
                _dbContext.Products.Add(product);
                await _dbContext.SaveChangesAsync();
            }

            ShowSuccess("保存成功");
            _onSaveCompleted?.Invoke();
        }
        catch (Exception ex)
        {
            ShowError($"保存失败: {ex.Message}");
        }
    }
}

// ==================== 配送员编辑 ====================

public partial class DeliveryPersonEditViewModel : ViewModelBase
{
    private readonly ProDbContext _dbContext;
    private int? _deliveryPersonId;
    private Action? _onSaveCompleted;

    public Action? OnSaveCompleted
    {
        get => _onSaveCompleted;
        set => _onSaveCompleted = value;
    }

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string? _phone;

    [ObservableProperty]
    private int _branchId;

    [ObservableProperty]
    private string? _serviceArea;

    [ObservableProperty]
    private string? _vehicleNumber;

    [ObservableProperty]
    private string? _weChatId;

    [ObservableProperty]
    private int _maxLoad = 50;

    [ObservableProperty]
    private int _currentLoad;

    [ObservableProperty]
    private DeliveryPersonStatus _status = DeliveryPersonStatus.Available;

    [ObservableProperty]
    private bool _isEdit;

    [ObservableProperty]
    private string _windowTitle = "新增配送员";

    public DeliveryPersonEditViewModel()
    {
        _dbContext = App.Services.GetService(typeof(ProDbContext)) as ProDbContext 
            ?? throw new InvalidOperationException("无法获取数据库上下文");
        
        _deliveryPersonId = null;
        IsEdit = false;
        WindowTitle = "新增配送员";
        BranchId = CurrentSession.CurrentBranchId;
    }

    public void LoadDeliveryPerson(int deliveryPersonId)
    {
        _deliveryPersonId = deliveryPersonId;
        IsEdit = true;
        WindowTitle = "编辑配送员";
        _ = LoadDeliveryPersonAsync();
    }

    private async Task LoadDeliveryPersonAsync()
    {
        if (!_deliveryPersonId.HasValue) return;

        var deliveryPerson = await _dbContext.DeliveryPersons.FindAsync(_deliveryPersonId.Value);
        if (deliveryPerson != null)
        {
            Name = deliveryPerson.Name;
            Phone = deliveryPerson.Phone;
            BranchId = deliveryPerson.BranchId;
            ServiceArea = deliveryPerson.ServiceArea;
            VehicleNumber = deliveryPerson.VehicleNumber;
            WeChatId = deliveryPerson.WeChatId;
            MaxLoad = deliveryPerson.MaxLoad;
            CurrentLoad = deliveryPerson.CurrentLoad;
            Status = deliveryPerson.Status;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Phone))
        {
            ShowError("姓名和手机号不能为空");
            return;
        }

        try
        {
            if (IsEdit && _deliveryPersonId.HasValue)
            {
                var deliveryPerson = await _dbContext.DeliveryPersons.FindAsync(_deliveryPersonId.Value);
                if (deliveryPerson != null)
                {
                    deliveryPerson.Name = Name;
                    deliveryPerson.Phone = Phone;
                    deliveryPerson.BranchId = BranchId;
                    deliveryPerson.ServiceArea = ServiceArea;
                    deliveryPerson.VehicleNumber = VehicleNumber;
                    deliveryPerson.WeChatId = WeChatId;
                    deliveryPerson.MaxLoad = MaxLoad;
                    deliveryPerson.CurrentLoad = CurrentLoad;
                    deliveryPerson.Status = Status;
                    await _dbContext.SaveChangesAsync();
                }
            }
            else
            {
                var deliveryPerson = new DeliveryPerson
                {
                    Name = Name,
                    Phone = Phone,
                    BranchId = BranchId,
                    ServiceArea = ServiceArea,
                    VehicleNumber = VehicleNumber,
                    WeChatId = WeChatId,
                    MaxLoad = MaxLoad,
                    CurrentLoad = 0,
                    Status = Status
                };
                _dbContext.DeliveryPersons.Add(deliveryPerson);
                await _dbContext.SaveChangesAsync();
            }

            ShowSuccess("保存成功");
            _onSaveCompleted?.Invoke();
        }
        catch (Exception ex)
        {
            ShowError($"保存失败: {ex.Message}");
        }
    }
}

// ==================== 结算详情 ====================

public partial class SettlementDetailViewModel : ViewModelBase
{
    private readonly ProDbContext _dbContext;
    private int _settlementId;

    [ObservableProperty]
    private Settlement? _settlement;

    [ObservableProperty]
    private ObservableCollection<Order> _orders = new();

    [ObservableProperty]
    private int _totalOrderCount;

    [ObservableProperty]
    private decimal _totalAmount;

    [ObservableProperty]
    private decimal _totalCollectedAmount;

    public SettlementDetailViewModel()
    {
        _dbContext = App.Services.GetService(typeof(ProDbContext)) as ProDbContext 
            ?? throw new InvalidOperationException("无法获取数据库上下文");
    }

    public void LoadData(int settlementId)
    {
        _settlementId = settlementId;
        _ = LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        var settlement = await _dbContext.Settlements.FindAsync(_settlementId);
        Settlement = settlement;

        if (settlement != null)
        {
            var orders = await _dbContext.Orders
                .Where(o => o.SettlementId == _settlementId)
                .Include(o => o.Customer)
                .ToListAsync();

            Orders = new ObservableCollection<Order>(orders);
            TotalOrderCount = orders.Count;
            TotalAmount = orders.Sum(o => o.TotalAmount);
            TotalCollectedAmount = orders.Sum(o => o.ReceivedAmount);
        }
    }
}

// ==================== 部门树 ====================

public partial class DepartmentTreeViewModel : ViewModelBase
{
    private readonly ProDbContext _dbContext;

    [ObservableProperty]
    private ObservableCollection<Department> _departments = new();

    [ObservableProperty]
    private Department? _selectedDepartment;

    public DepartmentTreeViewModel()
    {
        _dbContext = App.Services.GetService(typeof(ProDbContext)) as ProDbContext 
            ?? throw new InvalidOperationException("无法获取数据库上下文");
        
        _ = LoadDepartmentsAsync();
    }

    private async Task LoadDepartmentsAsync()
    {
        try
        {
            var branchId = CurrentSession.CurrentBranchId;
            var departments = await _dbContext.Departments
                .Where(d => d.BranchId == branchId && d.Status == EntityStatus.Active)
                .OrderBy(d => d.SortOrder)
                .ToListAsync();

            Departments = new ObservableCollection<Department>(departments);
        }
        catch (Exception ex) { Log.Error(ex, "部门数据加载失败"); }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadDepartmentsAsync();
    }
}

// ==================== 员工列表 ====================

public partial class EmployeeListViewModel : ViewModelBase
{
    private readonly ProDbContext _dbContext;

    [ObservableProperty]
    private ObservableCollection<EmployeeListItem> _employees = new();

    [ObservableProperty]
    private EmployeeListItem? _selectedEmployee;

    [ObservableProperty]
    private string? _searchKeyword;

    public EmployeeListViewModel()
    {
        _dbContext = App.Services.GetService(typeof(ProDbContext)) as ProDbContext 
            ?? throw new InvalidOperationException("无法获取数据库上下文");
        
        _ = LoadEmployeesAsync();
    }

    private async Task LoadEmployeesAsync()
    {
        try
        {
            var branchId = CurrentSession.CurrentBranchId;
            var query = _dbContext.Employees
                .Include(e => e.Department)
                .Include(e => e.Role)
                .Where(e => e.BranchId == branchId && e.Status == EmployeeStatus.Active);

            if (!string.IsNullOrWhiteSpace(SearchKeyword))
            {
                query = query.Where(e => e.Name.Contains(SearchKeyword) || e.EmployeeNo.Contains(SearchKeyword));
            }

            var employees = await query.ToListAsync();

            Employees = new ObservableCollection<EmployeeListItem>(employees.Select(e => new EmployeeListItem
            {
                Id = e.Id,
                Name = e.Name,
                EmployeeNo = e.EmployeeNo,
                DepartmentName = e.Department?.Name ?? "",
                DepartmentId = e.DepartmentId,
                BranchName = "",
                BranchId = e.BranchId,
                RoleName = e.Role?.Name ?? "",
                Status = e.Status,
                StatusName = e.Status == EmployeeStatus.Active ? "在职" : "离职"
            }));
        }
        catch (Exception ex) { Log.Error(ex, "员工列表加载失败"); }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        await LoadEmployeesAsync();
    }

    [RelayCommand]
    private void NewEmployee()
    {
        var editVm = App.Services.GetService(typeof(EmployeeEditViewModel)) as EmployeeEditViewModel
            ?? throw new InvalidOperationException("无法创建员工编辑视图模型");
        
        editVm.OnSaveCompleted = async () => { await LoadEmployeesAsync(); };
        
        var dialog = new Views.EmployeeEditWindow(editVm) { Owner = System.Windows.Application.Current.MainWindow };
        dialog.ShowDialog();
    }

    [RelayCommand]
    private void EditEmployee(EmployeeListItem? employee)
    {
        if (employee == null) return;
        
        var editVm = App.Services.GetService(typeof(EmployeeEditViewModel)) as EmployeeEditViewModel
            ?? throw new InvalidOperationException("无法创建员工编辑视图模型");
        
        editVm.LoadEmployee(employee.Id);
        editVm.OnSaveCompleted = async () => { await LoadEmployeesAsync(); };
        
        var dialog = new Views.EmployeeEditWindow(editVm) { Owner = System.Windows.Application.Current.MainWindow };
        dialog.ShowDialog();
    }

    [ObservableProperty]
    private ObservableCollection<Department> _departments = new();

    [ObservableProperty]
    private bool _isDepartmentPanelOpen;

    [ObservableProperty]
    private string _newDepartmentName = string.Empty;

    [ObservableProperty]
    private int _newDepartmentSortOrder;

    [RelayCommand]
    private void ToggleDepartmentPanel()
    {
        IsDepartmentPanelOpen = !IsDepartmentPanelOpen;
        if (IsDepartmentPanelOpen) _ = LoadDepartmentsAsync();
    }

    private async Task LoadDepartmentsAsync()
    {
        try
        {
            var branchId = CurrentSession.CurrentBranchId;
            var depts = await _dbContext.Departments
                .Where(d => d.BranchId == branchId && d.Status == EntityStatus.Active)
                .OrderBy(d => d.SortOrder).ToListAsync();
            Departments = new ObservableCollection<Department>(depts);
        }
        catch (Exception ex) { Log.Error(ex, "加载部门失败"); }
    }

    [RelayCommand]
    private async Task AddDepartmentAsync()
    {
        if (string.IsNullOrWhiteSpace(NewDepartmentName)) return;
        try
        {
            var dept = new Department
            {
                Name = NewDepartmentName,
                SortOrder = NewDepartmentSortOrder,
                BranchId = CurrentSession.CurrentBranchId,
                Status = EntityStatus.Active
            };
            _dbContext.Departments.Add(dept);
            await _dbContext.SaveChangesAsync();
            NewDepartmentName = string.Empty;
            NewDepartmentSortOrder = 0;
            await LoadDepartmentsAsync();
            ShowSuccess("部门已新增");
        }
        catch (Exception ex) { ShowError($"新增失败: {ex.Message}"); }
    }

    [RelayCommand]
    private async Task EditDepartmentAsync(Department? dept)
    {
        if (dept == null) return;
        try
        {
            dept.Name = NewDepartmentName ?? dept.Name;
            if (NewDepartmentSortOrder > 0) dept.SortOrder = NewDepartmentSortOrder;
            await _dbContext.SaveChangesAsync();
            await LoadDepartmentsAsync();
            ShowSuccess("部门已更新");
        }
        catch (Exception ex) { ShowError($"编辑失败: {ex.Message}"); }
    }

    [RelayCommand]
    private async Task DeleteDepartmentAsync(Department? dept)
    {
        if (dept == null) return;
        try
        {
            _dbContext.Departments.Remove(dept);
            await _dbContext.SaveChangesAsync();
            await LoadDepartmentsAsync();
            ShowSuccess("部门已删除");
        }
        catch (Exception ex) { ShowError($"删除失败: {ex.Message}"); }
    }

    [RelayCommand]
    private async Task DeleteEmployeeAsync(EmployeeListItem? emp)
    {
        if (emp == null) return;
        try
        {
            var employee = await _dbContext.Employees.FindAsync(emp.Id);
            if (employee != null)
            {
                employee.Status = EmployeeStatus.Inactive;
                await _dbContext.SaveChangesAsync();
                await LoadEmployeesAsync();
                ShowSuccess("员工已停用");
            }
        }
        catch (Exception ex) { ShowError($"删除失败: {ex.Message}"); }
    }
}

// ==================== 员工编辑 ====================

public partial class EmployeeEditViewModel : ViewModelBase
{
    private readonly ProDbContext _dbContext;
    private int? _employeeId;
    private Action? _onSaveCompleted;

    public Action? OnSaveCompleted
    {
        get => _onSaveCompleted;
        set => _onSaveCompleted = value;
    }

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _employeeNo = string.Empty;

    [ObservableProperty]
    private string? _password;

    [ObservableProperty]
    private int _departmentId;

    [ObservableProperty]
    private int _branchId;

    [ObservableProperty]
    private int _roleId;

    [ObservableProperty]
    private string? _phone;

    [ObservableProperty]
    private string? _email;

    [ObservableProperty]
    private Gender _gender = Gender.Male;

    [ObservableProperty]
    private EmployeeStatus _status = EmployeeStatus.Active;

    [ObservableProperty]
    private ObservableCollection<Department> _departments = new();

    [ObservableProperty]
    private ObservableCollection<Role> _roles = new();

    [ObservableProperty]
    private bool _isEdit;

    [ObservableProperty]
    private string _windowTitle = "新增员工";

    public EmployeeEditViewModel()
    {
        _dbContext = App.Services.GetService(typeof(ProDbContext)) as ProDbContext 
            ?? throw new InvalidOperationException("无法获取数据库上下文");
        
        _employeeId = null;
        IsEdit = false;
        WindowTitle = "新增员工";
        BranchId = CurrentSession.CurrentBranchId;

        _ = LoadDataAsync();
    }

    public void LoadEmployee(int employeeId)
    {
        _employeeId = employeeId;
        IsEdit = true;
        WindowTitle = "编辑员工";
        _ = LoadEmployeeAsync();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            var departments = await _dbContext.Departments
                .Where(d => d.BranchId == BranchId && d.Status == EntityStatus.Active)
                .ToListAsync();
            Departments = new ObservableCollection<Department>(departments);

            var roles = await _dbContext.Roles.ToListAsync();
            Roles = new ObservableCollection<Role>(roles);
        }
        catch (Exception ex) { Log.Error(ex, "员工编辑初始化加载失败"); }
    }

    private async Task LoadEmployeeAsync()
    {
        if (!_employeeId.HasValue) return;

        var employee = await _dbContext.Employees.FindAsync(_employeeId.Value);
        if (employee != null)
        {
            Name = employee.Name;
            EmployeeNo = employee.EmployeeNo;
            DepartmentId = employee.DepartmentId;
            RoleId = employee.RoleId;
            Phone = employee.Phone;
            Email = employee.Email;
            Gender = employee.Gender;
            Status = employee.Status;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(EmployeeNo))
        {
            ShowError("姓名和工号不能为空");
            return;
        }

        // 密码校验
        if (!IsEdit && string.IsNullOrWhiteSpace(Password))
        {
            ShowError("密码不能为空");
            return;
        }
        if (!string.IsNullOrWhiteSpace(Password) && Password.Length < 6)
        {
            ShowError("密码长度不能少于6位");
            return;
        }

        // 编辑模式下，工号只有总部管理员可以修改
        if (IsEdit && _employeeId.HasValue)
        {
            var currentEmployee = await _dbContext.Employees.FindAsync(CurrentSession.CurrentEmployeeId);
            if (currentEmployee?.RoleId != 1) // RoleId=1 = 总部管理员
            {
                // 获取原工号并锁定
                var original = await _dbContext.Employees.FindAsync(_employeeId.Value);
                if (original != null && original.EmployeeNo != EmployeeNo)
                {
                    ShowError("只有总部管理员可以修改工号");
                    EmployeeNo = original.EmployeeNo;
                    return;
                }
            }
        }

        try
        {
            if (IsEdit && _employeeId.HasValue)
            {
                var employee = await _dbContext.Employees.FindAsync(_employeeId.Value);
                if (employee != null)
                {
                    employee.Name = Name;
                    employee.DepartmentId = DepartmentId;
                    employee.RoleId = RoleId;
                    employee.Phone = Phone;
                    employee.Email = Email;
                    employee.Gender = Gender;
                    employee.Status = Status;
                    
                    // 工号只有总部管理员可以修改
                    if (CurrentSession.Current.IsHeadquartersAdmin)
                    {
                        employee.EmployeeNo = EmployeeNo;
                    }
                    
                    if (!string.IsNullOrWhiteSpace(Password))
                    {
                        var encryption = App.Services.GetService(typeof(PRO.Application.Interfaces.IEncryptionService)) 
                            as PRO.Application.Interfaces.IEncryptionService;
                        if (encryption != null)
                            employee.PasswordHash = encryption.HashPassword(Password);
                    }
                    
                    await _dbContext.SaveChangesAsync();
                }
            }
            else
            {
                var encryption = App.Services.GetService(typeof(PRO.Application.Interfaces.IEncryptionService)) 
                    as PRO.Application.Interfaces.IEncryptionService;
                
                // 密码为空时默认使用工号
                var password = !string.IsNullOrWhiteSpace(Password) ? Password : EmployeeNo;
                
                var employee = new Employee
                {
                    Name = Name,
                    EmployeeNo = EmployeeNo,
                    PasswordHash = encryption?.HashPassword(password) ?? string.Empty,
                    DepartmentId = DepartmentId,
                    BranchId = BranchId,
                    RoleId = RoleId,
                    Phone = Phone,
                    Email = Email,
                    Gender = Gender,
                    Status = Status
                };
                _dbContext.Employees.Add(employee);
                await _dbContext.SaveChangesAsync();
            }

            ShowSuccess("保存成功");
            _onSaveCompleted?.Invoke();
        }
        catch (Exception ex)
        {
            ShowError($"保存失败: {ex.Message}");
        }
    }
}

// ==================== 同步状态 ====================

public partial class SyncStatusViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _isOnline = true;

    [ObservableProperty]
    private DateTime? _lastSyncTime;

    [ObservableProperty]
    private string _syncStatus = "已同步";

    [ObservableProperty]
    private int _pendingSyncCount;

    public SyncStatusViewModel()
    {
        CheckNetworkStatus();
    }

    private void CheckNetworkStatus()
    {
        IsOnline = System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();
    }

    [RelayCommand]
    private Task RefreshAsync()
    {
        CheckNetworkStatus();
        return Task.CompletedTask;
    }
}

// ==================== 企业微信同步日志 ====================

public partial class WeChatSyncLogViewModel : ViewModelBase
{
    private readonly ProDbContext _dbContext;

    [ObservableProperty]
    private ObservableCollection<WeChatSyncLog> _logs = new();

    public WeChatSyncLogViewModel()
    {
        _dbContext = App.Services.GetService(typeof(ProDbContext)) as ProDbContext 
            ?? throw new InvalidOperationException("无法获取数据库上下文");
        
        _ = LoadLogsAsync();
    }

    private async Task LoadLogsAsync()
    {
        try
        {
            var logs = await _dbContext.WeChatSyncLogs
                .OrderByDescending(l => l.SyncTime)
                .Take(100)
                .ToListAsync();

            Logs = new ObservableCollection<WeChatSyncLog>(logs);
        }
        catch (Exception ex) { Log.Error(ex, "同步日志加载失败"); }
    }
}

// ==================== 工作计划（别名WorkPlanViewModel） ====================

public partial class WorkPlanViewModel : WorkScheduleViewModel { }

/// <summary>
/// 产品状态选择项
/// </summary>
public class ProductStatusItem
{
    public string Name { get; set; } = string.Empty;
    public ProductStatus Value { get; set; }
}
