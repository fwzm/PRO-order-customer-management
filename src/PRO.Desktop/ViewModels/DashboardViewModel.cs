using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRO.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using PRO.Domain.Enums;

namespace PRO.Desktop.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly ProDbContext _dbContext;

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

    [ObservableProperty]
    private int _customerCount;

    [ObservableProperty]
    private int _monthlyOrderCount;

    [ObservableProperty]
    private decimal _monthlyReceivedAmount;

    [ObservableProperty]
    private int _pendingDeliveryCount;

    [ObservableProperty]
    private string _syncStatusText = "已同步";

    [ObservableProperty]
    private DateTime _lastSyncTime = DateTime.Now;

    [ObservableProperty]
    private List<RecentOrderItem> _recentOrders = new();

    public DashboardViewModel()
    {
        _dbContext = App.Services.GetService(typeof(ProDbContext)) as ProDbContext 
            ?? throw new InvalidOperationException("无法获取数据库上下文");
        
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
            var startOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

            // 客户总数
            CustomerCount = await _dbContext.Customers
                .Where(c => c.BranchId == branchId && c.Status == CustomerStatus.Active)
                .CountAsync();

            // 本月订单数
            MonthlyOrderCount = await _dbContext.Orders
                .Where(o => o.BranchId == branchId && o.CreatedAt >= startOfMonth && o.Status != OrderStatus.Draft)
                .CountAsync();

            // 本月收款
            MonthlyReceivedAmount = await _dbContext.Orders
                .Where(o => o.BranchId == branchId && o.CreatedAt >= startOfMonth && o.Status != OrderStatus.Draft)
                .SumAsync(o => o.ReceivedAmount);

            // 待配送
            PendingDeliveryCount = await _dbContext.Orders
                .Where(o => o.BranchId == branchId && o.Status == OrderStatus.Pending)
                .CountAsync();

            // 最近订单
            var recentOrdersQuery = await _dbContext.Orders
                .Include(o => o.Customer)
                .Where(o => o.BranchId == branchId && o.Status != OrderStatus.Draft)
                .OrderByDescending(o => o.CreatedAt)
                .Take(10)
                .ToListAsync();

            RecentOrders = recentOrdersQuery.Select(o => new RecentOrderItem
            {
                Id = o.Id,
                OrderNo = o.OrderNo,
                CustomerName = o.Customer?.Name ?? "未知",
                TotalAmount = o.TotalAmount,
                Status = o.Status,
                StatusName = GetStatusName(o.Status),
                StatusColor = GetStatusColor(o.Status),
                CreatedAt = o.CreatedAt
            }).ToList();
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

    private string GetStatusName(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "待分配",
        OrderStatus.Assigned => "已分配",
        OrderStatus.Delivering => "配送中",
        OrderStatus.Completed => "已完成",
        OrderStatus.Failed => "配送失败",
        OrderStatus.Cancelled => "已取消",
        _ => "未知"
    };

    private string GetStatusColor(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "#F39C12",
        OrderStatus.Assigned => "#3498DB",
        OrderStatus.Delivering => "#9B59B6",
        OrderStatus.Completed => "#27AE60",
        OrderStatus.Failed => "#E74C3C",
        OrderStatus.Cancelled => "#95A5A6",
        _ => "#95A5A6"
    };

    [RelayCommand]
    private void QuickCreateOrder()
    {
        var mainVm = System.Windows.Application.Current.MainWindow?.DataContext as PRO.Desktop.ViewModels.MainViewModel;
        if (mainVm == null) return;
        var navItem = mainVm.NavigationItems.FirstOrDefault(n => n.Id == "order");
        if (navItem != null)
        {
            mainVm.NavigateToTabCommand.Execute(navItem);
            // 打开新建订单对话框
            var orderVm = App.Services.GetService(typeof(PRO.Desktop.ViewModels.OrderEditViewModel)) as PRO.Desktop.ViewModels.OrderEditViewModel;
            if (orderVm != null)
            {
                var win = new PRO.Desktop.Views.OrderEditWindow(orderVm)
                {
                    Owner = System.Windows.Application.Current.MainWindow
                };
                win.ShowDialog();
            }
        }
    }

    [RelayCommand]
    private void QuickCreateCustomer()
    {
        var mainVm = System.Windows.Application.Current.MainWindow?.DataContext as PRO.Desktop.ViewModels.MainViewModel;
        if (mainVm == null) return;
        var navItem = mainVm.NavigationItems.FirstOrDefault(n => n.Id == "customer");
        if (navItem != null)
        {
            mainVm.NavigateToTabCommand.Execute(navItem);
            // 打开新建客户对话框
            var custVm = App.Services.GetService(typeof(PRO.Desktop.ViewModels.CustomerEditViewModel)) as PRO.Desktop.ViewModels.CustomerEditViewModel;
            if (custVm != null)
            {
                var win = new PRO.Desktop.Views.CustomerEditWindow(custVm)
                {
                    Owner = System.Windows.Application.Current.MainWindow
                };
                win.ShowDialog();
            }
        }
    }

    [RelayCommand]
    private void ViewPendingOrders()
    {
        var mainVm = System.Windows.Application.Current.MainWindow?.DataContext as PRO.Desktop.ViewModels.MainViewModel;
        if (mainVm == null) return;
        var navItem = mainVm.NavigationItems.FirstOrDefault(n => n.Id == "order");
        if (navItem != null)
        {
            mainVm.NavigateToTabCommand.Execute(navItem);
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadDataAsync();
    }
}

public class RecentOrderItem
{
    public int Id { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public string StatusColor { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
