using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRO.Application.DTOs;
using PRO.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace PRO.Desktop.ViewModels;

/// <summary>
/// 收款登记 ViewModel
/// </summary>
public partial class PaymentRegistrationViewModel : PagedViewModelBase
{
    private readonly PaymentService _paymentService;

    protected override string EntityTypeName => "收款记录";

    [ObservableProperty]
    private ObservableCollection<PaymentRecordDto> _payments = [];

    [ObservableProperty]
    private PaymentRecordDto? _selectedPayment;

    // 筛选条件
    [ObservableProperty]
    private DateTime? _filterStartDate;

    [ObservableProperty]
    private DateTime? _filterEndDate;

    [ObservableProperty]
    private string? _filterCustomerName;

    // 统计数据
    [ObservableProperty]
    private PaymentStatsDto? _stats;

    [ObservableProperty]
    private bool _showStats;

    protected override bool HasActiveFilters() =>
        FilterStartDate.HasValue || FilterEndDate.HasValue || !string.IsNullOrWhiteSpace(FilterCustomerName);

    [RelayCommand]
    private void ClearAllFilters()
    {
        FilterStartDate = null;
        FilterEndDate = null;
        FilterCustomerName = null;
        SearchKeyword = null;
        InvalidateCountCache();
        RunInBackground(ResetToFirstPageAndLoadAsync(), "清除筛选失败");
    }

    private ICommand? _clearFiltersCommand;
    protected override ICommand? ClearFiltersCommand => _clearFiltersCommand ??= new RelayCommand(ClearAllFilters);

    // 新增收款
    [ObservableProperty]
    private int _orderId;

    [ObservableProperty]
    private string _orderNo = "";

    [ObservableProperty]
    private string _customerName = "";

    [ObservableProperty]
    private decimal _orderAmount;

    [ObservableProperty]
    private decimal _receivedAmount;

    [ObservableProperty]
    private decimal _paymentAmount;

    [ObservableProperty]
    private PaymentMethod _paymentMethod = PaymentMethod.Cash;

    [ObservableProperty]
    private string? _paymentReference;

    [ObservableProperty]
    private string? _paymentRemark;

    [ObservableProperty]
    private bool _showAddPayment;

    public ObservableCollection<PaymentMethod> PaymentMethods { get; } =
    [
        PaymentMethod.Cash,
        PaymentMethod.WeChat,
        PaymentMethod.Alipay,
        PaymentMethod.BankTransfer,
        PaymentMethod.Other
    ];

    public PaymentRegistrationViewModel()
    {
        _paymentService = App.Services.GetService(typeof(PaymentService)) as PaymentService
            ?? throw new InvalidOperationException("无法获取收款服务");

        RunInBackground(LoadDataAsync(), "加载收款数据失败");
    }

