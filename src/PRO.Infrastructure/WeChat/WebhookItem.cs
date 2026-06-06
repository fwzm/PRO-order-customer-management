namespace PRO.Infrastructure.WeChat;

/// <summary>
/// Webhook 配置项
/// </summary>
public class WebhookItem
{
    /// <summary>
    /// Webhook ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Webhook 名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Webhook URL
    /// </summary>
    public string WebhookUrl { get; set; } = string.Empty;

    /// <summary>
    /// 触发条件（如：订单创建、订单状态变更、客户新增等）
    /// </summary>
    public string? TriggerCondition { get; set; }

    /// <summary>
    /// 备注说明
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 最后测试时间
    /// </summary>
    public DateTime? LastTestTime { get; set; }

    /// <summary>
    /// 最后测试状态（success/failed/null）
    /// </summary>
    public string? LastTestStatus { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Webhook 消息内容
/// </summary>
public class WebhookMessage
{
    /// <summary>
    /// 消息类型：text/markdown/news
    /// </summary>
    public string MsgType { get; set; } = "text";

    /// <summary>
    /// 文本消息内容
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Markdown 内容
    /// </summary>
    public string? Markdown { get; set; }

    /// <summary>
    /// 图文消息
    /// </summary>
    public WebhookNewsArticle? News { get; set; }
}

/// <summary>
/// 图文消息文章
/// </summary>
public class WebhookNewsArticle
{
    /// <summary>
    /// 标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 点击后跳转的链接
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 图片链接（可使用图片的永久素材media_id）
    /// </summary>
    public string? PicUrl { get; set; }
}

/// <summary>
/// Webhook 触发类型枚举
/// </summary>
public static class WebhookTriggerTypes
{
    public const string OrderCreated = "order_created";
    public const string OrderAssigned = "order_assigned";
    public const string OrderDelivering = "order_delivering";
    public const string OrderCompleted = "order_completed";
    public const string OrderCancelled = "order_cancelled";
    public const string CustomerCreated = "customer_created";
    public const string CustomerUpdated = "customer_updated";
    public const string SettlementCreated = "settlement_created";
    public const string WorkScheduleChanged = "workschedule_changed";
    public const string SyncCompleted = "sync_completed";

    public static readonly Dictionary<string, string> DisplayNames = new()
    {
        { OrderCreated, "订单创建" },
        { OrderAssigned, "订单已分配" },
        { OrderDelivering, "订单配送中" },
        { OrderCompleted, "订单已完成" },
        { OrderCancelled, "订单已取消" },
        { CustomerCreated, "客户新增" },
        { CustomerUpdated, "客户更新" },
        { SettlementCreated, "结算完成" },
        { WorkScheduleChanged, "排班变动" },
        { SyncCompleted, "同步完成" }
    };
}
