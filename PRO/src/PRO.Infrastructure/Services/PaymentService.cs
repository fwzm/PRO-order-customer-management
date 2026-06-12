using Microsoft.EntityFrameworkCore;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 收款服务 — 收款登记和多对多核销
/// </summary>
public class PaymentService : IPaymentService
{
    private readonly ProDbContext _dbContext;

    public PaymentService(ProDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// 登记收款（支持未绑定订单的预收款）
    /// </summary>
    public async Task<ApiResponse<int>> CreatePaymentAsync(CreatePaymentRequest request, int receivedById)
    {
        try
        {
            if (request.Amount <= 0)
                return ApiResponse<int>.Fail("收款金额必须大于0");

            // 获取或推断客户ID
            int customerId;
            if (request is CreatePaymentRequestV2 v2 && v2.CustomerId > 0)
            {
                customerId = v2.CustomerId;
            }
            else if (request.OrderId > 0)
            {
                var order = await _dbContext.Orders.FindAsync(request.OrderId);
                if (order == null)
                    return ApiResponse<int>.Fail("订单不存在");
                customerId = order.CustomerId;
            }
            else
            {
                return ApiResponse<int>.Fail("必须指定订单ID或客户ID");
            }

            var paymentNo = await GeneratePaymentNoAsync();
            var payment = new PaymentRecord
            {
                PaymentNo = paymentNo,
                OrderId = request.OrderId > 0 ? request.OrderId : null,
                CustomerId = customerId,
                Amount = request.Amount,
                AllocatedAmount = 0,
                PaymentMethod = request.PaymentMethod.ToString(),
                Reference = request.Reference,
                Remark = request.Remark,
                ReceivedById = receivedById,
                PaymentDate = request.PaymentDate ?? DateTime.Now,
                CreatedAt = DateTime.Now
            };

            _dbContext.PaymentRecords.Add(payment);
            await _dbContext.SaveChangesAsync();

            return ApiResponse<int>.Ok(payment.Id, $"收款登记成功，金额 ¥{request.Amount:N2}");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"收款登记失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 批量收款
    /// </summary>
    public async Task<ApiResponse<int>> BatchCreatePaymentAsync(BatchPaymentRequest request, int receivedById)
    {
        try
        {
            int successCount = 0;
            foreach (var payment in request.Payments)
            {
                var result = await CreatePaymentAsync(payment, receivedById);
                if (result.Success) successCount++;
            }

            return ApiResponse<int>.Ok(successCount, $"成功登记 {successCount} 笔收款");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"批量收款失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 收款核销 — 多对多，支持部分核销
    /// </summary>
    public async Task<ApiResponse<PaymentAllocationResultDto>> AllocatePaymentAsync(
        AllocatePaymentRequest request, int operatorId)
    {
        try
        {
            var payment = await _dbContext.PaymentRecords.FindAsync(request.PaymentRecordId);
            if (payment == null)
                return ApiResponse<PaymentAllocationResultDto>.Fail("收款记录不存在");

            var totalAllocate = request.Allocations.Sum(a => a.Amount);
            var remaining = payment.Amount - payment.AllocatedAmount;

            if (totalAllocate > remaining)
                return ApiResponse<PaymentAllocationResultDto>.Fail(
                    $"核销金额 {totalAllocate:N2} 超过可核销余额 {remaining:N2}");

            // 验证所有订单存在且属于同一客户
            var orderIds = request.Allocations.Select(a => a.OrderId).Distinct().ToList();
            var orders = await _dbContext.Orders.Where(o => orderIds.Contains(o.Id)).ToListAsync();

            if (orders.Count != orderIds.Count)
                return ApiResponse<PaymentAllocationResultDto>.Fail("部分订单不存在");

            if (orders.Any(o => o.CustomerId != payment.CustomerId))
                return ApiResponse<PaymentAllocationResultDto>.Fail("核销订单与收款客户不一致");

            var allocations = new List<PaymentAllocation>();
            foreach (var item in request.Allocations)
            {
                var order = orders.First(o => o.Id == item.OrderId);
                var alloc = new PaymentAllocation
                {
                    PaymentRecordId = payment.Id,
                    OrderId = item.OrderId,
                    Amount = item.Amount,
                    AllocatedById = operatorId,
                    Remark = request.Remark,
                    CreatedAt = DateTime.Now
                };
                allocations.Add(alloc);

                // 更新订单收款金额
                order.ReceivedAmount += item.Amount;
                UpdateOrderPaymentStatus(order);
                order.UpdatedAt = DateTime.Now;
            }

            _dbContext.PaymentAllocations.AddRange(allocations);
            payment.AllocatedAmount += totalAllocate;
            await _dbContext.SaveChangesAsync();

            return ApiResponse<PaymentAllocationResultDto>.Ok(new PaymentAllocationResultDto
            {
                PaymentRecordId = payment.Id,
                PaymentNo = payment.PaymentNo,
                TotalAmount = payment.Amount,
                AllocatedAmount = payment.AllocatedAmount,
                UnallocatedAmount = payment.Amount - payment.AllocatedAmount,
                Details = allocations.Select(a => new PaymentAllocationDetailDto
                {
                    Id = a.Id,
                    OrderId = a.OrderId,
                    OrderNo = orders.First(o => o.Id == a.OrderId).OrderNo,
                    Amount = a.Amount,
                    Remark = a.Remark,
                    CreatedAt = a.CreatedAt
                }).ToList()
            }, $"核销成功，金额 ¥{totalAllocate:N2}");
        }
        catch (Exception ex)
        {
            return ApiResponse<PaymentAllocationResultDto>.Fail($"核销失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取收款的所有核销明细
    /// </summary>
    public async Task<ApiResponse<List<PaymentAllocationDetailDto>>> GetAllocationsAsync(int paymentRecordId)
    {
        try
        {
            var allocations = await _dbContext.PaymentAllocations
                .AsNoTracking()
                .Include(a => a.Order)
                .Where(a => a.PaymentRecordId == paymentRecordId)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new PaymentAllocationDetailDto
                {
                    Id = a.Id,
                    OrderId = a.OrderId,
                    OrderNo = a.Order != null ? a.Order.OrderNo : "",
                    Amount = a.Amount,
                    Remark = a.Remark,
                    CreatedAt = a.CreatedAt
                })
                .ToListAsync();

            return ApiResponse<List<PaymentAllocationDetailDto>>.Ok(allocations);
        }
        catch (Exception ex)
        {
            return ApiResponse<List<PaymentAllocationDetailDto>>.Fail($"查询失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取订单的收款记录
    /// </summary>
    public async Task<ApiResponse<List<PaymentRecordDto>>> GetOrderPaymentsAsync(int orderId)
    {
        try
        {
            var records = await _dbContext.PaymentRecords
                .AsNoTracking()
                .Include(p => p.ReceivedBy)
                .Include(p => p.Customer)
                .Where(p => p.OrderId == orderId)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();

            var payments = records.Select(MapToDto).ToList();
            return ApiResponse<List<PaymentRecordDto>>.Ok(payments);
        }
        catch (Exception ex)
        {
            return ApiResponse<List<PaymentRecordDto>>.Fail($"查询失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取客户的收款记录
    /// </summary>
    public async Task<ApiResponse<List<PaymentRecordDto>>> GetCustomerPaymentsAsync(int customerId)
    {
        try
        {
            var records = await _dbContext.PaymentRecords
                .AsNoTracking()
                .Include(p => p.ReceivedBy)
                .Include(p => p.Customer)
                .Where(p => p.CustomerId == customerId)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();

            var payments = records.Select(MapToDto).ToList();
            return ApiResponse<List<PaymentRecordDto>>.Ok(payments);
        }
        catch (Exception ex)
        {
            return ApiResponse<List<PaymentRecordDto>>.Fail($"查询失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取收款列表
    /// </summary>
    public async Task<ApiResponse<PagedResult<PaymentRecordDto>>> GetPaymentListAsync(
        PagedRequest request, int? branchId = null, int? customerId = null,
        DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            var query = _dbContext.PaymentRecords
                .AsNoTracking()
                .Include(p => p.Order)
                .ThenInclude(o => o!.Customer)
                .Include(p => p.ReceivedBy)
                .Include(p => p.Customer)
                .AsQueryable();

            if (branchId.HasValue)
                query = query.Where(p => p.Order != null && p.Order.BranchId == branchId.Value);
            if (customerId.HasValue)
                query = query.Where(p => p.CustomerId == customerId.Value);
            if (startDate.HasValue)
                query = query.Where(p => p.PaymentDate >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(p => p.PaymentDate < endDate.Value.AddDays(1));

            var totalCount = await query.CountAsync();
            var records = await query
                .OrderByDescending(p => p.PaymentDate)
                .Skip((request.PageIndex - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync();

            var items = records.Select(p =>
            {
                var dto = MapToDto(p);
                dto.OrderNo = p.Order != null ? p.Order.OrderNo : "";
                return dto;
            }).ToList();

            return ApiResponse<PagedResult<PaymentRecordDto>>.Ok(new PagedResult<PaymentRecordDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageIndex = request.PageIndex,
                PageSize = request.PageSize
            });
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<PaymentRecordDto>>.Fail($"查询失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取收款统计
    /// </summary>
    public async Task<ApiResponse<PaymentStatsDto>> GetPaymentStatsAsync(int branchId, DateTime startDate, DateTime endDate)
    {
        try
        {
            var payments = await _dbContext.PaymentRecords
                .AsNoTracking()
                .Include(p => p.Order)
                .ThenInclude(o => o!.Customer)
                .Where(p => p.Order != null && p.Order.BranchId == branchId
                    && p.PaymentDate >= startDate && p.PaymentDate < endDate.AddDays(1))
                .ToListAsync();

            var stats = new PaymentStatsDto
            {
                StartDate = startDate,
                EndDate = endDate,
                TotalReceived = payments.Sum(p => p.Amount),
                TotalCount = payments.Count,
                CashAmount = payments.Where(p => p.PaymentMethod == nameof(PaymentMethod.Cash)).Sum(p => p.Amount),
                WeChatAmount = payments.Where(p => p.PaymentMethod == nameof(PaymentMethod.WeChat)).Sum(p => p.Amount),
                AlipayAmount = payments.Where(p => p.PaymentMethod == nameof(PaymentMethod.Alipay)).Sum(p => p.Amount),
                BankTransferAmount = payments.Where(p => p.PaymentMethod == nameof(PaymentMethod.BankTransfer)).Sum(p => p.Amount),
                DailyPayments = payments
                    .GroupBy(p => p.PaymentDate.Date)
                    .Select(g => new DailyPaymentDto
                    {
                        Date = g.Key,
                        Amount = g.Sum(p => p.Amount),
                        Count = g.Count()
                    })
                    .OrderBy(d => d.Date)
                    .ToList(),
                TopCustomers = payments
                    .Where(p => p.Order?.Customer != null)
                    .GroupBy(p => new { p.Order!.CustomerId, CustomerName = p.Order.Customer!.Name })
                    .Select(g => new CustomerPaymentSummary
                    {
                        CustomerId = g.Key.CustomerId,
                        CustomerName = g.Key.CustomerName,
                        TotalAmount = g.Sum(p => p.Amount),
                        PaymentCount = g.Count()
                    })
                    .OrderByDescending(c => c.TotalAmount)
                    .Take(10)
                    .ToList()
            };

            return ApiResponse<PaymentStatsDto>.Ok(stats);
        }
        catch (Exception ex)
        {
            return ApiResponse<PaymentStatsDto>.Fail($"获取统计失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 删除收款记录（退款）
    /// </summary>
    public async Task<ApiResponse<bool>> DeletePaymentAsync(int paymentId, int operatorId, string reason)
    {
        try
        {
            var payment = await _dbContext.PaymentRecords
                .Include(p => p.Allocations)
                .FirstOrDefaultAsync(p => p.Id == paymentId);
            if (payment == null)
                return ApiResponse<bool>.Fail("收款记录不存在");

            // 回退所有已核销的订单收款金额
            foreach (var alloc in payment.Allocations)
            {
                var order = await _dbContext.Orders.FindAsync(alloc.OrderId);
                if (order != null)
                {
                    order.ReceivedAmount -= alloc.Amount;
                    if (order.ReceivedAmount < 0) order.ReceivedAmount = 0;
                    UpdateOrderPaymentStatus(order);
                    order.UpdatedAt = DateTime.Now;
                }
            }

            // 如果收款还关联了OrderId但未核销，也要更新
            if (payment.OrderId.HasValue && !payment.Allocations.Any())
            {
                var order = await _dbContext.Orders.FindAsync(payment.OrderId.Value);
                if (order != null)
                {
                    order.ReceivedAmount -= payment.Amount;
                    if (order.ReceivedAmount < 0) order.ReceivedAmount = 0;
                    UpdateOrderPaymentStatus(order);
                    order.UpdatedAt = DateTime.Now;
                }
            }

            _dbContext.PaymentAllocations.RemoveRange(payment.Allocations);
            _dbContext.PaymentRecords.Remove(payment);
            await _dbContext.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, $"收款记录已删除，原因：{reason}");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"删除失败: {ex.Message}");
        }
    }

    private static void UpdateOrderPaymentStatus(Order order)
    {
        if (order.ReceivedAmount >= order.TotalAmount - order.DiscountAmount)
            order.PaymentStatus = PaymentStatus.Paid;
        else if (order.ReceivedAmount > 0)
            order.PaymentStatus = PaymentStatus.PartialPaid;
        else
            order.PaymentStatus = PaymentStatus.Unpaid;
    }

    private async Task<string> GeneratePaymentNoAsync()
    {
        var today = DateTime.Now.ToString("yyyyMMdd");
        var prefix = $"PAY{today}";

        var maxNo = await _dbContext.PaymentRecords
            .Where(p => p.PaymentNo.StartsWith(prefix))
            .MaxAsync(p => (string?)p.PaymentNo) ?? "";

        var seq = 1;
        if (maxNo.Length >= prefix.Length + 4)
        {
            var lastSeqStr = maxNo.Substring(prefix.Length, 4);
            int.TryParse(lastSeqStr, out seq);
            seq++;
        }

        return $"{prefix}{seq:D4}";
    }

    private static PaymentRecordDto MapToDto(PaymentRecord p)
    {
        return new PaymentRecordDto
        {
            Id = p.Id,
            PaymentNo = p.PaymentNo,
            OrderId = p.OrderId ?? 0,
            CustomerId = p.CustomerId,
            CustomerName = p.Customer?.Name ?? "",
            Amount = p.Amount,
            PaymentMethod = Enum.TryParse<PaymentMethod>(p.PaymentMethod, out var pm) ? pm : PaymentMethod.Cash,
            Reference = p.Reference,
            Remark = p.Remark,
            ReceivedById = p.ReceivedById,
            ReceivedByName = p.ReceivedBy?.Name ?? "",
            PaymentDate = p.PaymentDate,
            CreatedAt = p.CreatedAt
        };
    }
}

/// <summary>
/// 扩展CreatePaymentRequest以支持直接指定CustomerId
/// </summary>
public class CreatePaymentRequestV2 : CreatePaymentRequest
{
    public int CustomerId { get; set; }
}
