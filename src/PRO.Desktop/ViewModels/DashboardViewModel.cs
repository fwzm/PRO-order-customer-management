using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClosedXML.Excel;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Infrastructure.Persistence;
using PRO.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using PRO.Domain.Enums;
using Serilog;

namespace PRO.Desktop.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly ProDbContext _dbContext;
    private readonly DashboardService _dashboardService;
    private readonly AuditService _auditService;
    private readonly BusinessRuleService _ruleService;

    [ObservableProperty]
    private string _currentUserName = string.Empty;

    [ObservableProperty]
    private string _welcomeMessage = string.Empty;

    public DateTime CurrentDate => DateTime.Now;

    public string CurrentDateDisplay
    {
        get
        {
            var now = DateTime.Now;
            var weekday = now.DayOfWeek switch
            {
                DayOfWeek.Monday => "星期一",
                DayOfWeek.Tuesday => "星期二",
                DayOfWeek.Wednesday => "星期三",
                DayOfWeek.Thursday => "星期四",
                DayOfWeek.Friday => "星期五",
                DayOfWeek.Saturday => "星期六",
                DayOfWeek.Sunday => "星期日",
                _ => ""
            };
            return $"{now:MM月dd日} {weekday}";
        }
    }

    // ==================== 业务数据 ====================
    [ObservableProperty]
    private BusinessDashboardDto? _dashboardData;

    [ObservableProperty]
    private int _todayOrderCount;

    [ObservableProperty]
    private decimal _todayOrderAmount;

    [ObservableProperty]
    private int _pendingOrderCount;

    [ObservableProperty]
    private int _deliveringOrderCount;

    [ObservableProperty]
    private int _unassignedOrderCount;

    [ObservableProperty]
    private int _draftOrderCount;

    [ObservableProperty]
    private int _overduePaymentCount;

    [ObservableProperty]
    private decimal _overduePaymentAmount;

    [ObservableProperty]
    private int _pendingSettlementCount;

    [ObservableProperty]
    private decimal _pendingSettlementAmount;

    [ObservableProperty]
    private int _todayDeliveryCount;

    [ObservableProperty]
    private int _deliveryFailedCount;

    [ObservableProperty]
    private int _newCustomerCount;

    [ObservableProperty]
    private int _visitReminderCount;

    [ObservableProperty]
    private List<DashboardAlert> _alerts = [];

    [ObservableProperty]
    private List<DashboardTask> _todayTasks = [];

    [ObservableProperty]
    private List<DashboardShortcut> _shortcuts = [];

    [ObservableProperty]
    private List<OrderListItem> _recentOrders = [];

    [ObservableProperty]
    private string _syncStatusText = "本地数据已加载";

    [ObservableProperty]
    private DateTime _lastSyncTime = DateTime.Now;

    public DashboardViewModel()
    {
        _dbContext = App.Services.GetService(typeof(ProDbContext)) as ProDbContext
            ?? throw new InvalidOperationException("无法获取数据库上下文");
        _dashboardService = App.Services.GetService(typeof(DashboardService)) as DashboardService
            ?? throw new InvalidOperationException("无法获取工作台服务");
        _auditService = App.Services.GetService(typeof(AuditService)) as AuditService
            ?? throw new InvalidOperationException("无法获取审计服务");
        _ruleService = App.Services.GetService(typeof(BusinessRuleService)) as BusinessRuleService
            ?? throw new InvalidOperationException("无法获取业务规则服务");

        CurrentUserName = CurrentSession.Current.Name;
        WelcomeMessage = $"今天是 {DateTime.Now:yyyy年MM月dd日}，{GetGreeting()}";

        RunInBackground(LoadDataAsync(), "工作台加载失败");
    }

    private string GetGreeting()
    {
        var hour = DateTime.Now.Hour;
        return hour switch
        {
            >= 5 and < 11 => "早上好",
            >= 11 and < 14 => "中午好",
            >= 14 and < 18 => "下午好",
            _ => "晚上好"
        };
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        IsLoading = true;
        try
        {
            var branchId = CurrentSession.CurrentBranchId;

            // 通过 DashboardService 获取（Service 层已内置缓存逻辑）
            var result = await _dashboardService.GetDashboardDataAsync(branchId);

            if (result.Success && result.Data != null)
            {
                PopulateDashboard(result.Data);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "仪表盘数据加载失败");
            ShowBusinessException(ex, "加载仪表盘");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void PopulateDashboard(BusinessDashboardDto data)
    {
        DashboardData = data;

        // 今日概览
        TodayOrderCount = data.TodayOrderCount;
        TodayOrderAmount = data.TodayOrderAmount;
        PendingOrderCount = data.PendingOrderCount;
        DeliveringOrderCount = data.DeliveringOrderCount;

        // 待处理事项
        UnassignedOrderCount = data.UnassignedOrderCount;
        DraftOrderCount = data.DraftOrderCount;
        OverduePaymentCount = data.OverduePaymentCount;
        OverduePaymentAmount = data.OverduePaymentAmount;
        PendingSettlementCount = data.PendingSettlementCount;
        PendingSettlementAmount = data.PendingSettlementAmount;

        // 配送相关
        TodayDeliveryCount = data.TodayDeliveryCount;
        DeliveryFailedCount = data.DeliveryFailedCount;

        // 客户相关
        NewCustomerCount = data.NewCustomerCount;
        VisitReminderCount = data.VisitReminderCount;

        // 列表数据
        Alerts = data.Alerts;
        TodayTasks = data.TodayTasks;
        Shortcuts = data.Shortcuts;
        RecentOrders = data.RecentOrders;
        SyncStatusText = data.RecentErrorCount > 0
            ? $"今日有 {data.RecentErrorCount} 个异常"
            : "业务数据正常";
        LastSyncTime = DateTime.Now;
    }

    // ==================== 快捷操作 ====================

    [RelayCommand]
    private void NavigateToShortcut(DashboardShortcut? shortcut)
    {
        if (shortcut == null) return;
        NavigateToRoute(shortcut.Route);
    }

    [RelayCommand]
    private void NavigateToTask(DashboardTask? task)
    {
        if (task == null || string.IsNullOrEmpty(task.ActionRoute)) return;
        NavigateToRoute(task.ActionRoute);
    }

    [RelayCommand]
    private void NavigateToAlert(DashboardAlert? alert)
    {
        if (alert == null || string.IsNullOrEmpty(alert.ActionRoute)) return;
        NavigateToRoute(alert.ActionRoute);
    }

    [RelayCommand]
    private async Task ExecuteDashboardActionAsync(DashboardAction? action)
    {
        if (action == null) return;

        switch (action.Action)
        {
            case "AutoAssignOrders":
                await AutoAssignOrdersAsync();
                break;
            case "ConfirmAllDrafts":
                await ConfirmAllDraftsAsync();
                break;
            case "ExportOverdueList":
                await ExportOverdueListAsync();
                break;
            case "ViewDrafts":
                NavigateToRoute("order_draft");
                break;
            case "ViewOverdueDetails":
                NavigateToRoute("ar");
                break;
            case "QuickSettlement":
            case "ViewSettlementOrders":
                NavigateToRoute("settlement");
                break;
            case "NavigateToOrders":
            case "ExportDeliveryPlan":
                NavigateToRoute("order");
                break;
            case "MarkFollowUp":
                NavigateToRoute("visit_opportunity");
                break;
            default:
                NavigateToRoute("order");
                break;
        }
    }

    private async Task AutoAssignOrdersAsync()
    {
        if (!CheckDeliveryPermission("AutoAssign")) return;
        if (!ConfirmAction("自动分配", "确认对当前分公司的待分配订单执行自动分配？")) return;

        var service = App.Services.GetService(typeof(IOrderDistributionService)) as IOrderDistributionService;
        if (service == null)
        {
            ShowError("无法获取自动分配服务");
            return;
        }

        var pendingBefore = await _dbContext.Orders.CountAsync(o =>
            o.BranchId == CurrentSession.CurrentBranchId && o.Status == OrderStatus.Pending);

        ApiResponse<bool>? result = null;
        var executed = await ExecuteWithRetryAsync(async () =>
        {
            result = await service.AutoAssignAsync(CurrentSession.CurrentBranchId);
            if (result == null || !result.Success)
                throw new InvalidOperationException(result?.Message ?? "自动分配失败");
        }, "自动分配订单", showSuccess: false);

        if (!executed || result == null) return;

        var pendingAfter = await _dbContext.Orders.CountAsync(o =>
            o.BranchId == CurrentSession.CurrentBranchId && o.Status == OrderStatus.Pending);
        var successCount = Math.Max(0, pendingBefore - pendingAfter);
        await _auditService.LogAutoAssignAsync(CurrentSession.CurrentEmployeeId, CurrentSession.CurrentBranchId, successCount, pendingBefore);
        ShowSuccess(result.Message);
        await LoadDataAsync();
    }

    private async Task ConfirmAllDraftsAsync()
    {
        if (!CheckOrderPermission("BatchStatusChange")) return;

        var draftIds = await _dbContext.Orders
            .AsNoTracking()
            .Where(o => o.BranchId == CurrentSession.CurrentBranchId && o.Status == OrderStatus.Draft)
            .OrderBy(o => o.CreatedAt)
            .Select(o => o.Id)
            .ToListAsync();

        if (draftIds.Count == 0)
        {
            ShowSuccess("当前没有待确认的草稿订单");
            return;
        }

        if (!ConfirmAction("全部确认草稿", $"确认将 {draftIds.Count} 条草稿订单全部转为待分配？")) return;

        var orderService = App.Services.GetService(typeof(IOrderService)) as IOrderService;
        if (orderService == null)
        {
            ShowError("无法获取订单服务");
            return;
        }

        var successCount = 0;
        var executed = await ExecuteWithRetryAsync(async () =>
        {
            successCount = 0;
            foreach (var orderId in draftIds)
            {
                var confirmResult = await orderService.ConfirmDraftAsync(orderId, CurrentSession.CurrentEmployeeId);
                if (confirmResult.Success) successCount++;
            }
        }, "全部确认草稿", showSuccess: false);

        if (!executed) return;

        await _auditService.LogOrderStatusChangeAsync(
            CurrentSession.CurrentEmployeeId,
            0,
            $"批量草稿订单({successCount})",
            OrderStatus.Draft,
            OrderStatus.Pending,
            "工作台全部确认");
        ShowSuccess($"已确认 {successCount}/{draftIds.Count} 条草稿订单");
        await LoadDataAsync();
    }

    private async Task ExportOverdueListAsync()
    {
        if (!CheckOrderPermission("Export")) return;

        var overdueDays = _ruleService.GetExportFileRetentionDays();
        var overdueDate = DateTime.Now.AddDays(-overdueDays);
        var orders = await _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Where(o => o.BranchId == CurrentSession.CurrentBranchId
                && o.PaymentStatus != PaymentStatus.Paid
                && o.Status == OrderStatus.Completed
                && o.CreatedAt < overdueDate)
            .OrderBy(o => o.CreatedAt)
            .ToListAsync();

        if (orders.Count == 0)
        {
            ShowSuccess("当前没有超期应收订单");
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "Excel文件|*.xlsx",
            FileName = $"催收清单_{DateTime.Now:yyyyMMdd}"
        };

        if (dialog.ShowDialog() != true) return;

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("催收清单");
        var headers = new[] { "订单号", "客户", "联系电话", "订单金额", "已收金额", "应收金额", "超期天数", "创建时间", "配送地址" };
        for (var i = 0; i < headers.Length; i++)
            sheet.Cell(1, i + 1).Value = headers[i];

        var row = 2;
        var now = DateTime.Now;
        foreach (var order in orders)
        {
            sheet.Cell(row, 1).Value = order.OrderNo;
            sheet.Cell(row, 2).Value = order.Customer?.Name ?? "";
            sheet.Cell(row, 3).Value = order.Customer?.Phone ?? "";
            sheet.Cell(row, 4).Value = order.TotalAmount;
            sheet.Cell(row, 5).Value = order.ReceivedAmount;
            sheet.Cell(row, 6).Value = order.TotalAmount - order.ReceivedAmount;
            sheet.Cell(row, 7).Value = (int)(now - order.CreatedAt).TotalDays;
            sheet.Cell(row, 8).Value = order.CreatedAt;
            sheet.Cell(row, 9).Value = order.DeliveryAddress ?? "";
            row++;
        }

        sheet.Columns().AdjustToContents();
        workbook.SaveAs(dialog.FileName);
        await _auditService.LogExportAsync(CurrentSession.CurrentEmployeeId, "催收清单", orders.Count);
        ShowSuccess($"催收清单已生成：{orders.Count} 条");
    }

    private void NavigateToRoute(string route)
    {
        var mainVm = System.Windows.Application.Current.MainWindow?.DataContext as MainViewModel;
        if (mainVm == null) return;

        // 处理特殊路由
        if (route == "order_new")
        {
            var navItem = mainVm.NavigationItems.FirstOrDefault(n => n.Id == "order");
            if (navItem != null)
            {
                mainVm.NavigateToTabCommand.Execute(navItem);
                var orderVm = App.Services.GetService(typeof(OrderEditViewModel)) as OrderEditViewModel;
                if (orderVm != null)
                {
                    var win = new Views.OrderEditWindow(orderVm) { Owner = System.Windows.Application.Current.MainWindow };
                    win.ShowDialog();
                }
            }
            return;
        }

        // 查找导航项
        var nav = mainVm.NavigationItems
            .SelectMany<NavigationItem, NavigationItem>(n => n.Children.Count > 0 ? n.Children : [n])
            .FirstOrDefault(n => n.Id == route);

        if (nav != null)
        {
            mainVm.NavigateToTabCommand.Execute(nav);
        }
    }

    [RelayCommand]
    private void QuickCreateOrder()
    {
        NavigateToRoute("order_new");
    }

    [RelayCommand]
    private void QuickCreateCustomer()
    {
        var mainVm = System.Windows.Application.Current.MainWindow?.DataContext as MainViewModel;
        if (mainVm == null) return;
        var navItem = mainVm.NavigationItems.FirstOrDefault(n => n.Id == "customer");
        if (navItem != null)
        {
            mainVm.NavigateToTabCommand.Execute(navItem);
            var custVm = App.Services.GetService(typeof(CustomerEditViewModel)) as CustomerEditViewModel;
            if (custVm != null)
            {
                var win = new Views.CustomerEditWindow(custVm) { Owner = System.Windows.Application.Current.MainWindow };
                win.ShowDialog();
            }
        }
    }

    [RelayCommand]
    private void ViewPendingOrders()
    {
        NavigateToRoute("order");
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            var branchId = CurrentSession.CurrentBranchId;
            // 手动刷新：绕过缓存
            var result = await _dashboardService.GetDashboardDataAsync(branchId, forceRefresh: true);
            if (result.Success && result.Data != null)
                PopulateDashboard(result.Data);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "仪表盘手动刷新失败");
        }
        finally
        {
            IsLoading = false;
        }
    }
}
