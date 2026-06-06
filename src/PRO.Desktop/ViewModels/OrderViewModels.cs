using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRO.Application.DTOs;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Windows;
using System.IO;
using Microsoft.Win32;
using ClosedXML.Excel;
using System.Text.Json;
using PRO.Desktop.Views;

namespace PRO.Desktop.ViewModels;

public partial class OrderListViewModel : PagedViewModelBase
{
    private readonly ProDbContext _dbContext;

    [ObservableProperty]
    private ObservableCollection<OrderListItem> _orders = new();

    [ObservableProperty]
    private OrderListItem? _selectedOrder;

    [ObservableProperty]
    private OrderStatus? _filterStatus;

    [ObservableProperty]
    private PaymentStatus? _filterPaymentStatus;

    [ObservableProperty]
    private DateTime? _filterStartDate;

    [ObservableProperty]
    private DateTime? _filterEndDate;

    [ObservableProperty]
    private bool _isEditMode;

    [ObservableProperty]
    private OrderDetailDto? _editingOrder;

    [ObservableProperty]
    private bool _showOnlyDrafts;

    public OrderListViewModel()
    {
        _dbContext = App.Services.GetService(typeof(ProDbContext)) as ProDbContext 
            ?? throw new InvalidOperationException("无法获取数据库上下文");
        
        RunInBackground(LoadDataAsync(), "订单列表加载失败");
    }

