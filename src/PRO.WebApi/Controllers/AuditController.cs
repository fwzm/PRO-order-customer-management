using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;

namespace PRO.WebApi.Controllers;

/// <summary>
/// 审计追踪 API — 状态历史查询、状态回滚、审计日志查询
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AuditController(IAuditTrailService auditTrail) : ControllerBase
{
    private readonly IAuditTrailService _auditTrail = auditTrail;

    // ==================== 状态历史 ====================

    /// <summary>订单状态变更历史</summary>
    [HttpGet("order/{orderId}/status-history")]
    public async Task<IActionResult> GetOrderStatusHistory(int orderId)
    {
        var result = await _auditTrail.GetOrderStatusHistoryAsync(orderId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>收款状态变更历史</summary>
    [HttpGet("{entityType}/{entityId}/payment-status-history")]
    public async Task<IActionResult> GetPaymentStatusHistory(string entityType, int entityId)
    {
        var result = await _auditTrail.GetPaymentStatusHistoryAsync(entityType, entityId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ==================== 状态回滚 ====================

    /// <summary>订单状态回滚（权限内，需强制填写原因）</summary>
    [HttpPost("order/rollback-status")]
    public async Task<IActionResult> RollbackOrderStatus([FromBody] StatusRollbackRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(ApiResponse<bool>.Fail("回滚原因不能为空"));

        var employeeId = GetEmployeeId();
        var result = await _auditTrail.RollbackOrderStatusAsync(request, employeeId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>收款状态回滚</summary>
    [HttpPost("payment/rollback-status")]
    public async Task<IActionResult> RollbackPaymentStatus([FromBody] StatusRollbackRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(ApiResponse<bool>.Fail("回滚原因不能为空"));

        var employeeId = GetEmployeeId();
        var result = await _auditTrail.RollbackPaymentStatusAsync(request, employeeId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ==================== 审计日志查询 ====================

    /// <summary>审计日志详情查询（支持按对象、字段、操作人、时间筛选）</summary>
    [HttpGet("logs")]
    public async Task<IActionResult> QueryLogs(
        [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? entityType = null, [FromQuery] int? entityId = null,
        [FromQuery] string? fieldName = null, [FromQuery] string? actionType = null,
        [FromQuery] int? operatorId = null, [FromQuery] int? branchId = null,
        [FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
    {
        var request = new AuditLogQueryRequest
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            EntityType = entityType,
            EntityId = entityId,
            FieldName = fieldName,
            ActionType = actionType,
            OperatorId = operatorId,
            BranchId = branchId,
            StartDate = startDate,
            EndDate = endDate
        };
        var result = await _auditTrail.QueryAuditLogsAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    private int GetEmployeeId()
    {
        var claim = User.FindFirst("EmployeeId");
        return claim != null && int.TryParse(claim.Value, out var id) ? id : 0;
    }
}
