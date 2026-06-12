using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;
using PRO.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using Serilog;

namespace PRO.Desktop.ViewModels;

/// <summary>
/// 订单编辑ViewModel - 负责订单创建和编辑
/// </summary>
public partial class OrderEditViewModel : ViewModelBase
{
    private readonly ProDbContext _dbContext;
    private readonly IOrderService _orderService;
    private readonly IOrderNumberService _orderNumberService;
    private readonly DraftService _draftService;
    private int? _orderId;
    private Action? _onSaveCompleted;
    private bool _hasUnsavedChanges;
    private System.Windows.Threading.DispatcherTimer? _autoSaveTimer;
    private bool _isDraftDirty;
    private const string OrderDraftKey = "order_edit";

    /// <summary>是否有未保存的修改</summary>
    public bool HasUnsavedChanges => _hasUnsavedChanges;

    public Action? OnSaveCompleted
    {
        get => _onSaveCompleted;
        set => _onSaveCompleted = value;
    }

    [ObservableProperty]
    private bool _isReadOnly;

    [ObservableProperty]
    private bool _showAssignmentPanel;

    [ObservableProperty]
    private string _windowTitle = "新建订单";

    [ObservableProperty]
    private string _orderNo = string.Empty;

    [ObservableProperty]
    private int _customerId;

    [ObservableProperty]
    private string? _customerName;

    [ObservableProperty]
    private string? _customerPhone;

    [ObservableProperty]
    private string? _deliveryAddress;

    [ObservableProperty]
    private double? _deliveryLongitude;

    [ObservableProperty]
    private double? _deliveryLatitude;

    [ObservableProperty]
    private DateTime? _deliveryTime;

    [ObservableProperty]
    private decimal _totalAmount;

    [ObservableProperty]
    private decimal _receivedAmount;

    [ObservableProperty]
    private decimal _discountAmount;

    [ObservableProperty]
    private decimal _receivableAmount;

    [ObservableProperty]
    private PaymentStatus _paymentStatus = PaymentStatus.Unpaid;

    [ObservableProperty]
    private string? _remark;

    [ObservableProperty]
    private bool _isDraft;

    [ObservableProperty]
    private string? _cancelReason;

    [ObservableProperty]
    private ObservableCollection<OrderItemDto> _items = [];

    [ObservableProperty]
    private ObservableCollection<CustomerListItem> _customers = [];

    [ObservableProperty]
    private CustomerListItem? _selectedCustomer;

    [ObservableProperty]
    private ObservableCollection<ProductListItem> _products = [];

    [ObservableProperty]
    private ProductListItem? _selectedProduct;

    [ObservableProperty]
    private int _addQuantity = 1;

    [ObservableProperty]
    private ObservableCollection<DeliveryPersonListItem> _deliveryPersons = [];

    [ObservableProperty]
    private DeliveryPersonListItem? _selectedDeliveryPerson;

    [ObservableProperty]
    private ObservableCollection<OrderModificationRecordDto> _modificationRecords = [];

    [ObservableProperty]
    private string _customerSearchText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<CustomerListItem> _customerSearchResults = [];

    [ObservableProperty]
    private bool _hasCustomerSearchResults;

