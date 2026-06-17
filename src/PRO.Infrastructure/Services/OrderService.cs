using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 订单服务实现。所有订单状态、订单号和库存变更规则统一在服务层执行。
/// </summary>
public class OrderService : IOrderService
{
    private const int MaxSaveRetries = 5;

    private readonly ProDbContext _dbContext;
    private readonly MemoryCacheService _cache;
    private readonly InventoryService _inventoryService;
    private readonly IAuditTrailService _auditTrail;
    private readonly BusinessConfigService _configService;
    private readonly IOrderNumberService _orderNumberService;

    public OrderService(
        ProDbContext dbContext,
        MemoryCacheService cache,
        InventoryService inventoryService,
        IAuditTrailService auditTrail,
        BusinessConfigService configService,
        IOrderNumberService orderNumberService)
    {
        _dbContext = dbContext;
        _cache = cache;
        _inventoryService = inventoryService;
        _auditTrail = auditTrail;
        _configService = configService;
        _orderNumberService = orderNumberService;
    }

    public async Task<ApiResponse<PagedResult<OrderListItem>>> GetListAsync(PagedRequest request, int? branchId = null, OrderStatus? status = null, PaymentStatus? paymentStatus = null)
    {
        try
        {
            var query = _dbContext.Orders.AsNoTracking()
                .Include(o => o.Customer)
                .Include(o => o.Branch)
                .Include(o => o.DeliveryPerson)
                .Include(o => o.Creator)
                .AsQueryable();

            if (branchId.HasValue)
                query = query.Where(o => o.BranchId == branchId.Value);
            if (status.HasValue)
                query = query.Where(o => o.Status == status.Value);
            if (paymentStatus.HasValue)
                query = query.Where(o => o.PaymentStatus == paymentStatus.Value);
            if (!string.IsNullOrWhiteSpace(request.Keyword))
                query = query.Where(o => o.OrderNo.Contains(request.Keyword) || (o.Customer != null && o.Customer.Name.Contains(request.Keyword)));

            var cacheKey = CacheKeys.Format(CacheKeys.OrderCount, $"{branchId}_{status}_{paymentStatus}_{request.Keyword?.GetHashCode() ?? 0}");
            var totalCount = await _cache.GetOrCreateAsync(cacheKey, () => query.CountAsync(), TimeSpan.FromMinutes(2));
            var items = await query.OrderByDescending(o => o.CreatedAt)
                .Skip((request.PageIndex - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(o => new OrderListItem
                {
                    Id = o.Id,
                    OrderNo = o.OrderNo,
                    CustomerId = o.CustomerId,
                    CustomerName = o.Customer != null ? o.Customer.Name : "未知",
                    BranchId = o.BranchId,
                    BranchName = o.Branch != null ? o.Branch.Name : "",
                    TotalAmount = o.TotalAmount,
                    ReceivedAmount = o.ReceivedAmount,
                    Status = o.Status,
                    PaymentStatus = o.PaymentStatus,
                    CreatedAt = o.CreatedAt,
                    DeliveryPersonName = o.DeliveryPerson != null ? o.DeliveryPerson.Name : null,
                    CreatedByName = o.Creator != null ? o.Creator.Name : "",
                    DeliveryLongitude = o.DeliveryLongitude,
                    DeliveryLatitude = o.DeliveryLatitude,
                    DeliveryAddress = o.DeliveryAddress,
                    DeliveryTime = o.DeliveryTime
                })
                .ToListAsync();

            foreach (var item in items)
            {
                item.ValidNextStatuses = OrderStatusManager.GetValidNextStatuses(item.Status).ToList();
            }

            return ApiResponse<PagedResult<OrderListItem>>.Ok(new PagedResult<OrderListItem>
            {
                Items = items,
                TotalCount = totalCount,
                PageIndex = request.PageIndex,
                PageSize = request.PageSize
            });
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<OrderListItem>>.Fail($"查询订单列表失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<OrderDetailDto>> GetByIdAsync(int id)
    {
        try
        {
            var order = await _dbContext.Orders.AsNoTracking()
                .Include(o => o.Customer)
                .Include(o => o.Branch)
                .Include(o => o.DeliveryPerson)
                .Include(o => o.Creator)
                .Include(o => o.Items).ThenInclude(i => i.Product)
                .Include(o => o.ModificationRecords).ThenInclude(r => r.ModifiedBy)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
                return BusinessMessages.OrderNotFound.ToResponse<OrderDetailDto>();

            return ApiResponse<OrderDetailDto>.Ok(MapOrderDetail(order));
        }
        catch (Exception ex)
        {
            return ApiResponse<OrderDetailDto>.Fail($"查询订单详情失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<int>> CreateAsync(CreateOrderRequest request, int createdById)
    {
        for (var retry = 0; retry < MaxSaveRetries; retry++)
        {
            IDbContextTransaction? transaction = null;
            try
            {
                var customerBranch = await _dbContext.Customers
                    .AsNoTracking()
                    .Where(c => c.Id == request.CustomerId && c.Status == CustomerStatus.Active)
                    .Select(c => (int?)c.BranchId)
                    .FirstOrDefaultAsync();
                if (!customerBranch.HasValue)
                    return BusinessMessages.CustomerNotFound.ToResponse<int>();

                if (request.DeliveryPersonId.HasValue)
                {
                    var deliveryValidation = await ValidateDeliveryPersonAsync(request.DeliveryPersonId.Value, customerBranch.Value);
                    if (!deliveryValidation.Success)
                        return ApiResponse<int>.Fail(deliveryValidation.Message);
                }

                if (!request.IsDraft)
                {
                    var stockValidation = await ValidateStockAvailabilityAsync(request.Items);
                    if (!stockValidation.Success)
                        return ApiResponse<int>.Fail(stockValidation.Message);
                }

                transaction = await BeginTransactionIfSupportedAsync(System.Data.IsolationLevel.Serializable);
                var now = DateTime.Now;
                var orderNo = await _orderNumberService.GenerateAsync(customerBranch.Value, now);
                var draftExpireMinutes = await _configService.GetOrderDraftExpireMinutesAsync();

                var order = new Order
                {
                    OrderNo = orderNo,
                    CustomerId = request.CustomerId,
                    BranchId = customerBranch.Value,
                    CreatedById = createdById,
                    DeliveryPersonId = request.IsDraft ? null : request.DeliveryPersonId,
                    DeliveryAddress = request.DeliveryAddress,
                    DeliveryLongitude = request.DeliveryLongitude,
                    DeliveryLatitude = request.DeliveryLatitude,
                    DeliveryTime = request.DeliveryTime,
                    Remark = request.Remark,
                    Status = request.IsDraft ? OrderStatus.Draft : request.DeliveryPersonId.HasValue ? OrderStatus.Assigned : OrderStatus.Pending,
                    PaymentStatus = request.PaymentStatus,
                    ReceivedAmount = request.ReceivedAmount,
                    DraftExpireTime = request.IsDraft ? now.AddMinutes(draftExpireMinutes) : null,
                    CreatedAt = now,
                    UpdatedAt = now,
                    LocalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    SyncStatus = SyncStatus.Pending
                };

                AddOrderItems(order, request.Items);
                order.TotalAmount = order.Items.Sum(i => i.Amount);
                _dbContext.Orders.Add(order);

                _dbContext.OrderModificationRecords.Add(new OrderModificationRecord
                {
                    Order = order,
                    ModifiedById = createdById,
                    ModifiedAt = now,
                    Content = "创建订单",
                    ModificationType = request.IsDraft ? "CreateDraft" : "Create"
                });

                await _dbContext.SaveChangesAsync();

                if (!request.IsDraft)
                {
                    await ApplyInventoryDeltaAsync(
                        request.Items.GroupBy(i => i.ProductId).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity)),
                        order.Id,
                        order.OrderNo,
                        createdById,
                        "OrderCreate",
                        $"订单 {order.OrderNo} 创建");
                    await _dbContext.SaveChangesAsync();
                }

                if (transaction != null)
                    await transaction.CommitAsync();

                _cache.RemoveByPrefix(CacheKeys.PrefixDashboard);
                _cache.RemoveByPrefix(CacheKeys.PrefixOrderCount);
                return ApiResponse<int>.Ok(order.Id, "订单创建成功");
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (transaction != null)
                    await transaction.RollbackAsync();
                _dbContext.ChangeTracker.Clear();

                if (retry == MaxSaveRetries - 1)
                    return ApiResponse<int>.Fail("库存数据已被其他用户修改，请刷新后重试");

                Serilog.Log.Warning(ex, "订单创建发生并发冲突，第 {Retry}/{MaxRetries} 次重试", retry + 1, MaxSaveRetries);
                await Task.Delay(100 * (retry + 1));
            }
            catch (DbUpdateException ex) when (IsOrderNoUniqueViolation(ex))
            {
                if (transaction != null)
                    await transaction.RollbackAsync();
                _dbContext.ChangeTracker.Clear();

                if (retry == MaxSaveRetries - 1)
                    return ApiResponse<int>.Fail("订单号生成冲突，已重试多次仍失败，请稍后再试");

                Serilog.Log.Warning(ex, "订单号唯一约束冲突，第 {Retry}/{MaxRetries} 次重试", retry + 1, MaxSaveRetries);
                await Task.Delay(50 * (retry + 1));
            }
            catch (Exception ex)
            {
                if (transaction != null)
                    await transaction.RollbackAsync();
                _dbContext.ChangeTracker.Clear();
                return ApiResponse<int>.Fail($"创建订单失败: {ex.Message}");
            }
            finally
            {
                if (transaction != null)
                    await transaction.DisposeAsync();
            }
        }

        return ApiResponse<int>.Fail("创建订单失败：超过最大重试次数");
    }

    public async Task<ApiResponse<bool>> UpdateAsync(UpdateOrderRequest request, int modifiedById)
    {
        for (var retry = 0; retry < MaxSaveRetries; retry++)
        {
            IDbContextTransaction? transaction = null;
            try
            {
                var order = await _dbContext.Orders
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.Id == request.Id);
                if (order == null)
                    return BusinessMessages.OrderNotFound.ToResponse();

                var customerBranch = await _dbContext.Customers
                    .AsNoTracking()
                    .Where(c => c.Id == request.CustomerId && c.Status == CustomerStatus.Active)
                    .Select(c => (int?)c.BranchId)
                    .FirstOrDefaultAsync();
                if (!customerBranch.HasValue)
                    return BusinessMessages.CustomerNotFound.ToResponse();
                if (customerBranch.Value != order.BranchId)
                    return ApiResponse<bool>.Fail("客户与订单不属于同一分公司，无法修改订单客户");

                if (request.DeliveryPersonId.HasValue)
                {
                    var deliveryValidation = await ValidateDeliveryPersonAsync(request.DeliveryPersonId.Value, order.BranchId);
                    if (!deliveryValidation.Success)
                        return ApiResponse<bool>.Fail(deliveryValidation.Message);
                }

                var previousStatus = order.Status;
                if (request.DeliveryPersonId.HasValue && !order.DeliveryPersonId.HasValue && order.Status == OrderStatus.Pending)
                {
                    if (!OrderStatusManager.IsValidTransition(order.Status, OrderStatus.Assigned))
                        return InvalidStatusResponse(order.Status, OrderStatus.Assigned);
                    order.Status = OrderStatus.Assigned;
                }

                var oldQuantities = order.Status == OrderStatus.Draft
                    ? new Dictionary<int, int>()
                    : GroupQuantities(order.Items);
                var newQuantities = order.Status == OrderStatus.Draft
                    ? new Dictionary<int, int>()
                    : GroupQuantities(request.Items);
                var delta = CalculateDelta(oldQuantities, newQuantities);
                var stockValidation = await ValidatePositiveDeltaAvailabilityAsync(delta);
                if (!stockValidation.Success)
                    return ApiResponse<bool>.Fail(stockValidation.Message);

                transaction = await BeginTransactionIfSupportedAsync();
                var now = DateTime.Now;
                order.CustomerId = request.CustomerId;
                order.DeliveryPersonId = request.DeliveryPersonId ?? order.DeliveryPersonId;
                order.DeliveryAddress = request.DeliveryAddress;
                order.DeliveryLongitude = request.DeliveryLongitude;
                order.DeliveryLatitude = request.DeliveryLatitude;
                order.DeliveryTime = request.DeliveryTime;
                order.ReceivedAmount = request.ReceivedAmount;
                order.PaymentStatus = request.PaymentStatus;
                order.Remark = request.Remark;
                order.CancelReason = request.CancelReason;
                order.UpdatedAt = now;
                order.LocalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                order.SyncStatus = SyncStatus.Pending;

                _dbContext.OrderItems.RemoveRange(order.Items);
                order.Items.Clear();
                AddOrderItems(order, request.Items);
                order.TotalAmount = order.Items.Sum(i => i.Amount);

                if (delta.Count > 0)
                {
                    await ApplyInventoryDeltaAsync(delta, order.Id, order.OrderNo, modifiedById, "OrderModify", $"订单 {order.OrderNo} 修改明细");
                }

                _dbContext.OrderModificationRecords.Add(new OrderModificationRecord
                {
                    OrderId = order.Id,
                    ModifiedById = modifiedById,
                    ModifiedAt = now,
                    Content = previousStatus == order.Status ? "订单更新" : $"订单更新，状态变为 {OrderStatusManager.GetStatusName(order.Status)}",
                    ModificationType = "Update"
                });

                await _dbContext.SaveChangesAsync();
                if (transaction != null)
                    await transaction.CommitAsync();

                _cache.RemoveByPrefix(CacheKeys.PrefixDashboard);
                _cache.RemoveByPrefix(CacheKeys.PrefixOrderCount);
                return ApiResponse<bool>.Ok(true, "订单更新成功");
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (transaction != null)
                    await transaction.RollbackAsync();
                _dbContext.ChangeTracker.Clear();

                if (retry == MaxSaveRetries - 1)
                    return ApiResponse<bool>.Fail("库存数据已被其他用户修改，请刷新后重试");

                Serilog.Log.Warning(ex, "订单更新发生并发冲突，第 {Retry}/{MaxRetries} 次重试", retry + 1, MaxSaveRetries);
                await Task.Delay(100 * (retry + 1));
            }
            catch (Exception ex)
            {
                if (transaction != null)
                    await transaction.RollbackAsync();
                _dbContext.ChangeTracker.Clear();
                return ApiResponse<bool>.Fail($"更新订单失败: {ex.Message}");
            }
            finally
            {
                if (transaction != null)
                    await transaction.DisposeAsync();
            }
        }

        return ApiResponse<bool>.Fail("更新订单失败：超过最大重试次数");
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id)
    {
        return await UpdateStatusAsync(new UpdateOrderStatusRequest
        {
            OrderId = id,
            NewStatus = OrderStatus.Cancelled,
            Reason = "用户手动删除"
        }, modifiedById: 0);
    }

    public async Task<ApiResponse<bool>> AssignAsync(AssignOrderRequest request, int assignedById)
    {
        try
        {
            var order = await _dbContext.Orders.FindAsync(request.OrderId);
            if (order == null)
                return BusinessMessages.OrderNotFound.ToResponse();

            var deliveryValidation = await ValidateDeliveryPersonAsync(request.DeliveryPersonId, order.BranchId);
            if (!deliveryValidation.Success)
                return ApiResponse<bool>.Fail(deliveryValidation.Message);

            if (!OrderStatusManager.IsValidTransition(order.Status, OrderStatus.Assigned))
                return InvalidStatusResponse(order.Status, OrderStatus.Assigned);

            var previousStatus = order.Status;
            order.DeliveryPersonId = request.DeliveryPersonId;
            order.Status = OrderStatus.Assigned;
            order.UpdatedAt = DateTime.Now;
            order.LocalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            order.SyncStatus = SyncStatus.Pending;

            _dbContext.OrderModificationRecords.Add(new OrderModificationRecord
            {
                OrderId = order.Id,
                ModifiedById = assignedById,
                ModifiedAt = DateTime.Now,
                Content = OrderStatusManager.GetTransitionDescription(previousStatus, OrderStatus.Assigned),
                ModificationType = "Assign"
            });

            await _dbContext.SaveChangesAsync();
            _cache.RemoveByPrefix(CacheKeys.PrefixDashboard);
            _cache.RemoveByPrefix(CacheKeys.PrefixOrderCount);
            return ApiResponse<bool>.Ok(true, "订单分配成功");
        }
        catch (DbUpdateConcurrencyException ex)
        {
            Serilog.Log.Warning(ex, "订单分配并发冲突");
            return ApiResponse<bool>.Fail("订单数据已被其他用户修改，请刷新后重试");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"分配订单失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> UpdateStatusAsync(UpdateOrderStatusRequest request, int modifiedById)
    {
        for (var retry = 0; retry < MaxSaveRetries; retry++)
        {
            IDbContextTransaction? transaction = null;
            try
            {
                var order = await _dbContext.Orders
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.Id == request.OrderId);
                if (order == null)
                    return BusinessMessages.OrderNotFound.ToResponse();

                if (!OrderStatusManager.IsValidTransition(order.Status, request.NewStatus))
                    return InvalidStatusResponse(order.Status, request.NewStatus);

                if (request.NewStatus == OrderStatus.Cancelled && string.IsNullOrWhiteSpace(request.Reason))
                    return BusinessMessages.OrderCancelReasonRequired.ToResponse();

                transaction = await BeginTransactionIfSupportedAsync();
                var previousStatus = order.Status;
                order.Status = request.NewStatus;
                order.UpdatedAt = DateTime.Now;
                order.LocalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                order.SyncStatus = SyncStatus.Pending;

                if (request.NewStatus == OrderStatus.Completed)
                    order.SignedTime = DateTime.Now;

                if (request.NewStatus == OrderStatus.Cancelled)
                {
                    order.CancelReason = request.Reason;
                    if (previousStatus != OrderStatus.Draft)
                    {
                        var returnDelta = GroupQuantities(order.Items).ToDictionary(kv => kv.Key, kv => -kv.Value);
                        await ApplyInventoryDeltaAsync(returnDelta, order.Id, order.OrderNo, modifiedById, "OrderCancel", $"订单 {order.OrderNo} 取消-库存回补");
                    }
                }

                _dbContext.OrderModificationRecords.Add(new OrderModificationRecord
                {
                    OrderId = order.Id,
                    ModifiedById = modifiedById,
                    ModifiedAt = DateTime.Now,
                    Content = OrderStatusManager.GetTransitionDescription(previousStatus, request.NewStatus) +
                              (string.IsNullOrWhiteSpace(request.Reason) ? "" : $"，原因：{request.Reason}"),
                    ModificationType = "StatusChange"
                });

                await _dbContext.SaveChangesAsync();
                if (transaction != null)
                    await transaction.CommitAsync();

                await _auditTrail.LogOrderStatusChangeAsync(order.Id, order.OrderNo,
                    (int)previousStatus, (int)request.NewStatus,
                    request.Reason ?? "状态变更", modifiedById);
                await _auditTrail.LogDetailAsync("Order", order.Id, order.OrderNo, "Status",
                    previousStatus.ToString(), request.NewStatus.ToString(),
                    "StatusChange", request.Reason ?? "状态变更", modifiedById, order.BranchId);

                _cache.RemoveByPrefix(CacheKeys.PrefixDashboard);
                _cache.RemoveByPrefix(CacheKeys.PrefixOrderCount);
                return ApiResponse<bool>.Ok(true, $"订单状态已更新为：{OrderStatusManager.GetStatusName(request.NewStatus)}");
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (transaction != null)
                    await transaction.RollbackAsync();
                _dbContext.ChangeTracker.Clear();

                if (retry == MaxSaveRetries - 1)
                    return ApiResponse<bool>.Fail("订单或库存数据已被其他用户修改，请刷新后重试");

                Serilog.Log.Warning(ex, "订单状态更新发生并发冲突，第 {Retry}/{MaxRetries} 次重试", retry + 1, MaxSaveRetries);
                await Task.Delay(100 * (retry + 1));
            }
            catch (Exception ex)
            {
                if (transaction != null)
                    await transaction.RollbackAsync();
                _dbContext.ChangeTracker.Clear();
                return ApiResponse<bool>.Fail($"更新订单状态失败: {ex.Message}");
            }
            finally
            {
                if (transaction != null)
                    await transaction.DisposeAsync();
            }
        }

        return ApiResponse<bool>.Fail("更新订单状态失败：超过最大重试次数");
    }

    public async Task<ApiResponse<bool>> ConfirmDraftAsync(int orderId, int modifiedById)
    {
        for (var retry = 0; retry < MaxSaveRetries; retry++)
        {
            IDbContextTransaction? transaction = null;
            try
            {
                var order = await _dbContext.Orders
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.Id == orderId);
                if (order == null)
                    return BusinessMessages.OrderNotFound.ToResponse();

                if (!OrderStatusManager.IsValidTransition(order.Status, OrderStatus.Pending))
                    return InvalidStatusResponse(order.Status, OrderStatus.Pending);

                var stockValidation = await ValidateStockAvailabilityAsync(order.Items);
                if (!stockValidation.Success)
                    return ApiResponse<bool>.Fail(stockValidation.Message);

                transaction = await BeginTransactionIfSupportedAsync();
                var previousStatus = order.Status;
                order.Status = OrderStatus.Pending;
                order.DraftExpireTime = null;
                order.UpdatedAt = DateTime.Now;
                order.LocalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                order.SyncStatus = SyncStatus.Pending;

                await ApplyInventoryDeltaAsync(
                    GroupQuantities(order.Items),
                    order.Id,
                    order.OrderNo,
                    modifiedById,
                    "OrderCreate",
                    $"草稿确认-订单 {order.OrderNo}");

                _dbContext.OrderModificationRecords.Add(new OrderModificationRecord
                {
                    OrderId = order.Id,
                    ModifiedById = modifiedById,
                    ModifiedAt = DateTime.Now,
                    Content = OrderStatusManager.GetTransitionDescription(previousStatus, OrderStatus.Pending),
                    ModificationType = "DraftConfirm"
                });

                await _dbContext.SaveChangesAsync();
                if (transaction != null)
                    await transaction.CommitAsync();

                _cache.RemoveByPrefix(CacheKeys.PrefixDashboard);
                _cache.RemoveByPrefix(CacheKeys.PrefixOrderCount);
                return ApiResponse<bool>.Ok(true, "草稿已确认");
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (transaction != null)
                    await transaction.RollbackAsync();
                _dbContext.ChangeTracker.Clear();

                if (retry == MaxSaveRetries - 1)
                    return ApiResponse<bool>.Fail("库存数据已被其他用户修改，请刷新后重试");

                Serilog.Log.Warning(ex, "草稿确认发生并发冲突，第 {Retry}/{MaxRetries} 次重试", retry + 1, MaxSaveRetries);
                await Task.Delay(100 * (retry + 1));
            }
            catch (Exception ex)
            {
                if (transaction != null)
                    await transaction.RollbackAsync();
                _dbContext.ChangeTracker.Clear();
                return ApiResponse<bool>.Fail($"确认草稿失败: {ex.Message}");
            }
            finally
            {
                if (transaction != null)
                    await transaction.DisposeAsync();
            }
        }

        return ApiResponse<bool>.Fail("确认草稿失败：超过最大重试次数");
    }

    public async Task<ApiResponse<BatchOperationResult>> BatchAssignAsync(IEnumerable<int> orderIds, int deliveryPersonId, int assignedById)
    {
        var result = CreateBatchResult(orderIds);
        if (result.TotalCount == 0)
            return ApiResponse<BatchOperationResult>.Fail("请选择需要分配的订单");

        foreach (var orderId in result.Items.Select(i => i.EntityId))
        {
            var orderNo = await GetOrderNoAsync(orderId);
            var response = await AssignAsync(new AssignOrderRequest { OrderId = orderId, DeliveryPersonId = deliveryPersonId }, assignedById);
            UpdateBatchItem(result, orderId, orderNo, response.Success, response.Message);
        }

        return ApiResponse<BatchOperationResult>.Ok(result, BuildBatchMessage("批量分配", result));
    }

    /// <summary>批量分配（带进度和取消支持）</summary>
    public async Task<ApiResponse<BatchOperationResult>> BatchAssignWithProgressAsync(
        IEnumerable<int> orderIds, int deliveryPersonId, int assignedById,
        BatchOperationContext? context = null, IProgress<BatchOperationProgress>? progress = null)
    {
        return await ExecuteBatchWithProgressAsync(
            orderIds, "批量分配订单",
            async (orderId, ctx) =>
            {
                ctx.CancellationToken.ThrowIfCancellationRequested();
                var orderNo = await GetOrderNoAsync(orderId);
                var response = await AssignAsync(
                    new AssignOrderRequest { OrderId = orderId, DeliveryPersonId = deliveryPersonId }, assignedById);
                return (orderNo, response.Success, response.Message);
            }, context, progress);
    }

    public async Task<ApiResponse<BatchOperationResult>> BatchUpdateStatusAsync(IEnumerable<int> orderIds, OrderStatus newStatus, int modifiedById, string? reason = null)
    {
        var result = CreateBatchResult(orderIds);
        if (result.TotalCount == 0)
            return ApiResponse<BatchOperationResult>.Fail("请选择需要变更状态的订单");

        foreach (var orderId in result.Items.Select(i => i.EntityId))
        {
            var orderNo = await GetOrderNoAsync(orderId);
            var response = await UpdateStatusAsync(new UpdateOrderStatusRequest
            {
                OrderId = orderId,
                NewStatus = newStatus,
                Reason = reason
            }, modifiedById);
            UpdateBatchItem(result, orderId, orderNo, response.Success, response.Message);
        }

        return ApiResponse<BatchOperationResult>.Ok(result, BuildBatchMessage("批量状态变更", result));
    }

    /// <summary>批量状态变更（带进度和取消支持，走 OrderStatusManager）</summary>
    public async Task<ApiResponse<BatchOperationResult>> BatchUpdateStatusWithProgressAsync(
        IEnumerable<int> orderIds, OrderStatus newStatus, int modifiedById, string? reason = null,
        BatchOperationContext? context = null, IProgress<BatchOperationProgress>? progress = null)
    {
        return await ExecuteBatchWithProgressAsync(
            orderIds, "批量状态变更",
            async (orderId, ctx) =>
            {
                ctx.CancellationToken.ThrowIfCancellationRequested();
                var orderNo = await GetOrderNoAsync(orderId);
                var response = await UpdateStatusAsync(new UpdateOrderStatusRequest
                {
                    OrderId = orderId,
                    NewStatus = newStatus,
                    Reason = reason
                }, modifiedById);
                return (orderNo, response.Success, response.Message);
            }, context, progress);
    }

    public async Task<ApiResponse<BatchOperationResult>> BatchConfirmDraftsAsync(IEnumerable<int> orderIds, int modifiedById)
    {
        var result = CreateBatchResult(orderIds);
        if (result.TotalCount == 0)
            return ApiResponse<BatchOperationResult>.Fail("请选择需要确认的草稿订单");

        foreach (var orderId in result.Items.Select(i => i.EntityId))
        {
            var orderNo = await GetOrderNoAsync(orderId);
            var response = await ConfirmDraftAsync(orderId, modifiedById);
            UpdateBatchItem(result, orderId, orderNo, response.Success, response.Message);
        }

        return ApiResponse<BatchOperationResult>.Ok(result, BuildBatchMessage("批量确认草稿", result));
    }

    /// <summary>批量确认草稿（带进度和取消支持）</summary>
    public async Task<ApiResponse<BatchOperationResult>> BatchConfirmDraftsWithProgressAsync(
        IEnumerable<int> orderIds, int modifiedById,
        BatchOperationContext? context = null, IProgress<BatchOperationProgress>? progress = null)
    {
        return await ExecuteBatchWithProgressAsync(
            orderIds, "批量确认草稿",
            async (orderId, ctx) =>
            {
                ctx.CancellationToken.ThrowIfCancellationRequested();
                var orderNo = await GetOrderNoAsync(orderId);
                var response = await ConfirmDraftAsync(orderId, modifiedById);
                return (orderNo, response.Success, response.Message);
            }, context, progress);
    }

    /// <summary>统一批量执行引擎（支持进度报告、取消、失败明细）</summary>
    private async Task<ApiResponse<BatchOperationResult>> ExecuteBatchWithProgressAsync(
        IEnumerable<int> orderIds, string operationName,
        Func<int, BatchOperationContext, Task<(string ItemName, bool Success, string Message)>> operation,
        BatchOperationContext? context = null, IProgress<BatchOperationProgress>? progress = null)
    {
        context ??= new BatchOperationContext { OperationName = operationName };
        var ids = orderIds.Distinct().ToList();
        if (ids.Count == 0)
            return ApiResponse<BatchOperationResult>.Fail("请选择需要操作的项目");

        if (ids.Count > context.MaxBatchSize)
            return ApiResponse<BatchOperationResult>.Fail($"单次批量操作不能超过 {context.MaxBatchSize} 项");

        var progressInfo = context.ProgressInfo;
        progressInfo.Start(operationName, ids.Count);

        var result = CreateBatchResult(ids);
        var failedItems = new List<BatchOperationItemResult>();

        foreach (var orderId in ids)
        {
            try
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                var (itemName, success, message) = await operation(orderId, context);
                UpdateBatchItem(result, orderId, itemName, success, message);

                if (success)
                    progressInfo.RecordSuccess(itemName);
                else
                {
                    progressInfo.RecordFailure(itemName, message, orderId);
                    failedItems.Add(result.Items.First(i => i.EntityId == orderId));
                }

                progress?.Report(progressInfo);
            }
            catch (OperationCanceledException)
            {
                progressInfo.Cancel();
                progress?.Report(progressInfo);
                return ApiResponse<BatchOperationResult>.Ok(result,
                    BuildBatchMessage($"{operationName}（已取消）", result));
            }
            catch (Exception ex)
            {
                var itemName = await GetOrderNoAsync(orderId);
                progressInfo.RecordFailure(itemName, ex.Message, orderId);
                UpdateBatchItem(result, orderId, itemName, false, ex.Message);
                progress?.Report(progressInfo);

                if (!context.ContinueOnError)
                    break;
            }
        }

        progressInfo.Complete();
        progress?.Report(progressInfo);

        if (progressInfo.HasFailures && !context.AllowPartialFailure)
            return ApiResponse<BatchOperationResult>.Fail(
                $"批量操作部分失败：成功 {progressInfo.SuccessCount}，失败 {progressInfo.FailedCount}");

        return ApiResponse<BatchOperationResult>.Ok(result,
            BuildBatchMessage(operationName, result));
    }

    public Task<ApiResponse<string>> ExportToExcelAsync(PagedRequest request, int? branchId = null, bool forGaode = false)
        => Task.FromResult(ApiResponse<string>.Fail("Excel导出功能请在界面层实现"));

    public Task<ApiResponse<string>> ExportDeliveryPlanAsync(List<int> orderIds, string groupName)
        => Task.FromResult(ApiResponse<string>.Fail("配送计划导出功能请在界面层实现"));

    private async Task<ApiResponse<bool>> ValidateDeliveryPersonAsync(int deliveryPersonId, int branchId)
    {
        var deliveryPerson = await _dbContext.DeliveryPersons
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == deliveryPersonId);
        if (deliveryPerson == null)
            return BusinessMessages.DeliveryPersonNotFound.ToResponse();
        if (deliveryPerson.BranchId != branchId)
            return ApiResponse<bool>.Fail("配送员与订单不属于同一分公司");
        if (deliveryPerson.Status != DeliveryPersonStatus.Available)
            return BusinessMessages.DeliveryPersonUnavailable(deliveryPerson.Name).ToResponse();
        if (deliveryPerson.CurrentLoad >= deliveryPerson.MaxLoad)
            return BusinessMessages.DeliveryPersonOverloaded(deliveryPerson.Name).ToResponse();

        return ApiResponse<bool>.Ok(true);
    }

    private async Task<ApiResponse<bool>> ValidateStockAvailabilityAsync(IEnumerable<OrderItem> items)
    {
        return await ValidateStockAvailabilityAsync(items.Select(i => new CreateOrderItemRequest
        {
            ProductId = i.ProductId,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            Remark = i.Remark
        }));
    }

    private async Task<ApiResponse<bool>> ValidateStockAvailabilityAsync(IEnumerable<CreateOrderItemRequest> items)
    {
        var quantities = GroupQuantities(items);
        return await ValidatePositiveDeltaAvailabilityAsync(quantities);
    }

    private async Task<ApiResponse<bool>> ValidatePositiveDeltaAvailabilityAsync(Dictionary<int, int> delta)
    {
        var positive = delta.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);
        if (positive.Count == 0)
            return ApiResponse<bool>.Ok(true);

        var productIds = positive.Keys.ToList();
        var products = await _dbContext.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        foreach (var (productId, quantity) in positive)
        {
            if (!products.TryGetValue(productId, out var product))
                return BusinessMessages.ProductNotFound.ToResponse();
            if (product.Stock < quantity)
                return BusinessMessages.ProductStockInsufficient(product.Name, product.Stock, quantity).ToResponse();
        }

        return ApiResponse<bool>.Ok(true);
    }

    private async Task ApplyInventoryDeltaAsync(Dictionary<int, int> delta, int orderId, string orderNo, int operatorId, string changeType, string reason)
    {
        foreach (var (productId, changeQuantity) in delta.Where(kv => kv.Value != 0))
        {
            var product = await _dbContext.Products.FirstOrDefaultAsync(p => p.Id == productId)
                ?? throw new InvalidOperationException("产品不存在");

            var before = product.Stock;
            var after = before - changeQuantity;
            if (after < 0)
                throw new InvalidOperationException($"产品「{product.Name}」库存不足：当前 {before}，需要 {changeQuantity}");

            product.Stock = after;
            product.UpdatedAt = DateTime.Now;
            product.SyncStatus = SyncStatus.Pending;

            await _inventoryService.AddChangeLogAsync(productId, before, after, changeType, reason, orderId, orderNo, operatorId);
        }
    }

    private static Dictionary<int, int> CalculateDelta(Dictionary<int, int> oldQuantities, Dictionary<int, int> newQuantities)
    {
        var productIds = oldQuantities.Keys.Concat(newQuantities.Keys).Distinct();
        return productIds.ToDictionary(
            productId => productId,
            productId => newQuantities.GetValueOrDefault(productId) - oldQuantities.GetValueOrDefault(productId));
    }

    private static Dictionary<int, int> GroupQuantities(IEnumerable<CreateOrderItemRequest> items)
    {
        return items
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
    }

    private static Dictionary<int, int> GroupQuantities(IEnumerable<OrderItem> items)
    {
        return items
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
    }

    private static void AddOrderItems(Order order, IEnumerable<CreateOrderItemRequest> items)
    {
        foreach (var item in items)
        {
            order.Items.Add(new OrderItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                Amount = item.Quantity * item.UnitPrice,
                Remark = item.Remark
            });
        }
    }

    private static ApiResponse<bool> InvalidStatusResponse(OrderStatus current, OrderStatus target)
    {
        var validStatuses = OrderStatusManager.GetValidNextStatuses(current);
        var validNames = validStatuses.Count == 0
            ? "无可用操作"
            : string.Join("、", validStatuses.Select(OrderStatusManager.GetStatusName));
        return BusinessMessages.OrderStatusInvalid(
            OrderStatusManager.GetStatusName(current),
            OrderStatusManager.GetStatusName(target),
            validNames).ToResponse();
    }

    private async Task<string> GetOrderNoAsync(int orderId)
    {
        return await _dbContext.Orders
            .AsNoTracking()
            .Where(o => o.Id == orderId)
            .Select(o => o.OrderNo)
            .FirstOrDefaultAsync() ?? $"ID:{orderId}";
    }

    private static BatchOperationResult CreateBatchResult(IEnumerable<int> ids)
    {
        var items = ids.Distinct().Select(id => new BatchOperationItemResult
        {
            EntityId = id,
            EntityNo = $"ID:{id}",
            Success = false,
            Message = "未执行"
        }).ToList();

        return new BatchOperationResult
        {
            TotalCount = items.Count,
            Items = items
        };
    }

    private static void UpdateBatchItem(BatchOperationResult result, int entityId, string entityNo, bool success, string message)
    {
        var item = result.Items.First(i => i.EntityId == entityId);
        item.EntityNo = entityNo;
        item.Success = success;
        item.Message = message;
        result.SuccessCount = result.Items.Count(i => i.Success);
    }

    private static string BuildBatchMessage(string actionName, BatchOperationResult result)
    {
        return $"{actionName}完成：成功 {result.SuccessCount} 条，失败 {result.FailedCount} 条";
    }

    private async Task<IDbContextTransaction?> BeginTransactionIfSupportedAsync(System.Data.IsolationLevel isolationLevel = System.Data.IsolationLevel.ReadCommitted)
    {
        if (_dbContext.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true)
            return null;

        return await _dbContext.Database.BeginTransactionAsync(isolationLevel);
    }

    private static bool IsOrderNoUniqueViolation(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("OrderNo", StringComparison.OrdinalIgnoreCase)
            || message.Contains("IX_Orders_OrderNo", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
            || message.Contains("23505", StringComparison.OrdinalIgnoreCase);
    }

    private static OrderDetailDto MapOrderDetail(Order order)
    {
        return new OrderDetailDto
        {
            Id = order.Id,
            OrderNo = order.OrderNo,
            CustomerId = order.CustomerId,
            CustomerName = order.Customer?.Name ?? "",
            CustomerPhone = order.Customer?.Phone,
            CustomerAddress = order.Customer?.Address,
            BranchId = order.BranchId,
            BranchName = order.Branch?.Name ?? "",
            CreatedById = order.CreatedById,
            CreatedByName = order.Creator?.Name ?? "",
            DeliveryPersonId = order.DeliveryPersonId,
            DeliveryPersonName = order.DeliveryPerson?.Name,
            Status = order.Status,
            PaymentStatus = order.PaymentStatus,
            TotalAmount = order.TotalAmount,
            ReceivedAmount = order.ReceivedAmount,
            DeliveryAddress = order.DeliveryAddress,
            DeliveryLongitude = order.DeliveryLongitude,
            DeliveryLatitude = order.DeliveryLatitude,
            DeliveryTime = order.DeliveryTime,
            SignedTime = order.SignedTime,
            CancelReason = order.CancelReason,
            Remark = order.Remark,
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt,
            SettlementId = order.SettlementId,
            DraftExpireTime = order.DraftExpireTime,
            Items = order.Items.Select(i => new OrderItemDto
            {
                Id = i.Id,
                OrderId = i.OrderId,
                ProductId = i.ProductId,
                ProductName = i.Product?.Name ?? "",
                ProductSku = i.Product?.SKU,
                ProductSpec = i.Product?.Specification,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                Amount = i.Amount,
                DiscountType = i.DiscountType,
                DiscountValue = i.DiscountValue,
                Remark = i.Remark
            }).ToList(),
            ModificationRecords = order.ModificationRecords
                .OrderByDescending(r => r.ModifiedAt)
                .Select(r => new OrderModificationRecordDto
                {
                    Id = r.Id,
                    OrderId = r.OrderId,
                    ModifiedById = r.ModifiedById,
                    ModifiedByName = r.ModifiedBy?.Name ?? "",
                    ModifiedByNo = r.ModifiedBy?.EmployeeNo ?? "",
                    ModifiedAt = r.ModifiedAt,
                    Content = r.Content,
                    ModificationType = r.ModificationType
                }).ToList()
        };
    }

    public async Task<List<CustomerListItem>> GetCustomersForSelectionAsync(int branchId)
    {
        return await _dbContext.Customers
            .AsNoTracking()
            .Where(c => c.BranchId == branchId && c.Status == CustomerStatus.Active)
            .OrderBy(c => c.Name)
            .Select(c => new CustomerListItem { Id = c.Id, Name = c.Name, CustomerNo = c.CustomerNo })
            .ToListAsync();
    }

    public async Task<List<ProductListItem>> GetProductsForSelectionAsync()
    {
        return await _dbContext.Products
            .AsNoTracking()
            .Where(p => p.Status == ProductStatus.Active)
            .OrderBy(p => p.Name)
            .Select(p => new ProductListItem { Id = p.Id, Name = p.Name, SKU = p.SKU, AverageSalePrice = p.AverageSalePrice, Unit = p.Unit })
            .ToListAsync();
    }

    public async Task<List<DeliveryPersonListItem>> GetDeliveryPersonsForSelectionAsync(int branchId)
    {
        return await _dbContext.DeliveryPersons
            .AsNoTracking()
            .Where(d => d.BranchId == branchId && d.Status == DeliveryPersonStatus.Available)
            .OrderBy(d => d.Name)
            .Select(d => new DeliveryPersonListItem { Id = d.Id, Name = d.Name, Phone = d.Phone })
            .ToListAsync();
    }

    public async Task<Order?> GetEntityByIdAsync(int id)
    {
        return await _dbContext.Orders.FindAsync(id);
    }
}
