using PRO.Domain.Enums;

namespace PRO.Application.DTOs;

/// <summary>
/// 操作日志查询请求（增强版）
/// </summary>
public class OperationLogQueryRequest : PagedRequest
{
    /// <summary>模块筛选</summary>
    public string? Module { get; set; }

    /// <summary>操作类型筛选</summary>
    public string? OperationType { get; set; }

    /// <summary>操作人ID</summary>
    public int? OperatorId { get; set; }

    /// <summary>操作人工号</summary>
    public string? OperatorNo { get; set; }

    /// <summary>开始时间</summary>
    public DateTime? StartDate { get; set; }

    /// <summary>结束时间</summary>
    public DateTime? EndDate { get; set; }

    /// <summary>结果筛选（Success/Failed）</summary>
    public string? Result { get; set; }

    /// <summary>实体类型</summary>
    public string? EntityType { get; set; }

    /// <summary>实体ID</summary>
    public int? EntityId { get; set; }
}

/// <summary>
/// 操作日志DTO（增强版）
/// </summary>
public class OperationLogDetailDto
{
    public int Id { get; set; }
    public int OperatorId { get; set; }
    public string OperatorName { get; set; } = "";
    public string OperatorNo { get; set; } = "";
    public string Module { get; set; } = "";
    public string OperationType { get; set; } = "";
    public string Content { get; set; } = "";
    public string? EntityType { get; set; }
    public int? EntityId { get; set; }
    public string? Result { get; set; }
    public string? ErrorMessage { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime OperatedAt { get; set; }
    public SyncStatus SyncStatus { get; set; }
}

/// <summary>
/// 操作日志统计DTO
/// </summary>
public class OperationLogStatsDto
{
    public int TotalCount { get; set; }
    public int TodayCount { get; set; }
    public int WeekCount { get; set; }
    public int MonthCount { get; set; }
    public int FailedCount { get; set; }
    public List<ModuleStats> ModuleStats { get; set; } = new();
    public List<DailyStats> DailyStats { get; set; } = new();
}

public class ModuleStats
{
    public string Module { get; set; } = "";
    public int Count { get; set; }
    public double Percentage { get; set; }
}

public class DailyStats
{
    public DateTime Date { get; set; }
    public int Count { get; set; }
}

/// <summary>
/// 操作日志导出请求
/// </summary>
public class ExportOperationLogRequest
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Module { get; set; }
    public string? Format { get; set; } = "xlsx"; // xlsx/csv
}
