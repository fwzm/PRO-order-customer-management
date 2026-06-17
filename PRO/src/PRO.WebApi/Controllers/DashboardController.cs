using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRO.Application.DTOs;
using PRO.Infrastructure.Services;
using PRO.WebApi.Filters;
using Swashbuckle.AspNetCore.Annotations;

namespace PRO.WebApi.Controllers;

/// <summary>
/// 移动端工作台控制器 — 为手机 App 提供聚合首页数据
/// </summary>
[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController(DashboardService dashboardService, BranchDataFilter branchFilter) : BaseApiController
{
    private readonly DashboardService _dashboardService = dashboardService;
    private readonly BranchDataFilter _branchFilter = branchFilter;

    /// <summary>
    /// 获取移动端工作台数据（今日概览 + 待办 + 告警 + 最近订单）
    /// </summary>
    [HttpGet("workbench")]
    [SwaggerOperation(Summary = "获取移动端工作台数据", Description = "返回今日概览、待办任务、告警列表、最近订单，适用于移动端首页")]
    public async Task<IActionResult> GetWorkbenchData([FromQuery] bool forceRefresh = false)
    {
        var branchId = _branchFilter.ApplyBranchScope(null);
        if (!_branchFilter.IsHeadquartersAdmin() && !branchId.HasValue)
            return BranchForbidden("当前用户未分配分公司");

        var result = await _dashboardService.GetDashboardDataAsync(branchId!.Value, forceRefresh);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
