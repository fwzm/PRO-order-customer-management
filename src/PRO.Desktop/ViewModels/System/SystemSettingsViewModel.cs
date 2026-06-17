using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;
using PRO.Infrastructure.Configuration;
using PRO.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace PRO.Desktop.ViewModels;

public partial class SystemSettingsViewModel : ViewModelBase
{
    private readonly ProDbContext _dbContext;
    private readonly BusinessConfigService _configService;
    private readonly BusinessRuleSettingsService _ruleSettingsService;

    [ObservableProperty]
    private CloseBehavior _closeBehavior = CloseBehavior.MinimizeToTray;

    [ObservableProperty]
    private bool _enableNotification = true;

    [ObservableProperty]
    private bool _enableSound = true;

    [ObservableProperty]
    private bool _autoSyncOnStartup = true;

    [ObservableProperty]
    private int _syncIntervalMinutes = 30;

    [ObservableProperty]
    private int _orderDraftExpireMinutes = 30;

    [ObservableProperty]
    private SyncConfigDto? _syncConfig;

    [ObservableProperty]
    private LocalSettingDto? _localSettings;

    // ==================== 14项业务规则属性 ====================

    [ObservableProperty] private int _draftExpireMinutes = 1440;
    [ObservableProperty] private int _maxBatchCount = 500;
    [ObservableProperty] private int _silentCustomerDays = 90;
    [ObservableProperty] private int _churnRiskDays = 30;
    [ObservableProperty] private int _stockWarningThreshold = 10;
    [ObservableProperty] private int _stockCriticalThreshold = 5;
    [ObservableProperty] private int _maxExportRows = 10000;
    [ObservableProperty] private int _passwordMinLength = 8;
    [ObservableProperty] private int _passwordExpireDays = 90;
    [ObservableProperty] private int _maxLoginFailCount = 5;
    [ObservableProperty] private int _accountLockoutMinutes = 30;
    [ObservableProperty] private int _defaultPageSize = 50;
    [ObservableProperty] private int _autoSaveIntervalSeconds = 120;
    [ObservableProperty] private int _backupRetentionDays = 30;

    public bool IsHeadquartersAdmin => CurrentSession.Current?.IsHeadquartersAdmin ?? false;
    public bool CanEditSettings => CurrentSession.Current?.IsHeadquartersAdmin == true
        || CurrentSession.Current?.IsBranchAdmin == true;

    private HeadquartersAdminViewModel? _headquartersAdminVM;
    public HeadquartersAdminViewModel? HeadquartersAdminVM
    {
        get
        {
            if (IsHeadquartersAdmin && _headquartersAdminVM == null)
            {
                _headquartersAdminVM = App.Services.GetService(typeof(HeadquartersAdminViewModel)) as HeadquartersAdminViewModel;
            }
            return _headquartersAdminVM;
        }
    }

    private OperationLogViewModel? _operationLogVM;
    public OperationLogViewModel? OperationLogVM
    {
        get
        {
            if (IsHeadquartersAdmin && _operationLogVM == null)
            {
                _operationLogVM = App.Services.GetService(typeof(OperationLogViewModel)) as OperationLogViewModel;
                RunInBackground(_operationLogVM?.InitializeCommand.ExecuteAsync(null) ?? Task.CompletedTask, "初始化操作日志失败");
            }
            return _operationLogVM;
        }
    }

    private FieldManagerViewModel? _fieldMgr;
    public FieldManagerViewModel FieldMgr
    {
        get
        {
            if (_fieldMgr == null)
            {
                _fieldMgr = new FieldManagerViewModel(_dbContext);
                RunInBackground(_fieldMgr.LoadDistrictsCommand.ExecuteAsync(null), "加载商圈失败");
                RunInBackground(_fieldMgr.LoadTagsCommand.ExecuteAsync(null), "加载标签失败");
                RunInBackground(_fieldMgr.LoadCategoriesCommand.ExecuteAsync(null), "加载分类失败");
            }
            return _fieldMgr;
        }
    }

    public SystemSettingsViewModel()
    {
        _dbContext = App.Services.GetService(typeof(ProDbContext)) as ProDbContext
            ?? throw new InvalidOperationException("无法获取数据库上下文");
        _configService = App.Services.GetService(typeof(BusinessConfigService)) as BusinessConfigService
            ?? throw new InvalidOperationException("无法获取配置服务");
        _ruleSettingsService = App.Services.GetService(typeof(BusinessRuleSettingsService)) as BusinessRuleSettingsService
            ?? throw new InvalidOperationException("无法获取规则设置服务");

        RunInBackground(LoadSettingsAsync(), "加载系统设置失败");
    }

