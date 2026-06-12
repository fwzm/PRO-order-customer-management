using PRO.Domain.Enums;

namespace PRO.Application.DTOs;

/// <summary>
/// 收款记录
/// </summary>
public class PaymentRecordDto
{
    public int Id { get; set; }
    public string PaymentNo { get; set; } = "";
    public int OrderId { get; set; }
    public string OrderNo { get; set; } = "";
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string PaymentMethodName { get; set; } = "";
    public string? Reference { get; set; } // 参考号/交易号
    public string? Remark { get; set; }
    public int ReceivedById { get; set; }
    public string ReceivedByName { get; set; } = "";
    public DateTime PaymentDate { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 创建收款记录
/// </summary>
public class CreatePaymentRequest
{
    public int OrderId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? Reference { get; set; }
    public string? Remark { get; set; }
    public DateTime? PaymentDate { get; set; }
}

/// <summary>
/// 批量收款
/// </summary>
public class BatchPaymentRequest
{
    public List<CreatePaymentRequest> Payments { get; set; } = [];
}

/// <summary>
/// 收款核销
/// </summary>
public class PaymentAllocationDto
{
    public int PaymentId { get; set; }
    public string PaymentNo { get; set; } = "";
    public decimal PaymentAmount { get; set; }
    public decimal AllocatedAmount { get; set; }
    public decimal UnallocatedAmount { get; set; }
    public List<PaymentAllocationItemDto> Allocations { get; set; } = [];
}

public class PaymentAllocationItemDto
{
    public int OrderId { get; set; }
    public string OrderNo { get; set; } = "";
    public decimal OrderAmount { get; set; }
    public decimal AllocatedAmount { get; set; }
}

/// <summary>
/// 收款统计
/// </summary>
public class PaymentStatsDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal TotalReceived { get; set; }
    public int TotalCount { get; set; }
    public decimal CashAmount { get; set; }
    public decimal WeChatAmount { get; set; }
    public decimal AlipayAmount { get; set; }
    public decimal BankTransferAmount { get; set; }
    public List<DailyPaymentDto> DailyPayments { get; set; } = [];
    public List<CustomerPaymentSummary> TopCustomers { get; set; } = [];
}

public class DailyPaymentDto
{
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public int Count { get; set; }
}

public class CustomerPaymentSummary
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public int PaymentCount { get; set; }
}

/// <summary>
/// 支付方式枚举
/// </summary>
public enum PaymentMethod
{
    Cash = 0,        // 现金
    WeChat = 1,      // 微信
    Alipay = 2,      // 支付宝
    BankTransfer = 3, // 银行转账
    Check = 4,       // 支票
    Other = 99       // 其他
}
