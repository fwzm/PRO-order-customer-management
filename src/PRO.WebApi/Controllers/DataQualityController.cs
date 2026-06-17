using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRO.Application.Interfaces;
using PRO.WebApi.Filters;
using Swashbuckle.AspNetCore.Annotations;

namespace PRO.WebApi.Controllers;

/// <summary>
/// 数据质量检测控制器
/// </summary>
/// <remarks>
/// 构造函数
/// </remarks>
[Authorize]
public class DataQualityController(IDataQualityService dataQualityService, BranchDataFilter branchFilter) : BaseApiController
{
    private readonly IDataQualityService _dataQualityService = dataQualityService;
    private readonly BranchDataFilter _branchFilter = branchFilter;

    /// <summary>
    /// 获取数据质量报告
    /// </summary>
    /// <param name="branchId">分公司ID（可选，总部管理员可查看全部）</param>
    /// <returns>数据质量报告</returns>
    [HttpGet("report")]
    [SwaggerOperation(Summary = "获取数据质量报告", Description = "检测客户、产品、订单、配送等维度的数据质量问题")]
    public async Task<IActionResult> GetReport([FromQuery] int? branchId)
    {
        branchId = _branchFilter.ApplyBranchScope(branchId);
        var result = await _dataQualityService.GetDataQualityReportAsync(branchId);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
