using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRO.Infrastructure.Services;
using PRO.WebApi.Authorization;
using Swashbuckle.AspNetCore.Annotations;

namespace PRO.WebApi.Controllers;

/// <summary>
/// 监控控制器 — API统计、慢查询、错误率、在线用户
/// </summary>
[ApiController]
[Route("api/monitoring")]
[Authorize(Policy = PermissionPolicies.SystemLogs)]
public class MonitoringController(MonitoringService monitoring) : BaseApiController
{
    private readonly MonitoringService _monitoring = monitoring;

    /// <summary>获取监控快照</summary>
    [HttpGet("snapshot")]
    [SwaggerOperation(Summary = "获取监控快照")]
    public async Task<IActionResult> GetSnapshot()
    {
        var snapshot = await _monitoring.GetSnapshotAsync();
        return Ok(snapshot);
    }

    /// <summary>更新用户心跳（前端定时调用）</summary>
    [HttpPost("heartbeat")]
    [AllowAnonymous]
    [SwaggerOperation(Summary = "更新用户心跳")]
    public IActionResult Heartbeat()
    {
        var userId = User.Identity?.Name ?? "anonymous";
        _monitoring.UpdateHeartbeat(userId);
        return Ok(new { status = "ok" });
    }

    /// <summary>重置计数器</summary>
    [HttpPost("reset")]
    [SwaggerOperation(Summary = "重置计数器")]
    public IActionResult Reset()
    {
        _monitoring.Reset();
        return Ok(new { status = "reset" });
    }
}
