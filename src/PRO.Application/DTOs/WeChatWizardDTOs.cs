namespace PRO.Application.DTOs;

/// <summary>
/// 企微配置向导步骤
/// </summary>
public enum WeChatWizardStep
{
    /// <summary>欢迎页</summary>
    Welcome = 0,
    /// <summary>创建应用</summary>
    CreateApp = 1,
    /// <summary>配置凭证</summary>
    ConfigureCredentials = 2,
    /// <summary>配置回调</summary>
    ConfigureCallback = 3,
    /// <summary>通讯录同步</summary>
    SyncContacts = 4,
    /// <summary>客户同步</summary>
    SyncCustomers = 5,
    /// <summary>完成</summary>
    Complete = 6
}

/// <summary>
/// 企微配置向导状态
/// </summary>
public class WeChatWizardState
{
    public WeChatWizardStep CurrentStep { get; set; } = WeChatWizardStep.Welcome;
    public bool IsCompleted { get; set; }
    public string? ErrorMessage { get; set; }

    // 步骤1: 应用信息
    public string? AppName { get; set; }
    public string? AppDescription { get; set; }

    // 步骤2: 凭证信息
    public string? CorpId { get; set; }
    public string? CorpSecret { get; set; }
    public string? AgentId { get; set; }
    public string? Token { get; set; }
    public string? EncodingAesKey { get; set; }

    // 步骤3: 回调配置
    public string? CallbackUrl { get; set; }
    public bool CallbackVerified { get; set; }

    // 步骤4: 通讯录同步
    public bool ContactsSyncEnabled { get; set; }
    public int SyncedEmployeeCount { get; set; }

    // 步骤5: 客户同步
    public bool CustomerSyncEnabled { get; set; }
    public int SyncedCustomerCount { get; set; }
}

/// <summary>
/// 企微配置DTO
/// </summary>
public class WeChatWizardConfigDto
{
    public int Id { get; set; }
    public string? CorpId { get; set; }
    public string? CorpSecret { get; set; }
    public string? AgentId { get; set; }
    public string? Token { get; set; }
    public string? EncodingAesKey { get; set; }
    public string? CallbackUrl { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsConfigured { get; set; }
    public DateTime? LastSyncTime { get; set; }
    public string? LastSyncStatus { get; set; }
}

/// <summary>
/// 保存企微配置请求
/// </summary>
public class SaveWeChatWizardConfigRequest
{
    public string CorpId { get; set; } = "";
    public string CorpSecret { get; set; } = "";
    public string AgentId { get; set; } = "";
    public string? Token { get; set; }
    public string? EncodingAesKey { get; set; }
    public string? CallbackUrl { get; set; }
    public bool IsEnabled { get; set; }
}

/// <summary>
/// 企微配置向导步骤DTO
/// </summary>
public class WeChatWizardStepDto
{
    public WeChatWizardStep Step { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsCompleted { get; set; }
    public bool IsCurrent { get; set; }
    public bool IsAccessible { get; set; }
}

/// <summary>
/// 企微连接测试结果
/// </summary>
public class WeChatConnectionTestResult
{
    public bool Success { get; set; }
    public string? CorpName { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime TestTime { get; set; }
}

/// <summary>
/// 企微同步配置
/// </summary>
public class WeChatSyncConfig
{
    public bool EnableContactSync { get; set; }
    public bool EnableCustomerSync { get; set; }
    public bool EnableVisitSync { get; set; }
    public int SyncIntervalMinutes { get; set; } = 30;
    public DateTime? LastContactSyncTime { get; set; }
    public DateTime? LastCustomerSyncTime { get; set; }
}

/// <summary>
/// 企微同步结果
/// </summary>
public class WeChatSyncResult
{
    public bool Success { get; set; }
    public string SyncType { get; set; } = "";
    public int TotalCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<string> Errors { get; set; } = [];
    public DateTime SyncTime { get; set; }
}
