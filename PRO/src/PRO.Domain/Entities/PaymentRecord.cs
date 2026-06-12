namespace PRO.Domain.Entities;

/// <summary>
/// 收款记录 — 独立收款登记模型
/// </summary>
public class PaymentRecord
{
    public int Id { get; set; }
    public string PaymentNo { get; set; } = string.Empty;
    /// <summary>关联订单ID（可空，支持未绑定订单的预收款）</summary>
    public int? OrderId { get; set; }
    /// <summary>付款客户ID（冗余，便于按客户维度查询）</summary>
    public int CustomerId { get; set; }
    public decimal Amount { get; set; }
    /// <summary>已核销金额</summary>
    public decimal AllocatedAmount { get; set; }
    /// <summary>付款方式</summary>
    public string PaymentMethod { get; set; } = string.Empty;  // Cash/BankTransfer/WeChat/Alipay/Check/Other
    public string? TransactionNo { get; set; }
    public string? Reference { get; set; }
    public string? Remark { get; set; }
    public int ReceivedById { get; set; }
    public string? Notes { get; set; }
    /// <summary>附件路径（JSON数组）</summary>
    public string? AttachmentPaths { get; set; }
    public DateTime PaymentDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Navigation
    public Order? Order { get; set; }
    public Customer? Customer { get; set; }
    public Employee? ReceivedBy { get; set; }
    public ICollection<PaymentAllocation> Allocations { get; set; } = [];
}

/// <summary>
/// 收款核销明细 — 多对多核销关系
/// 一笔收款可分多笔核销到不同订单，支持部分核销/超额/未分配余额
/// </summary>
public class PaymentAllocation
{
    public int Id { get; set; }
    /// <summary>收款记录ID</summary>
    public int PaymentRecordId { get; set; }
    /// <summary>被核销的订单ID</summary>
    public int OrderId { get; set; }
    /// <summary>本次核销金额</summary>
    public decimal Amount { get; set; }
    /// <summary>核销人ID</summary>
    public int AllocatedById { get; set; }
    /// <summary>核销备注</summary>
    public string? Remark { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Navigation
    public PaymentRecord? PaymentRecord { get; set; }
    public Order? Order { get; set; }
    public Employee? AllocatedBy { get; set; }
}
