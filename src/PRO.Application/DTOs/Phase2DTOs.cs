namespace PRO.Application.DTOs;

// ==================== 收款核销 ====================

/// <summary>收款核销请求</summary>
public class AllocatePaymentRequest
{
    /// <summary>收款记录ID</summary>
    public int PaymentRecordId { get; set; }
    /// <summary>核销明细列表</summary>
    public List<AllocatePaymentItem> Allocations { get; set; } = [];
    /// <summary>核销备注</summary>
    public string? Remark { get; set; }
}

public class AllocatePaymentItem
{
    public int OrderId { get; set; }
    public decimal Amount { get; set; }
}

/// <summary>收款核销结果</summary>
public class PaymentAllocationResultDto
{
    public int PaymentRecordId { get; set; }
    public string PaymentNo { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public decimal AllocatedAmount { get; set; }
    public decimal UnallocatedAmount { get; set; }
    public List<PaymentAllocationDetailDto> Details { get; set; } = [];
}

public class PaymentAllocationDetailDto
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string OrderNo { get; set; } = "";
    public decimal Amount { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ==================== 应收账款 ====================

/// <summary>客户应收账款摘要</summary>
public class CustomerReceivableDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public int BranchId { get; set; }
    public string BranchName { get; set; } = "";
    /// <summary>订单总金额（已确认+已完成，不含草稿/取消）</summary>
    public decimal TotalOrderAmount { get; set; }
    /// <summary>已收款总额（含核销）</summary>
    public decimal TotalReceivedAmount { get; set; }
    /// <summary>折扣总额</summary>
    public decimal TotalDiscountAmount { get; set; }
    /// <summary>应收余额 = TotalOrderAmount - TotalDiscountAmount - TotalReceivedAmount</summary>
    public decimal ReceivableBalance { get; set; }
    /// <summary>订单数量</summary>
    public int OrderCount { get; set; }
    /// <summary>最新下单日期</summary>
    public DateTime? LastOrderDate { get; set; }
}

/// <summary>应收账款筛选请求</summary>
public class ReceivableQueryRequest : PagedRequest
{
    public int? CustomerId { get; set; }
    public int? BranchId { get; set; }
    public int? SalespersonId { get; set; }
    /// <summary>最小应收余额（筛选有欠款的）</summary>
    public decimal? MinBalance { get; set; }
    public new string? SortField { get; set; }
    public new string? SortOrder { get; set; }
}

// ==================== 账龄分析 ====================

/// <summary>账龄分析请求</summary>
public class AgingAnalysisRequest
{
    public int? CustomerId { get; set; }
    public int? BranchId { get; set; }
    public int? SalespersonId { get; set; }
    /// <summary>基准日期（默认今天）</summary>
    public DateTime? AsOfDate { get; set; }
}

/// <summary>账龄分析结果</summary>
public class AgingAnalysisDto
{
    public DateTime AsOfDate { get; set; }
    /// <summary>30天内应收</summary>
    public AgingBucketDto Within30Days { get; set; } = new();
    /// <summary>31-60天应收</summary>
    public AgingBucketDto Days31To60 { get; set; } = new();
    /// <summary>61-90天应收</summary>
    public AgingBucketDto Days61To90 { get; set; } = new();
    /// <summary>90天以上应收</summary>
    public AgingBucketDto Over90Days { get; set; } = new();
    /// <summary>总计</summary>
    public decimal TotalReceivable { get; set; }
    public int TotalOrderCount { get; set; }
    /// <summary>明细列表</summary>
    public List<AgingDetailDto> Details { get; set; } = [];
}

public class AgingBucketDto
{
    public string Label { get; set; } = "";
    public decimal Amount { get; set; }
    public int OrderCount { get; set; }
    public int CustomerCount { get; set; }
    public decimal Percentage { get; set; }
}

public class AgingDetailDto
{
    public int OrderId { get; set; }
    public string OrderNo { get; set; } = "";
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public int BranchId { get; set; }
    public string BranchName { get; set; } = "";
    public int? SalespersonId { get; set; }
    public string? SalespersonName { get; set; }
    public DateTime OrderDate { get; set; }
    public int AgingDays { get; set; }
    public string AgingBucket { get; set; } = "";
    public decimal OrderAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal ReceivedAmount { get; set; }
    public decimal ReceivableBalance { get; set; }
}

// ==================== 状态历史 ====================

public class OrderStatusHistoryDto
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string OrderNo { get; set; } = "";
    public int FromStatus { get; set; }
    public string FromStatusName { get; set; } = "";
    public int ToStatus { get; set; }
    public string ToStatusName { get; set; } = "";
    public string Reason { get; set; } = "";
    public int OperatorId { get; set; }
    public string OperatorName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class PaymentStatusHistoryDto
{
    public int Id { get; set; }
    public string EntityType { get; set; } = "";
    public int EntityId { get; set; }
    public int FromStatus { get; set; }
    public string FromStatusName { get; set; } = "";
    public int ToStatus { get; set; }
    public string ToStatusName { get; set; } = "";
    public string Reason { get; set; } = "";
    public int OperatorId { get; set; }
    public string OperatorName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

// ==================== 审计日志详情 ====================

public class AuditLogDetailDto
{
    public int Id { get; set; }
    public string EntityType { get; set; } = "";
    public int EntityId { get; set; }
    public string EntityName { get; set; } = "";
    public string FieldName { get; set; } = "";
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string ActionType { get; set; } = "";
    public string Reason { get; set; } = "";
    public int OperatorId { get; set; }
    public string OperatorName { get; set; } = "";
    public int BranchId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AuditLogQueryRequest : PagedRequest
{
    public string? EntityType { get; set; }
    public int? EntityId { get; set; }
    public string? FieldName { get; set; }
    public string? ActionType { get; set; }
    public int? OperatorId { get; set; }
    public int? BranchId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

/// <summary>带原因的撤销请求</summary>
public class UndoWithReasonRequest
{
    public int OperationId { get; set; }
    /// <summary>撤销原因（强制填写）</summary>
    public string Reason { get; set; } = string.Empty;
}

/// <summary>状态回滚请求</summary>
public class StatusRollbackRequest
{
    public string EntityType { get; set; } = string.Empty; // Order/Payment
    public int EntityId { get; set; }
    /// <summary>回滚到的目标状态值</summary>
    public int TargetStatus { get; set; }
    /// <summary>回滚原因（强制）</summary>
    public string Reason { get; set; } = string.Empty;
}
