using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using PRO.Infrastructure.Persistence;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 监控服务 — API调用统计、慢查询追踪、错误率、在线用户
/// </summary>
public class MonitoringService
{
    private readonly ProDbContext _dbContext;
    private static readonly ConcurrentDictionary<string, long> _apiCallCounts = new();
    private static readonly ConcurrentQueue<SlowQueryRecord> _slowQueries = new();
    private static readonly ConcurrentDictionary<string, ErrorCount> _errorCounts = new();
    private static readonly ConcurrentDictionary<string, DateTime> _userHeartbeats = new();
    private const int SlowQueryThresholdMs = 500;
    private const int HeartbeatTimeoutMinutes = 5;

    public MonitoringService(ProDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>记录API调用</summary>
    public void RecordApiCall(string endpoint, string method, long elapsedMs)
    {
        _apiCallCounts.AddOrUpdate($"{method}:{endpoint}", 1, (_, v) => v + 1);
    }

    /// <summary>记录慢查询</summary>
    public void RecordSlowQuery(string sql, long elapsedMs)
    {
        if (elapsedMs >= SlowQueryThresholdMs)
        {
            _slowQueries.Enqueue(new SlowQueryRecord
            {
                Sql = sql.Length > 500 ? sql[..500] + "..." : sql,
                ElapsedMs = elapsedMs,
                Timestamp = DateTime.Now
            });

            // 保留最近1000条
            while (_slowQueries.Count > 1000)
                _slowQueries.TryDequeue(out _);
        }
    }

    /// <summary>记录错误</summary>
    public void RecordError(string endpoint, string errorType)
    {
        _errorCounts.AddOrUpdate($"{endpoint}:{errorType}",
            new ErrorCount { Count = 1, FirstSeen = DateTime.Now, LastSeen = DateTime.Now },
            (_, v) => { v.Count++; v.LastSeen = DateTime.Now; return v; });
    }

    /// <summary>更新用户心跳</summary>
    public void UpdateHeartbeat(string userId)
    {
        _userHeartbeats[userId] = DateTime.Now;
    }

    /// <summary>获取统计快照</summary>
    public async Task<MonitoringSnapshot> GetSnapshotAsync()
    {
        // 清理过期心跳
        var cutoff = DateTime.Now.AddMinutes(-HeartbeatTimeoutMinutes);
        foreach (var kv in _userHeartbeats.Where(kv => kv.Value < cutoff).ToList())
            _userHeartbeats.TryRemove(kv.Key, out _);

        // 数据库健康检查
        var dbHealthy = true;
        var dbResponseMs = 0L;
        try
        {
            var sw = Stopwatch.StartNew();
            await _dbContext.Database.ExecuteSqlRawAsync("SELECT 1");
            sw.Stop();
            dbResponseMs = sw.ElapsedMilliseconds;
        }
        catch
        {
            dbHealthy = false;
        }

        return new MonitoringSnapshot
        {
            Timestamp = DateTime.Now,
            ActiveUsers = _userHeartbeats.Count,
            TotalApiCalls = _apiCallCounts.Values.Sum(),
            ApiCallBreakdown = _apiCallCounts.OrderByDescending(kv => kv.Value).Take(20)
                .ToDictionary(kv => kv.Key, kv => kv.Value),
            SlowQueryCount = _slowQueries.Count,
            RecentSlowQueries = _slowQueries.Reverse().Take(10).ToList(),
            ErrorCount = _errorCounts.Values.Sum(e => e.Count),
            ErrorBreakdown = _errorCounts.OrderByDescending(kv => kv.Value.Count).Take(20)
                .ToDictionary(kv => kv.Key, kv => kv.Value),
            DatabaseHealthy = dbHealthy,
            DatabaseResponseMs = dbResponseMs,
            ProcessMemoryMB = Environment.WorkingSet / 1024 / 1024,
            ThreadCount = ThreadPool.ThreadCount
        };
    }

    /// <summary>重置计数器</summary>
    public void Reset()
    {
        _apiCallCounts.Clear();
        _errorCounts.Clear();
    }
}

public class SlowQueryRecord
{
    public string Sql { get; set; } = "";
    public long ElapsedMs { get; set; }
    public DateTime Timestamp { get; set; }
}

public class ErrorCount
{
    public int Count { get; set; }
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
}

public class MonitoringSnapshot
{
    public DateTime Timestamp { get; set; }
    public int ActiveUsers { get; set; }
    public long TotalApiCalls { get; set; }
    public Dictionary<string, long> ApiCallBreakdown { get; set; } = new();
    public int SlowQueryCount { get; set; }
    public List<SlowQueryRecord> RecentSlowQueries { get; set; } = new();
    public long ErrorCount { get; set; }
    public Dictionary<string, ErrorCount> ErrorBreakdown { get; set; } = new();
    public bool DatabaseHealthy { get; set; }
    public long DatabaseResponseMs { get; set; }
    public long ProcessMemoryMB { get; set; }
    public int ThreadCount { get; set; }
}
