using Microsoft.EntityFrameworkCore;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;

namespace PRO.Desktop.Prediction;

/// <summary>
/// 多层级降级预测引擎 — 纯统计，零外部依赖
/// 6层数据降级采集 + 加权融合，覆盖订单/客户/营业额/产品全面预测
/// </summary>
public class PredictionEngine
{
    private readonly ProDbContext _db;

    public PredictionEngine(ProDbContext dbContext)
    {
        _db = dbContext;
    }

    // ================================================================
    // 1. 预测概览
    // ================================================================

    public async Task<PredictionOverview> GetOverviewAsync(int branchId)
    {
        var overview = new PredictionOverview();
        var now = DateTime.Now;

        // ── 批量预加载：一次性查出所有客户及其订单，消除 N+1 ──
        var customerIds = await _db.Customers
            .Where(c => c.BranchId == branchId && c.Status == CustomerStatus.Active)
            .Select(c => c.Id)
            .ToListAsync();

        var customers = await _db.Customers
            .Where(c => customerIds.Contains(c.Id))
            .AsNoTracking()
            .ToListAsync();

        var allOrders = await _db.Orders
            .Where(o => customerIds.Contains(o.CustomerId)
                && (o.Status == OrderStatus.Completed || o.Status == OrderStatus.Pending))
            .OrderByDescending(o => o.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        var orderItemIds = allOrders.Select(o => o.Id).ToList();
        var allOrderItems = await _db.OrderItems
            .Where(i => orderItemIds.Contains(i.OrderId))
            .Include(i => i.Product)
            .AsNoTracking()
            .ToListAsync();

        // 未来3天即将下单的客户（批量预测）
        var upcoming = new List<CustomerOrderPrediction>();
        foreach (var c in customers)
        {
            var pred = PredictCustomerOrderBatch(c, allOrders, allOrderItems);
            if (pred.PredictedNextOrderDate.HasValue)
            {
                var daysAway = (pred.PredictedNextOrderDate.Value.Date - now.Date).Days;
                if (daysAway <= 3 && daysAway >= 0)
                    upcoming.Add(pred);
            }
        }
        overview.UpcomingOrders = upcoming.OrderBy(x => x.PredictedNextOrderDate).Take(10).ToList();

        // 营业额预测
        var revenue = await PredictBranchRevenueAsync(branchId, "月");
        if (revenue != null)
        {
            overview.MonthPredictedOrders = (int)revenue.PredictedOrderCount;
            overview.MonthPredictedRevenue = revenue.PredictedRevenue;
            overview.MonthlyNewCustomers = (await PredictNewCustomersAsync(branchId)).PredictedCount;
        }

        // 流失预警（批量版本）
        var churns = GetChurnWarningsBatch(branchId, customers, allOrders);
        overview.ChurnWarnings = churns;
        overview.HighRiskChurnCount = churns.Count(c => c.RiskLevel == "高");
        overview.MediumRiskChurnCount = churns.Count(c => c.RiskLevel == "中");

        return overview;
    }

    // ================================================================
    // 2. 客户订单预测
    // ================================================================

    /// <summary>批量预测 — 基于预加载的全部订单数据，零 DB 查询</summary>
    private CustomerOrderPrediction PredictCustomerOrderBatch(
        Customer customer, List<Order> allOrders, List<OrderItem> allOrderItems)
    {
        var result = new CustomerOrderPrediction
        {
            CustomerId = customer.Id,
            CustomerName = customer.Name,
            DaysSinceLastOrder = 999
        };

        var orders = allOrders.Where(o => o.CustomerId == customer.Id).ToList();
        var lastOrder = orders.FirstOrDefault();
        if (lastOrder != null)
            result.DaysSinceLastOrder = (int)(DateTime.Now - lastOrder.CreatedAt).TotalDays;

        var orderIds = orders.Select(o => o.Id).ToList();
        var orderItems = allOrderItems.Where(i => orderIds.Contains(i.OrderId)).ToList();

        // 预测金额（同步版本，使用内存数据）
        var amountResult = PredictAmountBatch(orders, customer, orderItems);
        result.PredictedAmount = amountResult.Value;
        result.ConfidenceScore = amountResult.Confidence;
        result.Breakdown = amountResult.Breakdown;
        result.AmountTrend = amountResult.Trend;

        // 预测下次订货日期
        if (orders.Count >= 2)
        {
            var intervals = new List<double>();
            for (int i = 0; i < orders.Count - 1; i++)
            {
                var diff = (orders[i].CreatedAt - orders[i + 1].CreatedAt).TotalDays;
                if (diff > 0 && diff < 365) intervals.Add(diff);
            }
            if (intervals.Count > 0)
            {
                var avgInterval = WeightedAverage(intervals);
                result.PredictedNextOrderDate = lastOrder!.CreatedAt.AddDays(avgInterval);
                result.ConfidenceScore = Math.Min(100, Math.Max(15, intervals.Count * 5));
            }
        }
        else if (orders.Count == 1)
        {
            // 只有1笔订单→用固定30天兜底（批量场景不做商圈查询避免破坏批量优势）
            result.PredictedNextOrderDate = orders[0].CreatedAt.AddDays(30);
            result.ConfidenceScore = 25;
        }

        // 预测产品
        if (orderItems.Count > 0)
        {
            var productGroups = orderItems.GroupBy(i => i.ProductId);
            foreach (var group in productGroups)
            {
                var product = group.First().Product;
                var quantities = group.Select(i => (double)i.Quantity).ToList();
                var avgQty = WeightedAverage(quantities);
                result.Products.Add(new ProductPrediction
                {
                    ProductId = group.Key,
                    ProductName = product?.Name ?? "",
                    PredictedQuantity = Math.Round(avgQty, 0),
                    PredictedAmount = Math.Round(avgQty * (double)(product?.ReferencePrice ?? 0), 2),
                    Confidence = Math.Min(90, quantities.Count * 8)
                });
            }
        }

        return result;
    }

    /// <summary>批量金额预测 — 纯内存计算，不访问DB</summary>
    private (double Value, double Confidence, List<LayerContribution> Breakdown, TrendDirection Trend)
        PredictAmountBatch(List<Order> orders, Customer _customer, List<OrderItem> _orderItems)
    {
        var layers = new List<LayerContribution>();
        var now = DateTime.Now;

        // Layer 1: 去年同期自身
        var lastYearStart = now.AddYears(-1).AddDays(-15);
        var lastYearEnd = now.AddYears(-1).AddDays(15);
        var lyOrders = orders.Where(o => o.CreatedAt >= lastYearStart && o.CreatedAt < lastYearEnd).ToList();
        if (lyOrders.Count > 0)
        {
            var totals = lyOrders.Select(o => (double)o.TotalAmount).ToList();
            layers.Add(new LayerContribution { Layer = 1, Description = "去年同期自身", Value = totals.Average(), Weight = 0.35, RecordCount = totals.Count });
        }

        // Layer 2: 上季度自身
        if (layers.Count == 0)
        {
            var lqStart = now.AddMonths(-4).AddDays(-22);
            var lqEnd = now.AddMonths(-3).AddDays(22);
            var lqOrders = orders.Where(o => o.CreatedAt >= lqStart && o.CreatedAt < lqEnd).ToList();
            if (lqOrders.Count > 0)
            {
                var totals = lqOrders.Select(o => (double)o.TotalAmount).ToList();
                layers.Add(new LayerContribution { Layer = 2, Description = "上季度自身", Value = totals.Average(), Weight = 0.25, RecordCount = totals.Count });
            }
        }

        // Layer 3: 近3月自身
        if (layers.Count == 0)
        {
            var recent = orders.Where(o => o.CreatedAt >= now.AddMonths(-3)).ToList();
            if (recent.Count > 0)
            {
                var totals = recent.Select(o => (double)o.TotalAmount).ToList();
                layers.Add(new LayerContribution { Layer = 3, Description = "近3月自身", Value = WeightedAverage(totals), Weight = 0.20, RecordCount = totals.Count });
            }
        }

        if (layers.Count > 0)
        {
            var sumWeight = layers.Sum(l => l.Weight);
            var weightedValue = layers.Sum(l => l.Value * l.Weight) / sumWeight;
            var confidence = layers.Min(l => l.Layer) switch { 1 => 85, 2 => 70, 3 => 55, _ => 30 };
            var trend = PredictTrend(orders.Select(o => (double)o.TotalAmount).ToList());
            return (Math.Round(weightedValue, 2), confidence, layers, trend);
        }

        return (0, 10, layers, TrendDirection.Unknown);
    }

    public async Task<CustomerOrderPrediction> PredictCustomerOrderAsync(int customerId)
    {
        var customer = await _db.Customers.FindAsync(customerId);
        if (customer == null) return new CustomerOrderPrediction { CustomerId = customerId };

        var result = new CustomerOrderPrediction
        {
            CustomerId = customerId,
            CustomerName = customer.Name,
            DaysSinceLastOrder = 999
        };

        // 获取客户所有有效订单
        var orders = await _db.Orders
            .Where(o => o.CustomerId == customerId && o.Status == OrderStatus.Completed)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var lastOrder = orders.FirstOrDefault();
        if (lastOrder != null)
        {
            result.DaysSinceLastOrder = (int)(DateTime.Now - lastOrder.CreatedAt).TotalDays;
        }

        // 获取客户所有订单明细（用于产品预测）
        var orderIds = orders.Select(o => o.Id).ToList();
        var orderItems = await _db.OrderItems
            .Where(i => orderIds.Contains(i.OrderId))
            .Include(i => i.Product)
            .ToListAsync();

        // 预测下次订单金额
        var amountResult = await PredictAmountAsync(orders, customer, "金额");
        result.PredictedAmount = amountResult.Value;
        result.ConfidenceScore = amountResult.Confidence;
        result.Breakdown = amountResult.Breakdown;
        result.AmountTrend = amountResult.Trend;

        // 预测下次送货日期
        if (orders.Count >= 2)
        {
            var intervals = new List<double>();
            for (int i = 0; i < orders.Count - 1; i++)
            {
                var diff = (orders[i].CreatedAt - orders[i + 1].CreatedAt).TotalDays;
                if (diff > 0 && diff < 365) intervals.Add(diff);
            }
            if (intervals.Count > 0)
            {
                var avgInterval = WeightedAverage(intervals);
                result.PredictedNextOrderDate = lastOrder!.CreatedAt.AddDays(avgInterval);
                result.ConfidenceScore = Math.Min(100, Math.Max(15, intervals.Count * 5));
            }
        }
        else if (orders.Count == 1)
        {
            // 只有1笔订单 → 用商圈均值兜底
            var districtResult = await GetDistrictAverageAsync(customer.BusinessDistrictId, customer.BranchId, "间隔");
            if (districtResult > 0)
            {
                result.PredictedNextOrderDate = orders[0].CreatedAt.AddDays(districtResult);
                result.ConfidenceScore = 30;
            }
        }
        else
        {
            // 无订单 → 同类型客户兜底
            var typeAvg = await GetTypeAverageAsync(customer.CustomerType, customer.BranchId, "间隔");
            if (typeAvg > 0)
            {
                result.PredictedNextOrderDate = DateTime.Now.AddDays(typeAvg);
                result.ConfidenceScore = 20;
            }
        }

        // 预测产品
        if (orderItems.Count > 0)
        {
            var productGroups = orderItems.GroupBy(i => i.ProductId);
            foreach (var group in productGroups)
            {
                var product = group.First().Product;
                var quantities = group.Select(i => (double)i.Quantity).ToList();
                var avgQty = WeightedAverage(quantities);
                var productPred = new ProductPrediction
                {
                    ProductId = group.Key,
                    ProductName = product?.Name ?? "",
                    PredictedQuantity = Math.Round(avgQty, 0),
                    PredictedAmount = Math.Round(avgQty * (double)(product?.ReferencePrice ?? 0), 2),
                    Confidence = Math.Min(90, quantities.Count * 8)
                };
                result.Products.Add(productPred);
            }
        }

        return result;
    }

    // ================================================================
    // 3. 客户集合预测（批量） — 消除 N+1，单次往返
    // ================================================================

    public async Task<List<CustomerOrderPrediction>> PredictAllCustomersAsync(int branchId)
    {
        var customers = await _db.Customers
            .Where(c => c.BranchId == branchId && c.Status == CustomerStatus.Active)
            .AsNoTracking()
            .ToListAsync();

        if (customers.Count == 0) return [];

        var customerIds = customers.Select(c => c.Id).ToList();
        var allOrders = await _db.Orders
            .Where(o => customerIds.Contains(o.CustomerId) && o.Status == OrderStatus.Completed)
            .OrderByDescending(o => o.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        var orderIds = allOrders.Select(o => o.Id).ToList();
        var allOrderItems = await _db.OrderItems
            .Where(i => orderIds.Contains(i.OrderId))
            .Include(i => i.Product)
            .AsNoTracking()
            .ToListAsync();

        var results = new List<CustomerOrderPrediction>();
        foreach (var c in customers)
        {
            results.Add(PredictCustomerOrderBatch(c, allOrders, allOrderItems));
        }
        return results;
    }

    // ================================================================
    // 4. 营业额预测
    // ================================================================

    public async Task<RevenuePrediction?> PredictBranchRevenueAsync(int branchId, string period = "月")
    {
        var branch = await _db.Branches.FindAsync(branchId);
        if (branch == null) return null;

        var now = DateTime.Now;
        var result = new RevenuePrediction
        {
            BranchId = branchId,
            BranchName = branch.Name,
            PeriodStart = now,
            PeriodEnd = period switch
            {
                "日" => now.Date.AddDays(1),
                "周" => now.Date.AddDays(7),
                _ => now.Date.AddMonths(1)
            }
        };

        // 层级1: 去年同期
        var lastYearStart = now.AddYears(-1).Date;
        var lastYearEnd = lastYearStart.AddMonths(1);
        var lastYearOrders = await _db.Orders
            .Where(o => o.BranchId == branchId && o.CreatedAt >= lastYearStart && o.CreatedAt < lastYearEnd
                && o.Status != OrderStatus.Cancelled)
            .AsNoTracking()
            .ToListAsync();
        double lastYearRevenue = lastYearOrders.Sum(o => (double)o.TotalAmount);
        double lastYearCount = lastYearOrders.Count;

        // 层级2: 上季度
        var lastQuarterStart = now.AddMonths(-3).Date;
        var lastQuarterOrders = await _db.Orders
            .Where(o => o.BranchId == branchId && o.CreatedAt >= lastQuarterStart && o.CreatedAt < now.Date
                && o.Status != OrderStatus.Cancelled)
            .AsNoTracking()
            .ToListAsync();
        double quarterCount = lastQuarterOrders.Count / 3.0;
        double quarterRevenue = lastQuarterOrders.Sum(o => (double)o.TotalAmount) / 3.0;

        // 层级3: 上月趋势
        var lastMonthStart = now.AddMonths(-1).Date;
        var lastMonthOrders = await _db.Orders
            .Where(o => o.BranchId == branchId && o.CreatedAt >= lastMonthStart && o.CreatedAt < now.Date
                && o.Status != OrderStatus.Cancelled)
            .AsNoTracking()
            .ToListAsync();
        double monthCount = lastMonthOrders.Count;
        double monthRevenue = lastMonthOrders.Sum(o => (double)o.TotalAmount);

        // 融合
        double revenue, orders, confidence;
        if (lastYearOrders.Count >= 10)
        {
            revenue = lastYearRevenue * 0.35 + quarterRevenue * 0.25 + monthRevenue * 0.20;
            orders = lastYearCount * 0.35 + quarterCount * 0.25 + monthCount * 0.20;
            confidence = 75;
        }
        else if (lastQuarterOrders.Count >= 5)
        {
            revenue = quarterRevenue * 0.45 + monthRevenue * 0.30;
            orders = quarterCount * 0.45 + monthCount * 0.30;
            confidence = 55;
        }
        else
        {
            revenue = monthRevenue * 0.50;
            orders = monthCount * 0.50;
            confidence = 35;
        }

        result.PredictedRevenue = Math.Round(revenue, 2);
        result.PredictedOrderCount = Math.Round(orders, 0);
        result.Confidence = confidence;
        result.PredictedReceivable = Math.Round(revenue * 0.85, 2); // 预估85%回款率

        // 同比/环比
        if (lastYearRevenue > 0 && monthRevenue > 0)
        {
            result.YoYChange = Math.Round((revenue - lastYearRevenue) / lastYearRevenue * 100, 1);
            result.MoMChange = Math.Round((revenue - monthRevenue) / monthRevenue * 100, 1);
        }

        return result;
    }

    // ================================================================
    // 5. 新客预测
    // ================================================================

    public async Task<NewCustomerPrediction> PredictNewCustomersAsync(int branchId)
    {
        var branch = await _db.Branches.FindAsync(branchId);
        var result = new NewCustomerPrediction
        {
            BranchId = branchId,
            BranchName = branch?.Name ?? ""
        };

        // 历史12个月新增客户
        var twelveMonthsAgo = DateTime.Now.AddMonths(-12);
        var monthlyNew = await _db.Customers
            .Where(c => c.BranchId == branchId && c.CreatedAt >= twelveMonthsAgo)
            .GroupBy(c => c.CreatedAt.Month)
            .Select(g => new { Month = g.Key, Count = g.Count() })
            .AsNoTracking()
            .ToListAsync();

        if (monthlyNew.Count >= 3)
        {
            var avg = monthlyNew.Average(m => (double)m.Count);
            var weighted = WeightedMonthly(monthlyNew.Select(m => (double)m.Count).ToList());
            result.PredictedCount = Math.Round(weighted, 0);
            result.Confidence = Math.Min(80, monthlyNew.Count * 8);
        }
        else
        {
            result.PredictedCount = 2; // 行业兜底
            result.Confidence = 15;
        }

        return result;
    }

    // ================================================================
    // 6. 产品销量预测
    // ================================================================

    public async Task<ProductDemandPrediction> PredictProductDemandAsync(int productId)
    {
        var product = await _db.Products.FindAsync(productId);
        if (product == null) return new ProductDemandPrediction();

        var threeMonthsAgo = DateTime.Now.AddMonths(-3);
        var items = await _db.OrderItems
            .Where(i => i.ProductId == productId && i.Order!.CreatedAt >= threeMonthsAgo
                && i.Order!.Status != OrderStatus.Cancelled)
            .Include(i => i.Order)
            .AsNoTracking()
            .ToListAsync();

        return BuildProductPrediction(product, items);
    }

    /// <summary>
    /// 批量产品销量预测 — 一次查询所有产品的近3月订单项，消除 N+1 查询
    /// </summary>
    public async Task<List<ProductDemandPrediction>> PredictProductDemandBatchAsync(List<Product> products)
    {
        if (products.Count == 0) return [];

        var threeMonthsAgo = DateTime.Now.AddMonths(-3);
        var productIds = products.Select(p => p.Id).ToList();

        // 一次性加载所有目标产品的近3月订单项
        var allItems = await _db.OrderItems
            .Where(i => productIds.Contains(i.ProductId)
                && i.Order!.CreatedAt >= threeMonthsAgo
                && i.Order!.Status != OrderStatus.Cancelled)
            .AsNoTracking()
            .ToListAsync();

        var itemsByProduct = allItems.GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var results = new List<ProductDemandPrediction>();
        foreach (var product in products)
        {
            var items = itemsByProduct.TryGetValue(product.Id, out var list) ? list : [];
            results.Add(BuildProductPrediction(product, items));
        }

        return results;
    }

    /// <summary>构建单产品预测结果（纯计算，无 DB 访问）</summary>
    private static ProductDemandPrediction BuildProductPrediction(Product product, List<OrderItem> items)
    {
        var result = new ProductDemandPrediction
        {
            ProductId = product.Id,
            ProductName = product.Name,
            Specification = product.Specification
        };

        if (items.Count >= 3)
        {
            var quantities = items.Select(i => (double)i.Quantity).ToList();
            var avg = WeightedAverage(quantities);
            result.PredictedSales = Math.Round(avg, 0);
            result.SuggestedStock = Math.Round(avg * 1.3, 0);
            result.Confidence = Math.Min(85, items.Count * 5);
            result.Trend = PredictTrend(quantities);
        }
        else if (items.Count > 0)
        {
            result.PredictedSales = Math.Round(items.Average(i => (double)i.Quantity), 0);
            result.SuggestedStock = Math.Round(result.PredictedSales * 1.3, 0);
            result.Confidence = 25;
        }
        else
        {
            result.PredictedSales = 0;
            result.SuggestedStock = 10;
            result.Confidence = 10;
        }

        return result;
    }

    // ================================================================
    // 7. 客户流失预警（批量版本 — 消除 N+1）
    // ================================================================

    public async Task<List<ChurnWarning>> GetChurnWarningsAsync(int branchId)
    {
        var customers = await _db.Customers
            .Where(c => c.BranchId == branchId && c.Status == CustomerStatus.Active)
            .AsNoTracking()
            .ToListAsync();

        if (customers.Count == 0) return [];

        var customerIds = customers.Select(c => c.Id).ToList();
        var allOrders = await _db.Orders
            .Where(o => customerIds.Contains(o.CustomerId) && o.Status == OrderStatus.Completed)
            .OrderByDescending(o => o.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        return GetChurnWarningsBatch(branchId, customers, allOrders);
    }

    /// <summary>批量流失预警 — 基于已加载数据，无额外DB查询</summary>
    private List<ChurnWarning> GetChurnWarningsBatch(int _branchId, List<Customer> customers, List<Order> allOrders)
    {
        var warnings = new List<ChurnWarning>();

        foreach (var c in customers)
        {
            var orders = allOrders.Where(o => o.CustomerId == c.Id).ToList();
            if (orders.Count < 1) continue;

            var lastOrder = orders.First();
            var daysSince = (int)(DateTime.Now - lastOrder.CreatedAt).TotalDays;

            var intervals = new List<double>();
            for (int i = 0; i < orders.Count - 1; i++)
            {
                var diff = (orders[i].CreatedAt - orders[i + 1].CreatedAt).TotalDays;
                if (diff > 0) intervals.Add(diff);
            }
            var avgInterval = intervals.Count > 0 ? intervals.Average() : 30;

            var warning = new ChurnWarning
            {
                CustomerId = c.Id,
                CustomerName = c.Name,
                LastOrderDate = lastOrder.CreatedAt,
                DaysSinceLastOrder = daysSince,
                AverageInterval = Math.Round(avgInterval, 1),
                RiskLevel = "低"
            };

            if (orders.Count >= 6)
            {
                warning.LastThreeOrdersTotal = (double)orders.Take(3).Sum(o => o.TotalAmount);
                warning.PreviousThreeOrdersTotal = (double)orders.Skip(3).Take(3).Sum(o => o.TotalAmount);
                if (warning.PreviousThreeOrdersTotal > 0)
                {
                    var ratio = warning.LastThreeOrdersTotal.Value / warning.PreviousThreeOrdersTotal.Value;
                    if (ratio < 0.5) warning.RiskLevel = "高";
                    else if (ratio < 0.8) warning.RiskLevel = "中";
                }
            }

            if (daysSince > avgInterval * 2 && daysSince > 14)
                warning.RiskLevel = warning.RiskLevel == "低" ? "中" : "高";
            else if (daysSince > avgInterval * 1.5 && daysSince > 7)
                warning.RiskLevel = warning.RiskLevel == "低" ? "低" : warning.RiskLevel;

            if (warning.RiskLevel != "低")
            {
                warning.SuggestedAction = warning.RiskLevel switch
                {
                    "高" => "建议业务员立即电话联系客户",
                    "中" => "建议发送促销信息或安排回访",
                    _ => null
                };
                warnings.Add(warning);
            }
        }

        return warnings.OrderByDescending(w => w.RiskLevel)
            .ThenByDescending(w => w.DaysSinceLastOrder).Take(20).ToList();
    }

    // ================================================================
    // 内部辅助方法
    // ================================================================

    /// <summary>6层数据降级采集 + 加权融合</summary>
    private async Task<(double Value, double Confidence, List<LayerContribution> Breakdown, TrendDirection Trend)>
        PredictAmountAsync(List<Order> orders, Customer customer, string target)
    {
        var layers = new List<LayerContribution>();
        var now = DateTime.Now;

        // Layer 1: 去年同期自身
        var lastYearStart = now.AddYears(-1).AddDays(-15);
        var lastYearEnd = now.AddYears(-1).AddDays(15);
        var lyOrders = orders.Where(o => o.CreatedAt >= lastYearStart && o.CreatedAt < lastYearEnd).ToList();
        if (lyOrders.Count > 0)
        {
            var totals = lyOrders.Select(o => (double)o.TotalAmount).ToList();
            layers.Add(new LayerContribution { Layer = 1, Description = "去年同期自身", Value = totals.Average(), Weight = 0.35, RecordCount = totals.Count });
        }

        // Layer 2: 上季度自身
        if (!layers.Any(l => l.Layer == 1))
        {
            var lqStart = now.AddMonths(-4).AddDays(-22);
            var lqEnd = now.AddMonths(-3).AddDays(22);
            var lqOrders = orders.Where(o => o.CreatedAt >= lqStart && o.CreatedAt < lqEnd).ToList();
            if (lqOrders.Count > 0)
            {
                var totals = lqOrders.Select(o => (double)o.TotalAmount).ToList();
                layers.Add(new LayerContribution { Layer = 2, Description = "上季度自身", Value = totals.Average(), Weight = 0.25, RecordCount = totals.Count });
            }
        }

        // Layer 3: 近3月自身
        if (!layers.Any(l => l.Layer <= 2))
        {
            var recent = orders.Where(o => o.CreatedAt >= now.AddMonths(-3)).ToList();
            if (recent.Count > 0)
            {
                var totals = recent.Select(o => (double)o.TotalAmount).ToList();
                layers.Add(new LayerContribution { Layer = 3, Description = "近3月自身", Value = WeightedAverage(totals), Weight = 0.20, RecordCount = totals.Count });
            }
        }

        // Layer 4: 同商圈
        if (customer.BusinessDistrictId.HasValue)
        {
            var districtAvg = await GetDistrictAverageAsync(customer.BusinessDistrictId.Value, customer.BranchId, target);
            if (districtAvg > 0)
            {
                layers.Add(new LayerContribution { Layer = 4, Description = "同商圈", Value = districtAvg, Weight = 0.12, RecordCount = 0 });
            }
        }

        // Layer 5: 同类型
        var typeAvg = await GetTypeAverageAsync(customer.CustomerType, customer.BranchId, target);
        if (typeAvg > 0 && !layers.Any(l => l.Layer == 4))
        {
            layers.Add(new LayerContribution { Layer = 5, Description = "同类型", Value = typeAvg, Weight = 0.06, RecordCount = 0 });
        }

        // 如果有3+层数据，重新均分权重
        if (layers.Count > 0)
        {
            var totalWeight = layers.Sum(l => l.Weight);
            if (totalWeight < 0.5)
            {
                // 数据不足，扩展兜底
                var branchAvg = await _db.Orders
                    .Where(o => o.BranchId == customer.BranchId && o.Status == OrderStatus.Completed
                        && o.CreatedAt >= now.AddMonths(-3))
                    .AverageAsync(o => (double?)o.TotalAmount) ?? 0;
                if (branchAvg > 0)
                {
                    layers.Add(new LayerContribution { Layer = 6, Description = "分公司均值", Value = branchAvg, Weight = 2.0 - totalWeight, RecordCount = 0 });
                }
            }

            var sumWeight = layers.Sum(l => l.Weight);
            var weightedValue = layers.Sum(l => l.Value * l.Weight) / sumWeight;

            // 置信度
            var maxLayer = layers.Min(l => l.Layer);
            var confidence = maxLayer switch
            {
                1 => 85,
                2 => 70,
                3 => 55,
                4 => 40,
                5 => 30,
                _ => 20
            };

            // 趋势
            var trend = PredictTrend(orders.Select(o => (double)o.TotalAmount).ToList());

            return (Math.Round(weightedValue, 2), confidence, layers, trend);
        }

        return (0, 10, layers, TrendDirection.Unknown);
    }

    /// <summary>同商圈均值</summary>
    private async Task<double> GetDistrictAverageAsync(int? districtId, int branchId, string target)
    {
        if (!districtId.HasValue) return 0;
        var threeMonthsAgo = DateTime.Now.AddMonths(-3);

        var orders = await _db.Orders
            .Where(o => o.BranchId == branchId && o.Status == OrderStatus.Completed
                && o.CreatedAt >= threeMonthsAgo)
            .Include(o => o.Customer)
            .Where(o => o.Customer!.BusinessDistrictId == districtId)
            .ToListAsync();

        if (orders.Count == 0) return 0;

        return target switch
        {
            "间隔" => orders.Select(o => (double)(DateTime.Now - o.CreatedAt).TotalDays).DefaultIfEmpty(0).Average(),
            _ => orders.Average(o => (double)o.TotalAmount)
        };
    }

    /// <summary>同类型客户均值</summary>
    private async Task<double> GetTypeAverageAsync(CustomerType type, int branchId, string target)
    {
        var threeMonthsAgo = DateTime.Now.AddMonths(-3);
        var orders = await _db.Orders
            .Where(o => o.BranchId == branchId && o.Status == OrderStatus.Completed
                && o.CreatedAt >= threeMonthsAgo)
            .Include(o => o.Customer)
            .Where(o => o.Customer!.CustomerType == type)
            .ToListAsync();

        if (orders.Count == 0) return 0;

        return target switch
        {
            "间隔" => orders.Select(o => (double)(DateTime.Now - o.CreatedAt).TotalDays).DefaultIfEmpty(0).Average(),
            _ => orders.Average(o => (double)o.TotalAmount)
        };
    }

    /// <summary>加权平均（越近权重越高）</summary>
    private static double WeightedAverage(List<double> values)
    {
        if (values.Count == 0) return 0;
        if (values.Count == 1) return values[0];

        double sum = 0, weightSum = 0;
        for (int i = 0; i < values.Count; i++)
        {
            double w = values.Count - i; // 最新的权重最高
            sum += values[i] * w;
            weightSum += w;
        }
        return sum / weightSum;
    }

    /// <summary>月度加权平均</summary>
    private static double WeightedMonthly(List<double> monthlyValues)
    {
        if (monthlyValues.Count == 0) return 0;
        double sum = 0, weightSum = 0;
        for (int i = 0; i < monthlyValues.Count; i++)
        {
            double w = 1.0 + (i * 0.15); // 最近月份权重递增
            sum += monthlyValues[i] * w;
            weightSum += w;
        }
        return sum / weightSum;
    }

    /// <summary>判断趋势</summary>
    private static TrendDirection PredictTrend(List<double> values)
    {
        if (values.Count < 3) return TrendDirection.Unknown;

        var firstHalf = values.Take(values.Count / 2).DefaultIfEmpty().Average();
        var secondHalf = values.Skip(values.Count / 2).DefaultIfEmpty().Average();
        var diff = secondHalf - firstHalf;
        var threshold = firstHalf * 0.1;

        if (Math.Abs(diff) < threshold) return TrendDirection.Stable;
        return diff > 0 ? TrendDirection.Rising : TrendDirection.Declining;
    }
}
