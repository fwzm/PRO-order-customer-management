using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRO.Application.DTOs;
using PRO.Mobile.Services;

namespace PRO.Mobile.ViewModels;

/// <summary>
/// 新建订单（轻量）ViewModel
/// </summary>
public partial class OrderNewViewModel : ObservableObject
{
    private readonly IOrderMobileService _orderService;
    private readonly ICustomerMobileService _customerService;
    private readonly IApiClient _apiClient;
    private readonly IMobileToastService _toast;

    public OrderNewViewModel(IOrderMobileService orderService, ICustomerMobileService customerService,
        IApiClient apiClient, IMobileToastService toast)
    {
        _orderService = orderService;
        _customerService = customerService;
        _apiClient = apiClient;
        _toast = toast;
    }

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private CustomerListItem? _selectedCustomer;
    [ObservableProperty] private string? _deliveryAddress;
    [ObservableProperty] private string? _remark;

    public ObservableCollection<CustomerListItem> Customers { get; } = [];
    public ObservableCollection<OrderItemEntry> Items { get; } = [new()];
    public ObservableCollection<ProductListItem> Products { get; } = [];

    [RelayCommand]
    private async Task LoadCustomersAsync(string? search = null)
    {
        var result = await _customerService.GetListAsync(1, 200, search);
        if (result.Success && result.Data != null)
        {
            Customers.Clear();
            foreach (var c in result.Data.Items) Customers.Add(c);
        }
    }

    [RelayCommand]
    private async Task LoadProductsAsync()
    {
        var result = await _apiClient.GetAsync<PagedResult<ProductListItem>>("api/products");
        if (result.Success && result.Data != null)
        {
            Products.Clear();
            foreach (var p in result.Data.Items) Products.Add(p);
        }
    }

    [RelayCommand]
    private void AddItem()
    {
        Items.Add(new OrderItemEntry());
    }

    [RelayCommand]
    private void RemoveItem(OrderItemEntry? item)
    {
        if (item != null && Items.Count > 1) Items.Remove(item);
    }

    /// <summary>保存草稿</summary>
    [RelayCommand]
    private async Task SaveDraftAsync()
    {
        await CreateOrderAsync(isDraft: true);
    }

    /// <summary>提交订单</summary>
    [RelayCommand]
    private async Task SubmitAsync()
    {
        await CreateOrderAsync(isDraft: false);
    }

    private async Task CreateOrderAsync(bool isDraft)
    {
        if (SelectedCustomer == null)
        {
            await _toast.ShowErrorAsync("请选择客户");
            return;
        }

        if (Items.Count == 0 || Items.All(i => i.ProductId <= 0))
        {
            await _toast.ShowErrorAsync("请添加至少一个产品");
            return;
        }

        IsSaving = true;
        try
        {
            var request = new CreateOrderRequest
            {
                CustomerId = SelectedCustomer.Id,
                DeliveryAddress = DeliveryAddress,
                Remark = Remark,
                IsDraft = isDraft,
                Items = Items.Where(i => i.ProductId > 0).Select(i => new CreateOrderItemRequest
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    Remark = i.Remark
                }).ToList()
            };

            var result = await _orderService.CreateAsync(request);
            if (result.Success)
            {
                await _toast.ShowSuccessAsync(isDraft ? "草稿已保存" : "订单创建成功");
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                await _toast.ShowErrorAsync(result.Message);
            }
        }
        catch (Exception ex)
        {
            await _toast.ShowErrorAsync($"操作失败: {ex.Message}");
        }
        finally { IsSaving = false; }
    }

    public void Initialize()
    {
        LoadCustomersCommand.Execute(null);
        LoadProductsCommand.Execute(null);
    }
}

/// <summary>
/// 移动端订单条目模型（绑定时使用）
/// </summary>
public partial class OrderItemEntry : ObservableObject
{
    [ObservableProperty] private int _productId;
    [ObservableProperty] private string _productName = string.Empty;
    [ObservableProperty] private int _quantity = 1;
    [ObservableProperty] private decimal _unitPrice;
    [ObservableProperty] private string? _remark;
    public decimal Amount => Quantity * UnitPrice;
}
