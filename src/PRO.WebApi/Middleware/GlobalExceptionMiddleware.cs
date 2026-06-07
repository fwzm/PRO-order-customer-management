using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace PRO.WebApi.Middleware;

/// <summary>
/// 全局异常处理中间件 — 拦截未处理的异常，返回统一格式的 ApiResponse
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = 499;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "未授权访问 {Path}", context.Request.Path);
            await WriteErrorResponseAsync(context, HttpStatusCode.Unauthorized, "未授权访问", false);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "业务逻辑异常 {Path}", context.Request.Path);
            await WriteErrorResponseAsync(context, HttpStatusCode.BadRequest, ex.Message, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "未处理的异常 {Path}", context.Request.Path);
            var message = _env.IsDevelopment() ? ex.ToString() : "服务器内部错误，请稍后重试";
            await WriteErrorResponseAsync(context, HttpStatusCode.InternalServerError, message, _env.IsDevelopment());
        }
    }

    private static async Task WriteErrorResponseAsync(HttpContext context, HttpStatusCode statusCode, string message, bool includeDetails)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.StatusCode = (int)statusCode;

        var response = new
        {
            Success = false,
            Message = includeDetails ? message : "服务器内部错误，请稍后重试",
            Data = (object?)null,
            Errors = new List<string> { message },
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
    }
}
