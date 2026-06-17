using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRO.Application.DTOs;
using PRO.Mobile.Services;

namespace PRO.Mobile.ViewModels;

/// <summary>
/// 客户列表 ViewModel — 分页、搜索、下拉刷新
/// </summary>
public partial class CustomerListViewModel : ObservableObject
{
    private readonly ICustomerMobileService _customerService;
    private readonly IMobileToastService _toast;
    private readonly IConnectivityService _connectivity;

    public CustomerListViewModel(ICustomerMobileService customerService, IMobileToastService toast, IConnectivityService connectivity)
    {
        _customerService = customerService;
        _toast = toast;
        _connectivity = connectivity;
    }

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _keyword;

    [ObservableProperty] private int _pageIndex = 1;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private bool _hasMore;

    public ObservableCollection<CustomerListItem> Customers { get; } = [];

    /// <summary>初始化加载</summary>
    [RelayCommand]
    private async Task LoadDataAsync()
    {
        if (!_connectivity.IsConnected) { ErrorMessage = "网络不可用"; return; }
        IsLoading = true; ErrorMessage = null; PageIndex = 1;

        try
        {
            var result = await _customerService.GetListAsync(1, 20, Keyword);
            if (result.Success && result.Data != null)
            {
                Customers.Clear();
                foreach (var item in result.Data.Items) Customers.Add(item);
                TotalCount = result.Data.TotalCount;
                HasMore = result.Data.HasNext;
                IsEmpty = Customers.Count == 0;
            }
        }
        catch { ErrorMessage = "加载失败"; }
        finally { IsLoading = false; }
    }

    /// <summary>下拉刷新</summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true; PageIndex = 1;
        var result = await _customerService.GetListAsync(1, 20, Keyword);
        if (result.Success && result.Data != null)
        {
            Customers.Clear();
            foreach (var item in result.Data.Items) Customers.Add(item);
            HasMore = result.Data.HasNext;
        }
        IsRefreshing = false;
    }

    /// <summary>加载更多</summary>
    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (!HasMore || IsLoading) return;
        IsLoading = true; PageIndex++;

        var result = await _customerService.GetListAsync(PageIndex, 20, Keyword);
        if (result.Success && result.Data != null)
        {
            foreach (var item in result.Data.Items) Customers.Add(item);
            HasMore = result.Data.HasNext;
        }
        IsLoading = false;
    }

    /// <summary>搜索</summary>
    [RelayCommand]
    private async Task SearchAsync() => await LoadDataAsync();

    /// <summary>查看详情</summary>
    [RelayCommand]
    private async Task SelectCustomerAsync(CustomerListItem? customer)
    {
        if (customer == null) return;
        await Shell.Current.GoToAsync($"customerDetail?id={customer.Id}");
    }
}
