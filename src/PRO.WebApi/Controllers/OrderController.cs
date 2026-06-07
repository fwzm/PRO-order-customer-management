using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Enums;
using PRO.WebApi.Filters;

namespace PRO.WebApi.Controllers;

/// <summary>
/// 订单管理控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrderController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly IOrderDistributionService _distributionService;

    public OrderController(IOrderService orderService, IOrderDistributionService distributionService)
    {
        _orderService = orderService;
        _distributionService = distributionService;
    }

    /// <summary>获取订单列表（分页+筛选）</summary>
    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] PagedRequest request,
        [FromQuery] int? branchId, [FromQuery] OrderStatus? status, [FromQuery] PaymentStatus? paymentStatus)
    {
        var result = await _orderService.GetListAsync(request, branchId, status, paymentStatus);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>获取订单详情</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _orderService.GetByIdAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>创建订单</summary>
    [HttpPost]
    [ServiceFilter(typeof(AuditLogFilter))]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request)
    {
        var createdById = GetCurrentUserId();
        var result = await _orderService.CreateAsync(request, createdById);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>更新订单</summary>
    [HttpPut("{id:int}")]
    [ServiceFilter(typeof(AuditLogFilter))]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateOrderRequest request)
    {
        request.Id = id;
        var modifiedById = GetCurrentUserId();
        var result = await _orderService.UpdateAsync(request, modifiedById);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>删除订单</summary>
    [HttpDelete("{id:int}")]
    [ServiceFilter(typeof(AuditLogFilter))]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _orderService.DeleteAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>分配配送员</summary>
    [HttpPost("{id:int}/assign")]
    [ServiceFilter(typeof(AuditLogFilter))]
    public async Task<IActionResult> Assign(int id, [FromBody] AssignOrderRequest request)
    {
        request.OrderId = id;
        var assignedById = GetCurrentUserId();
        var result = await _orderService.AssignAsync(request, assignedById);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>更新订单状态</summary>
    [HttpPut("{id:int}/status")]
    [ServiceFilter(typeof(AuditLogFilter))]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateOrderStatusRequest request)
    {
        request.OrderId = id;
        var modifiedById = GetCurrentUserId();
        var result = await _orderService.UpdateStatusAsync(request, modifiedById);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>确认草稿</summary>
    [HttpPost("{id:int}/confirm")]
    [ServiceFilter(typeof(AuditLogFilter))]
    public async Task<IActionResult> ConfirmDraft(int id)
    {
        var modifiedById = GetCurrentUserId();
        var result = await _orderService.ConfirmDraftAsync(id, modifiedById);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>导出订单Excel</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] PagedRequest request,
        [FromQuery] int? branchId, [FromQuery] bool forGaode = false)
    {
        var result = await _orderService.ExportToExcelAsync(request, branchId, forGaode);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ─── 配送管理端点 ────────────────────────────────────────

    /// <summary>获取待分配订单</summary>
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingOrders([FromQuery] int branchId)
    {
        var result = await _distributionService.GetPendingOrdersAsync(branchId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>自动分配订单</summary>
    [HttpPost("auto-assign")]
    [ServiceFilter(typeof(AuditLogFilter))]
    public async Task<IActionResult> AutoAssign([FromQuery] int branchId, [FromQuery] string algorithm = "region_load_distance")
    {
        var result = await _distributionService.AutoAssignAsync(branchId, algorithm);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>获取最优分配方案（预览）</summary>
    [HttpGet("optimal-assignment")]
    public async Task<IActionResult> GetOptimalAssignment([FromQuery] int branchId)
    {
        var result = await _distributionService.GetOptimalAssignmentAsync(branchId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ─── 辅助 ────────────────────────────────────────────────

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst("EmployeeId");
        return claim != null && int.TryParse(claim.Value, out var id) ? id : 0;
    }
}
