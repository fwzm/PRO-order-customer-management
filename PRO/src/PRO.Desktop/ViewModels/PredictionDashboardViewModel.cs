using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using PRO.Desktop.Prediction;
using PRO.Infrastructure.Persistence;
using System.Collections.ObjectModel;

namespace PRO.Desktop.ViewModels;

/// <summary>
/// AI 预测仪表盘 ViewModel
/// </summary>
public partial class PredictionDashboardViewModel : ViewModelBase
{
    private readonly ProDbContext _dbContext;
    private readonly PredictionEngine _engine;

    public PredictionDashboardViewModel()
    {
        _dbContext = App.Services.GetService(typeof(ProDbContext)) as ProDbContext
            ?? throw new InvalidOperationException("无法获取数据库上下文");
        _engine = new PredictionEngine(_dbContext);

        RunInBackground(LoadDataAsync(), "智能预测加载失败");
    }

    [ObservableProperty]
    private PredictionOverview? _overview;

    [ObservableProperty]
    private CustomerOrderPrediction? _selectedCustomerPrediction;

    [ObservableProperty]
    private ProductDemandPrediction? _selectedProductPrediction;

    [ObservableProperty]
    private ObservableCollection<CustomerOrderPrediction> _customerPredictions = [];

    [ObservableProperty]
    private ObservableCollection<ProductDemandPrediction> _productPredictions = [];

    [ObservableProperty]
    private ObservableCollection<ChurnWarning> _churnWarnings = [];

    [ObservableProperty]
    private ObservableCollection<RevenuePrediction> _branchRevenuePredictions = [];

    [ObservableProperty]
    private string _lastRefreshTime = "未刷新";

    [ObservableProperty]
    private string _predictionDetail = "选择左侧预测结果查看详情";

    [ObservableProperty]
    private string _newCustomerPredictionText = "计算中...";

    // 概览数字
    [ObservableProperty] private string _todayOrdersText = "—";
    [ObservableProperty] private string _todayRevenueText = "—";
    [ObservableProperty] private string _weekOrdersText = "—";
    [ObservableProperty] private string _weekRevenueText = "—";
    [ObservableProperty] private string _monthOrdersText = "—";
    [ObservableProperty] private string _monthRevenueText = "—";
    [ObservableProperty] private string _newCustomerText = "—";
    [ObservableProperty] private string _churnHighText = "—";
    [ObservableProperty] private string _churnMediumText = "—";

