using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRO.Application.DTOs;
using PRO.Mobile.Services;

namespace PRO.Mobile.ViewModels;

/// <summary>
/// 订单列表 ViewModel
/// </summary>
public partial class OrderListViewModel : ObservableObject
{
    private readonly IOrderMobileService _orderService;
    private readonly IMobileToastService _toast;
    private readonly IConnectivityService _connectivity;

    public OrderListViewModel(IOrderMobileService orderService, IMobileToastService toast, IConnectivityService connectivity)
    {
        _orderService = orderService;
        _toast = toast;
        _connectivity = connectivity;
    }

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _keyword;
    [ObservableProperty] private int? _selectedStatus;

    [ObservableProperty] private int _pageIndex = 1;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private bool _hasMore;

    public ObservableCollection<OrderListItem> Orders { get; } = [];

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        if (!_connectivity.IsConnected) { ErrorMessage = "网络不可用"; return; }
        IsLoading = true; ErrorMessage = null; PageIndex = 1;

        try
        {
            var result = await _orderService.GetListAsync(1, 20, Keyword, SelectedStatus);
            if (result.Success && result.Data != null)
            {
                Orders.Clear();
                foreach (var item in result.Data.Items) Orders.Add(item);
                TotalCount = result.Data.TotalCount;
                HasMore = result.Data.HasNext;
                IsEmpty = Orders.Count == 0;
            }
        }
        catch { ErrorMessage = "加载失败"; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true; PageIndex = 1;
        var result = await _orderService.GetListAsync(1, 20, Keyword, SelectedStatus);
        if (result.Success && result.Data != null)
        {
            Orders.Clear();
            foreach (var item in result.Data.Items) Orders.Add(item);
            HasMore = result.Data.HasNext;
        }
        IsRefreshing = false;
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (!HasMore || IsLoading) return;
        IsLoading = true; PageIndex++;
        var result = await _orderService.GetListAsync(PageIndex, 20, Keyword, SelectedStatus);
        if (result.Success && result.Data != null)
        {
            foreach (var item in result.Data.Items) Orders.Add(item);
            HasMore = result.Data.HasNext;
        }
        IsLoading = false;
    }

    [RelayCommand]
    private async Task SearchAsync() => await LoadDataAsync();

    [RelayCommand]
    private async Task SelectOrderAsync(OrderListItem? order)
    {
        if (order == null) return;
        await Shell.Current.GoToAsync($"orderDetail?id={order.Id}");
    }

    [RelayCommand]
    private async Task NewOrderAsync() => await Shell.Current.GoToAsync("orderNew");
}
