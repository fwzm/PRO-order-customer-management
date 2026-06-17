using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using PRO.Infrastructure.Services;

namespace PRO.WebApi.Middleware;

/// <summary>
/// Prometheus 指标中间件 — 采集 API 请求耗时、次数、错误、缓存命中率
/// 通过 /metrics 端点暴露 Prometheus 格式指标
/// Production 环境支持 IP 白名单限制访问
/// </summary>
public class PrometheusMetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PrometheusMetricsMiddleware> _logger;
    private readonly List<IpRangeEntry> _allowedIpRanges;

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

    public PrometheusMetricsMiddleware(RequestDelegate next, ILogger<PrometheusMetricsMiddleware> logger, IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _allowedIpRanges = ParseAllowedIpRanges(configuration);
    }

    /// <summary>解析配置的 IP 白名单 (CIDR 或单 IP)</summary>
    private static List<IpRangeEntry> ParseAllowedIpRanges(IConfiguration configuration)
    {
        var ranges = new List<IpRangeEntry>();
        var configRanges = configuration.GetSection("Metrics:AllowedIpRanges").Get<string[]>();

        if (configRanges == null || configRanges.Length == 0) return ranges;

        foreach (var range in configRanges)
        {
            try
            {
                ranges.Add(IpRangeEntry.Parse(range.Trim()));
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning("PrometheusMetrics: 无法解析 IP 范围 '{Range}': {Error}", range, ex.Message);
            }
        }

        return ranges;
    }

    /// <summary>检查客户端 IP 是否在白名单内。白名单为空时仅允许本地回环。</summary>
    private bool IsIpAllowed(HttpContext context)
    {
        var remoteIp = context.Connection.RemoteIpAddress;
        if (remoteIp == null) return false;

        // 无配置时默认仅允许本地回环
        if (_allowedIpRanges.Count == 0)
            return IPAddress.IsLoopback(remoteIp);

        return _allowedIpRanges.Any(range => range.Contains(remoteIp));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // /metrics 端点 — 生产环境 IP 白名单检查
        if (context.Request.Path.StartsWithSegments("/metrics"))
        {
            if (!IsIpAllowed(context))
            {
                _logger.LogWarning("PrometheusMetrics: /metrics 访问被拒绝 — IP: {RemoteIp}", context.Connection.RemoteIpAddress);
                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                return;
            }
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

/// <summary>IP 范围条目 — 支持 CIDR 和单 IP 匹配，无外部依赖</summary>
internal readonly struct IpRangeEntry
{
    private readonly byte[] _network;
    private readonly byte[] _mask;
    private readonly bool _isV6;

    private IpRangeEntry(byte[] network, byte[] mask, bool isV6)
    {
        _network = network;
        _mask = mask;
        _isV6 = isV6;
    }

    public static IpRangeEntry Parse(string cidrOrIp)
    {
        if (string.IsNullOrWhiteSpace(cidrOrIp))
            throw new ArgumentException("IP 范围不能为空");

        if (cidrOrIp.Contains('/'))
        {
            var parts = cidrOrIp.Split('/');
            var ip = IPAddress.Parse(parts[0].Trim());
            var prefix = int.Parse(parts[1].Trim());
            return FromCidr(ip, prefix);
        }

        var singleIp = IPAddress.Parse(cidrOrIp.Trim());
        return singleIp.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
            ? FromCidr(singleIp, 128)
            : FromCidr(singleIp, 32);
    }

    private static IpRangeEntry FromCidr(IPAddress ip, int prefixLength)
    {
        var bytes = ip.GetAddressBytes();
        var bitLen = bytes.Length * 8;
        if (prefixLength < 0 || prefixLength > bitLen)
            throw new ArgumentException($"前缀长度 {prefixLength} 无效 (0-{bitLen})");

        var mask = new byte[bytes.Length];
        for (var i = 0; i < mask.Length; i++)
        {
            var bits = Math.Min(8, Math.Max(0, prefixLength - i * 8));
            mask[i] = (byte)(bits == 0 ? 0 : (0xFF << (8 - bits)) & 0xFF);
        }

        // 对 network 地址应用 mask
        var network = new byte[bytes.Length];
        for (var i = 0; i < bytes.Length; i++)
            network[i] = (byte)(bytes[i] & mask[i]);

        return new IpRangeEntry(network, mask, ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6);
    }

    public bool Contains(IPAddress ip)
    {
        if (ip.AddressFamily != (_isV6
            ? System.Net.Sockets.AddressFamily.InterNetworkV6
            : System.Net.Sockets.AddressFamily.InterNetwork))
            return false;

        var bytes = ip.GetAddressBytes();
        if (bytes.Length != _network.Length) return false;

        for (var i = 0; i < bytes.Length; i++)
        {
            if ((bytes[i] & _mask[i]) != _network[i])
                return false;
        }
        return true;
    }
}