    protected override async Task LoadDataAsync()
    {
        IsLoading = true;
        try
        {
            var branchId = CurrentSession.CurrentBranchId;
            var query = _dbContext.Orders
                .Include(o => o.Customer)
                .Include(o => o.Branch)
                .Include(o => o.DeliveryPerson)
                .Include(o => o.Creator)
                .AsQueryable();

            // 数据隔离
            if (!CurrentSession.Current.IsHeadquartersAdmin)
            {
                query = query.Where(o => o.BranchId == branchId);
            }

            // 关键词搜索
            if (!string.IsNullOrWhiteSpace(SearchKeyword))
            {
                query = query.Where(o => o.OrderNo.Contains(SearchKeyword) || 
                    (o.Customer != null && o.Customer.Name.Contains(SearchKeyword)));
            }

            // 状态筛选
            if (FilterStatus.HasValue)
            {
                query = query.Where(o => o.Status == FilterStatus.Value);
            }

            // 收款状态筛选
            if (FilterPaymentStatus.HasValue)
            {
                query = query.Where(o => o.PaymentStatus == FilterPaymentStatus.Value);
            }

            if (FilterStartDate.HasValue)
            {
                query = query.Where(o => o.CreatedAt >= FilterStartDate.Value);
            }

            if (FilterEndDate.HasValue)
            {
                query = query.Where(o => o.CreatedAt < FilterEndDate.Value);
            }

            // 排除草稿（草稿单独显示）
            if (ShowOnlyDrafts)
            {
                query = query.Where(o => o.Status == OrderStatus.Draft);
            }
            else
            {
                query = query.Where(o => o.Status != OrderStatus.Draft);
            }

            TotalCount = await query.CountAsync();

            var items = await query.OrderByDescending(o => o.CreatedAt)
                .Skip((PageIndex - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            Orders = new ObservableCollection<OrderListItem>(items.Select(o => new OrderListItem
            {
                Id = o.Id,
                OrderNo = o.OrderNo,
                CustomerId = o.CustomerId,
                CustomerName = o.Customer?.Name ?? "未知",
                BranchId = o.BranchId,
                BranchName = o.Branch?.Name ?? "",
                TotalAmount = o.TotalAmount,
                ReceivedAmount = o.ReceivedAmount,
                PaymentStatus = o.PaymentStatus,
                PaymentStatusName = GetPaymentStatusName(o.PaymentStatus),
                Status = o.Status,
                StatusName = GetStatusName(o.Status),
                DeliveryPersonName = o.DeliveryPerson?.Name,
                DeliveryAddress = o.DeliveryAddress,
                DeliveryLongitude = o.DeliveryLongitude,
                DeliveryLatitude = o.DeliveryLatitude,
                DeliveryTime = o.DeliveryTime,
                CreatedAt = o.CreatedAt,
                CreatedByName = o.Creator?.Name ?? ""
            }));
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

    private string GetPaymentStatusName(PaymentStatus status) => status switch
    {
        PaymentStatus.Unpaid => "未收款",
        PaymentStatus.PartialPaid => "部分收款",
        PaymentStatus.Paid => "已收款",
        PaymentStatus.Legal => "移交法务",
        _ => "未知"
    };

    [RelayCommand]
    private void NewOrder()
    {
        var editVm = App.Services.GetService(typeof(OrderEditViewModel)) as OrderEditViewModel
            ?? throw new InvalidOperationException("无法创建订单编辑视图模型");
        
        editVm.OnSaveCompleted = async () => { await LoadDataAsync(); };
        
        var dialog = new Views.OrderEditWindow(editVm) { Owner = System.Windows.Application.Current.MainWindow };
        dialog.ShowDialog();
    }

    private async Task ApplyDateFilterAsync(DateTime? startDate, DateTime? endDate)
    {
        FilterStartDate = startDate;
        FilterEndDate = endDate;
        await ResetToFirstPageAndLoadAsync();
    }

    [RelayCommand]
    private async Task FilterTodayAsync()
    {
        var start = DateTime.Today;
        await ApplyDateFilterAsync(start, start.AddDays(1));
    }

    [RelayCommand]
    private async Task FilterThisWeekAsync()
    {
        var today = DateTime.Today;
        var offset = ((int)today.DayOfWeek + 6) % 7;
        var start = today.AddDays(-offset);
        await ApplyDateFilterAsync(start, start.AddDays(7));
    }

    [RelayCommand]
    private async Task FilterThisMonthAsync()
    {
        var today = DateTime.Today;
        var start = new DateTime(today.Year, today.Month, 1);
        await ApplyDateFilterAsync(start, start.AddMonths(1));
    }

    [RelayCommand]
    private async Task FilterCustomAsync()
    {
        await ApplyDateFilterAsync(null, null);
        ShowSuccess("已清除日期筛选");
    }

    [RelayCommand]
    private Task ViewOrderAsync(OrderListItem? order)
    {
        if (order == null) return Task.CompletedTask;
        
        var editVm = App.Services.GetService(typeof(OrderEditViewModel)) as OrderEditViewModel
            ?? throw new InvalidOperationException("无法创建订单编辑视图模型");
        
        editVm.LoadOrder(order.Id);
        editVm.IsReadOnly = true;
        editVm.OnSaveCompleted = async () => { await LoadDataAsync(); };
        
        var dialog = new Views.OrderEditWindow(editVm) { Owner = System.Windows.Application.Current.MainWindow };
        dialog.ShowDialog();
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task EditOrderAsync(OrderListItem? order)
    {
        if (order == null) return Task.CompletedTask;
        
        var editVm = App.Services.GetService(typeof(OrderEditViewModel)) as OrderEditViewModel
            ?? throw new InvalidOperationException("无法创建订单编辑视图模型");
        
        editVm.LoadOrder(order.Id);
        editVm.IsReadOnly = false;
        editVm.OnSaveCompleted = async () => { await LoadDataAsync(); };
        
        var dialog = new Views.OrderEditWindow(editVm) { Owner = System.Windows.Application.Current.MainWindow };
        dialog.ShowDialog();
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task AssignOrderAsync(OrderListItem? order)
    {
        if (order == null) return Task.CompletedTask;
        
        var editVm = App.Services.GetService(typeof(OrderEditViewModel)) as OrderEditViewModel
            ?? throw new InvalidOperationException("无法创建订单编辑视图模型");
        
        editVm.LoadOrder(order.Id);
        editVm.ShowAssignmentPanel = true;
        
        var dialog = new Views.OrderEditWindow(editVm) { Owner = System.Windows.Application.Current.MainWindow };
        dialog.ShowDialog();
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task UpdateStatusAsync(OrderListItem? order)
    {
        if (order == null) return Task.CompletedTask;
        
        var statuses = new[] { OrderStatus.Pending, OrderStatus.Assigned, OrderStatus.Delivering, 
            OrderStatus.Completed, OrderStatus.Failed, OrderStatus.Cancelled };
        
        var statusNames = new[] { "待分配", "已分配", "配送中", "已完成", "配送失败", "已取消" };
        
        var menu = new System.Windows.Controls.ContextMenu();
        for (int i = 0; i < statuses.Length; i++)
        {
            var status = statuses[i];
            var item = new System.Windows.Controls.MenuItem { Header = statusNames[i], Tag = status };
            var orderId = order.Id;
            item.Click += async (s, e) => 
            {
                await ChangeOrderStatusAsync(orderId, (OrderStatus)((System.Windows.Controls.MenuItem)s!).Tag!);
            };
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
        return Task.CompletedTask;
    }

    private async Task ChangeOrderStatusAsync(int orderId, OrderStatus newStatus)
    {
        try
        {
            var order = await _dbContext.Orders.FindAsync(orderId);
            if (order != null)
            {
                // 先检查取消原因 - 从UI输入获取而非数据库旧值
                if (newStatus == OrderStatus.Cancelled)
                {
                    // 弹出输入框让用户输入取消原因
                    var inputDialog = new System.Windows.Controls.TextBox
                    {
                        TextWrapping = System.Windows.TextWrapping.Wrap,
                        AcceptsReturn = true,
                        Height = 80,
                        Margin = new System.Windows.Thickness(5)
                    };

                    var dialogContent = new System.Windows.Controls.StackPanel();
                    dialogContent.Children.Add(new System.Windows.Controls.Label { Content = "请输入取消原因：" });
                    dialogContent.Children.Add(inputDialog);

                    var dialogWindow = new System.Windows.Window
                    {
                        Title = "取消订单",
                        Content = dialogContent,
                        Width = 400,
                        Height = 200,
                        WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
                        Owner = System.Windows.Application.Current.MainWindow
                    };

                    var okButton = new System.Windows.Controls.Button
                    {
                        Content = "确定",
                        Width = 80,
                        Margin = new System.Windows.Thickness(5),
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Right
                    };
                    okButton.Click += (s, e) => dialogWindow.DialogResult = true;
                    dialogContent.Children.Add(okButton);

                    if (dialogWindow.ShowDialog() == true && string.IsNullOrWhiteSpace(inputDialog.Text))
                    {
                        ShowError("取消原因不能为空");
                        return;
                    }

                    order.CancelReason = inputDialog.Text;
                }

                var now = DateTime.Now;
                order.Status = newStatus;
                order.UpdatedAt = now;
                order.LocalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                order.SyncStatus = SyncStatus.Pending;

                // 记录修改
                _dbContext.OrderModificationRecords.Add(new OrderModificationRecord
                {
                    OrderId = orderId,
                    ModifiedById = CurrentSession.CurrentEmployeeId,
                    ModifiedAt = now,
                    Content = $"状态变更为：{GetStatusName(newStatus)}",
                    ModificationType = "StatusChange"
                });

                await _dbContext.SaveChangesAsync();
                ShowSuccess($"订单状态已更新为：{GetStatusName(newStatus)}");
                await LoadDataAsync();
            }
        }
        catch (Exception ex)
        {
            ShowError($"更新状态失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ExportToExcelAsync()
    {
        try
        {
            var branchId = CurrentSession.CurrentBranchId;
            var orders = await _dbContext.Orders
                .Include(o => o.Customer)
                .Include(o => o.DeliveryPerson)
                .Include(o => o.Items).ThenInclude(i => i.Product)
                .Where(o => o.BranchId == branchId && o.Status != OrderStatus.Draft)
                .OrderByDescending(o => o.CreatedAt)
                .Take(5000)  // 最多导出5000条
                .ToListAsync();

            var dialog = new SaveFileDialog
            {
                Filter = "Excel文件|*.xlsx",
                FileName = $"订单数据_{DateTime.Now:yyyyMMdd}"
            };

            if (dialog.ShowDialog() == true)
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("订单列表");

                // 表头
                var headers = new[] { "订单号", "客户名称", "手机号", "配送地址", "经度", "纬度", 
                    "总金额", "收款金额", "收款状态", "订单状态", "配送员", "配送时间", "创建时间", "产品明细" };
                
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(1, i + 1).Value = headers[i];
                }

                var headerRange = worksheet.Range(1, 1, 1, headers.Length);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                // 数据
                var row = 2;
                foreach (var o in orders)
                {
                    worksheet.Cell(row, 1).Value = o.OrderNo;
                    worksheet.Cell(row, 2).Value = o.Customer?.Name;
                    worksheet.Cell(row, 3).Value = o.Customer?.Phone;
                    worksheet.Cell(row, 4).Value = o.DeliveryAddress;
                    worksheet.Cell(row, 5).Value = o.DeliveryLongitude;
                    worksheet.Cell(row, 6).Value = o.DeliveryLatitude;
                    worksheet.Cell(row, 7).Value = o.TotalAmount;
                    worksheet.Cell(row, 8).Value = o.ReceivedAmount;
                    worksheet.Cell(row, 9).Value = GetPaymentStatusName(o.PaymentStatus);
                    worksheet.Cell(row, 10).Value = GetStatusName(o.Status);
                    worksheet.Cell(row, 11).Value = o.DeliveryPerson?.Name;
                    worksheet.Cell(row, 12).Value = o.DeliveryTime?.ToString("yyyy-MM-dd HH:mm");
                    worksheet.Cell(row, 13).Value = o.CreatedAt.ToString("yyyy-MM-dd HH:mm");
                    
                    // 产品明细
                    var items = o.Items.Select(i => $"{i.Product?.Name}({i.Quantity}x{i.UnitPrice})").ToList();
                    worksheet.Cell(row, 14).Value = string.Join(", ", items);
                    
                    row++;
                }

                worksheet.Columns().AdjustToContents();
                workbook.SaveAs(dialog.FileName);
                ShowSuccess($"导出成功，共 {orders.Count} 条订单");
            }
        }
        catch (Exception ex)
        {
            ShowError($"导出失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ExportForGaodeAsync()
    {
        try
        {
            var branchId = CurrentSession.CurrentBranchId;
            var orders = await _dbContext.Orders
                .Include(o => o.Customer)
                .Where(o => o.BranchId == branchId && 
                    o.Status == OrderStatus.Assigned && 
                    o.DeliveryLongitude.HasValue && o.DeliveryLatitude.HasValue)
                .OrderBy(o => o.DeliveryTime)
                .Take(500)
                .ToListAsync();

            if (orders.Count == 0)
            {
                ShowError("没有可导出的配送订单（需要已分配状态且有经纬度）");
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "Excel文件|*.xlsx",
                FileName = $"高德路径规划_{DateTime.Now:yyyyMMdd}"
            };

            if (dialog.ShowDialog() == true)
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("配送路线");

                // 高德路径规划标准表头
                var headers = new[] { "名称", "*经度", "*纬度", "*地址", "颜色", "图标(外轮廓)", "图标(填充物)", "描述", "文件夹" };
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(1, i + 1).Value = headers[i];
                }

                var headerRange = worksheet.Range(1, 1, 1, headers.Length);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                // 按日期分组
                var today = DateTime.Now.ToString("yyMMdd");
                var branchName = (await _dbContext.Branches.FindAsync(branchId))?.Name ?? "分公司";
                var groupPrefix = $"{today}{branchName}配送";

                var row = 2;
                var groupNum = 1;
                var itemsInGroup = 0;
                
                foreach (var o in orders.OrderBy(o => o.DeliveryTime))
                {
                    // 每20个订单一组
                    if (itemsInGroup > 0 && itemsInGroup % 20 == 0)
                    {
                        groupNum++;
                    }

                    worksheet.Cell(row, 1).Value = o.Customer?.Name ?? o.OrderNo;
                    worksheet.Cell(row, 2).Value = o.DeliveryLongitude ?? 0;  // GCJ-02坐标系
                    worksheet.Cell(row, 3).Value = o.DeliveryLatitude ?? 0;
                    worksheet.Cell(row, 4).Value = o.DeliveryAddress;
                    worksheet.Cell(row, 5).Value = "";  // 颜色
                    worksheet.Cell(row, 6).Value = "";  // 图标外轮廓
                    worksheet.Cell(row, 7).Value = "";  // 图标填充物
                    
                    // 描述：订单号+客户+金额
                    var desc = $"{o.OrderNo}\n客户：{o.Customer?.Name}\n金额：¥{o.TotalAmount}\n电话：{o.Customer?.Phone}";
                    worksheet.Cell(row, 8).Value = desc;
                    
                    // 文件夹：日期+分公司/组号
                    worksheet.Cell(row, 9).Value = $"{groupPrefix}/订单组{groupNum}";
                    
                    row++;
                    itemsInGroup++;
                }

                worksheet.Columns().AdjustToContents();
                workbook.SaveAs(dialog.FileName);
                ShowSuccess($"高德路径规划导出成功，共 {orders.Count} 个配送点");
            }
        }
        catch (Exception ex)
        {
            ShowError($"导出失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private void CloseEdit()
    {
        IsEditMode = false;
        EditingOrder = null;
    }
}

public partial class OrderEditViewModel : ViewModelBase
{
    private readonly ProDbContext _dbContext;
    private int? _orderId;
    private Action? _onSaveCompleted;

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
    private ObservableCollection<OrderItemDto> _items = new();

    [ObservableProperty]
    private ObservableCollection<CustomerListItem> _customers = new();

    [ObservableProperty]
    private CustomerListItem? _selectedCustomer;

    [ObservableProperty]
    private ObservableCollection<ProductListItem> _products = new();

    [ObservableProperty]
    private ProductListItem? _selectedProduct;

    [ObservableProperty]
    private int _addQuantity = 1;

    [ObservableProperty]
    private ObservableCollection<DeliveryPersonListItem> _deliveryPersons = new();

    [ObservableProperty]
    private DeliveryPersonListItem? _selectedDeliveryPerson;

    [ObservableProperty]
    private ObservableCollection<OrderModificationRecordDto> _modificationRecords = new();

    [ObservableProperty]
    private string _customerSearchText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<CustomerListItem> _customerSearchResults = new();

    [ObservableProperty]
    private bool _hasCustomerSearchResults;

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

    public OrderEditViewModel()
    {
        _dbContext = App.Services.GetService(typeof(ProDbContext)) as ProDbContext 
            ?? throw new InvalidOperationException("无法获取数据库上下文");
        
        _orderId = null;
        WindowTitle = "新建订单";
        IsReadOnly = false;

        _ = InitAsync();
    }

    private async Task InitAsync()
    {
        try
        {
            OrderNo = await GenerateOrderNoAsync();
            await LoadCustomersAsync();
            await LoadProductsAsync();
            await LoadDeliveryPersonsAsync();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "订单编辑初始化失败");
        }
    }

    public void LoadOrder(int orderId)
    {
        _orderId = orderId;
        WindowTitle = "编辑订单";
        IsReadOnly = false;
        _ = LoadOrderAsync();
    }

    private async Task<string> GenerateOrderNoAsync()
    {
        var branchId = CurrentSession.CurrentBranchId;
        var branch = await _dbContext.Branches.FindAsync(branchId);
        var branchCode = branch?.Code ?? "0000";
        if (branchCode.Length != 4)
            branchCode = branchCode.PadLeft(4, '0').Substring(0, 4);

        var datePart = DateTime.Now.ToString("yyyyMMdd");
        var prefix = $"D{datePart}{branchCode}";

        var maxNo = await _dbContext.Orders
            .Where(o => o.OrderNo.StartsWith(prefix))
            .MaxAsync(o => (string?)o.OrderNo) ?? "";

        var seq = 1;
        if (maxNo.Length >= prefix.Length + 4)
        {
            var lastSeqStr = maxNo.Substring(prefix.Length, 4);
            int.TryParse(lastSeqStr, out seq);
            seq++;
        }

        return $"{prefix}{seq:D4}";
    }

    private async Task LoadCustomersAsync()
    {
        var branchId = CurrentSession.CurrentBranchId;
        var customers = await _dbContext.Customers
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
            // 经纬度需要在保存时从 Customer 表获取
        }
    }

    private async Task LoadOrderAsync()
    {
        if (!_orderId.HasValue) return;

        var order = await _dbContext.Orders
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

    /// <summary>
    /// DataGrid行编辑完成后重算金额
    /// </summary>
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

        // 自动填充配送地址为客户地址（如果未设置）
        if (string.IsNullOrWhiteSpace(DeliveryAddress) && SelectedCustomer != null)
        {
            DeliveryAddress = SelectedCustomer.Address;
        }

        try
        {
            var now = DateTime.Now;
            var localTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            using var transaction = await _dbContext.Database.BeginTransactionAsync();

            if (_orderId.HasValue)
            {
                var order = await _dbContext.Orders
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.Id == _orderId.Value);
                    
                if (order != null)
                {
                    // 记录修改前状态
                    var changes = new System.Text.StringBuilder();
                    if (order.CustomerId != CustomerId) changes.Append($"客户: {order.Customer?.Name} -> {CustomerName}; ");
                    if (order.TotalAmount != TotalAmount) changes.Append($"金额: {order.TotalAmount} -> {TotalAmount}; ");
                    if (order.PaymentStatus != PaymentStatus) changes.Append($"收款状态: {order.PaymentStatus} -> {PaymentStatus}; ");

                    order.CustomerId = CustomerId;
                    order.DeliveryAddress = DeliveryAddress;
                    order.DeliveryLongitude = DeliveryLongitude;
                    order.DeliveryLatitude = DeliveryLatitude;
                    order.DeliveryTime = DeliveryTime;
                    order.TotalAmount = TotalAmount;
                    order.ReceivedAmount = ReceivedAmount;
                    order.DiscountAmount = DiscountAmount;
                    order.PaymentStatus = PaymentStatus;
                    order.Remark = Remark;
                    order.CancelReason = CancelReason;
                    order.UpdatedAt = now;
                    order.LocalTimestamp = localTimestamp;
                    order.SyncStatus = SyncStatus.Pending;

                    if (SelectedDeliveryPerson != null && order.Status == OrderStatus.Pending)
                    {
                        order.DeliveryPersonId = SelectedDeliveryPerson.Id;
                        order.Status = OrderStatus.Assigned;
                        changes.Append("已分配配送员; ");
                    }

                    // 更新明细
                    _dbContext.OrderItems.RemoveRange(order.Items);

                    foreach (var item in Items)
                    {
                        _dbContext.OrderItems.Add(new OrderItem
                        {
                            OrderId = order.Id,
                            ProductId = item.ProductId,
                            Quantity = item.Quantity,
                            UnitPrice = item.UnitPrice,
                            Amount = item.Amount,
                            DiscountType = item.DiscountType,
                            DiscountValue = item.DiscountValue
                        });
                    }

                    // 记录修改
                    if (changes.Length > 0)
                    {
                        _dbContext.OrderModificationRecords.Add(new OrderModificationRecord
                        {
                            OrderId = order.Id,
                            ModifiedById = CurrentSession.CurrentEmployeeId,
                            ModifiedAt = now,
                            Content = changes.ToString(),
                            ModificationType = "Update"
                        });
                    }

                    await _dbContext.SaveChangesAsync();
                    await UpdateStockForOrderAsync(order.Id, Items);

                    await transaction.CommitAsync();
                    ShowSuccess("保存成功");
                    _onSaveCompleted?.Invoke();
                }
            }
            else
            {
                var order = new Order
                {
                    OrderNo = OrderNo,
                    CustomerId = CustomerId,
                    BranchId = CurrentSession.CurrentBranchId,
                    CreatedById = CurrentSession.CurrentEmployeeId,
                    DeliveryAddress = DeliveryAddress,
                    DeliveryLongitude = DeliveryLongitude,
                    DeliveryLatitude = DeliveryLatitude,
                    DeliveryTime = DeliveryTime,
                    TotalAmount = TotalAmount,
                    ReceivedAmount = ReceivedAmount,
                    DiscountAmount = DiscountAmount,
                    PaymentStatus = PaymentStatus,
                    Status = IsDraft ? OrderStatus.Draft : OrderStatus.Pending,
                    Remark = Remark,
                    DraftExpireTime = IsDraft ? now.AddMinutes(30) : null,
                    CreatedAt = now,
                    UpdatedAt = now,
                    LocalTimestamp = localTimestamp,
                    SyncStatus = SyncStatus.Pending
                };

                if (SelectedDeliveryPerson != null)
                {
                    order.DeliveryPersonId = SelectedDeliveryPerson.Id;
                    order.Status = OrderStatus.Assigned;
                }

                _dbContext.Orders.Add(order);
                await _dbContext.SaveChangesAsync();

                foreach (var item in Items)
                {
                    _dbContext.OrderItems.Add(new OrderItem
                    {
                        OrderId = order.Id,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        Amount = item.Amount,
                        DiscountType = item.DiscountType,
                        DiscountValue = item.DiscountValue
                    });
                }

                // 记录创建
                _dbContext.OrderModificationRecords.Add(new OrderModificationRecord
                {
                    OrderId = order.Id,
                    ModifiedById = CurrentSession.CurrentEmployeeId,
                    ModifiedAt = now,
                    Content = "创建订单",
                    ModificationType = "Create"
                });

                await _dbContext.SaveChangesAsync();
                await UpdateStockForOrderAsync(order.Id, Items);

                await transaction.CommitAsync();
                ShowSuccess("保存成功");
                _onSaveCompleted?.Invoke();
            }
        }
        catch (Exception ex)
        {
            ShowError($"保存失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 更新订单的库存扣减
    /// </summary>
    private async Task UpdateStockForOrderAsync(int orderId, ObservableCollection<OrderItemDto> newItems)
    {
        // 获取订单原有的明细（用于计算库存差异）
        var originalItems = await _dbContext.OrderItems
            .Where(i => i.OrderId == orderId)
            .ToListAsync();

        // 原有商品：恢复库存
        foreach (var original in originalItems)
        {
            var product = await _dbContext.Products.FindAsync(original.ProductId);
            if (product != null)
            {
                product.Stock += original.Quantity;
            }
        }

        // 新商品：扣减库存
        foreach (var newItem in newItems)
        {
            var product = await _dbContext.Products.FindAsync(newItem.ProductId);
            if (product != null)
            {
                if (product.Stock < newItem.Quantity)
                {
                    throw new InvalidOperationException($"产品 [{product.Name}] 库存不足，当前库存：{product.Stock}，需要：{newItem.Quantity}");
                }
                product.Stock -= newItem.Quantity;
            }
        }

        await _dbContext.SaveChangesAsync();
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
}
