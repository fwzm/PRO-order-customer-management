using System.Diagnostics;

namespace PRO.WebApi.Middleware;

/// <summary>
/// 请求日志中间件
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestPath = context.Request.Path;
        var requestMethod = context.Request.Method;
        var clientIp = context.Connection.RemoteIpAddress?.ToString();

        try
        {
            await _next(context);
            stopwatch.Stop();

            var statusCode = context.Response.StatusCode;
            var logLevel = statusCode >= 400 ? LogLevel.Warning : LogLevel.Information;

            _logger.Log(logLevel, "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms from {IP}",
                requestMethod, requestPath, statusCode, stopwatch.ElapsedMilliseconds, clientIp);
        }
        catch (Exception)
        {
            stopwatch.Stop();
            _logger.LogError("HTTP {Method} {Path} failed in {ElapsedMs}ms from {IP}",
                requestMethod, requestPath, stopwatch.ElapsedMilliseconds, clientIp);
            throw;
        }
    }
}
