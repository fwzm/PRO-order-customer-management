using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.WebApi.Filters;

namespace PRO.WebApi.Controllers;

/// <summary>
/// 应收账款与账龄分析 API
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReceivablesController(IReceivableService receivableService, BranchDataFilter branchFilter) : ControllerBase
{
    private readonly IReceivableService _receivableService = receivableService;
    private readonly BranchDataFilter _branchFilter = branchFilter;

    /// <summary>应收账款列表（支持客户/业务员/分公司筛选与排序，含分公司隔离）</summary>
    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 50,
        [FromQuery] int? customerId = null, [FromQuery] int? branchId = null,
        [FromQuery] int? salespersonId = null, [FromQuery] decimal? minBalance = null,
        [FromQuery] string? sortField = null, [FromQuery] string? sortOrder = null)
    {
        branchId = _branchFilter.ApplyBranchScope(branchId);
        if (!_branchFilter.IsHeadquartersAdmin() && !branchId.HasValue)
            return BranchForbidden("当前用户未分配分公司");

        var request = new ReceivableQueryRequest
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            CustomerId = customerId,
            BranchId = branchId,
            SalespersonId = salespersonId,
            MinBalance = minBalance,
            SortField = sortField,
            SortOrder = sortOrder
        };
        var result = await _receivableService.GetCustomerReceivablesAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>单个客户应收明细</summary>
    [HttpGet("{customerId}")]
    public async Task<IActionResult> GetDetail(int customerId)
    {
        var result = await _receivableService.GetCustomerReceivableAsync(customerId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>账龄分析（含分公司隔离）</summary>
    [HttpGet("aging")]
    public async Task<IActionResult> GetAging(
        [FromQuery] int? customerId = null, [FromQuery] int? branchId = null,
        [FromQuery] int? salespersonId = null, [FromQuery] DateTime? asOfDate = null)
    {
        branchId = _branchFilter.ApplyBranchScope(branchId);
        var request = new AgingAnalysisRequest
        {
            CustomerId = customerId,
            BranchId = branchId,
            SalespersonId = salespersonId,
            AsOfDate = asOfDate
        };
        var result = await _receivableService.GetAgingAnalysisAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    private IActionResult BranchForbidden(string message)
    {
        return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(message));
    }
}
