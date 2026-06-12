using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.WebApi.Authorization;
using Swashbuckle.AspNetCore.Annotations;

namespace PRO.WebApi.Controllers;

/// <summary>
/// 异步导出控制器 — 后台任务模式
/// </summary>
[ApiController]
[Route("api/exports")]
[Authorize]
public class ExportController(IExportService exportService) : BaseApiController
{
    private readonly IExportService _exportService = exportService;

    /// <summary>提交导出任务</summary>
    [HttpPost]
    [Authorize(Policy = PermissionPolicies.OrderExport)]
    [SwaggerOperation(Summary = "提交导出任务")]
    public async Task<IActionResult> Submit([FromBody] ExportRequest request)
    {
        var result = await _exportService.SubmitAsync(request, CurrentEmployeeId, CurrentBranchId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>获取导出进度</summary>
    [HttpGet("{jobId:int}/progress")]
    [Authorize(Policy = PermissionPolicies.OrderExport)]
    [SwaggerOperation(Summary = "获取导出进度")]
    public async Task<IActionResult> GetProgress(int jobId)
    {
        var result = await _exportService.GetProgressAsync(jobId, CurrentEmployeeId, CurrentBranchId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>获取导出历史</summary>
    [HttpGet("history")]
    [Authorize(Policy = PermissionPolicies.OrderExport)]
    [SwaggerOperation(Summary = "获取导出历史")]
    public async Task<IActionResult> GetHistory([FromQuery] PagedRequest request)
    {
        var result = await _exportService.GetHistoryAsync(request, CurrentEmployeeId, CurrentBranchId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>重试失败的导出</summary>
    [HttpPost("{jobId:int}/retry")]
    [Authorize(Policy = PermissionPolicies.OrderExport)]
    [SwaggerOperation(Summary = "重试失败的导出")]
    public async Task<IActionResult> Retry(int jobId)
    {
        var result = await _exportService.RetryAsync(jobId, CurrentEmployeeId, CurrentBranchId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>取消导出</summary>
    [HttpPost("{jobId:int}/cancel")]
    [Authorize(Policy = PermissionPolicies.OrderExport)]
    [SwaggerOperation(Summary = "取消导出")]
    public async Task<IActionResult> Cancel(int jobId)
    {
        var result = await _exportService.CancelAsync(jobId, CurrentEmployeeId, CurrentBranchId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>获取下载路径</summary>
    [HttpGet("{jobId:int}/download")]
    [Authorize(Policy = PermissionPolicies.OrderExport)]
    [SwaggerOperation(Summary = "获取下载路径")]
    public async Task<IActionResult> Download(int jobId)
    {
        var result = await _exportService.GetDownloadPathAsync(jobId, CurrentEmployeeId, CurrentBranchId);
        if (!result.Success || result.Data == null)
            return BadRequest(result);

        var filePath = result.Data;
        if (!System.IO.File.Exists(filePath))
            return NotFound("导出文件不存在");

        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        return File(stream, "text/csv; charset=utf-8", Path.GetFileName(filePath));
    }
}
