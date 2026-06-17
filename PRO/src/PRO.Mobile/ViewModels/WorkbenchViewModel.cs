using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRO.Application.DTOs;
using PRO.Mobile.Services;
using PRO.Mobile.Stores;

namespace PRO.Mobile.ViewModels;

/// <summary>
/// 工作台 ViewModel — 聚合今日概览、告警、待办、最近订单
/// </summary>
public partial class WorkbenchViewModel : ObservableObject
{
    private readonly IDashboardMobileService _dashboardService;
    private readonly IHealthCheckService _healthCheck;
    private readonly IConnectivityService _connectivity;
    private readonly IMobileToastService _toast;
    private readonly UserStore _userStore;

    public WorkbenchViewModel(
        IDashboardMobileService dashboardService,
        IHealthCheckService healthCheck,
        IConnectivityService connectivity,
        IMobileToastService toast,
        UserStore userStore)
    {
        _dashboardService = dashboardService;
        _healthCheck = healthCheck;
        _connectivity = connectivity;
        _toast = toast;
        _userStore = userStore;

        WelcomeMessage = $"欢迎，{userStore.UserName ?? "用户"}";
    }

    // --- 欢迎词 ---
    [ObservableProperty] private string _welcomeMessage = "";

    // --- 今日概览 ---
    [ObservableProperty] private int _todayOrderCount;
    [ObservableProperty] private decimal _todayOrderAmount;
    [ObservableProperty] private int _pendingOrderCount;
    [ObservableProperty] private int _completedOrderCount;

    // --- 系统状态 ---
    [ObservableProperty] private bool _isOnline;
    [ObservableProperty] private string _healthStatus = "检测中…";
    [ObservableProperty] private string _healthColor = "#909399";

    // --- 加载状态 ---
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _errorMessage = "";
    [ObservableProperty] private bool _isEmpty;

    // --- 最近订单 ---
    [ObservableProperty]
    private ObservableCollection<OrderListItem> _recentOrders = [];

    // --- 告警列表 ---
    [ObservableProperty]
    private ObservableCollection<DashboardAlert> _alerts = [];

    // --- 待办任务 ---
    [ObservableProperty]
    private ObservableCollection<DashboardTask> _tasks = [];

    // --- 快捷入口 ---
    [ObservableProperty]
    private ObservableCollection<DashboardShortcut> _shortcuts = [];

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        if (IsLoading && !IsRefreshing) return;
        IsLoading = true;
        HasError = false;
        ErrorMessage = "";

        try
        {
            // 并行加载工作台数据和健康检查
            var dashTask = _dashboardService.GetWorkbenchDataAsync();
            var healthTask = _healthCheck.CheckAsync();

            await Task.WhenAll(dashTask, healthTask);

            // 处理工作台数据
            var dashResult = dashTask.Result;
            if (dashResult.Success && dashResult.Data != null)
            {
                var d = dashResult.Data;
                TodayOrderCount = d.TodayOrderCount;
                TodayOrderAmount = d.TodayOrderAmount;
                PendingOrderCount = d.PendingOrderCount;
                CompletedOrderCount = d.CompletedOrderCount;

                RecentOrders = new ObservableCollection<OrderListItem>(
                    d.RecentOrders?.Take(5) ?? []);
                Alerts = new ObservableCollection<DashboardAlert>(d.Alerts ?? []);
                Tasks = new ObservableCollection<DashboardTask>(d.TodayTasks ?? []);
                Shortcuts = new ObservableCollection<DashboardShortcut>(d.Shortcuts ?? []);

                IsEmpty = d.TodayOrderCount == 0 && RecentOrders.Count == 0;
            }
            else
            {
                HasError = true;
                ErrorMessage = dashResult.Message;
            }

            // 处理健康检查
            var healthResult = healthTask.Result;
            if (healthResult.Success && healthResult.Data != null)
            {
                var h = healthResult.Data;
                var isHealthy = h.Status?.ToLower() == "healthy" || h.Database?.ToLower() == "connected";
                HealthStatus = isHealthy ? "服务正常" : $"异常 ({h.Status})";
                HealthColor = isHealthy ? "#67C23A" : "#E6A23C";
            }
            else
            {
                HealthStatus = "离线";
                HealthColor = "#F56C6C";
            }

            IsOnline = _connectivity.IsConnected;
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"加载失败: {ex.Message}";
            await _toast.ShowErrorAsync(ErrorMessage);
        }
        finally
        {
            IsLoading = false;
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task NavigateToAsync(string route)
    {
        if (string.IsNullOrWhiteSpace(route)) return;
        await Shell.Current.GoToAsync(route);
    }
}
