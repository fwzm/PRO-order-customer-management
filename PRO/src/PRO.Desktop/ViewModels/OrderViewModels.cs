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
using System.Windows.Input;
using Microsoft.Win32;
using ClosedXML.Excel;
using Serilog;

namespace PRO.Desktop.ViewModels;

/// <summary>
/// 订单列表ViewModel - 负责列表展示、筛选、批量操作
/// </summary>
public partial class OrderListViewModel : PagedViewModelBase
{
    private readonly ProDbContext _dbContext;
    private readonly IOrderService _orderService;
    private readonly ViewMemoryService _viewMemoryService;
    private readonly AuditService _auditService;
    private const string ViewKey = "order_list";
    private bool _suppressNextAutoSave;

    protected override string EntityTypeName => "订单";

    [ObservableProperty]
    private ObservableCollection<OrderListItem> _orders = [];

    [ObservableProperty]
    private ObservableCollection<SelectableItem<OrderListItem>> _selectableOrders = [];

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

    [ObservableProperty]
    private int _pendingDraftCount;

    [ObservableProperty]
    private bool _hasSavedView;

    [ObservableProperty]
    private bool _isExporting;

    [ObservableProperty]
    private int _exportProgress;

    [ObservableProperty]
    private string _exportStatusText = string.Empty;

    public bool HasPendingDrafts => PendingDraftCount > 0;

    protected override bool HasActiveFilters() =>
        FilterStatus != null || FilterPaymentStatus != null || FilterStartDate != null || FilterEndDate != null || ShowOnlyDrafts;

    [RelayCommand]
    private void ClearAllFilters()
    {
        FilterStatus = null;
        FilterPaymentStatus = null;
        FilterStartDate = null;
        FilterEndDate = null;
        ShowOnlyDrafts = false;
        SearchKeyword = null;
        InvalidateCountCache();
        RunInBackground(ResetToFirstPageAndLoadAsync(), "清除筛选失败");
    }

    private ICommand? _clearFiltersCommand;
    protected override ICommand? ClearFiltersCommand => _clearFiltersCommand ??= new RelayCommand(ClearAllFilters);

    private ICommand? _createNewCommand;
    protected override ICommand? CreateNewCommand => _createNewCommand ??= new RelayCommand(NewOrder);