    public OrderEditViewModel()
    {
        _dbContext = App.Services.GetService(typeof(ProDbContext)) as ProDbContext
            ?? throw new InvalidOperationException("无法获取数据库上下文");
        _orderService = App.Services.GetService(typeof(IOrderService)) as IOrderService
            ?? throw new InvalidOperationException("无法获取订单服务");
        _orderNumberService = App.Services.GetService(typeof(IOrderNumberService)) as IOrderNumberService
            ?? throw new InvalidOperationException("无法获取订单号服务");
        _draftService = App.Services.GetService(typeof(DraftService)) as DraftService
            ?? throw new InvalidOperationException("无法获取草稿服务");

        _orderId = null;
        WindowTitle = "新建订单";
        IsReadOnly = false;

        // 跟踪未保存修改
        PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != nameof(IsLoading) && e.PropertyName != nameof(ErrorMessage)
                && e.PropertyName != nameof(SuccessMessage) && e.PropertyName != nameof(HasUnsavedChanges))
                _hasUnsavedChanges = true;
        };

        RunInBackground(InitAsync(), "初始化订单编辑失败");
        InitializeAutoSave();
    }

    partial void OnCustomerSearchTextChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            CustomerSearchResults.Clear();
            HasCustomerSearchResults = false;
            return;
        }

        var results = Customers.Where(c =>
            c.Name.Contains(value, StringComparison.OrdinalIgnoreCase) ||
            (c.CustomerNo != null && c.CustomerNo.Contains(value, StringComparison.OrdinalIgnoreCase)))
            .Take(10)
            .ToList();
        CustomerSearchResults = new ObservableCollection<CustomerListItem>(results);
        HasCustomerSearchResults = results.Count > 0;
    }

    [RelayCommand]
    private void SelectCustomerFromSearch(CustomerListItem? customer)
    {
        if (customer == null) return;
        SelectedCustomer = customer;
        CustomerSearchText = customer.Name;
        CustomerSearchResults.Clear();
        HasCustomerSearchResults = false;
    }

    [RelayCommand]
    private async Task OpenCustomerPickerAsync()
    {
        var selected = await Views.CustomerPickerWindow.ShowAsync(
            owner: System.Windows.Application.Current.MainWindow,
            preselected: SelectedCustomer);

        if (selected != null)
        {
            var matched = Customers.FirstOrDefault(c => c.Id == selected.Id);
            if (matched != null)
            {
                SelectedCustomer = matched;
                CustomerSearchText = matched.Name;
            }
        }
    }

    private void InitializeAutoSave()
    {
        _autoSaveTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        _autoSaveTimer.Tick += async (s, e) => await SaveDraftAsync();
        _autoSaveTimer.Start();

        PropertyChanged += (s, e) =>
        {
            if (!IsReadOnly && e.PropertyName != nameof(IsLoading))
                _isDraftDirty = true;
        };
    }

    private async Task SaveDraftAsync()
    {
        if (!_isDraftDirty) return;
        try
        {
            var draftData = new OrderDraftData
            {
                CustomerId = CustomerId,
                CustomerName = CustomerName,
                DeliveryAddress = DeliveryAddress,
                DeliveryLongitude = DeliveryLongitude,
                DeliveryLatitude = DeliveryLatitude,
                DeliveryTime = DeliveryTime,
                Remark = Remark,
                DiscountAmount = DiscountAmount,
                Items = Items.Select(i => new OrderItemDraftData
                {
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    Amount = i.Amount
                }).ToList()
            };

            await _draftService.SaveDraftAsync(OrderDraftKey, draftData, CurrentSession.CurrentEmployeeId);
            _isDraftDirty = false;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "订单草稿自动保存失败");
        }
    }

    private async Task<OrderDraftData?> LoadDraftAsync()
    {
        return await _draftService.LoadDraftAsync<OrderDraftData>(OrderDraftKey, CurrentSession.CurrentEmployeeId);
    }

    private async Task DeleteDraftAsync()
    {
        await _draftService.DeleteDraftAsync(OrderDraftKey, CurrentSession.CurrentEmployeeId);
    }

    private async Task InitAsync()
    {
        try
        {
            // 检查是否有未保存的草稿
            var draft = await LoadDraftAsync();
            if (draft != null)
            {
                var result = System.Windows.MessageBox.Show(
                    "检测到上次未保存的订单草稿，是否恢复？",
                    "恢复草稿", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    CustomerId = draft.CustomerId;
                    CustomerName = draft.CustomerName;
                    DeliveryAddress = draft.DeliveryAddress;
                    DeliveryLongitude = draft.DeliveryLongitude;
                    DeliveryLatitude = draft.DeliveryLatitude;
                    DeliveryTime = draft.DeliveryTime;
                    Remark = draft.Remark;
                    DiscountAmount = draft.DiscountAmount;
                    if (draft.Items != null)
                    {
                        Items = new ObservableCollection<OrderItemDto>(
                            draft.Items.Select(i => new OrderItemDto
                            {
                                ProductId = i.ProductId,
                                ProductName = i.ProductName ?? "",
                                Quantity = i.Quantity,
                                UnitPrice = i.UnitPrice,
                                Amount = i.Amount
                            }));
                    }
                    RecalculateTotal();
                }
                else
                {
                    await DeleteDraftAsync();
                }
            }

            OrderNo = await GenerateOrderNoAsync();
            await LoadCustomersAsync();
            await LoadProductsAsync();
            await LoadDeliveryPersonsAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "订单编辑初始化失败");
        }
    }

    public void LoadOrder(int orderId)
    {
        _orderId = orderId;
        WindowTitle = "编辑订单";
        IsReadOnly = false;
        RunInBackground(LoadOrderAsync(), "加载订单详情失败");
    }

    private async Task<string> GenerateOrderNoAsync()
    {
        try
        {
            return await _orderNumberService.GenerateAsync(CurrentSession.CurrentBranchId, DateTime.Now);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "预生成订单号失败");
            return "保存后自动生成";
        }
    }

    private async Task LoadCustomersAsync()
    {
        var branchId = CurrentSession.CurrentBranchId;
        var customers = await _dbContext.Customers
            .AsNoTracking()
            .Where(c => c.BranchId == branchId && c.Status == CustomerStatus.Active)
            .ToListAsync();

        Customers = new ObservableCollection<CustomerListItem>(
            customers.Select(c => new CustomerListItem
            {
                Id = c.Id,
                Name = c.Name,
                Phone = c.Phone,
                Address = c.Address
            }));
    }

    private async Task LoadProductsAsync()
    {
        var products = await _dbContext.Products
            .AsNoTracking()
            .Where(p => p.Status == ProductStatus.Active)
            .ToListAsync();

        Products = new ObservableCollection<ProductListItem>(
            products.Select(p => new ProductListItem
            {
                Id = p.Id,
                SKU = p.SKU,
                Name = p.Name,
                Specification = p.Specification,
                ReferencePrice = p.ReferencePrice,
                Stock = p.Stock,
                Unit = p.Unit
            }));
    }

    private async Task LoadDeliveryPersonsAsync()
    {
        var branchId = CurrentSession.CurrentBranchId;
        var persons = await _dbContext.DeliveryPersons
            .AsNoTracking()
            .Where(d => d.BranchId == branchId && d.Status == DeliveryPersonStatus.Available)
            .ToListAsync();

        DeliveryPersons = new ObservableCollection<DeliveryPersonListItem>(
            persons.Select(d => new DeliveryPersonListItem
            {
                Id = d.Id,
                Name = d.Name,
                Phone = d.Phone,
                CurrentLoad = d.CurrentLoad,
                MaxLoad = d.MaxLoad
            }));
    }

    partial void OnSelectedCustomerChanged(CustomerListItem? value)
    {
        if (value != null)
        {
            CustomerId = value.Id;
            CustomerName = value.Name;
            CustomerPhone = value.Phone;
            DeliveryAddress = value.Address;
        }
    }

    private async Task LoadOrderAsync()
    {
        if (!_orderId.HasValue) return;

        var order = await _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Include(o => o.DeliveryPerson)
            .Include(o => o.ModificationRecords).ThenInclude(r => r.ModifiedBy)
            .FirstOrDefaultAsync(o => o.Id == _orderId);

        if (order != null)
        {
            OrderNo = order.OrderNo;
            CustomerId = order.CustomerId;
            CustomerName = order.Customer?.Name;
            CustomerPhone = order.Customer?.Phone;
            DeliveryAddress = order.DeliveryAddress;
            DeliveryLongitude = order.DeliveryLongitude;
            DeliveryLatitude = order.DeliveryLatitude;
            DeliveryTime = order.DeliveryTime;
            TotalAmount = order.TotalAmount;
            ReceivedAmount = order.ReceivedAmount;
            DiscountAmount = order.DiscountAmount;
            ReceivableAmount = order.TotalAmount - order.DiscountAmount;
            PaymentStatus = order.PaymentStatus;
            Remark = order.Remark;
            IsDraft = order.Status == OrderStatus.Draft;
            CancelReason = order.CancelReason;

            if (order.DeliveryPersonId.HasValue && DeliveryPersons.Any())
            {
                SelectedDeliveryPerson = DeliveryPersons.FirstOrDefault(d => d.Id == order.DeliveryPersonId);
            }

            Items = new ObservableCollection<OrderItemDto>(
                order.Items.Select(i => new OrderItemDto
                {
                    Id = i.Id,
                    ProductId = i.ProductId,
                    ProductName = i.Product?.Name ?? "",
                    ProductSku = i.Product?.SKU,
                    ProductSpec = i.Product?.Specification,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    Amount = i.Amount,
                    DiscountType = i.DiscountType,
                    DiscountValue = i.DiscountValue
                }));

            ModificationRecords = new ObservableCollection<OrderModificationRecordDto>(
                order.ModificationRecords.OrderByDescending(r => r.ModifiedAt).Select(r => new OrderModificationRecordDto
                {
                    Id = r.Id,
                    OrderId = r.OrderId,
                    ModifiedById = r.ModifiedById,
                    ModifiedByName = r.ModifiedBy?.Name ?? "",
                    ModifiedByNo = r.ModifiedBy?.EmployeeNo ?? "",
                    ModifiedAt = r.ModifiedAt,
                    Content = r.Content,
                    ModificationType = r.ModificationType
                }));
        }
    }

    [RelayCommand]
    private void AddItem()
    {
        if (SelectedProduct == null || AddQuantity <= 0) return;

        var unitPrice = SelectedProduct.ReferencePrice ?? 0m;

        if (SelectedProduct.Stock < AddQuantity)
        {
            ShowError($"库存不足，当前库存：{SelectedProduct.Stock}");
            return;
        }

        var amount = unitPrice * AddQuantity;
        Items.Add(new OrderItemDto
        {
            ProductId = SelectedProduct.Id,
            ProductName = SelectedProduct.Name,
            ProductSku = SelectedProduct.SKU,
            ProductSpec = SelectedProduct.Specification,
            Quantity = AddQuantity,
            UnitPrice = unitPrice,
            Amount = amount
        });

        RecalculateTotal();
        SelectedProduct = null;
        AddQuantity = 1;
    }

    [RelayCommand]
    private void RecalcItems()
    {
        foreach (var item in Items)
        {
            item.Amount = item.Quantity * item.UnitPrice;
        }
        RecalculateTotal();
    }

    [RelayCommand]
    private void RemoveItem(OrderItemDto? item)
    {
        if (item != null)
        {
            Items.Remove(item);
            RecalculateTotal();
        }
    }

    partial void OnDiscountAmountChanged(decimal value)
    {
        ReceivableAmount = TotalAmount - value;
    }

    private void RecalculateTotal()
    {
        TotalAmount = Items.Sum(i => i.Amount);
        ReceivableAmount = TotalAmount - DiscountAmount;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (CustomerId == 0)
        {
            ShowError("请选择客户");
            return;
        }

        if (!Items.Any())
        {
            ShowError("请添加产品");
            return;
        }

        if (TotalAmount <= 0)
        {
            ShowError("订单总金额必须大于0");
            return;
        }

        if (string.IsNullOrWhiteSpace(DeliveryAddress) && SelectedCustomer != null)
        {
            DeliveryAddress = SelectedCustomer.Address;
        }

        try
        {
            var requestItems = Items.Select(i => new CreateOrderItemRequest
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                Remark = i.Remark
            }).ToList();

            ApiResponse<bool>? updateResult = null;
            ApiResponse<int>? createResult = null;
            if (_orderId.HasValue)
            {
                updateResult = await _orderService.UpdateAsync(new UpdateOrderRequest
                {
                    Id = _orderId.Value,
                    CustomerId = CustomerId,
                    DeliveryPersonId = SelectedDeliveryPerson?.Id,
                    DeliveryAddress = DeliveryAddress,
                    DeliveryLongitude = DeliveryLongitude,
                    DeliveryLatitude = DeliveryLatitude,
                    DeliveryTime = DeliveryTime,
                    ReceivedAmount = ReceivedAmount,
                    PaymentStatus = PaymentStatus,
                    Status = IsDraft ? OrderStatus.Draft : OrderStatus.Pending,
                    CancelReason = CancelReason,
                    Remark = Remark,
                    Items = requestItems
                }, CurrentSession.CurrentEmployeeId);
            }
            else
            {
                createResult = await _orderService.CreateAsync(new CreateOrderRequest
                {
                    CustomerId = CustomerId,
                    DeliveryPersonId = SelectedDeliveryPerson?.Id,
                    DeliveryAddress = DeliveryAddress,
                    DeliveryLongitude = DeliveryLongitude,
                    DeliveryLatitude = DeliveryLatitude,
                    DeliveryTime = DeliveryTime,
                    ReceivedAmount = ReceivedAmount,
                    PaymentStatus = PaymentStatus,
                    Remark = Remark,
                    IsDraft = IsDraft,
                    Items = requestItems
                }, CurrentSession.CurrentEmployeeId);
            }

            var success = updateResult?.Success ?? createResult?.Success ?? false;
            var message = updateResult?.Message ?? createResult?.Message ?? "保存失败";

            if (!success)
            {
                ShowError(message);
                return;
            }

            if (createResult?.Data > 0)
                _orderId = createResult.Data;

            await DeleteDraftAsync();
            _hasUnsavedChanges = false;
            ShowSuccess(message);
            _onSaveCompleted?.Invoke();
        }
        catch (Exception ex)
        {
            ShowError($"保存失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SaveAsDraftAsync()
    {
        IsDraft = true;
        await SaveAsync();
    }

    [RelayCommand]
    private void Cancel()
    {
        // 窗口关闭由View处理
    }

    /// <summary>
    /// 停止自动保存定时器
    /// </summary>
    public void StopAutoSave()
    {
        _autoSaveTimer?.Stop();
    }
}