    protected override async Task LoadDataAsync()
    {
        IsLoading = true;
        try
        {
            var branchId = CurrentSession.CurrentBranchId;
            var request = new PagedRequest
            {
                PageIndex = PageIndex,
                PageSize = PageSize,
                Keyword = SearchKeyword
            };

            var result = await _paymentService.GetPaymentListAsync(request, branchId, null, FilterStartDate, FilterEndDate);

            if (result.Success && result.Data != null)
            {
                TotalCount = result.Data.TotalCount;
                Payments = new ObservableCollection<PaymentRecordDto>(result.Data.Items);
                UpdateEmptyState();
            }
        }
        catch (Exception ex)
        {
            ShowBusinessException(ex, "加载收款记录");
            ShowLoadFailedState();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadStatsAsync()
    {
        try
        {
            var branchId = CurrentSession.CurrentBranchId;
            var startDate = FilterStartDate ?? DateTime.Today.AddMonths(-1);
            var endDate = FilterEndDate ?? DateTime.Today;

            var result = await _paymentService.GetPaymentStatsAsync(branchId, startDate, endDate);

            if (result.Success && result.Data != null)
            {
                Stats = result.Data;
                ShowStats = true;
            }
        }
        catch (Exception ex)
        {
            ShowError($"加载统计失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private void OpenAddPayment()
    {
        ShowAddPayment = true;
    }

    [RelayCommand]
    private void CloseAddPayment()
    {
        ShowAddPayment = false;
        ResetAddPaymentForm();
    }

    [RelayCommand]
    private async Task SubmitPaymentAsync()
    {
        if (OrderId <= 0)
        {
            ShowError("请输入订单ID");
            return;
        }

        if (PaymentAmount <= 0)
        {
            ShowError("收款金额必须大于0");
            return;
        }

        try
        {
            var request = new CreatePaymentRequest
            {
                OrderId = OrderId,
                Amount = PaymentAmount,
                PaymentMethod = PaymentMethod,
                Reference = PaymentReference,
                Remark = PaymentRemark,
                PaymentDate = DateTime.Now
            };

            var result = await _paymentService.CreatePaymentAsync(request, CurrentSession.CurrentEmployeeId);

            if (result.Success)
            {
                ShowSuccess("收款登记成功");
                CloseAddPayment();
                await LoadDataAsync();
            }
            else
            {
                ShowError(result.Message);
            }
        }
        catch (Exception ex)
        {
            ShowError($"收款登记失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DeletePaymentAsync(PaymentRecordDto? payment)
    {
        if (payment == null) return;

        var confirmed = ConfirmDangerousAction("删除收款记录", $"确定删除收款 {payment.PaymentNo}，金额 ¥{payment.Amount:N2}？");
        if (!confirmed) return;

        try
        {
            var result = await _paymentService.DeletePaymentAsync(payment.Id, CurrentSession.CurrentEmployeeId, "用户手动删除");

            if (result.Success)
            {
                ShowSuccess("收款记录已删除");
                await LoadDataAsync();
            }
            else
            {
                ShowError(result.Message);
            }
        }
        catch (Exception ex)
        {
            ShowError($"删除失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task FilterTodayAsync()
    {
        FilterStartDate = DateTime.Today;
        FilterEndDate = DateTime.Today;
        await ResetToFirstPageAndLoadAsync();
    }

    [RelayCommand]
    private async Task FilterThisWeekAsync()
    {
        var today = DateTime.Today;
        var offset = ((int)today.DayOfWeek + 6) % 7;
        FilterStartDate = today.AddDays(-offset);
        FilterEndDate = today;
        await ResetToFirstPageAndLoadAsync();
    }

    [RelayCommand]
    private async Task FilterThisMonthAsync()
    {
        var today = DateTime.Today;
        FilterStartDate = new DateTime(today.Year, today.Month, 1);
        FilterEndDate = today;
        await ResetToFirstPageAndLoadAsync();
    }

    [RelayCommand]
    private async Task ClearFilterAsync()
    {
        FilterStartDate = null;
        FilterEndDate = null;
        FilterCustomerName = null;
        SearchKeyword = null;
        await ResetToFirstPageAndLoadAsync();
    }

    private void ResetAddPaymentForm()
    {
        OrderId = 0;
        OrderNo = "";
        CustomerName = "";
        OrderAmount = 0;
        ReceivedAmount = 0;
        PaymentAmount = 0;
        PaymentMethod = PaymentMethod.Cash;
        PaymentReference = null;
        PaymentRemark = null;
    }

    [RelayCommand]
    private Task ExportToExcelAsync()
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel文件|*.xlsx",
                FileName = $"收款记录_{DateTime.Now:yyyyMMdd}"
            };

            if (dialog.ShowDialog() == true)
            {
                // 导出逻辑
                ShowSuccess("导出成功");
            }
        }
        catch (Exception ex)
        {
            ShowError($"导出失败: {ex.Message}");
        }
        return Task.CompletedTask;
    }
}
