using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;
using PRO.Infrastructure.Services;
using Serilog;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 状态历史与审计日志服务
/// </summary>
public class AuditTrailService : IAuditTrailService
{
    private readonly ProDbContext _dbContext;
    private readonly DataMaskingService _maskingService;

    public AuditTrailService(ProDbContext dbContext, DataMaskingService maskingService)
    {
        _dbContext = dbContext;
        _maskingService = maskingService;
    }

    // ==================== 状态历史记录 ====================

    public async Task LogOrderStatusChangeAsync(int orderId, string orderNo, int fromStatus, int toStatus,
        string reason, int operatorId)
    {
        try
        {
            _dbContext.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderId = orderId,
                OrderNo = orderNo,
                FromStatus = fromStatus,
                ToStatus = toStatus,
                Reason = reason,
                OperatorId = operatorId,
                CreatedAt = DateTime.Now
            });
            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "记录订单状态历史失败 OrderId={OrderId}", orderId);
        }
    }

    public async Task LogPaymentStatusChangeAsync(string entityType, int entityId, int fromStatus, int toStatus,
        string reason, int operatorId)
    {
        try
        {
            _dbContext.PaymentStatusHistories.Add(new PaymentStatusHistory
            {
                EntityType = entityType,
                EntityId = entityId,
                FromStatus = fromStatus,
                ToStatus = toStatus,
                Reason = reason,
                OperatorId = operatorId,
                CreatedAt = DateTime.Now
            });
            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "记录收款状态历史失败 {EntityType}={EntityId}", entityType, entityId);
        }
    }

    public async Task<ApiResponse<List<OrderStatusHistoryDto>>> GetOrderStatusHistoryAsync(int orderId)
    {
        try
        {
            var history = await _dbContext.OrderStatusHistories
                .AsNoTracking()
                .Include(h => h.Operator)
                .Where(h => h.OrderId == orderId)
                .OrderByDescending(h => h.CreatedAt)
                .Select(h => new OrderStatusHistoryDto
                {
                    Id = h.Id,
                    OrderId = h.OrderId,
                    OrderNo = h.OrderNo,
                    FromStatus = h.FromStatus,
                    FromStatusName = OrderStatusManager.GetStatusName((OrderStatus)h.FromStatus),
                    ToStatus = h.ToStatus,
                    ToStatusName = OrderStatusManager.GetStatusName((OrderStatus)h.ToStatus),
                    Reason = h.Reason,
                    OperatorId = h.OperatorId,
                    OperatorName = h.Operator != null ? h.Operator.Name : "",
                    CreatedAt = h.CreatedAt
                })
                .ToListAsync();

            return ApiResponse<List<OrderStatusHistoryDto>>.Ok(history);
        }
        catch (Exception ex)
        {
            return ApiResponse<List<OrderStatusHistoryDto>>.Fail($"查询失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<List<PaymentStatusHistoryDto>>> GetPaymentStatusHistoryAsync(string entityType, int entityId)
    {
        try
        {
            var history = await _dbContext.PaymentStatusHistories
                .AsNoTracking()
                .Include(h => h.Operator)
                .Where(h => h.EntityType == entityType && h.EntityId == entityId)
                .OrderByDescending(h => h.CreatedAt)
                .Select(h => new PaymentStatusHistoryDto
                {
                    Id = h.Id,
                    EntityType = h.EntityType,
                    EntityId = h.EntityId,
                    FromStatus = h.FromStatus,
                    FromStatusName = ((PaymentStatus)h.FromStatus).ToString(),
                    ToStatus = h.ToStatus,
                    ToStatusName = ((PaymentStatus)h.ToStatus).ToString(),
                    Reason = h.Reason,
                    OperatorId = h.OperatorId,
                    OperatorName = h.Operator != null ? h.Operator.Name : "",
                    CreatedAt = h.CreatedAt
                })
                .ToListAsync();

            return ApiResponse<List<PaymentStatusHistoryDto>>.Ok(history);
        }
        catch (Exception ex)
        {
            return ApiResponse<List<PaymentStatusHistoryDto>>.Fail($"查询失败: {ex.Message}");
        }
    }

    // ==================== 状态回滚（带审计） ====================

    public async Task<ApiResponse<bool>> RollbackOrderStatusAsync(StatusRollbackRequest request, int operatorId)
    {
        try
        {
            var order = await _dbContext.Orders.FindAsync(request.EntityId);
            if (order == null)
                return ApiResponse<bool>.Fail("订单不存在");

            var oldStatus = (int)order.Status;
            var newStatus = (OrderStatus)request.TargetStatus;

            // 权限校验：只能回滚到之前存在过的状态
            var hasHistory = await _dbContext.OrderStatusHistories
                .AnyAsync(h => h.OrderId == request.EntityId && h.ToStatus == request.TargetStatus);
            if (!hasHistory && request.TargetStatus != (int)order.Status)
                return ApiResponse<bool>.Fail("只能回滚到历史存在的状态");

            order.Status = newStatus;
            order.UpdatedAt = DateTime.Now;
            await _dbContext.SaveChangesAsync();

            // 追加审计日志（回滚本身也是操作）
            await LogOrderStatusChangeAsync(order.Id, order.OrderNo, oldStatus, request.TargetStatus,
                $"【回滚】{request.Reason}", operatorId);

            // 记录详细审计
            await LogDetailAsync("Order", order.Id, order.OrderNo, "Status",
                ((OrderStatus)oldStatus).ToString(), newStatus.ToString(),
                "Rollback", request.Reason, operatorId, order.BranchId);

            return ApiResponse<bool>.Ok(true, $"订单状态已回滚至 {OrderStatusManager.GetStatusName(newStatus)}");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"回滚失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> RollbackPaymentStatusAsync(StatusRollbackRequest request, int operatorId)
    {
        try
        {
            if (request.EntityType == "Order")
            {
                var order = await _dbContext.Orders.FindAsync(request.EntityId);
                if (order == null) return ApiResponse<bool>.Fail("订单不存在");

                var oldStatus = (int)order.PaymentStatus;
                var newStatus = (PaymentStatus)request.TargetStatus;

                order.PaymentStatus = newStatus;
                order.UpdatedAt = DateTime.Now;
                await _dbContext.SaveChangesAsync();

                await LogPaymentStatusChangeAsync("Order", order.Id, oldStatus, request.TargetStatus,
                    $"【回滚】{request.Reason}", operatorId);

                await LogDetailAsync("Order", order.Id, order.OrderNo, "PaymentStatus",
                    ((PaymentStatus)oldStatus).ToString(), newStatus.ToString(),
                    "Rollback", request.Reason, operatorId, order.BranchId);

                return ApiResponse<bool>.Ok(true, "收款状态已回滚");
            }

            return ApiResponse<bool>.Fail("不支持的回滚类型");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"回滚失败: {ex.Message}");
        }
    }

    // ==================== 审计日志详情 ====================

    public async Task LogDetailAsync(string entityType, int entityId, string entityName, string fieldName,
        string? oldValue, string? newValue, string actionType, string reason, int operatorId, int branchId)
    {
        try
        {
            // 对敏感字段脱敏存储到审计日志
            var maskedOldValue = MaskFieldValue(fieldName, oldValue);
            var maskedNewValue = MaskFieldValue(fieldName, newValue);

            _dbContext.AuditLogDetails.Add(new AuditLogDetail
            {
                EntityType = entityType,
                EntityId = entityId,
                EntityName = entityName,
                FieldName = fieldName,
                OldValue = maskedOldValue,
                NewValue = maskedNewValue,
                ActionType = actionType,
                Reason = reason,
                OperatorId = operatorId,
                BranchId = branchId,
                CreatedAt = DateTime.Now
            });
            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "记录审计详情失败 {EntityType}={EntityId}", entityType, entityId);
        }
    }

    /// <summary>
    /// 对审计日志中的敏感字段值进行脱敏
    /// </summary>
    private string? MaskFieldValue(string fieldName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;

        var lower = fieldName.ToLowerInvariant();
        if (lower.Contains("phone") || lower.Contains("mobile") || lower.Contains("telephone"))
            return _maskingService.MaskPhone(value);
        if (lower.Contains("idcard") || lower.Contains("identity"))
            return _maskingService.MaskIdCard(value);
        if (lower.Contains("bankcard") || lower.Contains("account"))
            return _maskingService.MaskBankCard(value);
        if (lower.Contains("name") && (lower.Contains("customer") || lower.Contains("legal") || lower.Contains("contact")))
            return _maskingService.MaskName(value);

        return value;
    }

    public async Task<ApiResponse<PagedResult<AuditLogDetailDto>>> QueryAuditLogsAsync(AuditLogQueryRequest request)
    {
        try
        {
            var query = _dbContext.AuditLogDetails
                .AsNoTracking()
                .Include(l => l.Operator)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.EntityType))
                query = query.Where(l => l.EntityType == request.EntityType);
            if (request.EntityId.HasValue)
                query = query.Where(l => l.EntityId == request.EntityId.Value);
            if (!string.IsNullOrWhiteSpace(request.FieldName))
                query = query.Where(l => l.FieldName == request.FieldName);
            if (!string.IsNullOrWhiteSpace(request.ActionType))
                query = query.Where(l => l.ActionType == request.ActionType);
            if (request.OperatorId.HasValue)
                query = query.Where(l => l.OperatorId == request.OperatorId.Value);
            if (request.BranchId.HasValue)
                query = query.Where(l => l.BranchId == request.BranchId.Value);
            if (request.StartDate.HasValue)
                query = query.Where(l => l.CreatedAt >= request.StartDate.Value);
            if (request.EndDate.HasValue)
                query = query.Where(l => l.CreatedAt < request.EndDate.Value.AddDays(1));

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(l => l.CreatedAt)
                .Skip((request.PageIndex - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(l => new AuditLogDetailDto
                {
                    Id = l.Id,
                    EntityType = l.EntityType,
                    EntityId = l.EntityId,
                    EntityName = l.EntityName,
                    FieldName = l.FieldName,
                    OldValue = l.OldValue,
                    NewValue = l.NewValue,
                    ActionType = l.ActionType,
                    Reason = l.Reason,
                    OperatorId = l.OperatorId,
                    OperatorName = l.Operator != null ? l.Operator.Name : "",
                    BranchId = l.BranchId,
                    CreatedAt = l.CreatedAt
                })
                .ToListAsync();

            return ApiResponse<PagedResult<AuditLogDetailDto>>.Ok(new PagedResult<AuditLogDetailDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageIndex = request.PageIndex,
                PageSize = request.PageSize
            });
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<AuditLogDetailDto>>.Fail($"查询失败: {ex.Message}");
        }
    }
}
