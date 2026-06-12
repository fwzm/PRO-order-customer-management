using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using ClosedXML.Excel;

namespace PRO.Desktop.ViewModels;

public partial class DeliveryPersonListViewModel : PagedViewModelBase
{
    private readonly ProDbContext _dbContext;
    private readonly IDeliveryPersonService _deliveryPersonService;

    protected override string EntityTypeName => "配送员";

    [ObservableProperty]
    private ObservableCollection<DeliveryPersonListItem> _deliveryPersons = [];

    [ObservableProperty]
    private DeliveryPersonListItem? _selectedDeliveryPerson;

    [ObservableProperty]
    private DeliveryPersonStatus? _filterStatus;

    [ObservableProperty]
    private ObservableCollection<OrderListItem> _pendingOrders = [];

    public DeliveryPersonListViewModel()
    {
        _dbContext = App.Services.GetService(typeof(ProDbContext)) as ProDbContext
            ?? throw new InvalidOperationException("无法获取数据库上下文");
        _deliveryPersonService = App.Services.GetRequiredService<IDeliveryPersonService>();

        RunInBackground(LoadDataAsync(), "加载物流数据失败");
    }

    protected override bool HasActiveFilters() => FilterStatus != null;

    [RelayCommand]
    private void ClearAllFilters()
    {
        FilterStatus = null;
        SearchKeyword = null;
        InvalidateCountCache();
        RunInBackground(ResetToFirstPageAndLoadAsync(), "清除筛选失败");
    }

    private ICommand? _clearFiltersCommand;
    protected override ICommand? ClearFiltersCommand => _clearFiltersCommand ??= new RelayCommand(ClearAllFilters);
    private ICommand? _createNewCommand;
    protected override ICommand? CreateNewCommand => _createNewCommand ??= new RelayCommand(NewDeliveryPerson);

    partial void OnFilterStatusChanged(DeliveryPersonStatus? value)
    {
        RunInBackground(ResetToFirstPageAndLoadAsync(), "筛选配送员失败");
    }

    protected override async Task LoadDataAsync()
    {
        IsLoading = true;
        try
        {
            var request = new PagedRequest
            {
                PageIndex = PageIndex,
                PageSize = PageSize,
                Keyword = SearchKeyword
            };
            int? branchId = CurrentSession.Current.IsHeadquartersAdmin ? null : CurrentSession.CurrentBranchId;
            var result = await _deliveryPersonService.GetListAsync(request, branchId, FilterStatus);
            if (!result.Success || result.Data == null)
            {
                ShowError(result.Message);
                return;
            }

            TotalCount = result.Data.TotalCount;
            DeliveryPersons = new ObservableCollection<DeliveryPersonListItem>(result.Data.Items);
            UpdateEmptyState();
        }
        catch (Exception ex)
        {
            ShowBusinessException(ex, "加载配送员列表");
            ShowLoadFailedState();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void NewDeliveryPerson()
    {
        var editVm = App.Services.GetService(typeof(DeliveryPersonEditViewModel)) as DeliveryPersonEditViewModel
            ?? throw new InvalidOperationException("无法创建配送员编辑视图模型");

        editVm.OnSaveCompleted = async () => { await LoadDataAsync(); };

        var dialog = new Views.DeliveryPersonEditWindow(editVm) { Owner = System.Windows.Application.Current.MainWindow };
        dialog.ShowDialog();
    }

    [RelayCommand]
    private void EditDeliveryPerson(DeliveryPersonListItem? person)
    {
        if (person == null) return;

        var editVm = App.Services.GetService(typeof(DeliveryPersonEditViewModel)) as DeliveryPersonEditViewModel
            ?? throw new InvalidOperationException("无法创建配送员编辑视图模型");

        editVm.LoadDeliveryPerson(person.Id);
        editVm.OnSaveCompleted = async () => { await LoadDataAsync(); };

        var dialog = new Views.DeliveryPersonEditWindow(editVm) { Owner = System.Windows.Application.Current.MainWindow };
        dialog.ShowDialog();
    }

    [RelayCommand]
    private async Task LoadPendingOrdersAsync()
    {
        var branchId = CurrentSession.CurrentBranchId;
        var orders = await _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Where(o => o.BranchId == branchId && o.Status == OrderStatus.Pending)
            .OrderBy(o => o.CreatedAt)
            .Take(50)
            .ToListAsync();

        PendingOrders = new ObservableCollection<OrderListItem>(orders.Select(o => new OrderListItem
        {
            Id = o.Id,
            OrderNo = o.OrderNo,
            CustomerId = o.CustomerId,
            CustomerName = o.Customer?.Name ?? "",
            TotalAmount = o.TotalAmount,
            DeliveryAddress = o.DeliveryAddress,
            DeliveryLongitude = o.DeliveryLongitude,
            DeliveryLatitude = o.DeliveryLatitude,
            CreatedAt = o.CreatedAt
        }));
    }

    [RelayCommand]
    private Task DeleteDeliveryPersonAsync(DeliveryPersonListItem? person)
    {
        if (person == null) return Task.CompletedTask;

        MessageBox.Show("当前基础修复版未扩展删除流程，如需停用配送员，请进入编辑页调整状态。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task ManualAssignAsync(OrderListItem? order)
    {
        if (order == null) return Task.CompletedTask;

        MessageBox.Show("当前基础修复版保留了入口，请在订单管理页面执行人工分配。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task AutoAssignAsync()
    {
        MessageBox.Show("自动分配仍属于业务能力，当前基础修复版仅保留入口，不继续扩展。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task ExportForGaodeAsync()
    {
        try
        {
            var branchId = CurrentSession.CurrentBranchId;
            var orders = await _dbContext.Orders
                .AsNoTracking()
                .Include(o => o.Customer)
                .Where(o => o.BranchId == branchId &&
                    o.Status == OrderStatus.Assigned &&
                    o.DeliveryLongitude.HasValue && o.DeliveryLatitude.HasValue)
                .ToListAsync();

            if (orders.Count == 0)
            {
                MessageBox.Show("没有可导出的配送订单", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
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

                var headers = new[] { "名称", "*经度", "*纬度", "*地址", "颜色", "图标(外轮廓)", "图标(填充物)", "描述", "文件夹" };
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(1, i + 1).Value = headers[i];
                }

                var headerRange = worksheet.Range(1, 1, 1, headers.Length);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                var today = DateTime.Now.ToString("yyMMdd");
                var branchName = (await _dbContext.Branches.FindAsync(branchId))?.Name ?? "分公司";
                var groupPrefix = $"{today}{branchName}配送";

                var row = 2;
                var groupNum = 1;
                var itemsInGroup = 0;

                foreach (var o in orders.OrderBy(o => o.DeliveryTime))
                {
                    if (itemsInGroup > 0 && itemsInGroup % 20 == 0) groupNum++;

                    worksheet.Cell(row, 1).Value = o.Customer?.Name ?? o.OrderNo;
                    worksheet.Cell(row, 2).Value = o.DeliveryLongitude ?? 0;
                    worksheet.Cell(row, 3).Value = o.DeliveryLatitude ?? 0;
                    worksheet.Cell(row, 4).Value = o.DeliveryAddress;
                    worksheet.Cell(row, 8).Value = $"{o.OrderNo}\n客户：{o.Customer?.Name}\n金额：¥{o.TotalAmount}";
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
}