    private async Task LoadDataAsync()
    {
        IsLoading = true;
        try
        {
            var branchId = CurrentSession.CurrentBranchId;
            var overview = await _engine.GetOverviewAsync(branchId);
            Overview = overview;

            // 预测量
            TodayOrdersText = overview.WeekPredictedOrders > 0
                ? $"{overview.WeekPredictedOrders / 7}" : "—";
            TodayRevenueText = overview.TodayPredictedRevenue > 0
                ? $"¥{overview.TodayPredictedRevenue:N0}" : "—";
            WeekOrdersText = overview.WeekPredictedOrders > 0
                ? $"{overview.WeekPredictedOrders}" : "—";
            WeekRevenueText = overview.WeekPredictedRevenue > 0
                ? $"¥{overview.WeekPredictedRevenue:N0}" : "—";
            MonthOrdersText = overview.MonthPredictedOrders > 0
                ? $"{overview.MonthPredictedOrders}" : "—";
            MonthRevenueText = overview.MonthPredictedRevenue > 0
                ? $"¥{overview.MonthPredictedRevenue:N0}" : "—";
            NewCustomerText = overview.MonthlyNewCustomers > 0
                ? $"{overview.MonthlyNewCustomers:N0}" : "—";
            ChurnHighText = $"{overview.HighRiskChurnCount}";
            ChurnMediumText = $"{overview.MediumRiskChurnCount}";

            // 即将送货列表
            CustomerPredictions = new ObservableCollection<CustomerOrderPrediction>(overview.UpcomingOrders);
            ChurnWarnings = new ObservableCollection<ChurnWarning>(overview.ChurnWarnings);
            BranchRevenuePredictions = new ObservableCollection<RevenuePrediction>(overview.BranchRevenuePredictions);

            // 产品销量预测（批量 — 消除 N+1）
            var products = await _dbContext.Products
                .AsNoTracking()
                .Where(p => p.Status == Domain.Enums.ProductStatus.Active)
                .OrderBy(p => p.Name)
                .Take(20)
                .ToListAsync();

            var productPreds = await _engine.PredictProductDemandBatchAsync(products);
            ProductPredictions = new ObservableCollection<ProductDemandPrediction>(
                productPreds.OrderByDescending(p => p.PredictedSales));

            // 新客预测
            var newCust = await _engine.PredictNewCustomersAsync(branchId);
            NewCustomerPredictionText = newCust.Confidence >= 50
                ? $"预计下月新增 {newCust.PredictedCount:N0} 家客户"
                : $"数据不足，预估 {newCust.PredictedCount:N0} 家（以淡蓝色字体显示）";

            LastRefreshTime = DateTime.Now.ToString("HH:mm:ss");
        }
        catch (Exception ex)
        {
            ShowError($"预测加载失败: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadDataAsync();
        ShowSuccess("预测已刷新");
    }

    [RelayCommand]
    private void SelectCustomerPrediction(CustomerOrderPrediction? pred)
    {
        if (pred == null) return;
        SelectedCustomerPrediction = pred;

        var detail = $"客户：{pred.CustomerName}\n";
        if (pred.PredictedNextOrderDate.HasValue)
            detail += $"预测下次订货：{pred.PredictedNextOrderDate:yyyy-MM-dd}\n";
        detail += $"预测金额：¥{pred.PredictedAmount:N2}\n";
        detail += $"置信度：{pred.ConfidenceScore:N0}%\n";
        detail += $"距上次下单：{pred.DaysSinceLastOrder} 天\n";
        detail += $"趋势：{pred.AmountTrend}\n\n";

        if (pred.Products.Any())
        {
            detail += "预测产品清单：\n";
            foreach (var p in pred.Products)
            {
                detail += $"  {p.ProductName} × {p.PredictedQuantity:N0} = ¥{p.PredictedAmount:N0}（置信度{p.Confidence:N0}%）\n";
            }
        }

        if (pred.Breakdown.Any())
        {
            detail += "\n数据来源：\n";
            foreach (var layer in pred.Breakdown)
            {
                detail += $"  L{layer.Layer} {layer.Description}：{layer.Value:N2} × 权重{layer.Weight:P0}";
                if (layer.RecordCount > 0) detail += $"（{layer.RecordCount}条样本）";
                detail += "\n";
            }
        }

        PredictionDetail = detail;
    }

    [RelayCommand]
    private void SelectProductPrediction(ProductDemandPrediction? pred)
    {
        if (pred == null) return;
        SelectedProductPrediction = pred;

        var detail = $"产品：{pred.ProductName}\n";
        if (!string.IsNullOrEmpty(pred.Specification))
            detail += $"规格：{pred.Specification}\n";
        detail += $"预测销量：{pred.PredictedSales:N0}\n";
        detail += $"建议备货量：{pred.SuggestedStock:N0}\n";
        detail += $"置信度：{pred.Confidence:N0}%\n";
        detail += $"趋势：{pred.Trend}\n";

        PredictionDetail = detail;
    }

    [RelayCommand]
    private void SelectChurnWarning(ChurnWarning? warning)
    {
        if (warning == null) return;
        var detail = $"客户：{warning.CustomerName}\n";
        detail += $"风险等级：{warning.RiskLevel}\n";
        if (warning.LastOrderDate.HasValue)
            detail += $"最后下单：{warning.LastOrderDate:yyyy-MM-dd}（{warning.DaysSinceLastOrder}天前）\n";
        detail += $"平均间隔：{warning.AverageInterval:N1} 天\n";

        if (warning.LastThreeOrdersTotal.HasValue && warning.PreviousThreeOrdersTotal.HasValue)
        {
            detail += $"近3笔总额：¥{warning.LastThreeOrdersTotal:N0}\n";
            detail += $"前3笔总额：¥{warning.PreviousThreeOrdersTotal:N0}\n";
        }

        if (!string.IsNullOrEmpty(warning.SuggestedAction))
            detail += $"\n建议：{warning.SuggestedAction}";

        PredictionDetail = detail;
    }
}
