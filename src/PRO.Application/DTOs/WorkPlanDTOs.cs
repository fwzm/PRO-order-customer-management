using PRO.Domain.Enums;

namespace PRO.Application.DTOs;

// ==================== 工作计划相关 ====================

/// <summary>
/// 排班列表项
/// </summary>
public class WorkScheduleListItem
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNo { get; set; } = string.Empty;
    public DateTime ScheduleDate { get; set; }
    public TimeSpan WorkStartTime { get; set; }
    public TimeSpan WorkEndTime { get; set; }
    public TimeSpan? BreakStartTime { get; set; }
    public TimeSpan? BreakEndTime { get; set; }
    public int TotalWorkMinutes { get; set; }
    public string ScheduleType { get; set; } = string.Empty;
    public bool HasChanged { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 创建排班请求
/// </summary>
public class CreateWorkScheduleRequest
{
    public int EmployeeId { get; set; }
    public DateTime ScheduleDate { get; set; }
    public TimeSpan WorkStartTime { get; set; }
    public TimeSpan WorkEndTime { get; set; }
    public TimeSpan? BreakStartTime { get; set; }
    public TimeSpan? BreakEndTime { get; set; }
    public string ScheduleType { get; set; } = "Normal";
    public string? Remark { get; set; }
}

/// <summary>
/// 批量创建排班请求
/// </summary>
public class BatchCreateWorkScheduleRequest
{
    public int EmployeeId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public TimeSpan WorkStartTime { get; set; }
    public TimeSpan WorkEndTime { get; set; }
    public TimeSpan? BreakStartTime { get; set; }
    public TimeSpan? BreakEndTime { get; set; }
    public string ScheduleType { get; set; } = "Normal";
    public string? Remark { get; set; }
}

/// <summary>
/// 日历排班视图项
/// </summary>
public class CalendarScheduleItem
{
    public DateTime Date { get; set; }
    public bool IsWorkday { get; set; }
    public string? WorkStartTime { get; set; }
    public string? WorkEndTime { get; set; }
    public int TotalWorkMinutes { get; set; }
    public string ScheduleType { get; set; } = string.Empty;
    public bool HasChanged { get; set; }
    public string? Remark { get; set; }
    public List<WorkPlanDto> Plans { get; set; } = new();
}

/// <summary>
/// 工作计划列表项
/// </summary>
public class WorkPlanListItem
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public DateTime PlanDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int DurationMinutes { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public string PlanType { get; set; } = string.Empty;
    public string ExecutionStatus { get; set; } = string.Empty;
    public string? ExecutionRemark { get; set; }
}

/// <summary>
/// 工作计划DTO
/// </summary>
public class WorkPlanDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public DateTime PlanDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public string StartTimeStr => StartTime.ToString(@"hh\:mm");
    public TimeSpan EndTime { get; set; }
    public string EndTimeStr => EndTime.ToString(@"hh\:mm");
    public int DurationMinutes { get; set; }
    public string Content { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public int? OrderId { get; set; }
    public string PlanType { get; set; } = string.Empty;
    public string ExecutionStatus { get; set; } = string.Empty;
    public string? ExecutionRemark { get; set; }
    public string? Remark { get; set; }
    public bool HasChanged { get; set; }
}

/// <summary>
/// 创建工作计划请求
/// </summary>
public class CreateWorkPlanRequest
{
    public int EmployeeId { get; set; }
    public DateTime PlanDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string Content { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public int? OrderId { get; set; }
    public string PlanType { get; set; } = "Other";
    public string? Remark { get; set; }
}

/// <summary>
/// 更新工作计划请求
/// </summary>
public class UpdateWorkPlanRequest
{
    public int Id { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string Content { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public int? OrderId { get; set; }
    public string PlanType { get; set; } = "Other";
    public string ExecutionStatus { get; set; } = "Pending";
    public string? ExecutionRemark { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 日计划视图
/// </summary>
public class DailyPlanView
{
    public DateTime Date { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public WorkScheduleListItem? Schedule { get; set; }
    public List<WorkPlanDto> Plans { get; set; } = new();
    public int TotalWorkMinutes { get; set; }
    public int TotalPlanMinutes { get; set; }
}

/// <summary>
/// 计划草稿DTO
/// </summary>
public class PlanDraftDto
{
    public int Id { get; set; }
    public int CreatedById { get; set; }
    public string DraftType { get; set; } = string.Empty;
    public int? EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public DateTime? PlanDate { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 保存计划草稿请求
/// </summary>
public class SavePlanDraftRequest
{
    public int? Id { get; set; }
    public string DraftType { get; set; } = string.Empty;
    public int? EmployeeId { get; set; }
    public DateTime? PlanDate { get; set; }
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// 员工排班日历请求
/// </summary>
public class EmployeeScheduleCalendarRequest
{
    public int EmployeeId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

// ==================== 权限相关 ====================

/// <summary>
/// 权限树节点
/// </summary>
public class PermissionTreeNode
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public PermissionType PermissionType { get; set; }
    public int? ParentId { get; set; }
    public int SortOrder { get; set; }
    public bool IsAllowed { get; set; }
    public bool IsFieldLevel { get; set; }
    public string? FieldName { get; set; }
    public List<PermissionTreeNode> Children { get; set; } = new();
}

/// <summary>
/// 角色权限配置
/// </summary>
public class RolePermissionConfig
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public List<PermissionTreeNode> Permissions { get; set; } = new();
}

/// <summary>
/// 更新角色权限请求
/// </summary>
public class UpdateRolePermissionRequest
{
    public int RoleId { get; set; }
    public List<RolePermissionItem> Permissions { get; set; } = new();
}

/// <summary>
/// 角色权限项
/// </summary>
public class RolePermissionItem
{
    public int PermissionId { get; set; }
    public bool IsAllowed { get; set; }
    public bool IsFieldLevel { get; set; }
    public string? FieldName { get; set; }
}

// ==================== 系统配置相关 ====================

/// <summary>
/// 企业微信配置DTO
/// </summary>
public class WeChatConfigDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CorpId { get; set; } = string.Empty;
    public string AppSecretMasked { get; set; } = string.Empty;
    public int AgentId { get; set; }
    public string? WebhookUrl { get; set; }
    public bool IsEnabled { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 保存企业微信配置请求
/// </summary>
public class SaveWeChatConfigRequest
{
    public int? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CorpId { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;
    public int AgentId { get; set; }
    public string? WebhookUrl { get; set; }
    public string? Token { get; set; }
    public string? EncodingAESKey { get; set; }
    public bool IsEnabled { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// Webhook列表项
/// </summary>
public class WebhookListItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string WebhookUrl { get; set; } = string.Empty;
    public string? TriggerCondition { get; set; }
    public string? Remark { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime? LastTestTime { get; set; }
    public string? LastTestResult { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 同步配置DTO
/// </summary>
public class SyncConfigDto
{
    /// <summary>是否启用自动同步</summary>
    public bool AutoSyncEnabled { get; set; }
    
    /// <summary>同步间隔（分钟）</summary>
    public int SyncIntervalMinutes { get; set; } = 30;
    
    /// <summary>冲突解决策略</summary>
    public ConflictResolution ConflictResolution { get; set; } = ConflictResolution.TimestampFirst;
    
    /// <summary>组织架构同步开关</summary>
    public bool OrganizationSyncEnabled { get; set; }
    
    /// <summary>组织架构同步频率（小时）</summary>
    public int OrganizationSyncIntervalHours { get; set; } = 24;
}

/// <summary>
/// 备份记录DTO
/// </summary>
public class BackupRecordDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string BackupType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string FileSizeStr => FormatFileSize(FileSize);
    public DateTime BackupTime { get; set; }
    public DateTime ExpireTime { get; set; }
    public string Status { get; set; } = string.Empty;
    
    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}

/// <summary>
/// Nocodb配置DTO
/// </summary>
public class NocoDBConfigDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DatabaseType { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Database { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PasswordMasked { get; set; } = string.Empty;
    public string ApiUrl { get; set; } = string.Empty;
    public string? ApiKeyMasked { get; set; }
    public bool IsEnabled { get; set; }
}

/// <summary>
/// 保存Nocodb配置请求
/// </summary>
public class SaveNocoDBConfigRequest
{
    public int? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DatabaseType { get; set; } = "postgresql";
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 5432;
    public string Database { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ApiUrl { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public bool IsEnabled { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// Supabase 配置DTO
/// </summary>
public class SupabaseConfigDto
{
    public string Url { get; set; } = string.Empty;
    public string AnonKey { get; set; } = string.Empty;
    public string? ServiceKeyMasked { get; set; }
    public bool IsConfigured { get; set; }
}

/// <summary>
/// 保存Supabase配置请求
/// </summary>
public class SaveSupabaseConfigRequest
{
    public string Url { get; set; } = string.Empty;
    public string AnonKey { get; set; } = string.Empty;
    public string? ServiceKey { get; set; }
}

/// <summary>
/// 操作日志DTO
/// </summary>
public class OperationLogDto
{
    public int Id { get; set; }
    public int OperatorId { get; set; }
    public string OperatorName { get; set; } = string.Empty;
    public string OperatorNo { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string OperationType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public int? EntityId { get; set; }
    public string Result { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime OperatedAt { get; set; }
}

/// <summary>
/// 本地设置DTO
/// </summary>
public class LocalSettingDto
{
    public CloseBehavior CloseBehavior { get; set; } = CloseBehavior.MinimizeToTray;
    public bool EnableNotification { get; set; } = true;
    public bool EnableSound { get; set; } = true;
    public bool AutoSyncOnStartup { get; set; } = true;
    public int SyncIntervalMinutes { get; set; } = 30;
}

/// <summary>
/// 保存本地设置请求
/// </summary>
public class SaveLocalSettingRequest
{
    public CloseBehavior CloseBehavior { get; set; }
    public bool EnableNotification { get; set; }
    public bool EnableSound { get; set; }
    public bool AutoSyncOnStartup { get; set; }
    public int SyncIntervalMinutes { get; set; }
}
