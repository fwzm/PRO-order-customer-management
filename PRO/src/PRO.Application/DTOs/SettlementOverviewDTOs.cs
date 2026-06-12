using PRO.Domain.Enums;

namespace PRO.Application.DTOs;

/// <summary>
/// 结算概览数据
/// </summary>
public class SettlementOverviewDto
{
    // 未结算池
    public int UnsettledOrderCount { get; set; }
    public decimal UnsettledTotalAmount { get; set; }
    public decimal UnsettledReceivedAmount { get; set; }
    public decimal UnsettledReceivableAmount { get; set; }

    // 异常订单
    public int AbnormalOrderCount { get; set; }
    public List<AbnormalOrderDto> AbnormalOrders { get; set; } = [];

    // 收款差异
    public int PaymentDifferenceCount { get; set; }
    public decimal TotalPaymentDifference { get; set; }
    public List<PaymentDifferenceDto> PaymentDifferences { get; set; } = [];

    // 结算统计
    public int ThisMonthSettlementCount { get; set; }
    public decimal ThisMonthSettledAmount { get; set; }
    public int PendingPdfCount { get; set; }

    // 最近结算
    public List<SettlementListItem> RecentSettlements { get; set; } = [];
}

/// <summary>
/// 异常订单
/// </summary>
public class AbnormalOrderDto
{
    public int Id { get; set; }
    public string OrderNo { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public decimal ReceivedAmount { get; set; }
    public decimal Difference { get; set; }
    public string AbnormalType { get; set; } = ""; // OverPayment/UnderPayment/ZeroAmount/LongOverdue
    public string AbnormalDescription { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public int DaysOverdue { get; set; }
}

/// <summary>
/// 收款差异
/// </summary>
public class PaymentDifferenceDto
{
    public int OrderId { get; set; }
    public string OrderNo { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public decimal OrderAmount { get; set; }
    public decimal ReceivedAmount { get; set; }
    public decimal Difference { get; set; }
    public string DifferenceType { get; set; } = ""; // Over/Under/Partial
    public DateTime? LastPaymentDate { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 结算操作请求
/// </summary>
public class SettlementActionRequest
{
    public List<int> OrderIds { get; set; } = [];
    public string Action { get; set; } = ""; // Settle/MarkAbnormal/AddRemark
    public string? Remark { get; set; }
    public decimal? AdjustedAmount { get; set; }
}
