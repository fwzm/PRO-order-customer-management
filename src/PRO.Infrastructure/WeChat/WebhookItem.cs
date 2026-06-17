namespace PRO.Infrastructure.WeChat;

public class WebhookItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string WebhookUrl { get; set; } = string.Empty;
    public string? TriggerCondition { get; set; }
    public string? Remark { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime? LastTestTime { get; set; }
    public string? LastTestStatus { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }
}

public class WebhookMessage
{
    public string MsgType { get; set; } = "text";
    public string? Content { get; set; }
    public string? Markdown { get; set; }
    public WebhookNewsArticle? News { get; set; }
}

public class WebhookNewsArticle
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? PicUrl { get; set; }
}

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
