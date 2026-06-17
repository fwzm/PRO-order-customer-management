using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PRO.Application.DTOs;

namespace PRO.WebApi.Filters;

/// <summary>
/// API 异常过滤器
/// </summary>
public class ApiExceptionFilter(ILogger<ApiExceptionFilter> logger) : IExceptionFilter
{
    private readonly ILogger<ApiExceptionFilter> _logger = logger;

    public void OnException(ExceptionContext context)
    {
        _logger.LogError(context.Exception, "控制器异常: {Message}", context.Exception.Message);

        var response = ApiResponse<object>.Fail(
            "操作失败",
            [context.Exception.Message]);

        context.Result = new ObjectResult(response)
        {
            StatusCode = 500
        };

        context.ExceptionHandled = true;
    }
}
