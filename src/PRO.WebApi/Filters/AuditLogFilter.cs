using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using PRO.Application.Interfaces;

namespace PRO.WebApi.Filters;

/// <summary>
/// 操作审计过滤器 — 自动记录 API 请求日志
/// 标记在需要审计的 Controller Action 上
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class AuditLogFilter : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<AuditLogFilter>>();
        var logService = context.HttpContext.RequestServices.GetRequiredService<IOperationLogService>();

        var controllerName = (string)context.RouteData.Values["controller"]!;
        var actionName = (string)context.RouteData.Values["action"]!;
        var method = context.HttpContext.Request.Method;
        var userIdClaim = context.HttpContext.User.FindFirst("EmployeeId");
        var employeeId = userIdClaim != null && int.TryParse(userIdClaim.Value, out var id) ? id : 0;
        var userNoClaim = context.HttpContext.User.FindFirst("EmployeeNo");
        var employeeNo = userNoClaim?.Value ?? "API";

        var startTime = DateTime.UtcNow;

        // 执行 Action
        var executedContext = await next();

        var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
        var result = executedContext.Exception == null ? "Success" : "Failed";
        var errorMessage = executedContext.Exception?.Message;

        // 异步记录（不阻塞响应）
        _ = Task.Run(async () =>
        {
            try
            {
                await logService.CreateAsync(
                    employeeId,
                    $"{controllerName}Controller",
                    $"{method} {actionName}",
                    $"API请求: {method} /api/{controllerName}/{actionName} (耗时 {duration:F0}ms)",
                    controllerName,
                    null,
                    result,
                    errorMessage);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "审计日志记录失败");
            }
        });

        if (executedContext.Exception != null)
            logger.LogError(executedContext.Exception, "API请求异常 {Method} {Path} Duration={Duration:F0}ms",
                method, context.HttpContext.Request.Path, duration);
        else
            logger.LogInformation("API请求 {Method} {Path} Duration={Duration:F0}ms Status={Status}",
                method, context.HttpContext.Request.Path, duration, context.HttpContext.Response.StatusCode);
    }
}
