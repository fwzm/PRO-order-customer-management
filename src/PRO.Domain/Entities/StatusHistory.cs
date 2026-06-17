namespace PRO.Domain.Entities;

/// <summary>
/// 订单状态变更历史 — 完整记录每次状态变更的前后值
/// </summary>
public class OrderStatusHistory
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    /// <summary>变更前状态</summary>
    public int FromStatus { get; set; }
    /// <summary>变更后状态</summary>
    public int ToStatus { get; set; }
    /// <summary>变更原因（强制填写）</summary>
    public string Reason { get; set; } = string.Empty;
    /// <summary>操作人ID</summary>
    public int OperatorId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Navigation
    public Order? Order { get; set; }
    public Employee? Operator { get; set; }
}

/// <summary>
/// 收款状态变更历史
/// </summary>
public class PaymentStatusHistory
{
    public int Id { get; set; }
    /// <summary>关联对象类型（Order/Delivery）</summary>
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    /// <summary>变更前收款状态</summary>
    public int FromStatus { get; set; }
    /// <summary>变更后收款状态</summary>
    public int ToStatus { get; set; }
    /// <summary>变更原因</summary>
    public string Reason { get; set; } = string.Empty;
    public int OperatorId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public Employee? Operator { get; set; }
}

/// <summary>
/// 审计日志详情 — 记录关键变更的前后值（JSON）
/// </summary>
public class AuditLogDetail
{
    public int Id { get; set; }
    /// <summary>对象类型（Order/OrderItem/Customer/Product/Payment/Inventory等）</summary>
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    /// <summary>对象名称/编号（便于阅读）</summary>
    public string EntityName { get; set; } = string.Empty;
    /// <summary>变更字段名</summary>
    public string FieldName { get; set; } = string.Empty;
    /// <summary>变更前值</summary>
    public string? OldValue { get; set; }
    /// <summary>变更后值</summary>
    public string? NewValue { get; set; }
    /// <summary>操作类型（Create/Update/Delete/StatusChange/Allocate等）</summary>
    public string ActionType { get; set; } = string.Empty;
    /// <summary>操作原因（关键操作强制填写）</summary>
    public string Reason { get; set; } = string.Empty;
    /// <summary>操作人ID</summary>
    public int OperatorId { get; set; }
    /// <summary>分公司ID（用于权限隔离）</summary>
    public int BranchId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Navigation
    public Employee? Operator { get; set; }
}
