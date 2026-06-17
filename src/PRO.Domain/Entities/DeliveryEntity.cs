using PRO.Domain.Enums;

namespace PRO.Domain.Entities;

/// <summary>
/// 配送员实体
/// </summary>
public class DeliveryPerson : ISyncable
{
    public int Id { get; set; }

    /// <summary>姓名</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>手机号</summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>分公司ID</summary>
    public int BranchId { get; set; }

    /// <summary>服务区域</summary>
    public string? ServiceArea { get; set; }

    /// <summary>车辆牌号</summary>
    public string? VehicleNumber { get; set; }

    /// <summary>微信号</summary>
    public string? WeChatId { get; set; }

    /// <summary>当前负载（当前配送订单数）</summary>
    public int CurrentLoad { get; set; }

    /// <summary>最大负载</summary>
    public int MaxLoad { get; set; } = 10;

    /// <summary>状态</summary>
    public DeliveryPersonStatus Status { get; set; } = DeliveryPersonStatus.Available;

    /// <summary>备注</summary>
    public string? Remark { get; set; }

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

    // 导航属性
    public virtual Branch? Branch { get; set; }
    public virtual ICollection<Order> Orders { get; set; } = [];
}

/// <summary>
/// 结算实体
/// </summary>
public class Settlement : ISyncable
{
    public int Id { get; set; }

    /// <summary>结算单号</summary>
    public string SettlementNo { get; set; } = string.Empty;

    /// <summary>开始时间</summary>
    public DateTime StartDate { get; set; }

    /// <summary>结束时间</summary>
    public DateTime EndDate { get; set; }

    /// <summary>分公司ID</summary>
    public int BranchId { get; set; }

    /// <summary>确认人ID</summary>
    public int ConfirmedById { get; set; }

    /// <summary>订单总数</summary>
    public int OrderCount { get; set; }

    /// <summary>订单总金额</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>收款总金额</summary>
    public decimal ReceivedAmount { get; set; }

    /// <summary>未收款金额</summary>
    public decimal UnpaidAmount { get; set; }

    /// <summary>状态</summary>
    public SettlementStatus Status { get; set; } = SettlementStatus.Completed;

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    /// <summary>PDF文件路径</summary>
    public string? PdfPath { get; set; }

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

    // 导航属性
    public virtual Branch? Branch { get; set; }
    public virtual Employee? ConfirmedBy { get; set; }
    public virtual ICollection<Order> Orders { get; set; } = [];
}
