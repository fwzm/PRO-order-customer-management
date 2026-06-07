using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Enums;
using Swashbuckle.AspNetCore.Annotations;

namespace PRO.WebApi.Controllers;

/// <summary>
/// 订单管理控制器
/// </summary>
[Authorize]
public class OrdersController : BaseApiController
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    /// <summary>
    /// 获取订单列表
    /// </summary>
    [HttpGet]
    [SwaggerOperation(Summary = "获取订单列表")]
    public async Task<IActionResult> GetList([FromQuery] PagedRequest request, [FromQuery] OrderStatus? status, [FromQuery] PaymentStatus? paymentStatus)
    {
        int? branchId = IsHeadquartersAdmin ? null : CurrentBranchId;
        var result = await _orderService.GetListAsync(request, branchId, status, paymentStatus);
        return Ok(result);
    }

    /// <summary>
    /// 获取订单详情
    /// </summary>
    [HttpGet("{id}")]
    [SwaggerOperation(Summary = "获取订单详情")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _orderService.GetByIdAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// 创建订单
    /// </summary>
    [HttpPost]
    [SwaggerOperation(Summary = "创建订单")]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request)
    {
        var result = await _orderService.CreateAsync(request, CurrentEmployeeId);
        return result.Success
            ? CreatedAtAction(nameof(GetById), new { id = result.Data }, result)
            : BadRequest(result);
    }

    /// <summary>
    /// 更新订单
    /// </summary>
    [HttpPut("{id}")]
    [SwaggerOperation(Summary = "更新订单")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateOrderRequest request)
    {
        request.Id = id;
        var result = await _orderService.UpdateAsync(request, CurrentEmployeeId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// 删除订单
    /// </summary>
    [HttpDelete("{id}")]
    [SwaggerOperation(Summary = "删除订单")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _orderService.DeleteAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// 分配配送员
    /// </summary>
    [HttpPost("{id}/assign")]
    [SwaggerOperation(Summary = "分配配送员")]
    public async Task<IActionResult> Assign(int id, [FromBody] AssignOrderRequest request)
    {
        request.OrderId = id;
        var result = await _orderService.AssignAsync(request, CurrentEmployeeId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// 更新订单状态
    /// </summary>
    [HttpPost("{id}/status")]
    [SwaggerOperation(Summary = "更新订单状态")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateOrderStatusRequest request)
    {
        request.OrderId = id;
        var result = await _orderService.UpdateStatusAsync(request, CurrentEmployeeId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// 确认草稿
    /// </summary>
    [HttpPost("{id}/confirm-draft")]
    [SwaggerOperation(Summary = "确认草稿订单")]
    public async Task<IActionResult> ConfirmDraft(int id)
    {
        var result = await _orderService.ConfirmDraftAsync(id, CurrentEmployeeId);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
