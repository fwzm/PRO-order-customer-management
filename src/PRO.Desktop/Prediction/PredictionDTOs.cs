namespace PRO.Desktop.Prediction;

/// <summary>
/// 预测结果
/// </summary>
public class PredictionResult
{
    public string TargetName { get; set; } = string.Empty;  // 预测对象名称（客户名/产品名等）
    public double Value { get; set; }                        // 预测值
    public double Confidence { get; set; }                   // 置信度 0-100
    public string Engine { get; set; } = "统计";             // 使用的引擎
    public DateTime? PredictedDate { get; set; }             // 预测日期
    public List<LayerContribution> Breakdown { get; set; } = [];
    public TrendDirection Trend { get; set; } = TrendDirection.Stable;
}

public class LayerContribution
{
    public int Layer { get; set; }
    public string Description { get; set; } = string.Empty;
    public double Value { get; set; }
    public double Weight { get; set; }
    public int RecordCount { get; set; }
}

public enum TrendDirection
{
    Rising, Stable, Declining, Unknown
}

/// <summary>
/// 客户订单预测结果
/// </summary>
public class CustomerOrderPrediction
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public DateTime? PredictedNextOrderDate { get; set; }
    public double PredictedAmount { get; set; }
    public double ConfidenceScore { get; set; }
    public TrendDirection AmountTrend { get; set; }
    public int DaysSinceLastOrder { get; set; }
    public List<ProductPrediction> Products { get; set; } = [];
    public List<LayerContribution> Breakdown { get; set; } = [];
}

public class ProductPrediction
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public double PredictedQuantity { get; set; }
    public double PredictedAmount { get; set; }
    public double Confidence { get; set; }
}

/// <summary>
/// 营业额预测
/// </summary>
public class RevenuePrediction
{
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public double PredictedRevenue { get; set; }
    public double PredictedOrderCount { get; set; }
    public double PredictedReceivable { get; set; }
    public double Confidence { get; set; }
    public double YoYChange { get; set; }  // 同比变化(%)
    public double MoMChange { get; set; }  // 环比变化(%)
}

/// <summary>
/// 新客预测
/// </summary>
public class NewCustomerPrediction
{
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public double PredictedCount { get; set; }
    public double MajorCustomerCount { get; set; }
    public double SubCustomerCount { get; set; }
    public double Confidence { get; set; }
    public string[] PredictedDistricts { get; set; } = Array.Empty<string>();
}

/// <summary>
/// 产品销量预测
/// </summary>
public class ProductDemandPrediction
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Specification { get; set; }
    public double PredictedSales { get; set; }
    public double SuggestedStock { get; set; }   // 建议备货量 = 预测×1.3
    public double Confidence { get; set; }
    public TrendDirection Trend { get; set; }
}

/// <summary>
/// 客户流失预警
/// </summary>
public class ChurnWarning
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = "低";  // 高/中/低
    public DateTime? LastOrderDate { get; set; }
    public int DaysSinceLastOrder { get; set; }
    public double AverageInterval { get; set; }
    public double? LastThreeOrdersTotal { get; set; }
    public double? PreviousThreeOrdersTotal { get; set; }
    public string? SuggestedAction { get; set; }
}

/// <summary>
/// 预测概览
/// </summary>
public class PredictionOverview
{
    public int TodayPredictedOrders { get; set; }
    public double TodayPredictedRevenue { get; set; }
    public int WeekPredictedOrders { get; set; }
    public double WeekPredictedRevenue { get; set; }
    public int MonthPredictedOrders { get; set; }
    public double MonthPredictedRevenue { get; set; }
    public double MonthlyNewCustomers { get; set; }
    public int HighRiskChurnCount { get; set; }
    public int MediumRiskChurnCount { get; set; }
    public List<CustomerOrderPrediction> UpcomingOrders { get; set; } = [];
    public List<ChurnWarning> ChurnWarnings { get; set; } = [];
    public List<RevenuePrediction> BranchRevenuePredictions { get; set; } = [];
}
