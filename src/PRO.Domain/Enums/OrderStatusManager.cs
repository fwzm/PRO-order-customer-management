namespace PRO.Domain.Enums;

/// <summary>
/// 订单状态流转规则接口 - Infrastructure层通过此接口调用
/// </summary>
public interface IOrderStatusManager
{
    bool IsValidTransition(OrderStatus from, OrderStatus to);
    IReadOnlyList<OrderStatus> GetValidNextStatuses(OrderStatus from);
    string GetStatusName(OrderStatus status);
    string GetStatusColor(OrderStatus status);
    string GetTransitionDescription(OrderStatus from, OrderStatus to);
    string GetInvalidTransitionMessage(OrderStatus from, OrderStatus to);
    bool IsTerminalState(OrderStatus status);
    bool IsDeletable(OrderStatus status);
    bool IsEditable(OrderStatus status);
}

/// <summary>
/// 订单状态流转规则 - 定义合法的状态转换路径
/// 所有状态流转规则统一收归Domain层，Infrastructure层通过IOrderStatusManager接口调用。
/// </summary>
public class OrderStatusTransitionManager : IOrderStatusManager
{
    /// <summary>
    /// 合法的状态转换映射表（合并两版本：Domain + Infrastructure）
    /// Key = 当前状态, Value = 允许转换到的目标状态列表
    /// </summary>
    private static readonly Dictionary<OrderStatus, HashSet<OrderStatus>> AllowedTransitions = new()
    {
        [OrderStatus.Draft] = [OrderStatus.Pending, OrderStatus.Cancelled],
        [OrderStatus.Pending] = [OrderStatus.Assigned, OrderStatus.Cancelled],
        // Assigned → Pending（退回待分配）、Cancelled、Delivering
        [OrderStatus.Assigned] = [OrderStatus.Delivering, OrderStatus.Cancelled, OrderStatus.Pending],
        [OrderStatus.Delivering] = [OrderStatus.Completed, OrderStatus.Failed],
        [OrderStatus.Completed] = [], // 已完成是终态
        // Failed → Pending（重新排队）、Assigned（重新分配）、Cancelled
        [OrderStatus.Failed] = [OrderStatus.Pending, OrderStatus.Assigned, OrderStatus.Cancelled],
        [OrderStatus.Cancelled] = [], // 已取消是终态（业务规则：取消后不可自动恢复）
    };

    /// <summary>
    /// 状态显示名称
    /// </summary>
    private static readonly Dictionary<OrderStatus, string> StatusNames = new()
    {
        [OrderStatus.Draft] = "草稿",
        [OrderStatus.Pending] = "待分配",
        [OrderStatus.Assigned] = "已分配",
        [OrderStatus.Delivering] = "配送中",
        [OrderStatus.Completed] = "已完成",
        [OrderStatus.Failed] = "配送失败",
        [OrderStatus.Cancelled] = "已取消"
    };

    /// <summary>
    /// 状态颜色（Hex格式）
    /// </summary>
    private static readonly Dictionary<OrderStatus, string> StatusColors = new()
    {
        [OrderStatus.Draft] = "#9E9E9E",
        [OrderStatus.Pending] = "#FF9800",
        [OrderStatus.Assigned] = "#2196F3",
        [OrderStatus.Delivering] = "#9C27B0",
        [OrderStatus.Completed] = "#4CAF50",
        [OrderStatus.Failed] = "#F44336",
        [OrderStatus.Cancelled] = "#795548"
    };

    /// <summary>
    /// 检查状态转换是否合法
    /// </summary>
    public bool IsValidTransition(OrderStatus from, OrderStatus to)
    {
        if (from == to) return true;
        if (!AllowedTransitions.ContainsKey(from)) return false;
        return AllowedTransitions[from].Contains(to);
    }

    /// <summary>
    /// 获取状态转换的描述文本
    /// </summary>
    public string GetTransitionDescription(OrderStatus from, OrderStatus to)
    {
        return (from, to) switch
        {
            (OrderStatus.Draft, OrderStatus.Pending) => "草稿确认为待分配订单",
            (OrderStatus.Draft, OrderStatus.Cancelled) => "取消草稿订单",
            (OrderStatus.Pending, OrderStatus.Assigned) => "分配配送员",
            (OrderStatus.Pending, OrderStatus.Cancelled) => "取消待分配订单",
            (OrderStatus.Assigned, OrderStatus.Delivering) => "配送员开始配送",
            (OrderStatus.Assigned, OrderStatus.Pending) => "取消分配，退回待分配",
            (OrderStatus.Assigned, OrderStatus.Cancelled) => "取消已分配订单",
            (OrderStatus.Delivering, OrderStatus.Completed) => "配送完成",
            (OrderStatus.Delivering, OrderStatus.Failed) => "配送失败",
            (OrderStatus.Failed, OrderStatus.Pending) => "配送失败，退回待分配",
            (OrderStatus.Failed, OrderStatus.Assigned) => "配送失败，重新分配",
            (OrderStatus.Failed, OrderStatus.Cancelled) => "配送失败，取消订单",
            _ => $"状态变更为 {GetStatusName(to)}"
        };
    }

