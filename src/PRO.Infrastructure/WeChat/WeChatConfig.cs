using Newtonsoft.Json;

namespace PRO.Infrastructure.WeChat;

public class WeChatConfig
{
    public string CorpId { get; set; } = string.Empty;
    public string CorpSecret { get; set; } = string.Empty;
    public string AgentId { get; set; } = string.Empty;
    public string? DefaultWebhookUrl { get; set; }
    public bool IsEnabled { get; set; }

    [JsonIgnore]
    public string? AccessToken { get; set; }

    [JsonIgnore]
    public DateTime? TokenExpiresAt { get; set; }

    [JsonIgnore]
    public bool NeedsTokenRefresh => string.IsNullOrEmpty(AccessToken)
        || !TokenExpiresAt.HasValue
        || DateTime.UtcNow >= TokenExpiresAt.Value.AddMinutes(-5);
}

public class WeChatApiResponse
{
    [JsonProperty("errcode")]
    public int ErrCode { get; set; }

    [JsonProperty("errmsg")]
    public string ErrMsg { get; set; } = string.Empty;

    public bool IsSuccess => ErrCode == 0;
}

public class GetTokenResponse : WeChatApiResponse
{
    [JsonProperty("access_token")]
    public string? AccessToken { get; set; }

    [JsonProperty("expires_in")]
    public int ExpiresIn { get; set; }
}

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

public class SendMessageResponse : WeChatApiResponse
{
    [JsonProperty("invaliduser")]
    public string? InvalidUser { get; set; }

    [JsonProperty("invalidparty")]
    public string? InvalidParty { get; set; }

    [JsonProperty("invalidtag")]
    public string? InvalidTag { get; set; }
}

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

public class GetUserListResponse : WeChatApiResponse
{
    [JsonProperty("userlist")]
    public List<WeChatUser>? UserList { get; set; }
}

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

public class GetDepartmentListResponse : WeChatApiResponse
{
    [JsonProperty("department")]
    public List<WeChatDepartment>? Department { get; set; }
}

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

public class GetExternalContactListResponse : WeChatApiResponse
{
    [JsonProperty("external_userid")]
    public List<string>? ExternalUserId { get; set; }

    [JsonProperty("follow_user")]
    public List<string>? FollowUser { get; set; }
}
