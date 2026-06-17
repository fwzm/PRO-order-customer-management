namespace PRO.Application.DTOs;

/// <summary>
/// 业务工作台数据
/// </summary>
public class BusinessDashboardDto
{
    // ==================== 今日概览 ====================
    public int TodayOrderCount { get; set; }
    public decimal TodayOrderAmount { get; set; }
    public int PendingOrderCount { get; set; }
    public int DeliveringOrderCount { get; set; }
    public int CompletedOrderCount { get; set; }

    // ==================== 待处理事项 ====================
    public int UnassignedOrderCount { get; set; }
    public int DraftOrderCount { get; set; }
    public int OverduePaymentCount { get; set; }
    public decimal OverduePaymentAmount { get; set; }
    public int PendingSettlementCount { get; set; }
    public decimal PendingSettlementAmount { get; set; }

    // ==================== 配送相关 ====================
    public int TodayDeliveryCount { get; set; }
    public int DeliveryFailedCount { get; set; }
    public int OverloadedDeliveryPersons { get; set; }

    // ==================== 客户相关 ====================
    public int NewCustomerCount { get; set; }
    public int VisitReminderCount { get; set; }
    public int DuplicateCustomerCount { get; set; }

    // ==================== 异常/告警 ====================
    public int SyncFailedCount { get; set; }
    public int RecentErrorCount { get; set; }
    public List<DashboardAlert> Alerts { get; set; } = [];

    // ==================== 快捷数据 ====================
    public List<OrderListItem> RecentOrders { get; set; } = [];
    public List<DashboardTask> TodayTasks { get; set; } = [];
    public List<DashboardShortcut> Shortcuts { get; set; } = [];
}

/// <summary>
/// 工作台告警
/// </summary>
public class DashboardAlert
{
    public string Type { get; set; } = ""; // Warning/Error/Info
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string? ActionText { get; set; }
    public string? ActionRoute { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 工作台待办任务
/// </summary>
public class DashboardTask
{
    public string Category { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int Count { get; set; }
    public string Priority { get; set; } = "Normal"; // High/Normal/Low
    public string? ActionRoute { get; set; }

    /// <summary>直接操作按钮列表</summary>
    public List<DashboardAction> QuickActions { get; set; } = [];
}

/// <summary>
/// 工作台快捷操作
/// </summary>
public class DashboardAction
{
    public string Name { get; set; } = "";
    public string Action { get; set; } = "";
    public string? Icon { get; set; }
    public string? Style { get; set; } // Primary/Secondary/Danger
}

/// <summary>
/// 快捷入口
/// </summary>
public class DashboardShortcut
{
    public string Title { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Route { get; set; } = "";
    public string? Permission { get; set; }
}
