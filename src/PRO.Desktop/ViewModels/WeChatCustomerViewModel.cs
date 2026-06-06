using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRO.Application.DTOs;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Windows;

namespace PRO.Desktop.ViewModels;

/// <summary>
/// 企业微信客户管理 ViewModel
/// </summary>
public partial class WeChatCustomerViewModel : ViewModelBase
{
    private readonly ProDbContext _dbContext;

    public WeChatCustomerViewModel()
    {
        _dbContext = App.Services.GetService(typeof(ProDbContext)) as ProDbContext
            ?? throw new InvalidOperationException("无法获取数据库上下文");

        IsUnlinkedTab = true;
        _ = LoadDataAsync();
    }

    // ==================== 属性 ====================

    [ObservableProperty]
    private ObservableCollection<WeChatCustomerListItem> _customers = new();

    [ObservableProperty]
    private WeChatCustomerListItem? _selectedCustomer;

    [ObservableProperty]
    private bool _isUnlinkedTab = true;

    [ObservableProperty]
    private bool _isLinkedTab;

    [ObservableProperty]
    private bool _isUnidentifiableTab;

    [ObservableProperty]
    private string? _searchKeyword;

    [ObservableProperty]
    private ObservableCollection<BranchListItem> _branches = new();

    [ObservableProperty]
    private BranchListItem? _selectedBranch;

    [ObservableProperty]
    private int _unlinkedCount;

    [ObservableProperty]
    private int _linkedCount;

    [ObservableProperty]
    private int _unidentifiableCount;

    [ObservableProperty]
    private ObservableCollection<CustomerListItem> _searchableCustomers = new();

    [ObservableProperty]
    private string? _linkSearchKeyword;

    [ObservableProperty]
    private string? _quickCreateName;

    /// <summary>
    /// 当前选中的Tab字符串
    /// </summary>
    private string CurrentTab => IsUnlinkedTab ? "Unlinked" : IsLinkedTab ? "Linked" : "Unidentifiable";

    // ==================== 公共方法 ====================

