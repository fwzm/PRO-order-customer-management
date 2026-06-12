using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Enums;
using PRO.WebApi.Authorization;
using PRO.WebApi.Filters;

namespace PRO.WebApi.Controllers;

/// <summary>
/// 配送管理控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DeliveryController(
    IDeliveryPersonService deliveryService,
    IOrderDistributionService distributionService,
    IOrderService orderService,
    BranchDataFilter branchFilter) : ControllerBase
{
    private readonly IDeliveryPersonService _deliveryService = deliveryService;
    private readonly IOrderDistributionService _distributionService = distributionService;
    private readonly IOrderService _orderService = orderService;
    private readonly BranchDataFilter _branchFilter = branchFilter;

    /// <summary>获取配送员列表</summary>
    [HttpGet("persons")]
    [Authorize(Policy = PermissionPolicies.DeliveryView)]
    public async Task<IActionResult> GetList([FromQuery] PagedRequest request,
        [FromQuery] int? branchId, [FromQuery] DeliveryPersonStatus? status)
    {
        branchId = _branchFilter.ApplyBranchScope(branchId);
        if (!_branchFilter.IsHeadquartersAdmin() && !branchId.HasValue)
            return BranchForbidden("当前用户未分配分公司");

        var result = await _deliveryService.GetListAsync(request, branchId, status);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>获取可用配送员</summary>
    [HttpGet("persons/available")]
    [Authorize(Policy = PermissionPolicies.DeliveryView)]
    public async Task<IActionResult> GetAvailable([FromQuery] int branchId)
    {
        var branchFailure = ApplyRequiredBranchScope(ref branchId);
        if (branchFailure != null)
            return branchFailure;

        var result = await _deliveryService.GetAvailableAsync(branchId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>创建配送员</summary>
    [HttpPost("persons")]
    [Authorize(Policy = PermissionPolicies.DeliveryEdit)]
    [ServiceFilter(typeof(AuditLogFilter))]
    public async Task<IActionResult> Create([FromBody] CreateDeliveryPersonRequest request)
    {
        var branchId = _branchFilter.ApplyBranchScope(request.BranchId);
        if (!branchId.HasValue)
            return BranchForbidden("当前用户未分配分公司");
        if (branchId.Value <= 0)
            return BadRequest(ApiResponse<object>.Fail("请选择有效分公司"));

        request.BranchId = branchId.Value;

        var result = await _deliveryService.CreateAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>更新配送员</summary>
    [HttpPut("persons/{id:int}")]
    [Authorize(Policy = PermissionPolicies.DeliveryEdit)]
    [ServiceFilter(typeof(AuditLogFilter))]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateDeliveryPersonRequest request)
    {
        var existing = await _deliveryService.GetByIdAsync(id);
        if (!existing.Success || existing.Data == null)
            return BadRequest(existing);

        if (!_branchFilter.IsAccessible(existing.Data.BranchId))
            return BranchForbidden("无权操作其他分公司的配送员");

        request.Id = id;
        if (!_branchFilter.IsHeadquartersAdmin())
            request.BranchId = _branchFilter.RequireCurrentBranchId();
        if (request.BranchId <= 0)
            return BadRequest(ApiResponse<object>.Fail("请选择有效分公司"));

        var result = await _deliveryService.UpdateAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>手动分配订单</summary>
    [HttpPost("assign")]
    [Authorize(Policy = PermissionPolicies.DeliveryManualAssign)]
    [ServiceFilter(typeof(AuditLogFilter))]
    public async Task<IActionResult> ManualAssign([FromBody] ManualAssignBody body)
    {
        var order = await _orderService.GetByIdAsync(body.OrderId);
        if (!order.Success || order.Data == null)
            return BadRequest(ApiResponse<object>.Fail("订单不存在"));

        if (!_branchFilter.IsAccessible(order.Data.BranchId))
            return BranchForbidden("无权操作其他分公司的订单");

        var deliveryPerson = await _deliveryService.GetByIdAsync(body.DeliveryPersonId);
        if (!deliveryPerson.Success || deliveryPerson.Data == null)
            return BadRequest(ApiResponse<object>.Fail("配送员不存在"));

        if (!_branchFilter.IsAccessible(deliveryPerson.Data.BranchId))
            return BranchForbidden("无权使用其他分公司的配送员");

        if (deliveryPerson.Data.BranchId != order.Data.BranchId)
            return BadRequest(ApiResponse<object>.Fail("配送员与订单不属于同一分公司"));

        var userId = GetCurrentUserId();
        var result = await _distributionService.ManualAssignAsync(body.OrderId, body.DeliveryPersonId, userId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst("EmployeeId");
        return claim != null && int.TryParse(claim.Value, out var id) ? id : 0;
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

public class ManualAssignBody
{
    public int OrderId { get; set; }
    public int DeliveryPersonId { get; set; }
}
