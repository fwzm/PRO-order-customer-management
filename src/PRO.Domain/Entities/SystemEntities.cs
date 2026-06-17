using PRO.Domain.Enums;

namespace PRO.Domain.Entities;

/// <summary>
/// 操作日志实体
/// </summary>
public class OperationLog
{
    public int Id { get; set; }

    /// <summary>操作人ID</summary>
    public int OperatorId { get; set; }

    /// <summary>操作人工号</summary>
    public string OperatorNo { get; set; } = string.Empty;

    /// <summary>操作模块</summary>
    public string Module { get; set; } = string.Empty;

    /// <summary>操作类型</summary>
    public string OperationType { get; set; } = string.Empty;

    /// <summary>操作内容</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>关联实体类型</summary>
    public string? EntityType { get; set; }

    /// <summary>关联实体ID</summary>
    public int? EntityId { get; set; }

    /// <summary>操作结果</summary>
    public string Result { get; set; } = "Success";

    /// <summary>错误信息</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>IP地址</summary>
    public string? IpAddress { get; set; }

    /// <summary>操作时间</summary>
    public DateTime OperatedAt { get; set; } = DateTime.Now;

    /// <summary>同步状态（平时为Pending，结算时批量同步）</summary>
    public SyncStatus SyncStatus { get; set; } = SyncStatus.Pending;

    // 导航属性
    public virtual Employee? Operator { get; set; }
}

/// <summary>
/// 同步记录实体
/// </summary>
public class SyncRecord
{
    public int Id { get; set; }

    /// <summary>同步类型（全量/增量）</summary>
    public string SyncType { get; set; } = string.Empty;

    /// <summary>同步方向（上传/下载）</summary>
    public string SyncDirection { get; set; } = string.Empty;

    /// <summary>同步表名</summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>同步记录数</summary>
    public int RecordCount { get; set; }

    /// <summary>冲突记录数</summary>
    public int ConflictCount { get; set; }

    /// <summary>失败记录数</summary>
    public int FailedCount { get; set; }

    /// <summary>状态</summary>
    public SyncStatus Status { get; set; }

    /// <summary>开始时间</summary>
    public DateTime StartTime { get; set; }

    /// <summary>结束时间</summary>
    public DateTime? EndTime { get; set; }

    /// <summary>耗时（秒）</summary>
    public int DurationSeconds { get; set; }

    /// <summary>错误信息</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// 同步配置实体
/// </summary>
public class SyncConfig
{
    public int Id { get; set; }

    /// <summary>配置名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>配置键</summary>
    public string ConfigKey { get; set; } = string.Empty;

    /// <summary>配置值</summary>
    public string ConfigValue { get; set; } = string.Empty;

    /// <summary>配置类型</summary>
    public string ConfigType { get; set; } = string.Empty;

    /// <summary>描述</summary>
    public string? Description { get; set; }

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>同步间隔（分钟）</summary>
    public int SyncIntervalMinutes { get; set; } = 30;

    /// <summary>是否自动同步</summary>
    public bool AutoSyncEnabled { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>更新时间</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// 本地设置实体
/// </summary>
public class LocalSetting
{
    public int Id { get; set; }

    /// <summary>员工ID（用于区分不同用户的设置）</summary>
    public int? EmployeeId { get; set; }

    /// <summary>设置键</summary>
    public string SettingKey { get; set; } = string.Empty;

    /// <summary>设置值</summary>
    public string SettingValue { get; set; } = string.Empty;

    /// <summary>设置类型</summary>
    public string SettingType { get; set; } = string.Empty;

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>更新时间</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    // 导航属性
    public Employee? Employee { get; set; }
}

/// <summary>
/// 企业微信配置实体
/// </summary>
public class WeChatConfig
{
    public int Id { get; set; }

    /// <summary>配置名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>企业ID</summary>
    public string CorpId { get; set; } = string.Empty;

    /// <summary>应用Secret</summary>
    public string AppSecret { get; set; } = string.Empty;

    /// <summary>应用ID</summary>
    public int AgentId { get; set; }

    /// <summary>Webhook地址</summary>
    public string? WebhookUrl { get; set; }

    /// <summary>Token</summary>
    public string? Token { get; set; }

    /// <summary>EncodingAESKey</summary>
    public string? EncodingAESKey { get; set; }

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>更新时间</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// Webhook配置实体
/// </summary>
public class Webhook
{
    public int Id { get; set; }

    /// <summary>名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Webhook地址</summary>
    public string WebhookUrl { get; set; } = string.Empty;

    /// <summary>触发条件</summary>
    public string? TriggerCondition { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; }

    /// <summary>最后测试时间</summary>
    public DateTime? LastTestTime { get; set; }

    /// <summary>最后测试结果</summary>
    public string? LastTestResult { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>更新时间</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    /// <summary>创建人ID</summary>
    public int CreatedById { get; set; }

    // 导航属性
    public virtual Employee? Creator { get; set; }
}

/// <summary>
/// 数据备份记录实体
/// </summary>
public class BackupRecord
{
    public int Id { get; set; }

    /// <summary>备份文件名</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>备份文件路径</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>备份类型（自动/手动）</summary>
    public string BackupType { get; set; } = string.Empty;

    /// <summary>备份大小（字节）</summary>
    public long FileSize { get; set; }

    /// <summary>备份时间</summary>
    public DateTime BackupTime { get; set; } = DateTime.Now;

    /// <summary>过期时间</summary>
    public DateTime ExpireTime { get; set; }

    /// <summary>状态</summary>
    public string Status { get; set; } = "Success";

    /// <summary>备注</summary>
    public string? Remark { get; set; }
}

/// <summary>
/// 企业微信同步日志实体
/// </summary>
public class WeChatSyncLog
{
    public int Id { get; set; }

    /// <summary>同步类型</summary>
    public string SyncType { get; set; } = string.Empty;

    /// <summary>同步方向</summary>
    public string SyncDirection { get; set; } = string.Empty;

    /// <summary>同步记录数</summary>
    public int RecordCount { get; set; }

    /// <summary>状态</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>错误信息</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>同步时间</summary>
    public DateTime SyncTime { get; set; } = DateTime.Now;

    /// <summary>详情（JSON）</summary>
    public string? Details { get; set; }
}

/// <summary>
/// 应用版本实体（对应 app_versions 表）
/// </summary>
public class AppVersion
{
    public int Id { get; set; }

    /// <summary>版本号</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>发布日期</summary>
    public string? ReleaseDate { get; set; }

    /// <summary>更新说明</summary>
    public string? ReleaseNotes { get; set; }

    /// <summary>下载地址</summary>
    public string? DownloadUrl { get; set; }

    /// <summary>是否强制更新</summary>
    public bool IsRequired { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
