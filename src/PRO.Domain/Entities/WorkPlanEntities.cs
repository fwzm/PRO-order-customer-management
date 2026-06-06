using PRO.Domain.Enums;

namespace PRO.Domain.Entities;

/// <summary>
/// 工作计划实体
/// </summary>
public class WorkSchedule : ISyncable
{
    public int Id { get; set; }
    
    /// <summary>员工ID</summary>
    public int EmployeeId { get; set; }
    
    /// <summary>分公司ID</summary>
    public int BranchId { get; set; }
    
    /// <summary>排班日期</summary>
    public DateTime ScheduleDate { get; set; }
    
    /// <summary>工作开始时间</summary>
    public TimeSpan WorkStartTime { get; set; }
    
    /// <summary>工作结束时间</summary>
    public TimeSpan WorkEndTime { get; set; }
    
    /// <summary>休息开始时间</summary>
    public TimeSpan? BreakStartTime { get; set; }
    
    /// <summary>休息结束时间</summary>
    public TimeSpan? BreakEndTime { get; set; }
    
    /// <summary>总工时（分钟）</summary>
    public int TotalWorkMinutes { get; set; }
    
    /// <summary>排班类型（正常/加班/休息等）</summary>
    public string ScheduleType { get; set; } = "Normal";
    
    /// <summary>备注</summary>
    public string? Remark { get; set; }
    
    /// <summary>是否已变动（通知员工）</summary>
    public bool HasChanged { get; set; }
    
    /// <summary>创建人ID</summary>
    public int CreatedById { get; set; }
    
    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    /// <summary>更新时间</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    
    /// <summary>本地更新时间戳</summary>
    public long LocalTimestamp { get; set; }
    
    /// <summary>远程更新时间戳</summary>
    public long? RemoteTimestamp { get; set; }
    
    /// <summary>同步状态</summary>
    public SyncStatus SyncStatus { get; set; } = SyncStatus.Pending;
    
    // 导航属性
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
    
    /// <summary>员工ID</summary>
    public int EmployeeId { get; set; }
    
    /// <summary>计划日期</summary>
    public DateTime PlanDate { get; set; }
    
    /// <summary>开始时间</summary>
    public TimeSpan StartTime { get; set; }
    
    /// <summary>结束时间</summary>
    public TimeSpan EndTime { get; set; }
    
    /// <summary>时间段长度（分钟）</summary>
    public int DurationMinutes { get; set; }
    
    /// <summary>计划内容</summary>
    public string Content { get; set; } = string.Empty;
    
    /// <summary>关联客户ID</summary>
    public int? CustomerId { get; set; }
    
    /// <summary>关联订单ID</summary>
    public int? OrderId { get; set; }
    
    /// <summary>计划类型（拜访/配送/会议/其他）</summary>
    public string PlanType { get; set; } = "Other";
    
    /// <summary>执行状态（待执行/已完成/已取消）</summary>
    public string ExecutionStatus { get; set; } = "Pending";
    
    /// <summary>执行备注</summary>
    public string? ExecutionRemark { get; set; }
    
    /// <summary>备注</summary>
    public string? Remark { get; set; }
    
    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    /// <summary>更新时间</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    
    /// <summary>本地更新时间戳</summary>
    public long LocalTimestamp { get; set; }
    
    /// <summary>远程更新时间戳</summary>
    public long? RemoteTimestamp { get; set; }
    
    /// <summary>同步状态</summary>
    public SyncStatus SyncStatus { get; set; } = SyncStatus.Pending;
    
    // 导航属性
    public virtual Employee? Employee { get; set; }
    public virtual Customer? Customer { get; set; }
    public virtual Order? Order { get; set; }
}

/// <summary>
/// 计划草稿实体（仅本地保存，不同步Nocodb）
/// </summary>
public class PlanDraft
{
    public int Id { get; set; }
    
    /// <summary>创建人ID</summary>
    public int CreatedById { get; set; }
    
    /// <summary>草稿类型（排班草稿/计划草稿）</summary>
    public string DraftType { get; set; } = string.Empty;
    
    /// <summary>关联员工ID</summary>
    public int? EmployeeId { get; set; }
    
    /// <summary>计划日期</summary>
    public DateTime? PlanDate { get; set; }
    
    /// <summary>草稿内容（JSON）</summary>
    public string Content { get; set; } = string.Empty;
    
    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    /// <summary>更新时间</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
