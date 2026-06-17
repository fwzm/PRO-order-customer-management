using Microsoft.EntityFrameworkCore;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 数据质量检测服务
/// </summary>
public class DataQualityService : IDataQualityService
{
    private readonly ProDbContext _dbContext;

    public DataQualityService(ProDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// 获取数据质量汇总报告（P3-P4 API 端点）
    /// </summary>
    /// <param name="branchId">分公司ID，null表示全部</param>
    /// <returns>数据质量汇总报告</returns>
    public async Task<ApiResponse<DataQualitySummaryReportDto>> GetDataQualityReportAsync(int? branchId)
    {
        try
        {
            var report = new DataQualitySummaryReportDto();

            // ── 客户维度 ──────────────────────────────────────
            var customerQuery = _dbContext.Customers
                .AsNoTracking()
                .Where(c => c.Status == CustomerStatus.Active);
            if (branchId.HasValue)
                customerQuery = customerQuery.Where(c => c.BranchId == branchId.Value);

            var customers = await customerQuery.ToListAsync();

            // 重复客户（按手机号）
            report.DuplicateCustomerCount = customers
                .Where(c => !string.IsNullOrWhiteSpace(c.Phone))
                .GroupBy(c => c.Phone)
                .Count(g => g.Count() > 1);

            // 缺少手机号
            report.MissingPhoneCustomerCount = customers.Count(c => string.IsNullOrWhiteSpace(c.Phone));

            // 缺少地址
            report.MissingAddressCustomerCount = customers.Count(c => string.IsNullOrWhiteSpace(c.Address));

            // 未关联企微
            report.UnlinkedWeChatCustomerCount = customers.Count(c => string.IsNullOrEmpty(c.WeChatExternalUserId));

            // 不活跃客户（>90天无订单）
            var inactiveCutoff = DateTime.Now.AddDays(-90);
            var customerIds = customers.Select(c => c.Id).ToList();
            var recentCustomerIds = await _dbContext.Orders
                .Where(o => customerIds.Contains(o.CustomerId) && o.CreatedAt >= inactiveCutoff)
                .Select(o => o.CustomerId)
                .Distinct()
                .ToListAsync();
            report.InactiveCustomerCount = customers.Count - recentCustomerIds.Count;

            // ── 产品维度 ──────────────────────────────────────
            var products = await _dbContext.Products
                .AsNoTracking()
                .Where(p => p.Status == ProductStatus.Active)
                .ToListAsync();

            report.NegativeStockProductCount = products.Count(p => p.Stock < 0);

            // ── 订单维度 ──────────────────────────────────────
            var orderQuery = _dbContext.Orders.AsNoTracking();
            if (branchId.HasValue)
                orderQuery = orderQuery.Where(o => o.BranchId == branchId.Value);

            var overdueDate = DateTime.Now.AddDays(-7);
            report.OverdueOrderCount = await orderQuery
                .CountAsync(o => o.Status == OrderStatus.Pending && o.CreatedAt < overdueDate);

            // ── 配送维度 ──────────────────────────────────────
            var deliveryQuery = _dbContext.DeliveryPersons
                .AsNoTracking()
                .Where(dp => dp.Status == DeliveryPersonStatus.Available);
            if (branchId.HasValue)
                deliveryQuery = deliveryQuery.Where(dp => dp.BranchId == branchId.Value);

            report.OverloadedDeliveryPersonCount = await deliveryQuery
                .CountAsync(dp => dp.CurrentLoad >= dp.MaxLoad);

            // ── 汇总问题列表 ──────────────────────────────────
            if (report.DuplicateCustomerCount > 0)
                report.Issues.Add(new DataQualityIssue
                {
                    Category = "Customer",
                    Severity = report.DuplicateCustomerCount > 10 ? "Critical" : "Warning",
                    Description = $"存在 {report.DuplicateCustomerCount} 组手机号重复的客户",
                    AffectedCount = report.DuplicateCustomerCount,
                    SuggestedAction = "使用客户合并功能清理重复数据"
                });

            if (report.MissingPhoneCustomerCount > 0)
                report.Issues.Add(new DataQualityIssue
                {
                    Category = "Customer",
                    Severity = "Warning",
                    Description = $"{report.MissingPhoneCustomerCount} 个客户缺少手机号",
                    AffectedCount = report.MissingPhoneCustomerCount,
                    SuggestedAction = "批量补全客户联系方式"
                });

            if (report.MissingAddressCustomerCount > 0)
                report.Issues.Add(new DataQualityIssue
                {
                    Category = "Customer",
                    Severity = "Warning",
                    Description = $"{report.MissingAddressCustomerCount} 个客户缺少地址",
                    AffectedCount = report.MissingAddressCustomerCount,
                    SuggestedAction = "补全客户地址信息以便配送"
                });

            if (report.UnlinkedWeChatCustomerCount > 0)
                report.Issues.Add(new DataQualityIssue
                {
                    Category = "Customer",
                    Severity = "Info",
                    Description = $"{report.UnlinkedWeChatCustomerCount} 个客户未关联企业微信",
                    AffectedCount = report.UnlinkedWeChatCustomerCount,
                    SuggestedAction = "通过企微同步功能关联客户"
                });

            if (report.NegativeStockProductCount > 0)
                report.Issues.Add(new DataQualityIssue
                {
                    Category = "Product",
                    Severity = "Critical",
                    Description = $"{report.NegativeStockProductCount} 个产品库存为负数",
                    AffectedCount = report.NegativeStockProductCount,
                    SuggestedAction = "检查库存数据并进行盘点修正"
                });

            if (report.OverdueOrderCount > 0)
                report.Issues.Add(new DataQualityIssue
                {
                    Category = "Order",
                    Severity = "Critical",
                    Description = $"{report.OverdueOrderCount} 个订单超过7天仍未分配",
                    AffectedCount = report.OverdueOrderCount,
                    SuggestedAction = "及时分配待处理订单"
                });

            if (report.InactiveCustomerCount > 0)
                report.Issues.Add(new DataQualityIssue
                {
                    Category = "Customer",
                    Severity = report.InactiveCustomerCount > 50 ? "Warning" : "Info",
                    Description = $"{report.InactiveCustomerCount} 个客户超过90天未下单",
                    AffectedCount = report.InactiveCustomerCount,
                    SuggestedAction = "安排业务员跟进不活跃客户"
                });

            if (report.OverloadedDeliveryPersonCount > 0)
                report.Issues.Add(new DataQualityIssue
                {
                    Category = "Delivery",
                    Severity = "Warning",
                    Description = $"{report.OverloadedDeliveryPersonCount} 个配送员已达到或超过最大负载",
                    AffectedCount = report.OverloadedDeliveryPersonCount,
                    SuggestedAction = "调整配送分配或增加配送人员"
                });

            return ApiResponse<DataQualitySummaryReportDto>.Ok(report);
        }
        catch (Exception ex)
        {
            return ApiResponse<DataQualitySummaryReportDto>.Fail($"生成数据质量报告失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 生成数据质量报告
    /// </summary>
    public async Task<ApiResponse<DataQualityReportDto>> GenerateReportAsync(int branchId)
    {
        try
        {
            var branch = await _dbContext.Branches.FindAsync(branchId);
            var report = new DataQualityReportDto
            {
                BranchId = branchId,
                BranchName = branch?.Name ?? ""
            };

            // 并行执行各项检测
            var customerTask = AnalyzeCustomerQualityAsync(branchId);
            var orderTask = AnalyzeOrderQualityAsync(branchId);
            var productTask = AnalyzeProductQualityAsync(branchId);

            await Task.WhenAll(customerTask, orderTask, productTask);

            report.CustomerQuality = await customerTask;
            report.OrderQuality = await orderTask;
            report.ProductQuality = await productTask;

            // 计算总体评分
            report.OverallScore = (report.CustomerQuality.Score + report.OrderQuality.Score + report.ProductQuality.Score) / 3;
            report.OverallLevel = report.OverallScore switch
            {
                >= 80 => "Good",
                >= 60 => "Warning",
                _ => "Critical"
            };

            return ApiResponse<DataQualityReportDto>.Ok(report);
        }
        catch (Exception ex)
        {
            return ApiResponse<DataQualityReportDto>.Fail($"生成报告失败: {ex.Message}");
        }
    }

    private async Task<CustomerQualityDto> AnalyzeCustomerQualityAsync(int branchId)
    {
        var result = new CustomerQualityDto();

        var customers = await _dbContext.Customers
            .AsNoTracking()
            .Where(c => c.BranchId == branchId && c.Status == CustomerStatus.Active)
            .ToListAsync();

        result.TotalCount = customers.Count;

        // 空数据统计
        result.EmptyPhoneCount = customers.Count(c => string.IsNullOrWhiteSpace(c.Phone));
        result.EmptyAddressCount = customers.Count(c => string.IsNullOrWhiteSpace(c.Address));
        result.EmptyLegalPersonCount = customers.Count(c => string.IsNullOrWhiteSpace(c.LegalPerson));

        // 企微未绑定
        result.UnboundWeChatCount = customers.Count(c => string.IsNullOrEmpty(c.WeChatExternalUserId));

        // 长期未下单（60天）
        var cutoffDate = DateTime.Now.AddDays(-60);
        var customerIds = customers.Select(c => c.Id).ToList();
        var recentOrderCustomerIds = await _dbContext.Orders
            .Where(o => customerIds.Contains(o.CustomerId) && o.CreatedAt >= cutoffDate)
            .Select(o => o.CustomerId)
            .Distinct()
            .ToListAsync();
        result.InactiveCustomerCount = customers.Count - recentOrderCustomerIds.Count;

        // 重复客户检测
        var duplicateGroups = await DetectDuplicateCustomersAsync(customers).ConfigureAwait(false);
        result.DuplicateGroups = duplicateGroups;
        result.DuplicateCount = duplicateGroups.Sum(g => g.Customers.Count);

        // 地址不规范检测
        var invalidAddresses = customers
            .Where(c => !string.IsNullOrEmpty(c.Address) && c.Address.Length < 5)
            .Take(10)
            .Select(c => c.Address!)
            .ToList();
        result.InvalidAddressCount = customers.Count(c => !string.IsNullOrEmpty(c.Address) && c.Address.Length < 5);
        result.InvalidAddressExamples = invalidAddresses;

        // 计算评分
        result.Score = CalculateCustomerScore(result);

        return result;
    }

    private Task<List<DuplicateGroupDto>> DetectDuplicateCustomersAsync(List<Domain.Entities.Customer> customers)
    {
        var groups = new List<DuplicateGroupDto>();

        // 按手机号重复
        var phoneGroups = customers
            .Where(c => !string.IsNullOrWhiteSpace(c.Phone))
            .GroupBy(c => c.Phone)
            .Where(g => g.Count() > 1)
            .Take(20);

        foreach (var group in phoneGroups)
        {
            groups.Add(new DuplicateGroupDto
            {
                MatchType = "Phone",
                MatchValue = group.Key!,
                Customers = group.Select(c => new DuplicateCustomerItem
                {
                    Id = c.Id,
                    Name = c.Name,
                    Phone = c.Phone,
                    Address = c.Address,
                    CreatedAt = c.CreatedAt
                }).ToList()
            });
        }

        // 按名称+地址重复
        var nameAddressGroups = customers
            .Where(c => !string.IsNullOrWhiteSpace(c.Name) && !string.IsNullOrWhiteSpace(c.Address))
            .GroupBy(c => $"{c.Name}_{c.Address}")
            .Where(g => g.Count() > 1)
            .Take(20);

        foreach (var group in nameAddressGroups)
        {
            groups.Add(new DuplicateGroupDto
            {
                MatchType = "NameAddress",
                MatchValue = group.Key,
                Customers = group.Select(c => new DuplicateCustomerItem
                {
                    Id = c.Id,
                    Name = c.Name,
                    Phone = c.Phone,
                    Address = c.Address,
                    CreatedAt = c.CreatedAt
                }).ToList()
            });
        }

        return Task.FromResult(groups);
    }

    private async Task<OrderQualityDto> AnalyzeOrderQualityAsync(int branchId)
    {
        var result = new OrderQualityDto();

        var orders = await _dbContext.Orders
            .AsNoTracking()
            .Where(o => o.BranchId == branchId)
            .ToListAsync();

        result.TotalCount = orders.Count;

        // 金额异常
        result.ZeroAmountCount = orders.Count(o => o.TotalAmount == 0 && o.Status != OrderStatus.Draft);
        result.NegativeAmountCount = orders.Count(o => o.TotalAmount < 0);

        // 地址缺失
        result.MissingDeliveryAddressCount = orders.Count(o =>
            string.IsNullOrWhiteSpace(o.DeliveryAddress) &&
            o.Status != OrderStatus.Draft);

        // 状态异常
        var staleDate = DateTime.Now.AddDays(-7);
        result.StaleDraftCount = orders.Count(o => o.Status == OrderStatus.Draft && o.CreatedAt < staleDate);

        var longPendingDate = DateTime.Now.AddDays(-3);
        result.LongPendingCount = orders.Count(o => o.Status == OrderStatus.Pending && o.CreatedAt < longPendingDate);

        // 结算异常
        result.UnsettledCompletedCount = orders.Count(o =>
            o.Status == OrderStatus.Completed && o.SettlementId == null);

        result.PaymentMismatchCount = orders.Count(o =>
            o.PaymentStatus == PaymentStatus.Paid &&
            o.ReceivedAmount < o.TotalAmount);

        result.Score = CalculateOrderScore(result);

        return result;
    }

    private async Task<ProductQualityDto> AnalyzeProductQualityAsync(int branchId)
    {
        var result = new ProductQualityDto();

        var products = await _dbContext.Products
            .AsNoTracking()
            .Where(p => p.Status == ProductStatus.Active)
            .ToListAsync();

        result.TotalCount = products.Count;

        // 重复SKU
        var duplicateSkus = products
            .Where(p => !string.IsNullOrWhiteSpace(p.SKU))
            .GroupBy(p => p.SKU)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key!)
            .ToList();
        result.DuplicateSkuCount = duplicateSkus.Count;
        result.DuplicateSkus = duplicateSkus.Take(10).ToList();

        // 空数据
        result.EmptyNameCount = products.Count(p => string.IsNullOrWhiteSpace(p.Name));
        result.EmptySkuCount = products.Count(p => string.IsNullOrWhiteSpace(p.SKU));

        // 库存异常
        result.NegativeStockCount = products.Count(p => p.Stock < 0);
        result.ZeroStockWithOrdersCount = products.Count(p => p.Stock == 0);

        result.Score = CalculateProductScore(result);

        return result;
    }

    private int CalculateCustomerScore(CustomerQualityDto quality)
    {
        if (quality.TotalCount == 0) return 100;

        int score = 100;

        // 重复客户扣分
        score -= Math.Min(20, quality.DuplicateCount * 2);

        // 空数据扣分
        var emptyRate = (double)(quality.EmptyPhoneCount + quality.EmptyAddressCount) / (quality.TotalCount * 2);
        score -= (int)(emptyRate * 30);

        // 企微未绑定扣分
        var unboundRate = (double)quality.UnboundWeChatCount / quality.TotalCount;
        score -= (int)(unboundRate * 20);

        // 长期未下单扣分
        var inactiveRate = (double)quality.InactiveCustomerCount / quality.TotalCount;
        score -= (int)(inactiveRate * 30);

        return Math.Max(0, score);
    }

    private int CalculateOrderScore(OrderQualityDto quality)
    {
        if (quality.TotalCount == 0) return 100;

        int score = 100;

        // 金额异常扣分
        score -= Math.Min(30, (quality.ZeroAmountCount + quality.NegativeAmountCount) * 3);

        // 地址缺失扣分
        var addressRate = (double)quality.MissingDeliveryAddressCount / quality.TotalCount;
        score -= (int)(addressRate * 20);

        // 状态异常扣分
        score -= Math.Min(20, quality.StaleDraftCount * 2);
        score -= Math.Min(20, quality.LongPendingCount * 2);

        // 结算异常扣分
        score -= Math.Min(10, quality.PaymentMismatchCount * 2);

        return Math.Max(0, score);
    }

    private int CalculateProductScore(ProductQualityDto quality)
    {
        if (quality.TotalCount == 0) return 100;

        int score = 100;

        // 重复SKU扣分
        score -= Math.Min(30, quality.DuplicateSkuCount * 5);

        // 空数据扣分
        score -= Math.Min(40, (quality.EmptyNameCount + quality.EmptySkuCount) * 5);

        // 库存异常扣分
        score -= Math.Min(30, quality.NegativeStockCount * 10);

        return Math.Max(0, score);
    }

    /// <summary>
    /// 合并重复客户
    /// </summary>
    public async Task<ApiResponse<bool>> MergeDuplicateCustomersAsync(int keepId, List<int> mergeIds)
    {
        try
        {
            var keepCustomer = await _dbContext.Customers.FindAsync(keepId);
            if (keepCustomer == null)
                return ApiResponse<bool>.Fail("目标客户不存在");

            foreach (var mergeId in mergeIds)
            {
                var mergeCustomer = await _dbContext.Customers.FindAsync(mergeId);
                if (mergeCustomer == null) continue;

                // 转移订单
                var orders = await _dbContext.Orders.Where(o => o.CustomerId == mergeId).ToListAsync();
                foreach (var order in orders)
                {
                    order.CustomerId = keepId;
                }

                // 标记为已合并
                mergeCustomer.Status = CustomerStatus.Merged;
                mergeCustomer.Remark = $"已合并至 {keepCustomer.Name} (ID:{keepId})";
            }

            await _dbContext.SaveChangesAsync();
            return ApiResponse<bool>.Ok(true, $"已合并 {mergeIds.Count} 个重复客户");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"合并失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 批量补全客户手机号
    /// </summary>
    public async Task<ApiResponse<int>> BatchUpdateCustomerPhoneAsync(List<CustomerPhoneUpdate> updates)
    {
        try
        {
            int successCount = 0;
            foreach (var update in updates)
            {
                var customer = await _dbContext.Customers.FindAsync(update.CustomerId);
                if (customer != null && string.IsNullOrWhiteSpace(customer.Phone))
                {
                    customer.Phone = update.Phone;
                    customer.UpdatedAt = DateTime.Now;
                    successCount++;
                }
            }

            await _dbContext.SaveChangesAsync();
            return ApiResponse<int>.Ok(successCount, $"已更新 {successCount} 个客户的手机号");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"批量更新失败: {ex.Message}");
        }
    }
}

public class CustomerPhoneUpdate
{
    public int CustomerId { get; set; }
    public string Phone { get; set; } = "";
}
