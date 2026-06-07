using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Enums;
using PRO.WebApi.Filters;

namespace PRO.WebApi.Controllers;

/// <summary>
/// 配送管理控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DeliveryController : ControllerBase
{
    private readonly IDeliveryPersonService _deliveryService;
    private readonly IOrderDistributionService _distributionService;

    public DeliveryController(IDeliveryPersonService deliveryService, IOrderDistributionService distributionService)
    {
        _deliveryService = deliveryService;
        _distributionService = distributionService;
    }

    /// <summary>获取配送员列表</summary>
    [HttpGet("persons")]
    public async Task<IActionResult> GetList([FromQuery] PagedRequest request,
        [FromQuery] int? branchId, [FromQuery] DeliveryPersonStatus? status)
    {
        var result = await _deliveryService.GetListAsync(request, branchId, status);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>获取可用配送员</summary>
    [HttpGet("persons/available")]
    public async Task<IActionResult> GetAvailable([FromQuery] int branchId)
    {
        var result = await _deliveryService.GetAvailableAsync(branchId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>创建配送员</summary>
    [HttpPost("persons")]
    [ServiceFilter(typeof(AuditLogFilter))]
    public async Task<IActionResult> Create([FromBody] CreateDeliveryPersonRequest request)
    {
        var result = await _deliveryService.CreateAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>更新配送员</summary>
    [HttpPut("persons/{id:int}")]
    [ServiceFilter(typeof(AuditLogFilter))]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateDeliveryPersonRequest request)
    {
        request.Id = id;
        var result = await _deliveryService.UpdateAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>手动分配订单</summary>
    [HttpPost("assign")]
    [ServiceFilter(typeof(AuditLogFilter))]
    public async Task<IActionResult> ManualAssign([FromBody] ManualAssignBody body)
    {
        var userId = GetCurrentUserId();
        var result = await _distributionService.ManualAssignAsync(body.OrderId, body.DeliveryPersonId, userId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst("EmployeeId");
        return claim != null && int.TryParse(claim.Value, out var id) ? id : 0;
    }
}

public class ManualAssignBody
{
    public int OrderId { get; set; }
    public int DeliveryPersonId { get; set; }
}
