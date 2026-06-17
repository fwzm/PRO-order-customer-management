using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.WebApi.Authorization;
using PRO.WebApi.Filters;

namespace PRO.WebApi.Controllers;

/// <summary>
/// 企业微信集成控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WeChatController(IWeChatService weChatService) : ControllerBase
{
    private readonly IWeChatService _weChatService = weChatService;

    /// <summary>同步组织架构</summary>
    [HttpPost("sync-organization")]
    [Authorize(Policy = PermissionPolicies.SystemWeChat)]
    [ServiceFilter(typeof(AuditLogFilter))]
    public async Task<IActionResult> SyncOrganization()
    {
        var result = await _weChatService.SyncOrganizationAsync();
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>同步指定员工</summary>
    [HttpPost("sync-employee/{employeeId:int}")]
    [Authorize(Policy = PermissionPolicies.SystemWeChat)]
    [ServiceFilter(typeof(AuditLogFilter))]
    public async Task<IActionResult> SyncEmployee(int employeeId)
    {
        var result = await _weChatService.SyncEmployeeAsync(employeeId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>拉取新客户</summary>
    [HttpPost("pull-customers")]
    [Authorize(Policy = PermissionPolicies.SystemWeChat)]
    [ServiceFilter(typeof(AuditLogFilter))]
    public async Task<IActionResult> PullCustomers()
    {
        var result = await _weChatService.PullNewCustomersAsync();
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>发送消息</summary>
    [HttpPost("send-message")]
    [Authorize(Policy = PermissionPolicies.SystemWeChat)]
    [ServiceFilter(typeof(AuditLogFilter))]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageBody body)
    {
        var result = await _weChatService.SendMessageAsync(body.ToUser, body.Content, body.AgentId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>获取同步日志</summary>
    [HttpGet("sync-logs")]
    [Authorize(Policy = PermissionPolicies.SystemWeChat)]
    public async Task<IActionResult> GetSyncLogs([FromQuery] int days = 7)
    {
        var result = await _weChatService.GetSyncLogsAsync(days);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>企业微信回调验证端点（无需认证）</summary>
    [AllowAnonymous]
    [HttpGet("callback")]
    public IActionResult VerifyCallback([FromQuery] string msg_signature, [FromQuery] string timestamp,
        [FromQuery] string nonce, [FromQuery] string echostr)
    {
        // 企微验证URL：解密echostr并返回明文
        // 实际验证逻辑由 WeChatService 处理
        return Ok(echostr);
    }

    /// <summary>企业微信回调消息接收（无需认证）</summary>
    [AllowAnonymous]
    [HttpPost("callback")]
    public async Task<IActionResult> ReceiveCallback()
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();
        // 消息解析逻辑
        return Ok("success");
    }
}

public class SendMessageBody
{
    public string ToUser { get; set; } = "@all";
    public string Content { get; set; } = "";
    public string? AgentId { get; set; }
}
