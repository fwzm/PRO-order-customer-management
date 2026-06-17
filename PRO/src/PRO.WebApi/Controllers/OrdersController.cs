using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Enums;
using PRO.Infrastructure.Services;
using PRO.WebApi.Authorization;
using PRO.WebApi.Filters;
using Swashbuckle.AspNetCore.Annotations;

namespace PRO.WebApi.Controllers;

/// <summary>
/// 订单管理控制器
/// </summary>
[Authorize]
public class OrdersController(
    IOrderService orderService,
    ICustomerService customerService,
    IDeliveryPersonService deliveryPersonService,
    IOrderDistributionService distributionService,
    AuditService auditService,
    IAuthorizationService authorizationService,
    BranchDataFilter branchFilter) : BaseApiController
{
    private readonly IOrderService _orderService = orderService;
    private readonly ICustomerService _customerService = customerService;
    private readonly IDeliveryPersonService _deliveryPersonService = deliveryPersonService;
    private readonly IOrderDistributionService _distributionService = distributionService;
    private readonly AuditService _auditService = auditService;
    private readonly IAuthorizationService _authorizationService = authorizationService;
    private readonly BranchDataFilter _branchFilter = branchFilter;

    /// <summary>
    /// 获取订单列表
    /// </summary>
    [HttpGet]
    [Authorize(Policy = PermissionPolicies.OrderView)]
    [SwaggerOperation(Summary = "获取订单列表")]
    public async Task<IActionResult> GetList([FromQuery] PagedRequest request, [FromQuery] OrderStatus? status, [FromQuery] PaymentStatus? paymentStatus)
    {
        var branchId = _branchFilter.ApplyBranchScope(null);
        if (!_branchFilter.IsHeadquartersAdmin() && !branchId.HasValue)
            return BranchForbidden("当前用户未分配分公司");

        var result = await _orderService.GetListAsync(request, branchId, status, paymentStatus);
        return Ok(result);
    }

    /// <summary>
    /// 获取订单详情
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Policy = PermissionPolicies.OrderView)]
    [SwaggerOperation(Summary = "获取订单详情")]
    public async Task<IActionResult> GetById(int id)
    {
        var (failure, order) = await GetAccessibleOrderAsync(id);
        if (failure != null)
            return failure;

        return Ok(ApiResponse<OrderDetailDto>.Ok(order!));
    }

    /// <summary>
    /// 创建订单
    /// </summary>
    [HttpPost]
    [Authorize(Policy = PermissionPolicies.OrderCreate)]
    [SwaggerOperation(Summary = "创建订单")]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request)
    {
        var customerValidation = await EnsureCustomerAccessibleAsync(request.CustomerId);
        if (customerValidation != null)
            return customerValidation;

        var result = await _orderService.CreateAsync(request, CurrentEmployeeId);

        if (result.Success && result.Data > 0)
        {
            var createdOrder = await _orderService.GetByIdAsync(result.Data);
            var orderNo = createdOrder.Success && createdOrder.Data != null
                ? createdOrder.Data.OrderNo
                : $"订单ID:{result.Data}";
            var amount = createdOrder.Success && createdOrder.Data != null
                ? createdOrder.Data.TotalAmount
                : request.Items.Sum(i => i.UnitPrice * i.Quantity);

            await _auditService.LogOrderCreateAsync(CurrentEmployeeId, result.Data, orderNo, amount);
        }

        return result.Success
            ? CreatedAtAction(nameof(GetById), new { id = result.Data }, result)
            : BadRequest(result);
    }

    /// <summary>
    /// 更新订单
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Policy = PermissionPolicies.OrderEdit)]
    [SwaggerOperation(Summary = "更新订单")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateOrderRequest request)
    {
        var (Failure, Order) = await GetAccessibleOrderAsync(id);
        if (Failure != null)
            return Failure;

        var customerValidation = await EnsureCustomerAccessibleAsync(request.CustomerId);
        if (customerValidation != null)
            return customerValidation;

        request.Id = id;
        var result = await _orderService.UpdateAsync(request, CurrentEmployeeId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// 删除订单
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = PermissionPolicies.OrderDelete)]
    [SwaggerOperation(Summary = "删除订单")]
    public async Task<IActionResult> Delete(int id)
    {
        var (failure, order) = await GetAccessibleOrderAsync(id);
        if (failure != null)
            return failure;

        var result = await _orderService.DeleteAsync(id);

        if (result.Success)
        {
            await _auditService.LogOrderDeleteAsync(CurrentEmployeeId, id, order!.OrderNo);
        }

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// 分配配送员
    /// </summary>
    [HttpPost("{id}/assign")]
    [Authorize(Policy = PermissionPolicies.DeliveryManualAssign)]
    [SwaggerOperation(Summary = "分配配送员")]
    public async Task<IActionResult> Assign(int id, [FromBody] AssignOrderRequest request)
    {
        request.OrderId = id;
        var (orderFailure, order) = await GetAccessibleOrderAsync(id);
        if (orderFailure != null)
            return orderFailure;

        var (deliveryFailure, deliveryPerson) = await GetAccessibleDeliveryPersonAsync(request.DeliveryPersonId);
        if (deliveryFailure != null)
            return deliveryFailure;

        if (deliveryPerson!.BranchId != order!.BranchId)
            return BadRequest(ApiResponse<object>.Fail("配送员与订单不属于同一分公司"));

        var result = await _orderService.AssignAsync(request, CurrentEmployeeId);

        if (result.Success)
        {
            await _auditService.LogOrderAssignAsync(
                CurrentEmployeeId,
                id,
                order.OrderNo,
                request.DeliveryPersonId,
                deliveryPerson.Name);
        }

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// 更新订单状态
    /// </summary>
    [HttpPost("{id}/status")]
    [SwaggerOperation(Summary = "更新订单状态")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateOrderStatusRequest request)
    {
        var policy = request.NewStatus == OrderStatus.Cancelled
            ? PermissionPolicies.OrderCancel
            : PermissionPolicies.OrderEdit;
        if (!(await _authorizationService.AuthorizeAsync(User, policy)).Succeeded)
            return Forbid();

        request.OrderId = id;
        var (orderFailure, order) = await GetAccessibleOrderAsync(id);
        if (orderFailure != null)
            return orderFailure;

        var result = await _orderService.UpdateStatusAsync(request, CurrentEmployeeId);

        if (result.Success)
        {
            if (request.NewStatus == OrderStatus.Cancelled)
            {
                await _auditService.LogOrderCancelAsync(CurrentEmployeeId, id, order!.OrderNo, request.Reason ?? "未填写原因");
            }
            else
            {
                await _auditService.LogOrderStatusChangeAsync(
                    CurrentEmployeeId, id, order!.OrderNo,
                    order.Status, request.NewStatus, request.Reason);
            }
        }

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// 确认草稿
    /// </summary>
    [HttpPost("{id}/confirm-draft")]
    [Authorize(Policy = PermissionPolicies.OrderEdit)]
    [SwaggerOperation(Summary = "确认草稿订单")]
    public async Task<IActionResult> ConfirmDraft(int id)
    {
        var (orderFailure, order) = await GetAccessibleOrderAsync(id);
        if (orderFailure != null)
            return orderFailure;

        var result = await _orderService.ConfirmDraftAsync(id, CurrentEmployeeId);

        if (result.Success)
        {
            await _auditService.LogOrderStatusChangeAsync(
                CurrentEmployeeId, id, order!.OrderNo,
                OrderStatus.Draft, OrderStatus.Pending, "确认草稿");
        }

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// 批量分配
    /// </summary>
    [HttpPost("batch-assign")]
    [Authorize(Policy = PermissionPolicies.OrderBatchAssign)]
    [SwaggerOperation(Summary = "批量分配配送员")]
    public async Task<IActionResult> BatchAssign([FromBody] BatchAssignRequest request)
    {
        if (request.OrderIds.Count == 0)
            return BadRequest(ApiResponse<object>.Fail("请选择需要分配的订单"));

        var (deliveryFailure, deliveryPerson) = await GetAccessibleDeliveryPersonAsync(request.DeliveryPersonId);
        if (deliveryFailure != null)
            return deliveryFailure;

        var orders = new List<OrderDetailDto>();
        foreach (var orderId in request.OrderIds.Distinct())
        {
            var (orderFailure, order) = await GetAccessibleOrderAsync(orderId);
            if (orderFailure != null)
                return orderFailure;

            if (order!.BranchId != deliveryPerson!.BranchId)
                return BadRequest(ApiResponse<object>.Fail($"订单 {order.OrderNo} 与配送员不属于同一分公司"));

            orders.Add(order);
        }

        var batchResult = await _orderService.BatchAssignAsync(orders.Select(o => o.Id), request.DeliveryPersonId, CurrentEmployeeId);
        var successCount = batchResult.Data?.SuccessCount ?? 0;

        await _auditService.LogOrderBatchAssignAsync(CurrentEmployeeId, successCount, deliveryPerson!.Name);

        return batchResult.Success ? Ok(batchResult) : BadRequest(batchResult);
    }

    /// <summary>
    /// 批量状态变更
    /// </summary>
    [HttpPost("batch-status")]
    [SwaggerOperation(Summary = "批量变更订单状态")]
    public async Task<IActionResult> BatchStatusChange([FromBody] BatchStatusChangeRequest request)
    {
        if (request.OrderIds.Count == 0)
            return BadRequest(ApiResponse<object>.Fail("请选择需要变更状态的订单"));

        var policy = request.NewStatus == OrderStatus.Cancelled
            ? PermissionPolicies.OrderCancel
            : PermissionPolicies.OrderBatchStatusChange;
        if (!(await _authorizationService.AuthorizeAsync(User, policy)).Succeeded)
            return Forbid();

        var orderIds = request.OrderIds.Distinct().ToList();
        foreach (var orderId in orderIds)
        {
            var (orderFailure, _) = await GetAccessibleOrderAsync(orderId);
            if (orderFailure != null)
                return orderFailure;
        }

        var batchResult = await _orderService.BatchUpdateStatusAsync(orderIds, request.NewStatus, CurrentEmployeeId, request.Reason);
        var successCount = batchResult.Data?.SuccessCount ?? 0;

        await _auditService.LogOrderBatchStatusChangeAsync(
            CurrentEmployeeId,
            successCount,
            orderIds.Count,
            request.NewStatus,
            request.Reason);

        return batchResult.Success ? Ok(batchResult) : BadRequest(batchResult);
    }

    private async Task<(IActionResult? Failure, OrderDetailDto? Order)> GetAccessibleOrderAsync(int orderId)
    {
        var result = await _orderService.GetByIdAsync(orderId);
        if (!result.Success || result.Data == null)
            return (NotFound(result), null);

        if (!_branchFilter.IsAccessible(result.Data.BranchId))
            return (BranchForbidden("无权访问其他分公司的订单"), null);

        return (null, result.Data);
    }

    private async Task<IActionResult?> EnsureCustomerAccessibleAsync(int customerId)
    {
        var result = await _customerService.GetByIdAsync(customerId);
        if (!result.Success || result.Data == null)
            return BadRequest(ApiResponse<object>.Fail("客户不存在"));

        if (!_branchFilter.IsAccessible(result.Data.BranchId))
            return BranchForbidden("无权使用其他分公司的客户创建或修改订单");

        return null;
    }

    private async Task<(IActionResult? Failure, DeliveryPersonListItem? DeliveryPerson)> GetAccessibleDeliveryPersonAsync(int deliveryPersonId)
    {
        var result = await _deliveryPersonService.GetByIdAsync(deliveryPersonId);
        if (!result.Success || result.Data == null)
            return (BadRequest(ApiResponse<object>.Fail("配送员不存在")), null);

        if (!_branchFilter.IsAccessible(result.Data.BranchId))
            return (BranchForbidden("无权使用其他分公司的配送员"), null);

        return (null, result.Data);
    }

    /// <summary>
    /// 克隆订单
    /// </summary>
    [HttpPost("{id}/clone")]
    [Authorize(Policy = PermissionPolicies.OrderCreate)]
    [SwaggerOperation(Summary = "克隆订单")]
    public async Task<IActionResult> CloneOrder(int id)
    {
        var (failure, existing) = await GetAccessibleOrderAsync(id);
        if (failure != null)
            return failure;

        // Create clone request from existing order
        var cloneRequest = new CreateOrderRequest
        {
            CustomerId = existing!.CustomerId,
            DeliveryAddress = existing.DeliveryAddress,
            DeliveryLongitude = existing.DeliveryLongitude,
            DeliveryLatitude = existing.DeliveryLatitude,
            Items = existing.Items?.Select(i => new CreateOrderItemRequest
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                Remark = i.Remark
            }).ToList() ?? [],
            Remark = $"克隆自订单 {existing.OrderNo}"
        };

        var result = await _orderService.CreateAsync(cloneRequest, CurrentEmployeeId);

        if (result.Success && result.Data > 0)
        {
            var createdOrder = await _orderService.GetByIdAsync(result.Data);
            var orderNo = createdOrder.Success && createdOrder.Data != null
                ? createdOrder.Data.OrderNo
                : $"订单ID:{result.Data}";
            var amount = createdOrder.Success && createdOrder.Data != null
                ? createdOrder.Data.TotalAmount
                : existing.TotalAmount;

            await _auditService.LogOrderCreateAsync(CurrentEmployeeId, result.Data, orderNo, amount);
        }

        return result.Success
            ? CreatedAtAction(nameof(GetById), new { id = result.Data }, result)
            : BadRequest(result);
    }

    // ─── 配送管理端点 ────────────────────────────────────────

    /// <summary>
    /// 获取待分配订单
    /// </summary>
    [HttpGet("pending")]
    [Authorize(Policy = PermissionPolicies.OrderView)]
    [SwaggerOperation(Summary = "获取待分配订单")]
    public async Task<IActionResult> GetPendingOrders([FromQuery] int branchId)
    {
        var scopedBranchId = _branchFilter.ApplyBranchScope(branchId);
        if (!_branchFilter.IsHeadquartersAdmin() && !scopedBranchId.HasValue)
            return BranchForbidden("当前用户未分配分公司");

        var result = await _distributionService.GetPendingOrdersAsync(scopedBranchId ?? branchId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// 自动分配订单
    /// </summary>
    [HttpPost("auto-assign")]
    [Authorize(Policy = PermissionPolicies.DeliveryAutoAssign)]
    [SwaggerOperation(Summary = "自动分配订单")]
    public async Task<IActionResult> AutoAssign([FromQuery] int branchId, [FromQuery] string algorithm = "region_load_distance")
    {
        var scopedBranchId = _branchFilter.ApplyBranchScope(branchId);
        if (!_branchFilter.IsHeadquartersAdmin() && !scopedBranchId.HasValue)
            return BranchForbidden("当前用户未分配分公司");

        var result = await _distributionService.AutoAssignAsync(scopedBranchId ?? branchId, algorithm);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// 获取最优分配方案（预览）
    /// </summary>
    [HttpGet("optimal-assignment")]
    [Authorize(Policy = PermissionPolicies.DeliveryView)]
    [SwaggerOperation(Summary = "获取最优分配方案")]
    public async Task<IActionResult> GetOptimalAssignment([FromQuery] int branchId)
    {
        var scopedBranchId = _branchFilter.ApplyBranchScope(branchId);
        if (!_branchFilter.IsHeadquartersAdmin() && !scopedBranchId.HasValue)
            return BranchForbidden("当前用户未分配分公司");

        var result = await _distributionService.GetOptimalAssignmentAsync(scopedBranchId ?? branchId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

}

public class BatchAssignRequest
{
    public List<int> OrderIds { get; set; } = [];
    public int DeliveryPersonId { get; set; }
}

public class BatchStatusChangeRequest
{
    public List<int> OrderIds { get; set; } = [];
    public OrderStatus NewStatus { get; set; }
    public string? Reason { get; set; }
}
