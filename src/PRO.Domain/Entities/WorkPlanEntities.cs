using PRO.Domain.Enums;

namespace PRO.Domain.Entities;

/// <summary>
/// 工作计划实体
/// </summary>
public class WorkSchedule : ISyncable
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public int BranchId { get; set; }
    public DateTime ScheduleDate { get; set; }
    public TimeSpan WorkStartTime { get; set; }
    public TimeSpan WorkEndTime { get; set; }
    public TimeSpan? BreakStartTime { get; set; }
    public TimeSpan? BreakEndTime { get; set; }
    public int TotalWorkMinutes { get; set; }
    public string ScheduleType { get; set; } = "Normal";
    public string? Remark { get; set; }
    public bool HasChanged { get; set; }
    public int CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public long LocalTimestamp { get; set; }
    public long? RemoteTimestamp { get; set; }
    public SyncStatus SyncStatus { get; set; } = SyncStatus.Pending;

    public virtual Employee? Employee { get; set; }
    public virtual Branch? Branch { get; set; }
    public virtual Employee? Creator { get; set; }
}

/// <summary>
/// 工作计划明细实体
/// </summary>
public class WorkPlan : ISyncable
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public DateTime PlanDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int DurationMinutes { get; set; }
    public string Content { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public int? OrderId { get; set; }
    public string PlanType { get; set; } = "Other";
    public string ExecutionStatus { get; set; } = "Pending";
    public string? ExecutionRemark { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public long LocalTimestamp { get; set; }
    public long? RemoteTimestamp { get; set; }
    public SyncStatus SyncStatus { get; set; } = SyncStatus.Pending;

    public virtual Employee? Employee { get; set; }
    public virtual Customer? Customer { get; set; }
    public virtual Order? Order { get; set; }
}

/// <summary>
/// 计划草稿实体（仅本地保存，不同步NocoDB）
/// </summary>
public class PlanDraft
{
    public int Id { get; set; }
    public int CreatedById { get; set; }
    public string DraftType { get; set; } = string.Empty;
    public int? EmployeeId { get; set; }
    public DateTime? PlanDate { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
