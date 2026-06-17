using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRO.Application.DTOs;
using PRO.Mobile.Services;

namespace PRO.Mobile.ViewModels;

/// <summary>
/// 客户详情 ViewModel
/// </summary>
public partial class CustomerDetailViewModel : ObservableObject, IQueryAttributable
{
    private readonly ICustomerMobileService _customerService;
    private readonly IMobileToastService _toast;

    public CustomerDetailViewModel(ICustomerMobileService customerService, IMobileToastService toast)
    {
        _customerService = customerService;
        _toast = toast;
    }

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private CustomerDetailDto? _customer;
    private int _customerId;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var id) && int.TryParse(id?.ToString(), out var customerId))
        {
            _customerId = customerId;
            LoadCommand.Execute(null);
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        var result = await _customerService.GetDetailAsync(_customerId);
        if (result.Success)
            Customer = result.Data;
        else
            await _toast.ShowErrorAsync(result.Message);
        IsLoading = false;
    }
}
