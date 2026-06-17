namespace PRO.Domain.Entities;

/// <summary>
/// 异步导出任务
/// </summary>
public class ExportJob
{
    public int Id { get; set; }
    public string ExportType { get; set; } = string.Empty;  // Customer/Order/Settlement
    public string Status { get; set; } = "Pending";  // Pending/Processing/Completed/Failed
    public string? FilePath { get; set; }
    public string? FileName { get; set; }
    public int RequestedById { get; set; }
    public int BranchId { get; set; }
    public string? FilterCriteria { get; set; }  // JSON of applied filters
    public int? TotalRecords { get; set; }
    public int? ProcessedRecords { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    // Navigation
    public Employee? RequestedBy { get; set; }
    public Branch? Branch { get; set; }
}
