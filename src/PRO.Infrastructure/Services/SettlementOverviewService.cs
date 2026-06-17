using Microsoft.EntityFrameworkCore;
using PRO.Application.DTOs;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 结算概览服务 - 处理结算异常和差异
/// </summary>
public class SettlementOverviewService
{
    private readonly ProDbContext _dbContext;

    public SettlementOverviewService(ProDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// 获取结算概览
    /// </summary>
    public async Task<ApiResponse<SettlementOverviewDto>> GetOverviewAsync(int branchId)
    {
        try
        {
            var now = DateTime.Now;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);

            // 1. 未结算池
            var unsettledOrders = await _dbContext.Orders
                .AsNoTracking()
                .Include(o => o.Customer)
                .Where(o => o.BranchId == branchId
                    && o.Status == OrderStatus.Completed
                    && o.SettlementId == null)
                .ToListAsync();

            var unsettledTotal = unsettledOrders.Sum(o => o.TotalAmount);
            var unsettledReceived = unsettledOrders.Sum(o => o.ReceivedAmount);

            // 2. 异常订单检测
            var abnormalOrders = new List<AbnormalOrderDto>();

            // 多付/少付
            foreach (var order in unsettledOrders.Where(o => o.ReceivedAmount > 0))
            {
                var diff = order.ReceivedAmount - order.TotalAmount;
                if (Math.Abs(diff) > 0.01m)
                {
                    abnormalOrders.Add(new AbnormalOrderDto
                    {
                        Id = order.Id,
                        OrderNo = order.OrderNo,
                        CustomerName = order.Customer?.Name ?? "",
                        TotalAmount = order.TotalAmount,
                        ReceivedAmount = order.ReceivedAmount,
                        Difference = diff,
                        AbnormalType = diff > 0 ? "OverPayment" : "UnderPayment",
                        AbnormalDescription = diff > 0 ? $"多收 ¥{diff:N2}" : $"少收 ¥{Math.Abs(diff):N2}",
                        CreatedAt = order.CreatedAt
                    });
                }
            }

            // 长期未结算（超过30天）
            var overdueDate = now.AddDays(-30);
            var longOverdue = unsettledOrders
                .Where(o => o.CreatedAt < overdueDate)
                .Select(o => new AbnormalOrderDto
                {
                    Id = o.Id,
                    OrderNo = o.OrderNo,
                    CustomerName = o.Customer?.Name ?? "",
                    TotalAmount = o.TotalAmount,
                    ReceivedAmount = o.ReceivedAmount,
                    AbnormalType = "LongOverdue",
                    AbnormalDescription = $"已 {(int)(now - o.CreatedAt).TotalDays} 天未结算",
                    CreatedAt = o.CreatedAt,
                    DaysOverdue = (int)(now - o.CreatedAt).TotalDays
                })
                .ToList();

            abnormalOrders.AddRange(longOverdue);

            // 3. 收款差异
            var paymentDifferences = unsettledOrders
                .Where(o => o.PaymentStatus == PaymentStatus.PartialPaid)
                .Select(o => new PaymentDifferenceDto
                {
                    OrderId = o.Id,
                    OrderNo = o.OrderNo,
                    CustomerName = o.Customer?.Name ?? "",
                    OrderAmount = o.TotalAmount,
                    ReceivedAmount = o.ReceivedAmount,
                    Difference = o.TotalAmount - o.ReceivedAmount,
                    DifferenceType = "Partial"
                })
                .ToList();

            // 4. 本月结算统计
            var thisMonthSettlements = await _dbContext.Settlements
                .AsNoTracking()
                .Where(s => s.BranchId == branchId && s.CreatedAt >= startOfMonth)
                .ToListAsync();

            var pendingPdfCount = await _dbContext.Settlements
                .CountAsync(s => s.BranchId == branchId && string.IsNullOrEmpty(s.PdfPath));

            // 5. 最近结算
            var recentSettlements = await _dbContext.Settlements
                .AsNoTracking()
                .Include(s => s.Branch)
                .Where(s => s.BranchId == branchId)
                .OrderByDescending(s => s.CreatedAt)
                .Take(10)
                .Select(s => new SettlementListItem
                {
                    Id = s.Id,
                    SettlementNo = s.SettlementNo,
                    StartDate = s.StartDate,
                    EndDate = s.EndDate,
                    OrderCount = s.OrderCount,
                    TotalAmount = s.TotalAmount,
                    ReceivedAmount = s.ReceivedAmount,
                    UnpaidAmount = s.UnpaidAmount,
                    Status = s.Status,
                    CreatedAt = s.CreatedAt
                })
                .ToListAsync();

            return ApiResponse<SettlementOverviewDto>.Ok(new SettlementOverviewDto
            {
                UnsettledOrderCount = unsettledOrders.Count,
                UnsettledTotalAmount = unsettledTotal,
                UnsettledReceivedAmount = unsettledReceived,
                UnsettledReceivableAmount = unsettledTotal - unsettledReceived,

                AbnormalOrderCount = abnormalOrders.Count,
                AbnormalOrders = abnormalOrders.OrderByDescending(a => a.DaysOverdue).Take(20).ToList(),

                PaymentDifferenceCount = paymentDifferences.Count,
                TotalPaymentDifference = paymentDifferences.Sum(p => p.Difference),
                PaymentDifferences = paymentDifferences.Take(20).ToList(),

                ThisMonthSettlementCount = thisMonthSettlements.Count,
                ThisMonthSettledAmount = thisMonthSettlements.Sum(s => s.TotalAmount),
                PendingPdfCount = pendingPdfCount,

                RecentSettlements = recentSettlements
            });
        }
        catch (Exception ex)
        {
            return ApiResponse<SettlementOverviewDto>.Fail($"获取结算概览失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 批量结算订单
    /// </summary>
    public async Task<ApiResponse<bool>> BatchSettleAsync(List<int> orderIds, int confirmedById, string? remark = null)
    {
        try
        {
            var orders = await _dbContext.Orders
                .Where(o => orderIds.Contains(o.Id) && o.Status == OrderStatus.Completed && o.SettlementId == null)
                .ToListAsync();

            if (!orders.Any())
                return ApiResponse<bool>.Fail("没有可结算的订单");

            var now = DateTime.Now;
            var settlement = new Domain.Entities.Settlement
            {
                SettlementNo = $"STL{now:yyyyMMddHHmmss}",
                BranchId = orders.First().BranchId,
                StartDate = orders.Min(o => o.CreatedAt),
                EndDate = now,
                OrderCount = orders.Count,
                TotalAmount = orders.Sum(o => o.TotalAmount),
                ReceivedAmount = orders.Sum(o => o.ReceivedAmount),
                UnpaidAmount = orders.Sum(o => o.TotalAmount - o.ReceivedAmount),
                ConfirmedById = confirmedById,
                Status = SettlementStatus.Completed,
                Remark = remark,
                CreatedAt = now,
                UpdatedAt = now
            };

            _dbContext.Settlements.Add(settlement);
            await _dbContext.SaveChangesAsync();

            foreach (var order in orders)
            {
                order.SettlementId = settlement.Id;
                order.UpdatedAt = now;
            }

            await _dbContext.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, $"已结算 {orders.Count} 个订单");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"批量结算失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 标记异常订单
    /// </summary>
    public async Task<ApiResponse<bool>> MarkAbnormalAsync(int orderId, string abnormalType, string remark)
    {
        try
        {
            var order = await _dbContext.Orders.FindAsync(orderId);
            if (order == null)
                return ApiResponse<bool>.Fail("订单不存在");

            _dbContext.OrderModificationRecords.Add(new Domain.Entities.OrderModificationRecord
            {
                OrderId = orderId,
                ModifiedById = 0,
                ModifiedAt = DateTime.Now,
                Content = $"标记为异常：{abnormalType}，备注：{remark}",
                ModificationType = "MarkAbnormal"
            });

            await _dbContext.SaveChangesAsync();
            return ApiResponse<bool>.Ok(true, "已标记为异常");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"标记异常失败: {ex.Message}");
        }
    }
}
