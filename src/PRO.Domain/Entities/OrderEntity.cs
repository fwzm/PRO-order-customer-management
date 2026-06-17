using PRO.Domain.Enums;

namespace PRO.Domain.Entities;

public class Order : ISyncable
{
    public int Id { get; set; }

    /// <summary>订单号</summary>
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>客户ID</summary>
    public int CustomerId { get; set; }

    /// <summary>分公司ID</summary>
    public int BranchId { get; set; }

    /// <summary>创建人ID</summary>
    public int CreatedById { get; set; }

    /// <summary>配送员ID</summary>
    public int? DeliveryPersonId { get; set; }

    /// <summary>订单状态</summary>
    public OrderStatus Status { get; set; } = OrderStatus.Draft;

    /// <summary>收款状态</summary>
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;

    /// <summary>总金额</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>收款金额</summary>
    public decimal ReceivedAmount { get; set; }

    /// <summary>优惠金额</summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>配送地址</summary>
    public string? DeliveryAddress { get; set; }

    /// <summary>配送经度</summary>
    public double? DeliveryLongitude { get; set; }

    /// <summary>配送纬度</summary>
    public double? DeliveryLatitude { get; set; }

    /// <summary>配送时间</summary>
    public DateTime? DeliveryTime { get; set; }

    /// <summary>签收时间</summary>
    public DateTime? SignedTime { get; set; }

    /// <summary>取消原因</summary>
    public string? CancelReason { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    /// <summary>草稿自动转正时间</summary>
    public DateTime? DraftExpireTime { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>更新时间</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    /// <summary>本地更新时间戳</summary>
    public long LocalTimestamp { get; set; }

    /// <summary>远程更新时间戳</summary>
    public long? RemoteTimestamp { get; set; }

    /// <summary>同步状态</summary>
    public SyncStatus SyncStatus { get; set; } = SyncStatus.Pending;

    /// <summary>结算ID（结算后关联）</summary>
    public int? SettlementId { get; set; }

    // 导航属性
    public virtual Customer? Customer { get; set; }
    public virtual Branch? Branch { get; set; }
    public virtual Employee? Creator { get; set; }
    public virtual DeliveryPerson? DeliveryPerson { get; set; }
    public virtual Settlement? Settlement { get; set; }
    public virtual ICollection<OrderItem> Items { get; set; } = [];
    public virtual ICollection<OrderModificationRecord> ModificationRecords { get; set; } = [];
}

/// <summary>
/// 订单明细
/// </summary>
public class OrderItem
{
    public int Id { get; set; }

    /// <summary>订单ID</summary>
    public int OrderId { get; set; }

    /// <summary>产品ID</summary>
    public int ProductId { get; set; }

    /// <summary>数量</summary>
    public int Quantity { get; set; }

    /// <summary>单价</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>金额</summary>
    public decimal Amount { get; set; }

    /// <summary>优惠类型（Discount/PriceChange）</summary>
    public string? DiscountType { get; set; }

    /// <summary>优惠值（折扣率0-1或改价金额）</summary>
    public decimal DiscountValue { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    // 导航属性
    public virtual Order? Order { get; set; }
    public virtual Product? Product { get; set; }
}

/// <summary>
/// 订单修改记录
/// </summary>
public class OrderModificationRecord
{
    public int Id { get; set; }

    /// <summary>订单ID</summary>
    public int OrderId { get; set; }

    /// <summary>修改人ID</summary>
    public int ModifiedById { get; set; }

    /// <summary>修改时间</summary>
    public DateTime ModifiedAt { get; set; } = DateTime.Now;

    /// <summary>修改内容（JSON）</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>修改类型</summary>
    public string ModificationType { get; set; } = string.Empty;

    // 导航属性
    public virtual Order? Order { get; set; }
    public virtual Employee? ModifiedBy { get; set; }
}
