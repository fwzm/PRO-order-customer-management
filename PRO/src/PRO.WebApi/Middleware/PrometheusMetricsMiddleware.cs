using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using PRO.Infrastructure.Services;

namespace PRO.WebApi.Middleware;

/// <summary>
/// Prometheus 指标中间件 — 采集 API 请求耗时、次数、错误、缓存命中率
/// 通过 /metrics 端点暴露 Prometheus 格式指标
/// </summary>
public class PrometheusMetricsMiddleware
{
    private readonly RequestDelegate _next;

    // 指标存储
    private static readonly ConcurrentDictionary<string, long> _requestCounters = new();
    private static readonly ConcurrentDictionary<string, long> _errorCounters = new();
    private static readonly ConcurrentDictionary<string, List<long>> _requestDurations = new();
    private static long _slowRequestCount;
    private static long _loginFailedCount;
    private static long _permissionDeniedCount;
    private static long _branchIsolationDeniedCount;
    private static long _cacheHits;
    private static long _cacheMisses;
    private static long _exportSuccessCount;
    private static long _exportFailedCount;
    private static long _dbErrorCount;
    private static long _totalRequests;
    private static long _activeRequests;
    private static readonly object _lock = new();

    private const int SlowRequestThresholdMs = 1000;
    private const int MaxDurationSamples = 1000;

    public PrometheusMetricsMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 跳过 /metrics 端点自身
        if (context.Request.Path.StartsWithSegments("/metrics"))
        {
            await WriteMetricsAsync(context);
            return;
        }

        Interlocked.Increment(ref _totalRequests);
        Interlocked.Increment(ref _activeRequests);

        var sw = Stopwatch.StartNew();
        var method = context.Request.Method;
        var path = GetRoutePattern(context.Request.Path);