    // 快速筛选预设
    public List<QuickFilter> QuickFilters { get; } =
    [
        new("今日订单", () => DateTime.Today, () => DateTime.Today.AddDays(1)),
        new("本周订单", () => DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek + 1), () => DateTime.Today.AddDays(1)),
        new("本月订单", () => new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1), () => DateTime.Today.AddDays(1)),
        new("待分配", filterStatus: OrderStatus.Pending),
        new("配送中", filterStatus: OrderStatus.Delivering),
        new("已完成", filterStatus: OrderStatus.Completed),
        new("未收款", filterPaymentStatus: PaymentStatus.Unpaid),
        new("部分收款", filterPaymentStatus: PaymentStatus.PartialPaid),
    ];

    public OrderListViewModel()
    {
        _dbContext = App.Services.GetService(typeof(ProDbContext)) as ProDbContext
            ?? throw new InvalidOperationException("无法获取数据库上下文");
        _orderService = App.Services.GetService(typeof(IOrderService)) as IOrderService
            ?? throw new InvalidOperationException("无法获取订单服务");
        _viewMemoryService = App.Services.GetService(typeof(ViewMemoryService)) as ViewMemoryService
            ?? throw new InvalidOperationException("无法获取视图记忆服务");
        _auditService = App.Services.GetService(typeof(AuditService)) as AuditService
            ?? throw new InvalidOperationException("无法获取审计服务");

        RunInBackground(InitializeAsync(), "初始化订单列表失败");
    }

    private async Task InitializeAsync()
    {
        await LoadSavedViewAsync();
        await LoadDataAsync();
    }

    /// <summary>
    /// 加载保存的视图
    /// </summary>
    private async Task LoadSavedViewAsync()
    {
        try
        {
            var memory = await _viewMemoryService.LoadViewAsync<OrderListViewMemory>(ViewKey, CurrentSession.CurrentEmployeeId);
            if (memory != null)
            {
                HasSavedView = true;
                SearchKeyword = memory.SearchKeyword;
                PageSize = memory.PageSize;

                if (!string.IsNullOrEmpty(memory.FilterStatus) && Enum.TryParse<OrderStatus>(memory.FilterStatus, out var status))
                    FilterStatus = status;
                if (!string.IsNullOrEmpty(memory.FilterPaymentStatus) && Enum.TryParse<PaymentStatus>(memory.FilterPaymentStatus, out var payStatus))
                    FilterPaymentStatus = payStatus;

                FilterStartDate = memory.FilterStartDate;
                FilterEndDate = memory.FilterEndDate;
                ShowOnlyDrafts = memory.ShowOnlyDrafts;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "加载订单视图记忆失败");
        }
    }

    /// <summary>
    /// 保存当前视图
    /// </summary>
    [RelayCommand]
    private async Task SaveCurrentViewAsync()
    {
        try
        {
            await SaveCurrentViewCoreAsync();
            ShowSuccess("视图已保存");
        }
        catch (Exception ex)
        {
            ShowError($"保存视图失败: {ex.Message}");
        }
    }

    private async Task SaveCurrentViewCoreAsync()
    {
        var memory = new OrderListViewMemory
        {
            FilterStatus = FilterStatus?.ToString(),
            FilterPaymentStatus = FilterPaymentStatus?.ToString(),
            FilterStartDate = FilterStartDate,
            FilterEndDate = FilterEndDate,
            SearchKeyword = SearchKeyword,
            PageSize = PageSize,
            ShowOnlyDrafts = ShowOnlyDrafts
        };

        await _viewMemoryService.SaveViewAsync(ViewKey, memory, CurrentSession.CurrentEmployeeId);
        HasSavedView = true;
    }

    /// <summary>
    /// 清除保存的视图
    /// </summary>
    [RelayCommand]
    private async Task ClearSavedViewAsync()
    {
        try
        {
            await _viewMemoryService.DeleteViewAsync(ViewKey, CurrentSession.CurrentEmployeeId);
            HasSavedView = false;

            // 重置筛选条件
            FilterStatus = null;
            FilterPaymentStatus = null;
            FilterStartDate = null;
            FilterEndDate = null;
            SearchKeyword = null;
            ShowOnlyDrafts = false;

            ShowSuccess("视图已重置");
            _suppressNextAutoSave = true;
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            ShowError($"清除视图失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 快速筛选
    /// </summary>
    [RelayCommand]
    private async Task ApplyQuickFilterAsync(QuickFilter? filter)
    {
        if (filter == null) return;

        FilterStartDate = filter.GetStartDate?.Invoke();
        FilterEndDate = filter.GetEndDate?.Invoke();
        FilterStatus = filter.FilterStatus;
        FilterPaymentStatus = filter.FilterPaymentStatus;
        ShowOnlyDrafts = false;

        await ResetToFirstPageAndLoadAsync();
    }

    protected override async Task LoadDataAsync()
    {
        IsLoading = true;
        try
        {
            int? branchId = CurrentSession.Current.IsHeadquartersAdmin ? null : (int?)CurrentSession.CurrentBranchId;
            var request = new PagedRequest
            {
                PageIndex = PageIndex,
                PageSize = PageSize,
                Keyword = SearchKeyword
            };

            // 草稿模式：单独筛选草稿
            var statusFilter = ShowOnlyDrafts ? OrderStatus.Draft : FilterStatus;

            var result = await _orderService.GetListAsync(request,
                branchId: branchId,
                status: statusFilter,
                paymentStatus: ShowOnlyDrafts ? null : FilterPaymentStatus);

            if (result.Success && result.Data != null)
            {
                var items = result.Data.Items.AsEnumerable();

                // 日期范围过滤（服务端不支持，客户端过滤）
                if (!ShowOnlyDrafts)
                {
                    if (FilterStartDate.HasValue)
                        items = items.Where(o => o.CreatedAt >= FilterStartDate.Value);
                    if (FilterEndDate.HasValue)
                        items = items.Where(o => o.CreatedAt < FilterEndDate.Value);
                }

                var filteredItems = items.ToList();
                TotalCount = ShowOnlyDrafts ? result.Data.TotalCount : filteredItems.Count;

                var orderItems = filteredItems.Select(o =>
                {
                    o.PaymentStatusName = GetPaymentStatusName(o.PaymentStatus);
                    o.StatusName = GetStatusName(o.Status);
                    return o;
                }).ToList();

                Orders = new ObservableCollection<OrderListItem>(orderItems);
                SelectableOrders = new ObservableCollection<SelectableItem<OrderListItem>>(
                    orderItems.Select(CreateSelectableOrder));
                IsAllSelected = false;
                SelectedCount = 0;
                UpdateEmptyState();

                if (_suppressNextAutoSave)
                {
                    _suppressNextAutoSave = false;
                }
                else
                {
                    await SaveCurrentViewCoreAsync();
                }
            }
        }
        catch (Exception ex)
        {
            ShowBusinessException(ex, "加载订单列表");
            ShowLoadFailedState();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private SelectableItem<OrderListItem> CreateSelectableOrder(OrderListItem order)
    {
        var selectable = new SelectableItem<OrderListItem>
        {
            Id = order.Id,
            Data = order
        };
        selectable.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SelectableItem<OrderListItem>.IsSelected))
                UpdateSelectedCount();
        };
        return selectable;
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

    [RelayCommand]
    private async Task CopyOrderAsync(OrderListItem? order)
    {
        if (order == null) return;

        try
        {
            var templateService = App.Services.GetService(typeof(OrderTemplateService)) as OrderTemplateService;
            if (templateService == null) return;

            var result = await templateService.CopyOrderAsync(order.Id, CurrentSession.CurrentEmployeeId);
            if (result.Success)
            {
                ShowSuccess("订单已复制为草稿");
                await LoadDataAsync();
            }
            else
            {
                ShowError(result.Message);
            }
        }
        catch (Exception ex)
        {
            ShowError($"复制订单失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SaveAsTemplateAsync(OrderListItem? order)
    {
        if (order == null) return;

        var templateName = Microsoft.VisualBasic.Interaction.InputBox(
            "请输入模板名称：", "保存为模板", $"{order.CustomerName}_{DateTime.Now:MMdd}");

        if (string.IsNullOrWhiteSpace(templateName)) return;

        try
        {
            var templateService = App.Services.GetService(typeof(OrderTemplateService)) as OrderTemplateService;
            if (templateService == null) return;

            var request = new CreateTemplateFromOrderRequest
            {
                OrderId = order.Id,
                TemplateName = templateName
            };

            var result = await templateService.CreateTemplateFromOrderAsync(request, CurrentSession.CurrentEmployeeId);
            if (result.Success)
            {
                ShowSuccess($"模板「{templateName}」已保存");
            }
            else
            {
                ShowError(result.Message);
            }
        }
        catch (Exception ex)
        {
            ShowError($"保存模板失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task CreateFromTemplateAsync()
    {
        try
        {
            var templateService = App.Services.GetService(typeof(OrderTemplateService)) as OrderTemplateService;
            if (templateService == null) return;

            var result = await templateService.GetTemplatesAsync(CurrentSession.CurrentEmployeeId);
            if (!result.Success || result.Data == null || !result.Data.Any())
            {
                ShowError("没有可用的订单模板");
                return;
            }

            // 显示模板选择窗口
            var templates = result.Data;
            var selectWindow = new System.Windows.Window
            {
                Title = "选择订单模板",
                Width = 500,
                Height = 400,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
                Owner = System.Windows.Application.Current.MainWindow
            };

            var listBox = new System.Windows.Controls.ListBox
            {
                Margin = new System.Windows.Thickness(10)
            };
            foreach (var template in templates)
            {
                listBox.Items.Add(new System.Windows.Controls.ListBoxItem
                {
                    Content = $"{template.Name} ({template.CustomerName})",
                    Tag = template.Id
                });
            }

            var okButton = new System.Windows.Controls.Button
            {
                Content = "使用模板",
                Width = 80,
                Margin = new System.Windows.Thickness(10),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            };

            var panel = new System.Windows.Controls.StackPanel();
            panel.Children.Add(listBox);
            panel.Children.Add(okButton);
            selectWindow.Content = panel;

            okButton.Click += (s, e) => selectWindow.DialogResult = true;

            if (selectWindow.ShowDialog() == true && listBox.SelectedItem is System.Windows.Controls.ListBoxItem selected)
            {
                var templateId = (int)selected.Tag;
                var createResult = await templateService.CreateOrderFromTemplateAsync(templateId, CurrentSession.CurrentEmployeeId);

                if (createResult.Success)
                {
                    ShowSuccess("订单已从模板创建");
                    await LoadDataAsync();
                }
                else
                {
                    ShowError(createResult.Message);
                }
            }
        }
        catch (Exception ex)
        {
            ShowError($"从模板创建订单失败: {ex.Message}");
        }
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

        // 使用 OrderStatusManager 获取当前状态允许的下一步操作
        var validStatuses = PRO.Domain.Enums.OrderStatusManager.GetValidNextStatuses(order.Status);

        if (!validStatuses.Any())
        {
            ShowError($"当前状态「{order.StatusName}」不允许变更");
            return Task.CompletedTask;
        }

        var menu = new System.Windows.Controls.ContextMenu();
        foreach (var status in validStatuses)
        {
            var statusName = PRO.Domain.Enums.OrderStatusManager.GetStatusName(status);
            var item = new System.Windows.Controls.MenuItem
            {
                Header = statusName,
                Tag = status,
                // 取消操作用红色标识
                Foreground = status == OrderStatus.Cancelled
                    ? System.Windows.Media.Brushes.Red
                    : System.Windows.Media.Brushes.Black
            };
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
            var auditOrder = await _dbContext.Orders.AsNoTracking()
                .Where(o => o.Id == orderId)
                .Select(o => new { o.Id, o.OrderNo, o.Status })
                .FirstOrDefaultAsync();
            if (auditOrder == null)
            {
                ShowError("订单不存在");
                return;
            }

            if (newStatus == OrderStatus.Cancelled && !CheckOrderPermission("Cancel")) return;
            if (newStatus != OrderStatus.Cancelled && !CheckOrderPermission("BatchStatusChange")) return;

            string? reason = null;
            if (newStatus == OrderStatus.Cancelled)
            {
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

                if (dialogWindow.ShowDialog() != true)
                    return;

                reason = inputDialog.Text;
                if (string.IsNullOrWhiteSpace(reason))
                {
                    ShowError("取消原因不能为空");
                    return;
                }
            }

            ApiResponse<bool>? result = null;
            var executed = await ExecuteWithRetryAsync(async () =>
            {
                result = await _orderService.UpdateStatusAsync(new UpdateOrderStatusRequest
                {
                    OrderId = orderId,
                    NewStatus = newStatus,
                    Reason = reason
                }, CurrentSession.CurrentEmployeeId);

                if (result == null || !result.Success)
                    throw new InvalidOperationException(result?.Message ?? "状态更新失败");
            }, "订单状态变更", showSuccess: false);

            if (!executed || result == null)
            {
                return;
            }

            ShowSuccess(result.Message);
            await _auditService.LogOrderStatusChangeAsync(
                CurrentSession.CurrentEmployeeId,
                auditOrder.Id,
                auditOrder.OrderNo,
                auditOrder.Status,
                newStatus,
                reason);
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            ShowError($"更新状态失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ExportToExcelAsync()
    {
        if (!CheckOrderPermission("Export")) return;

        try
        {
            var branchId = CurrentSession.CurrentBranchId;

            // 显示导出进度
            IsExporting = true;
            ExportProgress = 0;
            ExportStatusText = "正在查询数据...";

            var orders = await _dbContext.Orders
                .AsNoTracking()
                .Include(o => o.Customer)
                .Include(o => o.DeliveryPerson)
                .Include(o => o.Items).ThenInclude(i => i.Product)
                .Where(o => o.BranchId == branchId && o.Status != OrderStatus.Draft)
                .OrderByDescending(o => o.CreatedAt)
                .Take(5000)  // 最多导出5000条
                .ToListAsync();

            if (orders.Count == 0)
            {
                IsExporting = false;
                ShowError("没有可导出的订单数据");
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "Excel文件|*.xlsx",
                FileName = $"订单数据_{DateTime.Now:yyyyMMdd}"
            };

            if (dialog.ShowDialog() == true)
            {
                ExportStatusText = "正在生成Excel...";
                ExportProgress = 0;

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
                var totalCount = orders.Count;
                var lastReportPercent = 0;
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

                    // 每5%报告一次进度
                    var percent = (int)((double)(row - 2) / totalCount * 100);
                    if (percent - lastReportPercent >= 5 || percent == 100)
                    {
                        lastReportPercent = percent;
                        ExportProgress = percent;
                        ExportStatusText = $"正在导出... {row - 2}/{totalCount}";
                        await Task.Yield(); // 让UI有机会刷新
                    }
                }

                ExportStatusText = "正在调整列宽...";
                ExportProgress = 95;
                worksheet.Columns().AdjustToContents();

                ExportStatusText = "正在保存文件...";
                ExportProgress = 98;
                workbook.SaveAs(dialog.FileName);

                ExportProgress = 100;
                await _auditService.LogExportAsync(CurrentSession.CurrentEmployeeId, "订单Excel", orders.Count);
                ShowSuccess($"导出成功，共 {orders.Count} 条订单");
            }

            IsExporting = false;
        }
        catch (Exception ex)
        {
            IsExporting = false;
            ShowError($"导出失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ExportForGaodeAsync()
    {
        if (!CheckOrderPermission("Export")) return;

        try
        {
            var branchId = CurrentSession.CurrentBranchId;
            var orders = await _dbContext.Orders
                .AsNoTracking()
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
                await _auditService.LogExportAsync(CurrentSession.CurrentEmployeeId, "高德配送规划", orders.Count);
                ShowSuccess($"高德路径规划导出成功，共 {orders.Count} 个配送点");
            }
        }
        catch (Exception ex)
        {
            ShowError($"导出失败: {ex.Message}");
        }
    }

    // ========== 批量操作 ==========

    protected override void OnBatchSelectAllChanged(bool value)
    {
        foreach (var item in SelectableOrders)
            item.IsSelected = value;
        UpdateSelectedCount();
    }

    private void UpdateSelectedCount()
    {
        SelectedCount = SelectableOrders.Count(x => x.IsSelected);
    }

    public override void ClearBatchSelection()
    {
        foreach (var item in SelectableOrders)
            item.IsSelected = false;
        base.ClearBatchSelection();
    }

    protected override List<int> GetSelectedIds()
    {
        return SelectableOrders.Where(x => x.IsSelected).Select(x => x.Id).ToList();
    }

    [RelayCommand]
    private void ToggleBatchMode()
    {
        IsBatchMode = !IsBatchMode;
        if (!IsBatchMode)
            ClearBatchSelection();
    }

    [RelayCommand]
    private async Task BatchAssignAsync()
    {
        // 权限检查
        if (!CheckOrderPermission("BatchAssign")) return;

        var selectedIds = GetSelectedIds();
        if (selectedIds.Count == 0)
        {
            ShowError("请先选择要分配的订单");
            return;
        }

        try
        {
            var branchId = CurrentSession.CurrentBranchId;
            var deliveryPersons = await _dbContext.DeliveryPersons
                .AsNoTracking()
                .Where(d => d.BranchId == branchId && d.Status == DeliveryPersonStatus.Available)
                .ToListAsync();

            if (!deliveryPersons.Any())
            {
                ShowError("没有可用的配送员");
                return;
            }

            var selectWindow = new System.Windows.Window
            {
                Title = "选择配送员",
                Width = 350,
                Height = 200,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
                Owner = System.Windows.Application.Current.MainWindow,
                ResizeMode = System.Windows.ResizeMode.NoResize
            };

            var panel = new System.Windows.Controls.StackPanel { Margin = new System.Windows.Thickness(15) };
            panel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = $"将 {selectedIds.Count} 条订单分配给：",
                Margin = new System.Windows.Thickness(0, 0, 0, 10)
            });

            var combo = new System.Windows.Controls.ComboBox { Margin = new System.Windows.Thickness(0, 0, 0, 15) };
            foreach (var dp in deliveryPersons)
                combo.Items.Add(new { dp.Id, Name = $"{dp.Name} (负载: {dp.CurrentLoad}/{dp.MaxLoad})" });
            combo.SelectedIndex = 0;
            panel.Children.Add(combo);

            var btn = new System.Windows.Controls.Button
            {
                Content = "确认分配",
                Width = 80,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            };
            btn.Click += (s, e) => selectWindow.DialogResult = true;
            panel.Children.Add(btn);
            selectWindow.Content = panel;

            if (selectWindow.ShowDialog() == true)
            {
                dynamic? selected = combo.SelectedItem;
                if (selected == null) return;
                var personId = (int)selected.Id;
                var personName = (string)selected.Name;

                if (!ConfirmAction("批量分配", $"确定将 {selectedIds.Count} 个订单分配给 {personName}？"))
                    return;

                var result = await _orderService.BatchAssignAsync(
                    selectedIds,
                    personId,
                    CurrentSession.CurrentEmployeeId);

                if (!result.Success || result.Data == null)
                {
                    ShowError(result.Message);
                    return;
                }

                await _auditService.LogOrderBatchAssignAsync(CurrentSession.CurrentEmployeeId, result.Data.SuccessCount, personName);
                ShowBatchOperationResult("批量分配结果", result.Data);
                ClearBatchSelection();
                await LoadDataAsync();
            }
        }
        catch (Exception ex)
        {
            ShowError($"批量分配失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task BatchConfirmDraftsAsync()
    {
        if (!CheckOrderPermission("BatchStatusChange")) return;

        var selectedIds = GetSelectedIds();
        if (selectedIds.Count == 0)
        {
            ShowError("请先选择草稿订单");
            return;
        }

        if (!ConfirmAction("批量确认", $"确认将 {selectedIds.Count} 条草稿订单转为待分配状态？")) return;

        // 使用 ExecuteBatchOperationAsync 带进度追踪
        var orders = await _dbContext.Orders.AsNoTracking()
            .Where(o => selectedIds.Contains(o.Id))
            .Select(o => new { o.Id, o.OrderNo })
            .ToListAsync();

        var progress = await ExecuteBatchOperationAsync(
            "批量确认草稿",
            orders.Count,
            async (i, ct) =>
            {
                var order = orders[i];
                try
                {
                    var result = await _orderService.BatchConfirmDraftsAsync(
                        new List<int> { order.Id }, CurrentSession.CurrentEmployeeId);
                    return (result.Success, result.Success ? null : result.Message);
                }
                catch (Exception ex)
                {
                    return (false, ex.Message);
                }
            },
            i => orders[i].OrderNo);

        await _auditService.LogOrderBatchStatusChangeAsync(
            CurrentSession.CurrentEmployeeId,
            progress.SuccessCount,
            selectedIds.Count,
            OrderStatus.Pending,
            "批量确认草稿");

        ShowSuccess(progress.Summary);
        ClearBatchSelection();
        await LoadDataAsync();
    }

    private void ShowBatchOperationResult(string title, BatchOperationResult result)
    {
        var summary = $"{title}：成功 {result.SuccessCount} 条，失败 {result.FailedCount} 条";
        if (!result.HasFailures)
        {
            ShowSuccess(summary);
            return;
        }

        var details = string.Join(Environment.NewLine,
            result.Items
                .Where(i => !i.Success)
                .Take(20)
                .Select(i => $"{i.EntityNo}: {i.Message}"));
        if (result.FailedCount > 20)
            details += $"{Environment.NewLine}... 还有 {result.FailedCount - 20} 条失败记录";

        ShowError(summary);
        System.Windows.MessageBox.Show(
            $"{summary}{Environment.NewLine}{Environment.NewLine}{details}",
            title,
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Warning);
    }

    [RelayCommand]
    private void CloseEdit()
    {
        IsEditMode = false;
        EditingOrder = null;
    }
}

/// <summary>
/// 快速筛选预设
/// </summary>
public class QuickFilter
{
    public string Name { get; }
    public Func<DateTime>? GetStartDate { get; }
    public Func<DateTime>? GetEndDate { get; }
    public OrderStatus? FilterStatus { get; }
    public PaymentStatus? FilterPaymentStatus { get; }

    public QuickFilter(string name,
        Func<DateTime>? getStartDate = null,
        Func<DateTime>? getEndDate = null,
        OrderStatus? filterStatus = null,
        PaymentStatus? filterPaymentStatus = null)
    {
        Name = name;
        GetStartDate = getStartDate;
        GetEndDate = getEndDate;
        FilterStatus = filterStatus;
        FilterPaymentStatus = filterPaymentStatus;
    }
}
