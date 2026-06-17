using System.Diagnostics;
using PRO.Infrastructure.Services;
using PRO.WebApi.Logging;
using Serilog;

namespace PRO.WebApi.Middleware;

/// <summary>
/// API性能监控中间件 — 记录每次API调用的端点、方法、耗时，输出结构化日志
/// </summary>
public class ApiMonitoringMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;

    public async Task InvokeAsync(HttpContext context, MonitoringService monitoring)
    {
        var sw = Stopwatch.StartNew();

        // 提取 UserId / BranchId（从 JWT Claims）
        var userId = context.User?.FindFirst("sub")?.Value
            ?? context.User?.FindFirst("UserId")?.Value;
        var branchId = context.User?.FindFirst("BranchId")?.Value;

        int? parsedUserId = int.TryParse(userId, out var uid) ? uid : null;
        int? parsedBranchId = int.TryParse(branchId, out var bid) ? bid : null;

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            // 记录错误
            monitoring.RecordError(
                context.Request.Path,
                ex.GetType().Name
            );

            Log.Error(ex,
                "[HTTP] {Method} {Path} failed after {ElapsedMs}ms (UserId={UserId}, BranchId={BranchId})",
                context.Request.Method, context.Request.Path, sw.ElapsedMilliseconds,
                userId ?? "-", branchId ?? "-");
            throw;
        }
        finally
        {
            sw.Stop();

            // 只记录API调用 — 结构化日志
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                monitoring.RecordApiCall(
                    context.Request.Path,
                    context.Request.Method,
                    sw.ElapsedMilliseconds
                );

                Log.Logger.LogRequest(
                    context.Request.Method,
                    context.Request.Path,
                    context.Response.StatusCode,
                    sw.ElapsedMilliseconds,
                    parsedUserId,
                    parsedBranchId);
            }

            // 心跳端点特殊处理
            if (context.Request.Path == "/api/monitoring/heartbeat" &&
                context.User?.Identity?.IsAuthenticated == true)
            {
                monitoring.UpdateHeartbeat(context.User?.Identity?.Name ?? "unknown");
            }
        }
    }
}
