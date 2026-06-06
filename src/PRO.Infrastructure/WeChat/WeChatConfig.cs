using Newtonsoft.Json;

namespace PRO.Infrastructure.WeChat;

/// <summary>
/// 企业微信配置
/// </summary>
public class WeChatConfig
{
    /// <summary>
    /// 企业ID
    /// </summary>
    public string CorpId { get; set; } = string.Empty;

    /// <summary>
    /// 应用Secret
    /// </summary>
    public string CorpSecret { get; set; } = string.Empty;

    /// <summary>
    /// 应用ID（AgentId）
    /// </summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>
    /// 默认Webhook地址
    /// </summary>
    public string? DefaultWebhookUrl { get; set; }

    /// <summary>
    /// 是否启用企业微信
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 访问令牌（从API获取，不持久化）
    /// </summary>
    [JsonIgnore]
    public string? AccessToken { get; set; }

    /// <summary>
    /// 令牌过期时间
    /// </summary>
    [JsonIgnore]
    public DateTime? TokenExpiresAt { get; set; }

    /// <summary>
    /// 是否需要刷新令牌
    /// </summary>
    [JsonIgnore]
    public bool NeedsTokenRefresh => string.IsNullOrEmpty(AccessToken) ||
                                      !TokenExpiresAt.HasValue ||
                                      DateTime.UtcNow >= TokenExpiresAt.Value.AddMinutes(-5);
}

/// <summary>
/// 企业微信 API 响应基类
/// </summary>
public class WeChatApiResponse
{
    [JsonProperty("errcode")]
    public int ErrCode { get; set; }

    [JsonProperty("errmsg")]
    public string ErrMsg { get; set; } = string.Empty;

    public bool IsSuccess => ErrCode == 0;
}

/// <summary>
/// 获取访问令牌响应
/// </summary>
public class GetTokenResponse : WeChatApiResponse
{
    [JsonProperty("access_token")]
    public string? AccessToken { get; set; }

    [JsonProperty("expires_in")]
    public int ExpiresIn { get; set; }
}

/// <summary>
/// 发送消息请求
/// </summary>
public class SendMessageRequest
{
    [JsonProperty("touser")]
    public string ToUser { get; set; } = string.Empty;

    [JsonProperty("toparty")]
    public string? ToParty { get; set; }

    [JsonProperty("totag")]
    public string? ToTag { get; set; }

    [JsonProperty("msgtype")]
    public string MsgType { get; set; } = "text";

    [JsonProperty("agentid")]
    public string AgentId { get; set; } = string.Empty;

    [JsonProperty("text")]
    public MessageContent? Text { get; set; }

    [JsonProperty("markdown")]
    public MessageContent? Markdown { get; set; }
}

public class MessageContent
{
    [JsonProperty("content")]
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// 发送消息响应
/// </summary>
public class SendMessageResponse : WeChatApiResponse
{
    [JsonProperty("invaliduser")]
    public string? InvalidUser { get; set; }

    [JsonProperty("invalidparty")]
    public string? InvalidParty { get; set; }

    [JsonProperty("invalidtag")]
    public string? InvalidTag { get; set; }
}

/// <summary>
/// 通讯录用户信息
/// </summary>
public class WeChatUser
{
    [JsonProperty("userid")]
    public string UserId { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("alias")]
    public string? Alias { get; set; }

    [JsonProperty("department")]
    public List<int>? Department { get; set; }

    [JsonProperty("position")]
    public string? Position { get; set; }

    [JsonProperty("mobile")]
    public string? Mobile { get; set; }

    [JsonProperty("email")]
    public string? Email { get; set; }

    [JsonProperty("is_leave_in_allow")]
    public int IsLeaveInAllow { get; set; }

    [JsonProperty("status")]
    public int Status { get; set; }
}

/// <summary>
/// 获取通讯录响应
/// </summary>
public class GetUserListResponse : WeChatApiResponse
{
    [JsonProperty("userlist")]
    public List<WeChatUser>? UserList { get; set; }
}

/// <summary>
/// 部门信息
/// </summary>
public class WeChatDepartment
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("parentid")]
    public int ParentId { get; set; }

    [JsonProperty("order")]
    public int Order { get; set; }
}

/// <summary>
/// 获取部门列表响应
/// </summary>
public class GetDepartmentListResponse : WeChatApiResponse
{
    [JsonProperty("department")]
    public List<WeChatDepartment>? Department { get; set; }
}

/// <summary>
/// 企业微信客户
/// </summary>
public class WeChatExternalContact
{
    [JsonProperty("external_userid")]
    public string ExternalUserId { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("type")]
    public int Type { get; set; }

    [JsonProperty("avatar")]
    public string? Avatar { get; set; }

    [JsonProperty("gender")]
    public int Gender { get; set; }

    [JsonProperty("unionid")]
    public string? UnionId { get; set; }

    [JsonProperty("position")]
    public string? Position { get; set; }

    [JsonProperty("corp_name")]
    public string? CorpName { get; set; }

    [JsonProperty("corp_full_name")]
    public string? CorpFullName { get; set; }
}

/// <summary>
/// 获取客户列表响应
/// </summary>
public class GetExternalContactListResponse : WeChatApiResponse
{
    [JsonProperty("external_userid")]
    public List<string>? ExternalUserId { get; set; }

    [JsonProperty("follow_user")]
    public List<string>? FollowUser { get; set; }
}
