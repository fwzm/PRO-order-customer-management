namespace PRO.Application.DTOs;

/// <summary>
/// 导出任务
/// </summary>
public class ExportTaskDto
{
    public int Id { get; set; }
    public string TaskId { get; set; } = "";
    public string ExportType { get; set; } = ""; // Customer/Order/Settlement/Inventory
    public string FileName { get; set; } = "";
    public string Status { get; set; } = ""; // Pending/Processing/Completed/Failed
    public int Progress { get; set; } // 0-100
    public int TotalRecords { get; set; }
    public int ProcessedRecords { get; set; }
    public string? FilePath { get; set; }
    public long FileSize { get; set; }
    public string? ErrorMessage { get; set; }
    public int RequestedById { get; set; }
    public string RequestedByName { get; set; } = "";
    public DateTime RequestedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? Duration => CompletedAt - RequestedAt;
    public bool CanDownload => Status == "Completed" && !string.IsNullOrEmpty(FilePath);
}

/// <summary>
/// 导出请求
/// </summary>
public class ExportRequest
{
    public string ExportType { get; set; } = "";
    public string? Filter { get; set; } // JSON格式的筛选条件
    public string? FileName { get; set; }
    public string Format { get; set; } = "xlsx"; // xlsx/csv
}

/// <summary>
/// 导出进度回调
/// </summary>
public class ExportProgress
{
    public string TaskId { get; set; } = "";
    public int Progress { get; set; }
    public int ProcessedRecords { get; set; }
    public int TotalRecords { get; set; }
    public string Status { get; set; } = "";
    public string? Message { get; set; }
}
