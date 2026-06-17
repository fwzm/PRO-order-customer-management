namespace PRO.Domain.Entities;

/// <summary>
/// 业务异常指标记录 - 用于统计分析各类异常的发生频率和分布
/// 在 BusinessMessageService 翻译异常时记录
/// </summary>
public class BusinessErrorMetric
{
    public long Id { get; set; }

    /// <summary>错误码（如 DUPLICATE_DATA、CONCURRENCY_CONFLICT）</summary>
    public string ErrorCode { get; set; } = string.Empty;

    /// <summary>业务模块（如 Order、Customer、Product）</summary>
    public string Module { get; set; } = string.Empty;

    /// <summary>操作类型（如 Create、Delete、Import）</summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>操作人ID（0表示未登录）</summary>
    public int? UserId { get; set; }

    /// <summary>跟踪ID（用于跨系统追踪）</summary>
    public string? TraceId { get; set; }

    /// <summary>用户看到的业务提示摘要（不包含技术细节）</summary>
    public string? UserMessage { get; set; }

    /// <summary>发生时间</summary>
    public DateTime OccurredAt { get; set; } = DateTime.Now;

    /// <summary>是否已标记为已处理</summary>
    public bool IsResolved { get; set; }
}

/// <summary>
/// 异常统计查询结果
/// </summary>
public class ErrorMetricSummary
{
    public string ErrorCode { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public int Count { get; set; }
    public DateTime LastOccurredAt { get; set; }
}

/// <summary>
/// 按时间分组的异常趋势
/// </summary>
public class ErrorTrendPoint
{
    public DateTime Date { get; set; }
    public int Count { get; set; }
    public string ErrorCode { get; set; } = string.Empty;
}