    private async Task LoadSettingsAsync()
    {
        var settings = await _dbContext.LocalSettings.AsNoTracking().ToListAsync();

        var closeBehavior = settings.FirstOrDefault(s => s.SettingKey == "CloseBehavior");
        CloseBehavior = closeBehavior != null ? (CloseBehavior)int.Parse(closeBehavior.SettingValue) : CloseBehavior.MinimizeToTray;

        var notification = settings.FirstOrDefault(s => s.SettingKey == "EnableNotification");
        EnableNotification = notification == null || bool.Parse(notification.SettingValue);

        var sound = settings.FirstOrDefault(s => s.SettingKey == "EnableSound");
        EnableSound = sound == null || bool.Parse(sound.SettingValue);

        var autoSync = settings.FirstOrDefault(s => s.SettingKey == "AutoSyncOnStartup");
        AutoSyncOnStartup = autoSync == null || bool.Parse(autoSync.SettingValue);

        var interval = settings.FirstOrDefault(s => s.SettingKey == "SyncIntervalMinutes");
        SyncIntervalMinutes = interval != null ? int.Parse(interval.SettingValue) : 30;

        OrderDraftExpireMinutes = await _configService.GetOrderDraftExpireMinutesAsync();

        // 加载14项业务规则
        DraftExpireMinutes = await _ruleSettingsService.GetDraftExpireMinutesAsync();
        MaxBatchCount = await _ruleSettingsService.GetMaxBatchCountAsync();
        SilentCustomerDays = await _ruleSettingsService.GetSilentCustomerDaysAsync();
        ChurnRiskDays = await _ruleSettingsService.GetChurnRiskDaysAsync();
        StockWarningThreshold = await _ruleSettingsService.GetStockWarningThresholdAsync();
        StockCriticalThreshold = await _ruleSettingsService.GetStockCriticalThresholdAsync();
        MaxExportRows = await _ruleSettingsService.GetMaxExportRowsAsync();
        PasswordMinLength = await _ruleSettingsService.GetPasswordMinLengthAsync();
        PasswordExpireDays = await _ruleSettingsService.GetPasswordExpireDaysAsync();
        MaxLoginFailCount = await _ruleSettingsService.GetMaxLoginFailCountAsync();
        AccountLockoutMinutes = await _ruleSettingsService.GetAccountLockoutMinutesAsync();
        DefaultPageSize = await _ruleSettingsService.GetDefaultPageSizeAsync();
        AutoSaveIntervalSeconds = await _ruleSettingsService.GetAutoSaveIntervalSecondsAsync();
        BackupRetentionDays = await _ruleSettingsService.GetBackupRetentionDaysAsync();
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        try
        {
            if (OrderDraftExpireMinutes <= 0)
            {
                OrderDraftExpireMinutes = 30;
                ShowError("订单草稿有效期必须大于0，已回退为30分钟");
                return;
            }

            await SaveSettingAsync("CloseBehavior", ((int)CloseBehavior).ToString());
            await SaveSettingAsync("EnableNotification", EnableNotification.ToString());
            await SaveSettingAsync("EnableSound", EnableSound.ToString());
            await SaveSettingAsync("AutoSyncOnStartup", AutoSyncOnStartup.ToString());
            await SaveSettingAsync("SyncIntervalMinutes", SyncIntervalMinutes.ToString());
            await _configService.SetValueAsync(ConfigKeys.OrderDraftExpireMinutes, OrderDraftExpireMinutes.ToString(), "订单草稿有效期（分钟）");

            ShowSuccess("保存成功");
        }
        catch (Exception ex)
        {
            ShowError($"保存失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SaveBusinessRulesAsync()
    {
        try
        {
            await _ruleSettingsService.SaveSettingAsync("DraftExpireMinutes", DraftExpireMinutes.ToString());
            await _ruleSettingsService.SaveSettingAsync("MaxBatchCount", MaxBatchCount.ToString());
            await _ruleSettingsService.SaveSettingAsync("SilentCustomerDays", SilentCustomerDays.ToString());
            await _ruleSettingsService.SaveSettingAsync("ChurnRiskDays", ChurnRiskDays.ToString());
            await _ruleSettingsService.SaveSettingAsync("StockWarningThreshold", StockWarningThreshold.ToString());
            await _ruleSettingsService.SaveSettingAsync("StockCriticalThreshold", StockCriticalThreshold.ToString());
            await _ruleSettingsService.SaveSettingAsync("MaxExportRows", MaxExportRows.ToString());
            await _ruleSettingsService.SaveSettingAsync("PasswordMinLength", PasswordMinLength.ToString());
            await _ruleSettingsService.SaveSettingAsync("PasswordExpireDays", PasswordExpireDays.ToString());
            await _ruleSettingsService.SaveSettingAsync("MaxLoginFailCount", MaxLoginFailCount.ToString());
            await _ruleSettingsService.SaveSettingAsync("AccountLockoutMinutes", AccountLockoutMinutes.ToString());
            await _ruleSettingsService.SaveSettingAsync("DefaultPageSize", DefaultPageSize.ToString());
            await _ruleSettingsService.SaveSettingAsync("AutoSaveIntervalSeconds", AutoSaveIntervalSeconds.ToString());
            await _ruleSettingsService.SaveSettingAsync("BackupRetentionDays", BackupRetentionDays.ToString());
            _ruleSettingsService.InvalidateCache();
            ShowSuccess("业务规则保存成功");
        }
        catch (Exception ex)
        {
            ShowError($"业务规则保存失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private void OpenDepartmentManagement()
    {
        try
        {
            var treeVm = App.Services.GetService(typeof(DepartmentTreeViewModel)) as DepartmentTreeViewModel;
            if (treeVm == null) return;
            var dialog = new Views.DepartmentManagementWindow(treeVm) { Owner = System.Windows.Application.Current.MainWindow };
            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            ShowError($"打开部门管理失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private void OpenUserManagement()
    {
        try
        {
            var employeeListVm = App.Services.GetService(typeof(EmployeeListViewModel)) as EmployeeListViewModel;
            if (employeeListVm == null) return;
            var dialog = new Views.EmployeeListWindow(employeeListVm) { Owner = System.Windows.Application.Current.MainWindow };
            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            ShowError($"打开用户管理失败: {ex.Message}");
        }
    }

    private async Task SaveSettingAsync(string key, string value)
    {
        var setting = await _dbContext.LocalSettings.FirstOrDefaultAsync(s => s.SettingKey == key);
        if (setting == null)
        {
            setting = new LocalSetting { SettingKey = key, SettingType = "String" };
            _dbContext.LocalSettings.Add(setting);
        }
        setting.SettingValue = value;
        setting.UpdatedAt = DateTime.Now;
        await _dbContext.SaveChangesAsync();
    }

    public partial class FieldManagerViewModel : ViewModelBase
    {
        private readonly ProDbContext _db;

        public FieldManagerViewModel(ProDbContext db) { _db = db; }

        [ObservableProperty] private string _newDistrictName = string.Empty;
        [ObservableProperty] private string _newDistrictCity = string.Empty;
        [ObservableProperty] private bool _newDistrictIsActive = true;
        [ObservableProperty] private ObservableCollection<BusinessDistrict> _businessDistricts = [];
        public List<string> CityList { get; } =
        [
            "北京","上海","广州","深圳","杭州","成都","武汉","南京","重庆","天津","苏州","西安","长沙","郑州","东莞","青岛","沈阳","宁波","昆明","大连","厦门","合肥","佛山","福州","哈尔滨","济南","温州","长春","石家庄","常州","无锡","南宁","贵阳","太原","南昌","中山","惠州","海口","兰州","珠海","乌鲁木齐","绍兴","呼和浩特","泉州","南通","徐州","潍坊","唐山","烟台"
        ];

        [RelayCommand]
        private async Task LoadDistrictsAsync()
        {
            var branchId = CurrentSession.CurrentBranchId;
            var list = await _db.BusinessDistricts
                .Where(d => d.BranchId == branchId)
                .OrderBy(d => d.Name).ToListAsync();
            BusinessDistricts = new ObservableCollection<BusinessDistrict>(list);
        }

        [RelayCommand]
        private async Task AddDistrictAsync()
        {
            if (string.IsNullOrWhiteSpace(NewDistrictName)) return;
            try
            {
                var d = new BusinessDistrict
                {
                    Name = NewDistrictName,
                    City = NewDistrictCity,
                    BranchId = CurrentSession.CurrentBranchId,
                    Status = NewDistrictIsActive ? "Active" : "Inactive",
                    CreatedAt = DateTime.Now
                };
                _db.BusinessDistricts.Add(d);
                await _db.SaveChangesAsync();
                NewDistrictName = string.Empty;
                NewDistrictCity = string.Empty;
                NewDistrictIsActive = true;
                await LoadDistrictsAsync();
            }
            catch (Exception ex) { Serilog.Log.Error(ex, "新增商圈失败"); }
        }

        [RelayCommand]
        private async Task EditDistrictAsync(BusinessDistrict? d)
        {
            if (d == null) return;
            d.Status = d.Status == "Active" ? "Inactive" : "Active";
            await _db.SaveChangesAsync();
            await LoadDistrictsAsync();
        }

        [RelayCommand]
        private async Task DeleteDistrictAsync(BusinessDistrict? d)
        {
            if (d == null) return;
            _db.BusinessDistricts.Remove(d);
            await _db.SaveChangesAsync();
            await LoadDistrictsAsync();
        }

        [ObservableProperty] private string _newTagName = string.Empty;
        [ObservableProperty] private string _newTagColor = "#007AFF";
        [ObservableProperty] private ObservableCollection<CustomerTag> _customerTags = [];

        [RelayCommand]
        private async Task LoadTagsAsync()
        {
            var list = await _db.CustomerTags.OrderBy(t => t.Name).ToListAsync();
            CustomerTags = new ObservableCollection<CustomerTag>(list);
        }

        [RelayCommand]
        private async Task AddTagAsync()
        {
            if (string.IsNullOrWhiteSpace(NewTagName)) return;
            try
            {
                _db.CustomerTags.Add(new CustomerTag
                {
                    Name = NewTagName,
                    Color = NewTagColor,
                    BranchId = CurrentSession.CurrentBranchId
                });
                await _db.SaveChangesAsync();
                NewTagName = string.Empty;
                await LoadTagsAsync();
            }
            catch (Exception ex) { Serilog.Log.Error(ex, "新增标签失败"); }
        }

        [RelayCommand]
        private async Task EditTagAsync(CustomerTag? t)
        {
            if (t == null) return;
            try { await _db.SaveChangesAsync(); await LoadTagsAsync(); }
            catch (Exception ex) { Serilog.Log.Error(ex, "编辑标签失败"); }
        }

        [RelayCommand]
        private async Task DeleteTagAsync(CustomerTag? t)
        {
            if (t == null) return;
            _db.CustomerTags.Remove(t);
            await _db.SaveChangesAsync();
            await LoadTagsAsync();
        }

        [ObservableProperty] private string _newCategoryName = string.Empty;
        [ObservableProperty] private int _newCategorySortOrder;
        [ObservableProperty] private ObservableCollection<ProductCategory> _productCategories = [];

        [RelayCommand]
        private async Task LoadCategoriesAsync()
        {
            var list = await _db.ProductCategories.OrderBy(c => c.SortOrder).ToListAsync();
            ProductCategories = new ObservableCollection<ProductCategory>(list);
        }

        [RelayCommand]
        private async Task AddCategoryAsync()
        {
            if (string.IsNullOrWhiteSpace(NewCategoryName)) return;
            try
            {
                _db.ProductCategories.Add(new ProductCategory
                {
                    Name = NewCategoryName,
                    SortOrder = NewCategorySortOrder
                });
                await _db.SaveChangesAsync();
                NewCategoryName = string.Empty;
                NewCategorySortOrder = 0;
                await LoadCategoriesAsync();
            }
            catch (Exception ex) { Serilog.Log.Error(ex, "新增产品分类失败"); }
        }

        [RelayCommand]
        private async Task EditCategoryAsync(ProductCategory? c)
        {
            if (c == null) return;
            try { await _db.SaveChangesAsync(); await LoadCategoriesAsync(); }
            catch (Exception ex) { Serilog.Log.Error(ex, "编辑产品分类失败"); }
        }

        [RelayCommand]
        private async Task DeleteCategoryAsync(ProductCategory? c)
        {
            if (c == null) return;
            _db.ProductCategories.Remove(c);
            await _db.SaveChangesAsync();
            await LoadCategoriesAsync();
        }
    }
}