        try
        {
            await _next(context);

            sw.Stop();
            var elapsedMs = sw.ElapsedMilliseconds;

            // 记录请求
            var counterKey = $"{method}:{path}:{context.Response.StatusCode}";
            _requestCounters.AddOrUpdate(counterKey, 1, (_, v) => v + 1);

            // 记录耗时
            RecordDuration(path, elapsedMs);

            // 慢请求
            if (elapsedMs >= SlowRequestThresholdMs)
                Interlocked.Increment(ref _slowRequestCount);

            // 错误统计
            if (context.Response.StatusCode >= 400)
            {
                var errorKey = $"{method}:{path}:{context.Response.StatusCode}";
                _errorCounters.AddOrUpdate(errorKey, 1, (_, v) => v + 1);

                if (context.Response.StatusCode == 401)
                    Interlocked.Increment(ref _loginFailedCount);
                if (context.Response.StatusCode == 403)
                    Interlocked.Increment(ref _permissionDeniedCount);
            }
        }
        catch (Exception)
        {
            sw.Stop();
            var errorKey = $"{method}:{path}:500";
            _errorCounters.AddOrUpdate(errorKey, 1, (_, v) => v + 1);
            RecordDuration(path, sw.ElapsedMilliseconds);
            throw;
        }
        finally
        {
            Interlocked.Decrement(ref _activeRequests);
        }
    }

    /// <summary>记录请求耗时</summary>
    private static void RecordDuration(string path, long elapsedMs)
    {
        lock (_lock)
        {
            if (!_requestDurations.TryGetValue(path, out var list))
            {
                list = new List<long>();
                _requestDurations[path] = list;
            }
            list.Add(elapsedMs);
            while (list.Count > MaxDurationSamples) list.RemoveAt(0);
        }
    }

    /// <summary>外部可调用的指标记录方法</summary>
    public static void RecordBranchIsolationDenied() => Interlocked.Increment(ref _branchIsolationDeniedCount);
    public static void RecordCacheHit() => Interlocked.Increment(ref _cacheHits);
    public static void RecordCacheMiss() => Interlocked.Increment(ref _cacheMisses);
    public static void RecordExportSuccess() => Interlocked.Increment(ref _exportSuccessCount);
    public static void RecordExportFailed() => Interlocked.Increment(ref _exportFailedCount);
    public static void RecordDbError() => Interlocked.Increment(ref _dbErrorCount);

    /// <summary>生成 Prometheus 格式指标</summary>
    private static async Task WriteMetricsAsync(HttpContext context)
    {
        context.Response.ContentType = "text/plain; version=0.0.4";
        var sb = new StringBuilder();

        // HELP/TYPE 注释
        sb.AppendLine("# HELP http_requests_total Total HTTP requests");
        sb.AppendLine("# TYPE http_requests_total counter");
        foreach (var kv in _requestCounters.OrderBy(k => k.Key))
            sb.AppendLine($"http_requests_total{{endpoint=\"{kv.Key}\"}} {kv.Value}");

        sb.AppendLine("# HELP http_errors_total Total HTTP errors");
        sb.AppendLine("# TYPE http_errors_total counter");
        foreach (var kv in _errorCounters.OrderBy(k => k.Key))
            sb.AppendLine($"http_errors_total{{endpoint=\"{kv.Key}\"}} {kv.Value}");

        sb.AppendLine("# HELP http_request_duration_ms HTTP request duration in ms");
        sb.AppendLine("# TYPE http_request_duration_ms gauge");
        lock (_lock)
        {
            foreach (var kv in _requestDurations.OrderBy(k => k.Key))
            {
                if (kv.Value.Count > 0)
                {
                    var avg = kv.Value.Average();
                    var max = kv.Value.Max();
                    sb.AppendLine($"http_request_duration_ms{{endpoint=\"{kv.Key}\",quantile=\"avg\"}} {avg:F1}");
                    sb.AppendLine($"http_request_duration_ms{{endpoint=\"{kv.Key}\",quantile=\"max\"}} {max}");
                }
            }
        }

        sb.AppendLine("# HELP http_slow_requests_total Requests exceeding 1s");
        sb.AppendLine("# TYPE http_slow_requests_total counter");
        sb.AppendLine($"http_slow_requests_total {_slowRequestCount}");

        sb.AppendLine("# HELP http_requests_in_flight Currently active requests");
        sb.AppendLine("# TYPE http_requests_in_flight gauge");
        sb.AppendLine($"http_requests_in_flight {_activeRequests}");

        sb.AppendLine("# HELP http_requests_total_all Total requests (all endpoints)");
        sb.AppendLine("# TYPE http_requests_total_all counter");
        sb.AppendLine($"http_requests_total_all {_totalRequests}");

        // 业务指标
        sb.AppendLine("# HELP login_failed_total Login failures");
        sb.AppendLine("# TYPE login_failed_total counter");
        sb.AppendLine($"login_failed_total {_loginFailedCount}");

        sb.AppendLine("# HELP permission_denied_total Permission denied count");
        sb.AppendLine("# TYPE permission_denied_total counter");
        sb.AppendLine($"permission_denied_total {_permissionDeniedCount}");

        sb.AppendLine("# HELP branch_isolation_denied_total Branch isolation denied count");
        sb.AppendLine("# TYPE branch_isolation_denied_total counter");
        sb.AppendLine($"branch_isolation_denied_total {_branchIsolationDeniedCount}");

        sb.AppendLine("# HELP cache_hits_total Cache hit count");
        sb.AppendLine("# TYPE cache_hits_total counter");
        sb.AppendLine($"cache_hits_total {_cacheHits}");

        sb.AppendLine("# HELP cache_misses_total Cache miss count");
        sb.AppendLine("# TYPE cache_misses_total counter");
        sb.AppendLine($"cache_misses_total {_cacheMisses}");

        sb.AppendLine("# HELP export_success_total Export success count");
        sb.AppendLine("# TYPE export_success_total counter");
        sb.AppendLine($"export_success_total {_exportSuccessCount}");

        sb.AppendLine("# HELP export_failed_total Export failed count");
        sb.AppendLine("# TYPE export_failed_total counter");
        sb.AppendLine($"export_failed_total {_exportFailedCount}");

        sb.AppendLine("# HELP db_errors_total Database error count");
        sb.AppendLine("# TYPE db_errors_total counter");
        sb.AppendLine($"db_errors_total {_dbErrorCount}");

        // 进程指标
        sb.AppendLine("# HELP process_memory_bytes Process memory usage");
        sb.AppendLine("# TYPE process_memory_bytes gauge");
        sb.AppendLine($"process_memory_bytes {Environment.WorkingSet}");

        sb.AppendLine("# HELP process_thread_count Thread count");
        sb.AppendLine("# TYPE process_thread_count gauge");
        sb.AppendLine($"process_thread_count {ThreadPool.ThreadCount}");

        await context.Response.WriteAsync(sb.ToString());
    }

    private static string GetRoutePattern(string path)
    {
        // 将路径中的数字ID替换为 {id} 以减少指标基数
        var segments = path.Split('/') ?? [];
        for (var i = 0; i < segments.Length; i++)
        {
            if (int.TryParse(segments[i], out _))
                segments[i] = "{id}";
        }
        return string.Join("/", segments);
    }
}
