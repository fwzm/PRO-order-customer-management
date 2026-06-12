using Serilog.Core;
using Serilog.Events;

namespace PRO.WebApi.Logging;

/// <summary>
/// 结构化日志增强器 — 为每条日志注入 TraceId、UserId、BranchId、ElapsedMs 等字段
/// 配合 ELK/Seq 等日志平台实现可观测性
/// </summary>
public class StructuredLogEnricher : ILogEventEnricher
{
    /// <summary>TraceId 属性名</summary>
    public const string TraceIdProperty = "TraceId";

    /// <summary>UserId 属性名</summary>
    public const string UserIdProperty = "UserId";

    /// <summary>BranchId 属性名</summary>
    public const string BranchIdProperty = "BranchId";

    /// <summary>耗时属性名（毫秒）</summary>
    public const string ElapsedMsProperty = "ElapsedMs";

    /// <summary>操作模块属性名</summary>
    public const string ModuleProperty = "Module";

    /// <summary>操作类型属性名</summary>
    public const string OperationProperty = "Operation";

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        // TraceId — 从日志上下文中读取，若无则使用 Activity.Current
        var traceId = logEvent.TraceId?.ToString()
            ?? System.Diagnostics.Activity.Current?.TraceId.ToString()
            ?? string.Empty;

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(TraceIdProperty, traceId));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(UserIdProperty, string.Empty));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(BranchIdProperty, string.Empty));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(ElapsedMsProperty, string.Empty));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(ModuleProperty, string.Empty));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(OperationProperty, string.Empty));
    }
}

/// <summary>
/// 结构化日志扩展方法 — 为 ILogger 提供业务级结构化日志方法
/// </summary>
public static class StructuredLoggerExtensions
{
    /// <summary>
    /// 记录业务操作日志（结构化字段方式，避免字符串拼接）
    /// </summary>
    public static void LogBusiness(
        this Serilog.ILogger logger,
        Serilog.Events.LogEventLevel level,
        string module,
        string operation,
        string message,
        int? userId = null,
        int? branchId = null,
        long? elapsedMs = null,
        Exception? exception = null)
    {
        logger.Write(level, exception,
            "[{Module}] [{Operation}] {Message}",
            module, operation, message);
    }

    /// <summary>记录业务操作成功</summary>
    public static void LogBusinessInfo(
        this Serilog.ILogger logger,
        string module,
        string operation,
        string message,
        int? userId = null,
        int? branchId = null,
        long? elapsedMs = null)
    {
        LogBusiness(logger, LogEventLevel.Information, module, operation, message, userId, branchId, elapsedMs);
    }

    /// <summary>记录业务操作警告</summary>
    public static void LogBusinessWarning(
        this Serilog.ILogger logger,
        string module,
        string operation,
        string message,
        int? userId = null,
        int? branchId = null,
        Exception? exception = null)
    {
        LogBusiness(logger, LogEventLevel.Warning, module, operation, message, userId, branchId, exception: exception);
    }

    /// <summary>记录业务操作错误</summary>
    public static void LogBusinessError(
        this Serilog.ILogger logger,
        string module,
        string operation,
        string message,
        int? userId = null,
        int? branchId = null,
        Exception? exception = null)
    {
        LogBusiness(logger, LogEventLevel.Error, module, operation, message, userId, branchId, exception: exception);
    }

    /// <summary>
    /// 记录请求耗时（用于中间件）
    /// </summary>
    public static void LogRequest(
        this Serilog.ILogger logger,
        string method,
        string path,
        int statusCode,
        long elapsedMs,
        int? userId = null,
        int? branchId = null)
    {
        logger.Information(
            "[HTTP] {Method} {Path} responded {StatusCode} in {ElapsedMs}ms (UserId={UserId}, BranchId={BranchId})",
            method, path, statusCode, elapsedMs, userId?.ToString() ?? "-", branchId?.ToString() ?? "-");
    }
}