    public async Task LoadDataAsync()
    {
        IsLoading = true;
        try
        {
            var query = _dbContext.WeChatCustomers
                .Include(w => w.LinkedCustomer)
                .Include(w => w.AssignedBranch)
                .AsQueryable();

            // 按Tab筛选
            var currentTab = CurrentTab;
            query = currentTab switch
            {
                "Unlinked" => query.Where(w => w.Status == "Unlinked"),
                "Linked" => query.Where(w => w.Status == "Linked"),
                "Unidentifiable" => query.Where(w => w.Status == "Unidentifiable"),
                _ => query.Where(w => w.Status == "Unlinked")
            };

            // 搜索关键字
            if (!string.IsNullOrWhiteSpace(SearchKeyword))
            {
                query = query.Where(w => w.Name.Contains(SearchKeyword) ||
                    (w.AddUserName != null && w.AddUserName.Contains(SearchKeyword)));
            }

            var list = await query.OrderByDescending(w => w.CreatedAt).ToListAsync();

            Customers = new ObservableCollection<WeChatCustomerListItem>(
                list.Select(w => new WeChatCustomerListItem
                {
                    Id = w.Id,
                    ExternalUserId = w.ExternalUserId,
                    Name = w.Name,
                    AddUserName = w.AddUserName,
                    AddUserDepartmentName = w.AddUserDepartmentName,
                    CustomerNoInDesc = w.CustomerNoInDesc,
                    Status = w.Status,
                    LinkedCustomerId = w.LinkedCustomerId,
                    LinkedCustomerName = w.LinkedCustomer?.Name,
                    AssignedBranchId = w.AssignedBranchId,
                    AssignedBranchName = w.AssignedBranchName,
                    AvatarUrl = w.AvatarUrl,
                    CreatedAt = w.CreatedAt
                }));

            // 更新计数
            UnlinkedCount = await _dbContext.WeChatCustomers.CountAsync(w => w.Status == "Unlinked");
            LinkedCount = await _dbContext.WeChatCustomers.CountAsync(w => w.Status == "Linked");
            UnidentifiableCount = await _dbContext.WeChatCustomers.CountAsync(w => w.Status == "Unidentifiable");
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

    public async Task InitAsync()
    {
        await LoadBranchesAsync();
        await LoadDataAsync();
    }

    private async Task LoadBranchesAsync()
    {
        try
        {
            var list = await _dbContext.Branches
                .Where(b => b.Status == EntityStatus.Active)
                .OrderBy(b => b.Name)
                .ToListAsync();

            Branches = new ObservableCollection<BranchListItem>(
                list.Select(b => new BranchListItem
                {
                    Id = b.Id,
                    Name = b.Name,
                    Code = b.Code
                }));
        }
        catch { }
    }

    // ==================== 属性变更处理 ====================

    partial void OnIsUnlinkedTabChanged(bool value)
    {
        if (value) OnTabChanged();
    }

    partial void OnIsLinkedTabChanged(bool value)
    {
        if (value) OnTabChanged();
    }

    partial void OnIsUnidentifiableTabChanged(bool value)
    {
        if (value) OnTabChanged();
    }

    private async void OnTabChanged()
    {
        SearchKeyword = null;
        await LoadDataAsync();
    }

    // ==================== 命令 ====================

    [RelayCommand]
    private async Task SearchAsync()
    {
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task LinkCustomerAsync(WeChatCustomerListItem? item)
    {
        if (item == null) return;

        var selected = await Views.CustomerPickerWindow.ShowAsync(
            owner: System.Windows.Application.Current.MainWindow);

        if (selected != null)
        {
            await DoLinkAsync(item.Id, selected.Id);
        }
    }

    [RelayCommand]
    private async Task QuickCreateAndLinkAsync(WeChatCustomerListItem? item)
    {
        if (item == null) return;

        try
        {
            var branchId = item.AssignedBranchId ?? CurrentSession.CurrentBranchId;

            var customerNo = await GenerateCustomerNoAsync(branchId);
            var customer = new Customer
            {
                Name = item.Name,
                CustomerNo = customerNo,
                CustomerType = CustomerType.Sub,
                BranchId = branchId,
                CreatedById = CurrentSession.CurrentEmployeeId,
                WeChatExternalUserId = item.ExternalUserId,
                Status = CustomerStatus.Active,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                LocalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                SyncStatus = SyncStatus.Pending
            };

            _dbContext.Customers.Add(customer);
            await _dbContext.SaveChangesAsync();

            await DoLinkAsync(item.Id, customer.Id);
            ShowSuccess($"已快速创建客户「{customer.Name}」并关联成功");
        }
        catch (Exception ex)
        {
            ShowError($"创建客户失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task UnlinkCustomerAsync(WeChatCustomerListItem? item)
    {
        if (item == null) return;

        var result = MessageBox.Show($"确定要取消客户「{item.Name}」的关联吗？", "确认取消关联",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        try
        {
            var entity = await _dbContext.WeChatCustomers.FindAsync(item.Id);
            if (entity != null)
            {
                entity.LinkedCustomerId = null;
                entity.Status = "Unlinked";
                entity.UpdatedAt = DateTime.Now;
                entity.LocalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                entity.SyncStatus = SyncStatus.Pending;
                await _dbContext.SaveChangesAsync();
            }

            ShowSuccess("已取消关联");
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            ShowError($"取消关联失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task AssignBranchAsync(WeChatCustomerListItem? item)
    {
        if (item == null || SelectedBranch == null) return;

        try
        {
            var entity = await _dbContext.WeChatCustomers.FindAsync(item.Id);
            if (entity != null)
            {
                entity.AssignedBranchId = SelectedBranch.Id;
                entity.AssignedBranchName = SelectedBranch.Name;
                entity.Status = "Unlinked";
                entity.UpdatedAt = DateTime.Now;
                entity.LocalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                entity.SyncStatus = SyncStatus.Pending;
                await _dbContext.SaveChangesAsync();
            }

            ShowSuccess($"已分配至「{SelectedBranch.Name}」");
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            ShowError($"分配失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task AssignAllUnidentifiableAsync()
    {
        if (SelectedBranch == null)
        {
            ShowError("请先选择目标分公司");
            return;
        }

        var result = MessageBox.Show($"确定将所有无法识别的客户分配至「{SelectedBranch.Name}」吗？", "批量分配",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        try
        {
            var entities = await _dbContext.WeChatCustomers
                .Where(w => w.Status == "Unidentifiable")
                .ToListAsync();

            foreach (var entity in entities)
            {
                entity.AssignedBranchId = SelectedBranch.Id;
                entity.AssignedBranchName = SelectedBranch.Name;
                entity.Status = "Unlinked";
                entity.UpdatedAt = DateTime.Now;
                entity.LocalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                entity.SyncStatus = SyncStatus.Pending;
            }

            await _dbContext.SaveChangesAsync();
            ShowSuccess($"已将 {entities.Count} 个无法识别的客户分配至「{SelectedBranch.Name}」");
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            ShowError($"批量分配失败: {ex.Message}");
        }
    }

    // ==================== 辅助方法 ====================

    private async Task DoLinkAsync(int weChatCustomerId, int customerId)
    {
        try
        {
            var entity = await _dbContext.WeChatCustomers.FindAsync(weChatCustomerId);
            var customer = await _dbContext.Customers.FindAsync(customerId);

            if (entity != null && customer != null)
            {
                entity.LinkedCustomerId = customerId;
                entity.Status = "Linked";
                entity.UpdatedAt = DateTime.Now;
                entity.LocalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                entity.SyncStatus = SyncStatus.Pending;

                customer.WeChatExternalUserId = entity.ExternalUserId;
                customer.WeChatCustomerId = entity.ExternalUserId;
                customer.UpdatedAt = DateTime.Now;
                customer.LocalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                customer.SyncStatus = SyncStatus.Pending;

                await _dbContext.SaveChangesAsync();
                ShowSuccess($"已关联客户「{customer.Name}」");
                await LoadDataAsync();
            }
        }
        catch (Exception ex)
        {
            ShowError($"关联失败: {ex.Message}");
        }
    }

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

    public async Task SimulateMockDataAsync()
    {
        if (await _dbContext.WeChatCustomers.AnyAsync()) return;

        var mockCustomers = new List<WeChatCustomer>
        {
            new()
            {
                ExternalUserId = "wx_zhangsan_001",
                Name = "北京科技有限公司",
                AddUserId = "zhangsan",
                AddUserName = "张三",
                AddUserDepartmentId = 1,
                AddUserDepartmentName = "销售部",
                Status = "Unlinked",
                AssignedBranchId = 1,
                AssignedBranchName = "无锡分公司",
                CreatedAt = DateTime.Now.AddDays(-5)
            },
            new()
            {
                ExternalUserId = "wx_lisi_002",
                Name = "上海贸易有限公司",
                AddUserId = "lisi",
                AddUserName = "李四",
                AddUserDepartmentId = 2,
                AddUserDepartmentName = "市场部",
                Status = "Unlinked",
                AssignedBranchId = 2,
                AssignedBranchName = "上海分公司",
                CreatedAt = DateTime.Now.AddDays(-3)
            },
            new()
            {
                ExternalUserId = "wx_wangwu_003",
                Name = "深圳创新科技",
                AddUserId = "wangwu",
                AddUserName = "王五",
                AddUserDepartmentId = 1,
                AddUserDepartmentName = "销售部",
                Status = "Unlinked",
                AssignedBranchId = 3,
                AssignedBranchName = "深圳分公司",
                CreatedAt = DateTime.Now.AddDays(-1)
            },
            new()
            {
                ExternalUserId = "wx_zhaoliu_004",
                Name = "广州咨询公司",
                AddUserId = "zhaoliu",
                AddUserName = "赵六",
                Status = "Unidentifiable",
                CreatedAt = DateTime.Now.AddHours(-12)
            },
            new()
            {
                ExternalUserId = "wx_sunqi_005",
                Name = "杭州电商平台",
                AddUserId = "sunqi",
                AddUserName = "孙七",
                AddUserDepartmentId = 3,
                AddUserDepartmentName = "客服部",
                CustomerNoInDesc = "K202605210001",
                Status = "Linked",
                AssignedBranchId = 4,
                AssignedBranchName = "杭州分公司",
                CreatedAt = DateTime.Now.AddDays(-7)
            },
            new()
            {
                ExternalUserId = "wx_zhouba_006",
                Name = "成都餐饮管理",
                AddUserId = "zhouba",
                AddUserName = "周八",
                Status = "Unidentifiable",
                CreatedAt = DateTime.Now.AddHours(-6)
            },
            new()
            {
                ExternalUserId = "wx_wujiu_007",
                Name = "南京软件工作室",
                AddUserId = "wujiu",
                AddUserName = "吴九",
                AddUserDepartmentId = 4,
                AddUserDepartmentName = "研发部",
                Status = "Unlinked",
                AssignedBranchId = 5,
                AssignedBranchName = "南京分公司",
                CreatedAt = DateTime.Now.AddHours(-2)
            }
        };

        _dbContext.WeChatCustomers.AddRange(mockCustomers);
        await _dbContext.SaveChangesAsync();
    }
}
