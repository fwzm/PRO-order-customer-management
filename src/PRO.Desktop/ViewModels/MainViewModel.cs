using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using Serilog;

namespace PRO.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _currentUserName = string.Empty;

    [ObservableProperty]
    private string _currentBranchName = string.Empty;

    [ObservableProperty]
    private string _currentRoleName = string.Empty;

    [ObservableProperty]
    private bool _isHeadquartersAdmin;

    [ObservableProperty]
    private bool _canManageOrganization;

    [ObservableProperty]
    private bool _canSettlement;

    [ObservableProperty]
    private bool _canManageWorkPlan;

    [ObservableProperty]
    private SyncStatus _syncStatus = SyncStatus.Synced;

    [ObservableProperty]
    private string _syncStatusText = "已同步";

    [ObservableProperty]
    private DateTime _lastSyncTime;

    [ObservableProperty]
    private int _pendingSyncCount;

    [ObservableProperty]
    private ObservableCollection<NavigationItem> _navigationItems = new();

    [ObservableProperty]
    private ObservableCollection<TabItem> _tabItems = new();

    [ObservableProperty]
    private TabItem? _selectedTab;

    [ObservableProperty]
    private NavigationItem? _selectedNavigation;

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    [ObservableProperty]
    private bool _isSearchVisible;

    [ObservableProperty]
    private bool _isSearchFocused;

    private ICollectionView? _navigationView;
    private readonly Dictionary<string, (DispatcherTimer Timer, EventHandler Handler)> _hoverTimers = new();
    private readonly Dictionary<string, (DispatcherTimer Timer, EventHandler Handler)> _collapseTimers = new();

    public MainViewModel()
    {
    }

    public void InitializeAfterLogin()
    {
        InitializeSession();
        InitializeNavigation();
        NavigateToDashboard();
        RunInBackground(LoadBadgeCountsAsync(), "加载导航角标失败");
    }

    private void InitializeSession()
    {
        var session = CurrentSession.Current;
        CurrentUserName = session.Name;
        CurrentBranchName = session.BranchName;
        CurrentRoleName = session.RoleName;
        IsHeadquartersAdmin = session.IsHeadquartersAdmin;
        CanManageOrganization = session.CanManageOrganization;
        CanSettlement = session.CanSettlement;
        CanManageWorkPlan = session.CanManageWorkPlan;
    }

    private void InitializeNavigation()
    {
        NavigationItems.Clear();

        // 1. 工作台
        NavigationItems.Add(new NavigationItem
        {
            Id = "dashboard",
            Title = "工作台",
            SortOrder = 1,
            ViewModelType = typeof(DashboardViewModel)
        });

        // 2. 客户·SCRM
        var customerCat = new NavigationItem
        {
            Id = "cat_customer",
            Title = "客户·SCRM",
            SortOrder = 2,
            IsExpanded = true
        };
        customerCat.Children.Add(new NavigationItem
        {
            Id = "customer_add", Title = "添加客户", SortOrder = 21, ParentId = "cat_customer",
            ViewModelType = typeof(CustomerListViewModel)
        });
        customerCat.Children.Add(new NavigationItem
        {
            Id = "customer", Title = "客户列表", SortOrder = 22, ParentId = "cat_customer",
            ViewModelType = typeof(CustomerListViewModel)
        });
        customerCat.Children.Add(new NavigationItem
        {
            Id = "wechat_scrm", Title = "企微SCRM", SortOrder = 23, ParentId = "cat_customer",
            ViewModelType = typeof(WeChatScrmViewModel)
        });
        customerCat.Children.Add(new NavigationItem
        {
            Id = "visit_opportunity", Title = "拜访/机会", SortOrder = 24, ParentId = "cat_customer",
            ViewModelType = typeof(VisitOpportunityViewModel)
        });
        customerCat.Children.Add(new NavigationItem
        {
            Id = "district_tag", Title = "商圈/标签", SortOrder = 25, ParentId = "cat_customer",
            ViewModelType = typeof(DistrictTagViewModel)
        });
        NavigationItems.Add(customerCat);

        // 3. 订单
        var orderCat = new NavigationItem
        {
            Id = "cat_order",
            Title = "订单",
            SortOrder = 3,
            IsExpanded = true
        };
        orderCat.Children.Add(new NavigationItem
        {
            Id = "order_new", Title = "新建订单", SortOrder = 31, ParentId = "cat_order",
            ViewModelType = typeof(OrderListViewModel)
        });
        orderCat.Children.Add(new NavigationItem
        {
            Id = "order_draft", Title = "草稿订单", SortOrder = 32, ParentId = "cat_order",
            ViewModelType = typeof(OrderListViewModel)
        });
        orderCat.Children.Add(new NavigationItem
        {
            Id = "order", Title = "订单列表", SortOrder = 33, ParentId = "cat_order",
            ViewModelType = typeof(OrderListViewModel)
        });
        NavigationItems.Add(orderCat);

        // 4. 财务
        var financeCat = new NavigationItem
        {
            Id = "cat_finance",
            Title = "财务",
            SortOrder = 4
        };
        if (CanSettlement)
        {
            financeCat.Children.Add(new NavigationItem
            {
                Id = "settlement", Title = "结算", SortOrder = 41, ParentId = "cat_finance",
                ViewModelType = typeof(SettlementListViewModel)
            });
        }
        financeCat.Children.Add(new NavigationItem
        {
            Id = "ar", Title = "应收款", SortOrder = 42, ParentId = "cat_finance",
            ViewModelType = typeof(AccountsReceivableViewModel)
        });
        NavigationItems.Add(financeCat);

        // 5. 库存·物流
        var logisticsCat = new NavigationItem
        {
            Id = "cat_logistics",
            Title = "库存·物流",
            SortOrder = 5
        };
        logisticsCat.Children.Add(new NavigationItem
        {
            Id = "product", Title = "产品管理", SortOrder = 51, ParentId = "cat_logistics",
            ViewModelType = typeof(ProductListViewModel)
        });
        logisticsCat.Children.Add(new NavigationItem
        {
            Id = "logistics", Title = "物流管理", SortOrder = 52, ParentId = "cat_logistics",
            ViewModelType = typeof(DeliveryPersonListViewModel)
        });
        logisticsCat.Children.Add(new NavigationItem
        {
            Id = "inventory", Title = "库存盘点", SortOrder = 53, ParentId = "cat_logistics",
            ViewModelType = typeof(InventoryViewModel)
        });
        NavigationItems.Add(logisticsCat);

        // 6. 工作计划
        NavigationItems.Add(new NavigationItem
        {
            Id = "workplan",
            Title = "工作计划",
            SortOrder = 6,
            ViewModelType = typeof(WorkScheduleViewModel)
        });

        // 7. 数据分析
        var analyticsCat = new NavigationItem
        {
            Id = "cat_analytics",
            Title = "数据分析",
            SortOrder = 7
        };
        analyticsCat.Children.Add(new NavigationItem
        {
            Id = "reports", Title = "报表中心", SortOrder = 71, ParentId = "cat_analytics",
            ViewModelType = typeof(ReportCenterViewModel)
        });
        analyticsCat.Children.Add(new NavigationItem
        {
            Id = "prediction", Title = "智能预测", SortOrder = 72, ParentId = "cat_analytics",
            ViewModelType = typeof(PredictionDashboardViewModel)
        });
        NavigationItems.Add(analyticsCat);

        // 分隔线
        NavigationItems.Add(new NavigationItem { Id = "sep1", Title = "-", SortOrder = 99 });

        // 8. 系统设置
        var systemCat = new NavigationItem
        {
            Id = "cat_system",
            Title = "系统设置",
            SortOrder = 100
        };

        if (CanManageOrganization)
        {
            systemCat.Children.Add(new NavigationItem
            {
                Id = "org_structure", Title = "组织架构", SortOrder = 101, ParentId = "cat_system",
                ViewModelType = typeof(EmployeeListViewModel)
            });
        }
        systemCat.Children.Add(new NavigationItem
        {
            Id = "system", Title = "应用设置", SortOrder = 102, ParentId = "cat_system",
            ViewModelType = typeof(SystemSettingsViewModel)
        });
        NavigationItems.Add(systemCat);

        // 初始化 CollectionView
        _navigationView = CollectionViewSource.GetDefaultView(NavigationItems);
        if (_navigationView != null)
        {
            _navigationView.Filter = FilterNavigation;
        }
    }

    private bool FilterNavigation(object obj)
    {
        if (string.IsNullOrWhiteSpace(SearchKeyword))
            return true;

        if (obj is not NavigationItem item)
            return true;

        var keyword = SearchKeyword.Trim().ToLower();

        // 单层菜单项：直接匹配标题
        if (!item.IsCategory && item.Id != "sep1")
        {
            var match = item.Title.ToLower().Contains(keyword);
            item.SearchScore = match ? 1 : 0;
            return match;
        }

        // 分类目录：匹配标题 或 任意子项匹配
        if (item.IsCategory)
        {
            var titleMatch = item.Title.ToLower().Contains(keyword);
            var anyChildMatch = item.Children.Any(c => c.Title.ToLower().Contains(keyword));

            if (titleMatch || anyChildMatch)
            {
                // 标记匹配的子项
                foreach (var child in item.Children)
                {
                    child.SearchScore = child.Title.ToLower().Contains(keyword) ? 1 : 0;
                }
                item.SearchScore = 1;
                if (!item.IsExpanded)
                    item.IsExpanded = true;
                return true;
            }
        }

        item.SearchScore = 0;
        return false;
    }

    partial void OnSearchKeywordChanged(string value)
    {
        _navigationView?.Refresh();

        // 搜索时自动展开分类
        if (!string.IsNullOrWhiteSpace(value))
        {
            foreach (var item in NavigationItems)
            {
                if (item.IsCategory && item.IsSearchMatch && !item.IsExpanded)
                    item.IsExpanded = true;
            }
        }
    }

    [RelayCommand]
    private void ToggleCategory(NavigationItem item)
    {
        if (item == null) return;

        bool expanding = !item.IsExpanded;

        if (expanding)
        {
            foreach (var navItem in NavigationItems)
            {
                if (navItem.IsCategory && navItem != item)
                {
                    navItem.IsExpanded = false;
                }
            }
        }

        item.IsExpanded = expanding;
    }

    // ==================== 悬浮展开/收起逻辑 ====================

    public void NotifyCategoryMouseEnter(NavigationItem item)
    {
        if (item == null || !item.IsCategory) return;

        CancelCollapseTimer(item.Id);
        CancelHoverTimer(item.Id);

        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        EventHandler handler = (s, e) =>
        {
            timer.Stop();
            item.IsExpanded = true;
        };
        timer.Tick += handler;
        timer.Start();

        _hoverTimers[item.Id] = (timer, handler);
    }

    public void NotifyCategoryMouseLeave(NavigationItem item)
    {
        if (item == null || !item.IsCategory) return;
        CancelHoverTimer(item.Id);

        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(800)
        };
        EventHandler handler = (s, e) =>
        {
            timer.Stop();
            item.IsExpanded = false;
        };
        timer.Tick += handler;
        timer.Start();

        _collapseTimers[item.Id] = (timer, handler);
    }

    public void NotifyCategoryChildEnter(NavigationItem item)
    {
        if (item == null || !item.IsCategory) return;
        CancelCollapseTimer(item.Id);
    }

    public void NotifyCategoryChildLeave(NavigationItem item)
    {
        if (item == null || !item.IsCategory) return;
        NotifyCategoryMouseLeave(item);
    }

    private void CancelHoverTimer(string id)
    {
        if (_hoverTimers.TryGetValue(id, out var entry))
        {
            entry.Timer.Stop();
            entry.Timer.Tick -= entry.Handler;
            _hoverTimers.Remove(id);
        }
    }

    private void CancelCollapseTimer(string id)
    {
        if (_collapseTimers.TryGetValue(id, out var entry))
        {
            entry.Timer.Stop();
            entry.Timer.Tick -= entry.Handler;
            _collapseTimers.Remove(id);
        }
    }

    // ==================== 角标加载 ====================

    private async Task LoadBadgeCountsAsync()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ProDbContext>();

            // 物流管理：待配送订单
            var logisticsItem = FindChildItem("logistics");
            if (logisticsItem != null)
            {
                var deliveringCount = await dbContext.Orders
                    .CountAsync(o => o.Status == OrderStatus.Assigned || o.Status == OrderStatus.Delivering);
                logisticsItem.BadgeCount = deliveringCount;
            }

            // 企微SCRM：待同步客户
            var wechatItem = FindChildItem("wechat_scrm");
            if (wechatItem != null)
            {
                var pendingSync = await dbContext.OperationLogs
                    .CountAsync(o => o.SyncStatus == SyncStatus.Pending);
                wechatItem.BadgeCount = pendingSync;
            }

            // 订单：待处理
            var orderItem = FindChildItem("order");
            if (orderItem != null)
            {
                var pendingOrders = await dbContext.Orders
                    .CountAsync(o => o.Status == OrderStatus.Pending);
                orderItem.BadgeCount = pendingOrders;
            }

            // 待同步总数
            PendingSyncCount = await dbContext.OperationLogs
                .CountAsync(o => o.SyncStatus == SyncStatus.Pending);

            UpdateSyncStatusText();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "加载导航角标失败");
        }
    }

    private NavigationItem? FindChildItem(string id)
    {
        foreach (var nav in NavigationItems)
        {
            var child = nav.Children.FirstOrDefault(c => c.Id == id);
            if (child != null) return child;
        }
        return null;
    }

    partial void OnPendingSyncCountChanged(int value)
    {
        UpdateSyncStatusText();
    }

    private void UpdateSyncStatusText()
    {
        if (PendingSyncCount > 0)
            SyncStatusText = $"待同步 {PendingSyncCount} 条";
        else
            SyncStatusText = "已同步";
    }

    // ==================== 导航逻辑 ====================

    partial void OnSelectedNavigationChanged(NavigationItem? value)
    {
        if (value == null || string.IsNullOrEmpty(value.Id) || value.Id.StartsWith("sep") || value.Id.StartsWith("cat_"))
            return;

        NavigateToTab(value);
    }

    [RelayCommand]
    private void NavigateToTab(NavigationItem navItem)
    {
        if (navItem == null || navItem.ViewModelType == null)
            return;

        if (navItem.Id == "customer_add")
        {
            var customerListNav = NavigationItems.FirstOrDefault(n => n.Id == "customer");
            if (customerListNav != null)
                NavigateToTab(customerListNav);

            var listTab = TabItems.FirstOrDefault(t => t.Id == "customer");
            if (listTab?.ViewModel is CustomerListViewModel customerVm)
            {
                customerVm.NewCustomerCommand.Execute(null);
            }
            return;
        }

        if (navItem.Id == "order_new")
        {
            var orderListNav = NavigationItems
                .SelectMany(n => n.Children)
                .FirstOrDefault(n => n.Id == "order");
            if (orderListNav != null)
                NavigateToTab(orderListNav);

            var listTab = TabItems.FirstOrDefault(t => t.Id == "order");
            if (listTab?.ViewModel is OrderListViewModel orderVm)
            {
                orderVm.NewOrderCommand.Execute(null);
            }
            return;
        }

        var existingTab = TabItems.FirstOrDefault(t => t.Id == navItem.Id);
        if (existingTab != null)
        {
            SelectTab(existingTab);
            return;
        }

        var tab = new TabItem
        {
            Id = navItem.Id,
            Title = navItem.Title,
            IsSelected = true,
            ViewModel = App.Services.GetService(navItem.ViewModelType) as ViewModelBase
        };

        if (navItem.Id == "order_draft" && tab.ViewModel is OrderListViewModel draftVm)
        {
            draftVm.ShowOnlyDrafts = true;
            RunInBackground(draftVm.RefreshCommand.ExecuteAsync(null), "加载草稿订单失败");
        }

        TabItems.Add(tab);
        SelectTab(tab);
    }

    private void SelectTab(TabItem tab)
    {
        foreach (var t in TabItems)
            t.IsSelected = false;

        tab.IsSelected = true;
        SelectedTab = tab;
    }

    [RelayCommand]
    private void CloseTab(TabItem? tab)
    {
        if (tab == null || !tab.IsClosable)
            return;

        var index = TabItems.IndexOf(tab);
        TabItems.Remove(tab);

        if (TabItems.Count > 0)
        {
            SelectedTab = TabItems[Math.Max(0, index - 1)];
        }
    }

    [RelayCommand]
    private void NavigateToDashboard()
    {
        var dashboardNav = NavigationItems.FirstOrDefault(n => n.Id == "dashboard");
        if (dashboardNav != null)
            NavigateToTab(dashboardNav);
    }

    [RelayCommand]
    private async Task RefreshCurrentTabAsync()
    {
        if (SelectedTab?.ViewModel is PagedViewModelBase pagedVm)
        {
            await pagedVm.RefreshCommand.ExecuteAsync(null);
        }
        else if (SelectedTab?.ViewModel is ViewModelBase vm)
        {
            var method = vm.GetType().GetMethod("RefreshAsync");
            if (method != null)
                await (Task)method.Invoke(vm, null)!;
        }

        LastSyncTime = DateTime.Now;
        RunInBackground(LoadBadgeCountsAsync(), "加载导航角标失败");
    }

    [RelayCommand]
    private void ToggleSearch()
    {
        IsSearchVisible = !IsSearchVisible;
        if (!IsSearchVisible)
        {
            SearchKeyword = string.Empty;
            IsSearchFocused = false;
        }
        else
        {
            IsSearchFocused = true;
        }
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchKeyword = string.Empty;
        IsSearchFocused = false;
    }

    [RelayCommand]
    private void Logout()
    {
        var result = MessageBox.Show("确定要退出登录吗？", "确认退出", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            var app = System.Windows.Application.Current as App;
            if (app != null)
            {
                app.StopAllTimers();
            }
            CurrentSession.ClearSession();
            System.Windows.Application.Current.Shutdown();
        }
    }

    [RelayCommand]
    private void ManualSync() => ShowSuccess("数据已实时同步至 PostgreSQL");
}
