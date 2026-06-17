namespace PRO.Application.DTOs;

/// <summary>
/// 数据质量报告
/// </summary>
public class DataQualityReportDto
{
    public DateTime GeneratedAt { get; set; } = DateTime.Now;
    public int BranchId { get; set; }
    public string BranchName { get; set; } = "";

    // 客户数据质量
    public CustomerQualityDto CustomerQuality { get; set; } = new();

    // 订单数据质量
    public OrderQualityDto OrderQuality { get; set; } = new();

    // 产品数据质量
    public ProductQualityDto ProductQuality { get; set; } = new();

    // 总体评分
    public int OverallScore { get; set; }
    public string OverallLevel { get; set; } = ""; // Good/Warning/Critical
}

/// <summary>
/// 客户数据质量
/// </summary>
public class CustomerQualityDto
{
    public int TotalCount { get; set; }

    // 重复客户
    public int DuplicateCount { get; set; }
    public List<DuplicateGroupDto> DuplicateGroups { get; set; } = [];

    // 空数据
    public int EmptyPhoneCount { get; set; }
    public int EmptyAddressCount { get; set; }
    public int EmptyLegalPersonCount { get; set; }

    // 地址不规范
    public int InvalidAddressCount { get; set; }
    public List<string> InvalidAddressExamples { get; set; } = [];

    // 企微未绑定
    public int UnboundWeChatCount { get; set; }

    // 长期未下单
    public int InactiveCustomerCount { get; set; } // 超过60天

    public int Score { get; set; } // 0-100
}

/// <summary>
/// 重复客户组
/// </summary>
public class DuplicateGroupDto
{
    public string MatchType { get; set; } = ""; // Phone/NameAddress/LegalPersonPhone
    public string MatchValue { get; set; } = "";
    public List<DuplicateCustomerItem> Customers { get; set; } = [];
}

public class DuplicateCustomerItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public int OrderCount { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 订单数据质量
/// </summary>
public class OrderQualityDto
{
    public int TotalCount { get; set; }

    // 异常订单
    public int ZeroAmountCount { get; set; }
    public int NegativeAmountCount { get; set; }
    public int MissingDeliveryAddressCount { get; set; }

    // 状态异常
    public int StaleDraftCount { get; set; } // 超过7天的草稿
    public int LongPendingCount { get; set; } // 超过3天待分配

    // 结算异常
    public int UnsettledCompletedCount { get; set; } // 已完成未结算
    public int PaymentMismatchCount { get; set; } // 收款金额不匹配

    public int Score { get; set; }
}

/// <summary>
/// 产品数据质量
/// </summary>
public class ProductQualityDto
{
    public int TotalCount { get; set; }

    // 重复SKU
    public int DuplicateSkuCount { get; set; }
    public List<string> DuplicateSkus { get; set; } = [];

    // 空数据
    public int EmptyNameCount { get; set; }
    public int EmptySkuCount { get; set; }

    // 库存异常
    public int NegativeStockCount { get; set; }
    public int ZeroStockWithOrdersCount { get; set; } // 有订单但库存为0

    public int Score { get; set; }
}

/// <summary>
/// 数据修复请求
/// </summary>
public class DataFixRequest
{
    public string FixType { get; set; } = "";
    public List<int> TargetIds { get; set; } = [];
    public int? MergeTargetId { get; set; } // 合并目标ID
    public string? DefaultValue { get; set; } // 默认值
}
