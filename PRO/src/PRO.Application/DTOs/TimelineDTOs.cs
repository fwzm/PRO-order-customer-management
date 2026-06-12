using PRO.Domain.Enums;

namespace PRO.Application.DTOs;

/// <summary>
/// 客户时间线事件
/// </summary>
public class CustomerTimelineEvent
{
    public DateTime EventTime { get; set; }
    public string EventType { get; set; } = ""; // Order/Visit/Payment/WeChat/StatusChange/Note
    public string EventIcon { get; set; } = "";
    public string EventColor { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string? OperatorName { get; set; }
    public int? RelatedId { get; set; }
    public string? RelatedType { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];
}

/// <summary>
/// 客户时间线数据
/// </summary>
public class CustomerTimelineDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public List<CustomerTimelineEvent> Events { get; set; } = [];
    public int TotalCount { get; set; }
    public bool HasMore { get; set; }
}

/// <summary>
/// 时间线事件类型定义
/// </summary>
public static class TimelineEventTypes
{
    public const string Order = "Order";
    public const string OrderStatusChange = "OrderStatusChange";
    public const string Visit = "Visit";
    public const string Payment = "Payment";
    public const string WeChat = "WeChat";
    public const string CustomerUpdate = "CustomerUpdate";
    public const string Note = "Note";
    public const string Opportunity = "Opportunity";
    public const string Settlement = "Settlement";

    public static string GetIcon(string eventType) => eventType switch
    {
        Order => "📦",
        OrderStatusChange => "🔄",
        Visit => "👋",
        Payment => "💰",
        WeChat => "💬",
        CustomerUpdate => "✏️",
        Note => "📝",
        Opportunity => "💼",
        Settlement => "📊",
        _ => "📌"
    };

    public static string GetColor(string eventType) => eventType switch
    {
        Order => "#007AFF",
        OrderStatusChange => "#5856D6",
        Visit => "#34C759",
        Payment => "#FF9500",
        WeChat => "#07C160",
        CustomerUpdate => "#8E8E93",
        Note => "#FF2D55",
        Opportunity => "#AF52DE",
        Settlement => "#FF9500",
        _ => "#8E8E93"
    };
}
