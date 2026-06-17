using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;
using Serilog;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 操作撤销服务 - 支持关键操作5分钟内撤销，撤销操作追加审计日志
/// </summary>
public class UndoService
{
    private readonly ProDbContext _dbContext;
    private readonly IAuditTrailService _auditTrail;
    private readonly TimeSpan _undoWindow = TimeSpan.FromMinutes(5);

    public UndoService(ProDbContext dbContext, IAuditTrailService auditTrail)
    {
        _dbContext = dbContext;
        _auditTrail = auditTrail;
    }

    /// <summary>
    /// 记录可撤销操作
    /// </summary>
    public async Task RecordOperationAsync(string operationType, string entityType, int entityId,
        string entityName, OperationSnapshot snapshot, int operatorId, string description)
    {
        try
        {
            var record = new UndoableOperation
            {
                OperationType = operationType,
                EntityType = entityType,
                EntityId = entityId,
                EntityName = entityName,
                Description = description,
                SnapshotData = JsonSerializer.Serialize(snapshot),
                OperatorId = operatorId,
                OperatedAt = DateTime.Now,
                UndoDeadline = DateTime.Now.Add(_undoWindow),
                IsUndone = false
            };

            _dbContext.UndoableOperations.Add(record);
            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "记录可撤销操作失败");
        }
    }

    /// <summary>
    /// 获取用户的可撤销操作列表
    /// </summary>
    public async Task<ApiResponse<List<UndoableOperationDto>>> GetUndoableOperationsAsync(int operatorId)
    {
        try
        {
            var operations = await _dbContext.UndoableOperations
                .AsNoTracking()
                .Include(o => o.Operator)
                .Where(o => o.OperatorId == operatorId && !o.IsUndone && o.UndoDeadline > DateTime.Now)
                .OrderByDescending(o => o.OperatedAt)
                .Select(o => new UndoableOperationDto
                {
                    Id = o.Id,
                    OperationType = o.OperationType,
                    EntityType = o.EntityType,
                    EntityId = o.EntityId,
                    EntityName = o.EntityName,
                    Description = o.Description,
                    SnapshotData = o.SnapshotData,
                    OperatorId = o.OperatorId,
                    OperatorName = o.Operator != null ? o.Operator.Name : "",
                    OperatedAt = o.OperatedAt,
                    UndoDeadline = o.UndoDeadline,
                    IsUndone = o.IsUndone,
                    UndoneAt = o.UndoneAt
                })
                .ToListAsync();

            return ApiResponse<List<UndoableOperationDto>>.Ok(operations);
        }
        catch (Exception ex)
        {
            return ApiResponse<List<UndoableOperationDto>>.Fail($"查询失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 执行撤销（带审计日志）
    /// </summary>
    public async Task<ApiResponse<bool>> UndoAsync(int operationId, int operatorId, string? reason = null)
    {
        try
        {
            var operation = await _dbContext.UndoableOperations.FindAsync(operationId);
            if (operation == null)
                return ApiResponse<bool>.Fail("操作记录不存在");

            if (operation.OperatorId != operatorId)
                return ApiResponse<bool>.Fail("只能撤销自己的操作");

            if (operation.IsUndone)
                return ApiResponse<bool>.Fail("该操作已被撤销");

            if (DateTime.Now >= operation.UndoDeadline)
                return ApiResponse<bool>.Fail("已超过撤销时限（5分钟）");

            var snapshot = JsonSerializer.Deserialize<OperationSnapshot>(operation.SnapshotData ?? "{}");
            if (snapshot == null)
                return ApiResponse<bool>.Fail("快照数据无效");

            var undoReason = reason ?? "用户主动撤销";

            // 执行撤销
            var undoResult = await ExecuteUndoAsync(operation.OperationType, operation.EntityType, operation.EntityId, snapshot);

            if (undoResult)
            {
                operation.IsUndone = true;
                operation.UndoneAt = DateTime.Now;
                await _dbContext.SaveChangesAsync();

                // 追加审计日志 — 撤销操作本身必须被记录
                await _auditTrail.LogDetailAsync(
                    operation.EntityType, operation.EntityId, operation.EntityName,
                    "Undo", operation.Description, $"已撤销（原因：{undoReason}）",
                    "Undo", undoReason, operatorId, 0);

                return ApiResponse<bool>.Ok(true, $"已撤销：{operation.Description}");
            }

            return ApiResponse<bool>.Fail("撤销执行失败");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "撤销操作失败");
            return ApiResponse<bool>.Fail($"撤销失败: {ex.Message}");
        }
    }

    private async Task<bool> ExecuteUndoAsync(string operationType, string entityType, int entityId, OperationSnapshot snapshot)
    {
        switch (entityType)
        {
            case "Order":
                return await UndoOrderAsync(operationType, entityId, snapshot);
            case "Customer":
                return await UndoCustomerAsync(operationType, entityId, snapshot);
            case "Payment":
                return await UndoPaymentAsync(operationType, entityId, snapshot);
            case "Delivery":
                return await UndoDeliveryAsync(operationType, entityId, snapshot);
            default:
                return false;
        }
    }

    private async Task<bool> UndoOrderAsync(string operationType, int orderId, OperationSnapshot snapshot)
    {
        var order = await _dbContext.Orders.FindAsync(orderId);
        if (order == null) return false;

        switch (operationType)
        {
            case "StatusChange":
                if (snapshot.BeforeValues.TryGetValue("Status", out var statusValue) && statusValue != null)
                {
                    var oldStatus = Enum.Parse<OrderStatus>(statusValue.ToString()!);
                    order.Status = oldStatus;
                    order.UpdatedAt = DateTime.Now;
                    await _dbContext.SaveChangesAsync();
                    return true;
                }
                break;

            case "Assign":
                if (snapshot.BeforeValues.TryGetValue("DeliveryPersonId", out var dpValue))
                {
                    order.DeliveryPersonId = null;
                    order.Status = OrderStatus.Pending;
                    order.UpdatedAt = DateTime.Now;
                    await _dbContext.SaveChangesAsync();
                    return true;
                }
                break;

            case "Delete":
                // 恢复软删除的订单
                if (order.Status == OrderStatus.Cancelled)
                {
                    order.Status = OrderStatus.Pending;
                    order.CancelReason = null;
                    order.UpdatedAt = DateTime.Now;
                    await _dbContext.SaveChangesAsync();
                    return true;
                }
                break;
        }

        return false;
    }

    private async Task<bool> UndoCustomerAsync(string operationType, int customerId, OperationSnapshot snapshot)
    {
        var customer = await _dbContext.Customers.FindAsync(customerId);
        if (customer == null) return false;

        switch (operationType)
        {
            case "Delete":
                customer.Status = CustomerStatus.Active;
                customer.UpdatedAt = DateTime.Now;
                await _dbContext.SaveChangesAsync();
                return true;
        }

        return false;
    }

    private async Task<bool> UndoPaymentAsync(string operationType, int paymentId, OperationSnapshot snapshot)
    {
        var payment = await _dbContext.PaymentRecords
            .Include(p => p.Allocations)
            .FirstOrDefaultAsync(p => p.Id == paymentId);
        if (payment == null) return false;

        switch (operationType)
        {
            case "Create":
                // 撤销新建的收款
                foreach (var alloc in payment.Allocations)
                {
                    var order = await _dbContext.Orders.FindAsync(alloc.OrderId);
                    if (order != null)
                    {
                        order.ReceivedAmount -= alloc.Amount;
                        if (order.ReceivedAmount < 0) order.ReceivedAmount = 0;
                        UpdatePaymentStatus(order);
                        order.UpdatedAt = DateTime.Now;
                    }
                }

                // 如果直接关联了订单
                if (payment.OrderId.HasValue && !payment.Allocations.Any())
                {
                    var order = await _dbContext.Orders.FindAsync(payment.OrderId.Value);
                    if (order != null)
                    {
                        order.ReceivedAmount -= payment.Amount;
                        if (order.ReceivedAmount < 0) order.ReceivedAmount = 0;
                        UpdatePaymentStatus(order);
                        order.UpdatedAt = DateTime.Now;
                    }
                }

                _dbContext.PaymentAllocations.RemoveRange(payment.Allocations);
                _dbContext.PaymentRecords.Remove(payment);
                await _dbContext.SaveChangesAsync();
                return true;
        }

        return false;
    }

    private async Task<bool> UndoDeliveryAsync(string operationType, int deliveryPersonId, OperationSnapshot snapshot)
    {
        // 撤销配送员分配
        if (snapshot.BeforeValues.TryGetValue("Status", out var statusValue) && statusValue != null)
        {
            var dp = await _dbContext.DeliveryPersons.FindAsync(deliveryPersonId);
            if (dp != null)
            {
                var oldStatus = Enum.Parse<DeliveryPersonStatus>(statusValue.ToString()!);
                dp.Status = oldStatus;
                await _dbContext.SaveChangesAsync();
                return true;
            }
        }
        return false;
    }

    private static void UpdatePaymentStatus(Order order)
    {
        if (order.ReceivedAmount >= order.TotalAmount - order.DiscountAmount)
            order.PaymentStatus = PaymentStatus.Paid;
        else if (order.ReceivedAmount > 0)
            order.PaymentStatus = PaymentStatus.PartialPaid;
        else
            order.PaymentStatus = PaymentStatus.Unpaid;
    }

    /// <summary>
    /// 清理过期的撤销记录
    /// </summary>
    public async Task CleanupExpiredAsync()
    {
        try
        {
            var expired = await _dbContext.UndoableOperations
                .Where(o => o.UndoDeadline < DateTime.Now.AddDays(-1))
                .ToListAsync();

            if (expired.Any())
            {
                _dbContext.UndoableOperations.RemoveRange(expired);
                await _dbContext.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "清理过期撤销记录失败");
        }
    }
}
