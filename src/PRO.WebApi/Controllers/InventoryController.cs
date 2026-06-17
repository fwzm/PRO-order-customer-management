using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRO.Application.DTOs;
using PRO.Infrastructure.Services;
using PRO.WebApi.Authorization;
using PRO.WebApi.Filters;
using Swashbuckle.AspNetCore.Annotations;

namespace PRO.WebApi.Controllers;

/// <summary>
/// 库存管理控制器
/// </summary>
[Authorize]
public class InventoryController(InventoryService inventoryService, BranchDataFilter branchFilter) : BaseApiController
{
    private readonly InventoryService _inventoryService = inventoryService;
    private readonly BranchDataFilter _branchFilter = branchFilter;

    /// <summary>
    /// 获取库存变动日志
    /// </summary>
    [HttpGet("change-logs")]
    [SwaggerOperation(Summary = "获取库存变动日志",
        Description = "分页查询库存变动记录，支持按产品、变动类型、时间范围筛选")]
    public async Task<IActionResult> GetChangeLogs(
        [FromQuery] PagedRequest request,
        [FromQuery] int? productId,
        [FromQuery] string? changeType,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate)
    {
        var result = await _inventoryService.GetChangeLogsAsync(
            request, productId, changeType, startDate, endDate);
        return Ok(result);
    }

    /// <summary>
    /// 获取库存盘点数据
    /// </summary>
    [HttpGet("stock")]
    [SwaggerOperation(Summary = "获取库存盘点数据",
        Description = "获取所有活跃产品的系统库存及最后变动信息")]
    public async Task<IActionResult> GetStockInventory()
    {
        var result = await _inventoryService.GetStockInventoryAsync();
        return Ok(result);
    }

    /// <summary>
    /// 手动调整库存
    /// </summary>
    [HttpPost("adjust")]
    [Authorize(Policy = PermissionPolicies.ProductEdit)]
    [SwaggerOperation(Summary = "手动调整产品库存",
        Description = "调整指定产品的库存数量，自动记录变动日志")]
    public async Task<IActionResult> AdjustStock([FromBody] InventoryAdjustRequest request)
    {
        var result = await _inventoryService.AdjustStockAsync(request, CurrentEmployeeId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// 批量盘点
    /// </summary>
    [HttpPost("batch-stock-take")]
    [Authorize(Policy = PermissionPolicies.ProductEdit)]
    [SwaggerOperation(Summary = "批量盘点库存",
        Description = "批量调整多个产品的库存并记录日志")]
    public async Task<IActionResult> BatchStockTake([FromBody] List<InventoryAdjustRequest> adjustments)
    {
        var result = await _inventoryService.BatchStockTakeAsync(adjustments, CurrentEmployeeId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// 获取库存变动统计
    /// </summary>
    [HttpGet("stats")]
    [SwaggerOperation(Summary = "获取库存变动统计",
        Description = "统计指定时间范围内的库存变动概要和排行")]
    public async Task<IActionResult> GetChangeStats([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
    {
        var result = await _inventoryService.GetChangeStatsAsync(startDate, endDate);
        return Ok(result);
    }
}
