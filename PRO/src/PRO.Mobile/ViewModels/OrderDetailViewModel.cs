using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRO.Application.DTOs;
using PRO.Mobile.Services;

namespace PRO.Mobile.ViewModels;

/// <summary>
/// 订单详情 ViewModel
/// </summary>
public partial class OrderDetailViewModel : ObservableObject, IQueryAttributable
{
    private readonly IOrderMobileService _orderService;
    private readonly IMobileToastService _toast;

    public OrderDetailViewModel(IOrderMobileService orderService, IMobileToastService toast)
    {
        _orderService = orderService;
        _toast = toast;
    }

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private OrderDetailDto? _order;
    private int _orderId;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var id) && int.TryParse(id?.ToString(), out var orderId))
        {
            _orderId = orderId;
            LoadCommand.Execute(null);
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        var result = await _orderService.GetDetailAsync(_orderId);
        if (result.Success)
            Order = result.Data;
        else
            await _toast.ShowErrorAsync(result.Message);
        IsLoading = false;
    }
}
