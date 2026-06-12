using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.WebApi.Filters;

namespace PRO.WebApi.Controllers;

/// <summary>
/// 收款与核销 API
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaymentsController(IPaymentService paymentService, BranchDataFilter branchFilter) : ControllerBase
{
    private readonly IPaymentService _paymentService = paymentService;
    private readonly BranchDataFilter _branchFilter = branchFilter;

    /// <summary>登记收款</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePaymentRequest request)
    {
        var employeeId = GetEmployeeId();
        var result = await _paymentService.CreatePaymentAsync(request, employeeId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>批量收款</summary>
    [HttpPost("batch")]
    public async Task<IActionResult> BatchCreate([FromBody] BatchPaymentRequest request)
    {
        var employeeId = GetEmployeeId();
        var result = await _paymentService.BatchCreatePaymentAsync(request, employeeId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>收款核销（多对多）</summary>
    [HttpPost("allocate")]
    public async Task<IActionResult> Allocate([FromBody] AllocatePaymentRequest request)
    {
        var employeeId = GetEmployeeId();
        var result = await _paymentService.AllocatePaymentAsync(request, employeeId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>获取核销明细</summary>
    [HttpGet("{id}/allocations")]
    public async Task<IActionResult> GetAllocations(int id)
    {
        var result = await _paymentService.GetAllocationsAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>获取订单收款记录</summary>
    [HttpGet("order/{orderId}")]
    public async Task<IActionResult> GetByOrder(int orderId)
    {
        var result = await _paymentService.GetOrderPaymentsAsync(orderId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>获取客户收款记录</summary>
    [HttpGet("customer/{customerId}")]
    public async Task<IActionResult> GetByCustomer(int customerId)
    {
        var result = await _paymentService.GetCustomerPaymentsAsync(customerId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>收款列表（含分公司隔离）</summary>
    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 20,
        [FromQuery] int? branchId = null, [FromQuery] int? customerId = null,
        [FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
    {
        branchId = _branchFilter.ApplyBranchScope(branchId);
        if (!_branchFilter.IsHeadquartersAdmin() && !branchId.HasValue)
            return BranchForbidden("当前用户未分配分公司");

        var request = new PagedRequest { PageIndex = pageIndex, PageSize = pageSize };
        var result = await _paymentService.GetPaymentListAsync(request, branchId, customerId, startDate, endDate);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>收款统计（含分公司隔离）</summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(
        [FromQuery] int branchId, [FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
    {
        branchId = _branchFilter.ApplyBranchScope(branchId) ?? branchId;
        var result = await _paymentService.GetPaymentStatsAsync(branchId, startDate, endDate);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>删除收款（退款）</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, [FromQuery] string reason = "手动删除")
    {
        var employeeId = GetEmployeeId();
        var result = await _paymentService.DeletePaymentAsync(id, employeeId, reason);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    private int GetEmployeeId()
    {
        var claim = User.FindFirst("EmployeeId");
        return claim != null && int.TryParse(claim.Value, out var id) ? id : 0;
    }

    private IActionResult BranchForbidden(string message)
    {
        return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(message));
    }
}