    /// <summary>
    /// 获取不允许跳转的错误提示
    /// </summary>
    public string GetInvalidTransitionMessage(OrderStatus from, OrderStatus to)
    {
        return $"订单不能从「{GetStatusName(from)}」直接变为「{GetStatusName(to)}」。\n\n" +
               $"允许的操作：{string.Join("、", GetValidNextStatuses(from).Select(GetStatusName))}";
    }

    /// <summary>
    /// 获取当前状态允许转换到的目标状态列表
    /// </summary>
    public IReadOnlyList<OrderStatus> GetValidNextStatuses(OrderStatus from)
    {
        if (!AllowedTransitions.ContainsKey(from)) return Array.Empty<OrderStatus>();
        return AllowedTransitions[from].ToList().AsReadOnly();
    }

    /// <summary>
    /// 获取状态的中文名称
    /// </summary>
    public string GetStatusName(OrderStatus status)
    {
        return StatusNames.GetValueOrDefault(status, "未知");
    }

    /// <summary>
    /// 获取状态的显示颜色（Hex格式）
    /// </summary>
    public string GetStatusColor(OrderStatus status)
    {
        return StatusColors.GetValueOrDefault(status, "#607D8B");
    }

    /// <summary>
    /// 判断状态是否为终态（不可再变更）
    /// Completed和Cancelled均为终态
    /// </summary>
    public bool IsTerminalState(OrderStatus status)
    {
        return status == OrderStatus.Completed || status == OrderStatus.Cancelled;
    }

    /// <summary>
    /// 判断订单是否可删除（只有草稿状态可删除）
    /// </summary>
    public bool IsDeletable(OrderStatus status)
    {
        return status == OrderStatus.Draft;
    }

    /// <summary>
    /// 判断订单是否可编辑
    /// </summary>
    public bool IsEditable(OrderStatus status)
    {
        return status == OrderStatus.Draft || status == OrderStatus.Pending;
    }
}

/// <summary>
/// 静态工具类 - 为已有代码提供向后兼容的静态方法调用
/// 所有方法委托给OrderStatusTransitionManager实例
/// </summary>
public static class OrderStatusManager
{
    private static readonly OrderStatusTransitionManager _manager = new();

    /// <summary>
    /// 检查状态转换是否合法
    /// </summary>
    public static bool IsValidTransition(OrderStatus from, OrderStatus to)
        => _manager.IsValidTransition(from, to);

    /// <summary>
    /// 获取状态转换的描述文本
    /// </summary>
    public static string GetTransitionDescription(OrderStatus from, OrderStatus to)
        => _manager.GetTransitionDescription(from, to);

    /// <summary>
    /// 获取不允许跳转的错误提示
    /// </summary>
    public static string GetInvalidTransitionMessage(OrderStatus from, OrderStatus to)
        => _manager.GetInvalidTransitionMessage(from, to);

    /// <summary>
    /// 获取当前状态允许转换到的目标状态列表
    /// </summary>
    public static IReadOnlyList<OrderStatus> GetValidNextStatuses(OrderStatus from)
        => _manager.GetValidNextStatuses(from);

    /// <summary>
    /// 获取状态的中文名称
    /// </summary>
    public static string GetStatusName(OrderStatus status)
        => _manager.GetStatusName(status);

    /// <summary>
    /// 判断状态是否为终态（不可再变更）
    /// </summary>
    public static bool IsTerminalState(OrderStatus status)
        => _manager.IsTerminalState(status);

    /// <summary>
    /// 获取状态的显示颜色（Hex格式）
    /// </summary>
    public static string GetStatusColor(OrderStatus status)
        => _manager.GetStatusColor(status);

    /// <summary>
    /// 判断订单是否可删除（只有草稿状态可删除）
    /// </summary>
    public static bool IsDeletable(OrderStatus status)
        => _manager.IsDeletable(status);

    /// <summary>
    /// 判断订单是否可编辑
    /// </summary>
    public static bool IsEditable(OrderStatus status)
        => _manager.IsEditable(status);
}
