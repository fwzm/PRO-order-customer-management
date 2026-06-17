using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRO.Application.DTOs;
using PRO.Mobile.Services;
using PRO.Mobile.Stores;

namespace PRO.Mobile.ViewModels;

/// <summary>
/// 首页工作台 ViewModel
/// </summary>
public partial class WorkbenchViewModel : ObservableObject
{
    private readonly IApiClient _apiClient;
    private readonly IMobileToastService _toast;
    private readonly IConnectivityService _connectivity;
    private readonly UserStore _userStore;

    public WorkbenchViewModel(IApiClient apiClient, IMobileToastService toast,
        IConnectivityService connectivity, UserStore userStore)
    {
        _apiClient = apiClient;
        _toast = toast;
        _connectivity = connectivity;
        _userStore = userStore;
    }

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string _userName = string.Empty;
    [ObservableProperty] private string _branchName = string.Empty;

    // 今日概览
    [ObservableProperty] private int _todayOrderCount;
    [ObservableProperty] private decimal _todayOrderAmount;
    [ObservableProperty] private int _pendingOrderCount;
    [ObservableProperty] private int _deliveringOrderCount;

    public ObservableCollection<OrderListItem> RecentOrders { get; } = [];

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        if (!_connectivity.IsConnected)
        {
            ErrorMessage = "网络不可用";
            IsEmpty = true;
            return;
        }

        IsLoading = true;
        ErrorMessage = null;
        IsEmpty = false;

        try
        {
            UserName = _userStore.UserName ?? "用户";
            BranchName = _userStore.BranchName ?? "";

            // 健康检查
            var healthResult = await _apiClient.GetAsync<object>("health");
            if (!healthResult.Success)
            {
                ErrorMessage = "服务器连接失败，请稍后重试";
                return;
            }

            // 工作台数据
            var result = await _apiClient.GetAsync<BusinessDashboardDto>("api/dashboard/workbench");
            if (result.Success && result.Data != null)
            {
                TodayOrderCount = result.Data.TodayOrderCount;
                TodayOrderAmount = result.Data.TodayOrderAmount;
                PendingOrderCount = result.Data.PendingOrderCount;
                DeliveringOrderCount = result.Data.DeliveringOrderCount;

                RecentOrders.Clear();
                foreach (var order in result.Data.RecentOrders.Take(5))
                    RecentOrders.Add(order);

                IsEmpty = false;
            }
            else
            {
                IsEmpty = true;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            await _toast.ShowErrorAsync("加载失败");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task NavigateToOrders() => await Shell.Current.GoToAsync("//main/orders");

    [RelayCommand]
    private async Task NavigateToCustomers() => await Shell.Current.GoToAsync("//main/customers");

    [RelayCommand]
    private async Task NavigateToNewOrder() => await Shell.Current.GoToAsync("orderNew");
}
