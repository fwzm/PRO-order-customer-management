using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Enums;
using PRO.WebApi.Authorization;
using PRO.WebApi.Filters;
using Swashbuckle.AspNetCore.Annotations;

namespace PRO.WebApi.Controllers;

/// <summary>
/// 订单管理控制器（V1 - 已废弃，请使用 api/Orders）
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Obsolete("请使用 OrdersController (api/Orders)。此控制器将在未来版本移除。")]
[ApiExplorerSettings(IgnoreApi = true)]
public class OrderController(
    IOrderService orderService,
    ICustomerService customerService,
    IDeliveryPersonService deliveryPersonService,
    IOrderDistributionService distributionService,
    IAuthorizationService authorizationService,
    BranchDataFilter branchFilter) : ControllerBase
{
    private readonly IOrderService _orderService = orderService;
    private readonly ICustomerService _customerService = customerService;
    private readonly IDeliveryPersonService _deliveryPersonService = deliveryPersonService;
    private readonly IOrderDistributionService _distributionService = distributionService;
    private readonly IAuthorizationService _authorizationService = authorizationService;
    private readonly BranchDataFilter _branchFilter = branchFilter;

    /// <summary>获取订单列表（分页+筛选）</summary>
    [HttpGet]
    [Authorize(Policy = PermissionPolicies.OrderView)]
    public async Task<IActionResult> GetList([FromQuery] PagedRequest request,
        [FromQuery] int? branchId, [FromQuery] OrderStatus? status, [FromQuery] PaymentStatus? paymentStatus)
    {
        branchId = _branchFilter.ApplyBranchScope(branchId);
        if (!_branchFilter.IsHeadquartersAdmin() && !branchId.HasValue)
            return BranchForbidden("当前用户未分配分公司");

        var result = await _orderService.GetListAsync(request, branchId, status, paymentStatus);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>获取订单详情</summary>
    [HttpGet("{id:int}")]
    [Authorize(Policy = PermissionPolicies.OrderView)]
    public async Task<IActionResult> GetById(int id)
    {
        var (failure, order) = await GetAccessibleOrderAsync(id);
        if (failure != null)
            return failure;

        return Ok(ApiResponse<OrderDetailDto>.Ok(order!));
    }

    /// <summary>创建订单</summary>
    [HttpPost]
    [Authorize(Policy = PermissionPolicies.OrderCreate)]
    [ServiceFilter(typeof(AuditLogFilter))]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request)
    {
        var customerValidation = await EnsureCustomerAccessibleAsync(request.CustomerId);
        if (customerValidation != null)
            return customerValidation;

        var createdById = GetCurrentUserId();
        var result = await _orderService.CreateAsync(request, createdById);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>更新订单</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = PermissionPolicies.OrderEdit)]
    [ServiceFilter(typeof(AuditLogFilter))]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateOrderRequest request)
    {
        var (Failure, Order) = await GetAccessibleOrderAsync(id);
        if (Failure != null)
            return Failure;

        var customerValidation = await EnsureCustomerAccessibleAsync(request.CustomerId);
        if (customerValidation != null)
            return customerValidation;

        request.Id = id;
        var modifiedById = GetCurrentUserId();
        var result = await _orderService.UpdateAsync(request, modifiedById);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>删除订单</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = PermissionPolicies.OrderDelete)]
    [ServiceFilter(typeof(AuditLogFilter))]
    public async Task<IActionResult> Delete(int id)
    {
        var (Failure, Order) = await GetAccessibleOrderAsync(id);
        if (Failure != null)
            return Failure;

        var result = await _orderService.DeleteAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>分配配送员</summary>
    [HttpPost("{id:int}/assign")]
    [Authorize(Policy = PermissionPolicies.DeliveryManualAssign)]
    [ServiceFilter(typeof(AuditLogFilter))]
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

        var assignedById = GetCurrentUserId();
        var result = await _orderService.AssignAsync(request, assignedById);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>更新订单状态</summary>
    [HttpPut("{id:int}/status")]
    [ServiceFilter(typeof(AuditLogFilter))]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateOrderStatusRequest request)
    {
        var policy = request.NewStatus == OrderStatus.Cancelled
            ? PermissionPolicies.OrderCancel
            : PermissionPolicies.OrderEdit;
        if (!(await _authorizationService.AuthorizeAsync(User, policy)).Succeeded)
            return Forbid();

        request.OrderId = id;
        var (Failure, Order) = await GetAccessibleOrderAsync(id);
        if (Failure != null)
            return Failure;

        var modifiedById = GetCurrentUserId();
        var result = await _orderService.UpdateStatusAsync(request, modifiedById);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>确认草稿</summary>
    [HttpPost("{id:int}/confirm")]
    [Authorize(Policy = PermissionPolicies.OrderEdit)]
    [ServiceFilter(typeof(AuditLogFilter))]
    public async Task<IActionResult> ConfirmDraft(int id)
    {
        var (Failure, Order) = await GetAccessibleOrderAsync(id);
        if (Failure != null)
            return Failure;

        var modifiedById = GetCurrentUserId();
        var result = await _orderService.ConfirmDraftAsync(id, modifiedById);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>导出订单Excel</summary>
    [HttpGet("export")]
    [Authorize(Policy = PermissionPolicies.OrderExport)]
    public async Task<IActionResult> Export([FromQuery] PagedRequest request,
        [FromQuery] int? branchId, [FromQuery] bool forGaode = false)
    {
        branchId = _branchFilter.ApplyBranchScope(branchId);
        if (!_branchFilter.IsHeadquartersAdmin() && !branchId.HasValue)
            return BranchForbidden("当前用户未分配分公司");

        var result = await _orderService.ExportToExcelAsync(request, branchId, forGaode);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ─── 配送管理端点 ────────────────────────────────────────

    /// <summary>获取待分配订单</summary>
    [HttpGet("pending")]
    [Authorize(Policy = PermissionPolicies.OrderView)]
    public async Task<IActionResult> GetPendingOrders([FromQuery] int branchId)
    {
        var branchFailure = ApplyRequiredBranchScope(ref branchId);
        if (branchFailure != null)
            return branchFailure;

        var result = await _distributionService.GetPendingOrdersAsync(branchId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>自动分配订单</summary>
    [HttpPost("auto-assign")]
    [Authorize(Policy = PermissionPolicies.DeliveryAutoAssign)]
    [ServiceFilter(typeof(AuditLogFilter))]
    public async Task<IActionResult> AutoAssign([FromQuery] int branchId, [FromQuery] string algorithm = "region_load_distance")
    {
        var branchFailure = ApplyRequiredBranchScope(ref branchId);
        if (branchFailure != null)
            return branchFailure;

        var result = await _distributionService.AutoAssignAsync(branchId, algorithm);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>获取最优分配方案（预览）</summary>
    [HttpGet("optimal-assignment")]
    [Authorize(Policy = PermissionPolicies.DeliveryView)]
    public async Task<IActionResult> GetOptimalAssignment([FromQuery] int branchId)
    {
        var branchFailure = ApplyRequiredBranchScope(ref branchId);
        if (branchFailure != null)
            return branchFailure;

        var result = await _distributionService.GetOptimalAssignmentAsync(branchId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ─── 辅助 ────────────────────────────────────────────────

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst("EmployeeId");
        return claim != null && int.TryParse(claim.Value, out var id) ? id : 0;
    }

    private async Task<(IActionResult? Failure, OrderDetailDto? Order)> GetAccessibleOrderAsync(int orderId)
    {
        var result = await _orderService.GetByIdAsync(orderId);
        if (!result.Success || result.Data == null)
            return (BadRequest(result), null);

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

    private IActionResult? ApplyRequiredBranchScope(ref int branchId)
    {
        var scopedBranchId = _branchFilter.ApplyBranchScope(branchId);
        if (!scopedBranchId.HasValue)
            return BranchForbidden("当前用户未分配分公司");

        branchId = scopedBranchId.Value;
        return null;
    }

    private IActionResult BranchForbidden(string message)
    {
        return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(message));
    }
}
