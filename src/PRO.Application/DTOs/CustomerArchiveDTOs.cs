using PRO.Domain.Enums;

namespace PRO.Application.DTOs;

/// <summary>
/// 客户档案 - 完整客户视图
/// </summary>
public class CustomerArchiveDto
{
    // ==================== 基本信息 ====================
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string CustomerNo { get; set; } = "";
    public CustomerType CustomerType { get; set; }
    public string CustomerTypeName { get; set; } = "";
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? FullAddress { get; set; }
    public double? Longitude { get; set; }
    public double? Latitude { get; set; }
    public string? LegalPerson { get; set; }
    public string? Remark { get; set; }
    public int BranchId { get; set; }
    public string BranchName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string CreatedByName { get; set; } = "";

    // ==================== 关联信息 ====================
    public int? ParentCustomerId { get; set; }
    public string? ParentCustomerName { get; set; }
    public List<CustomerListItem> SubCustomers { get; set; } = [];
    public List<CustomerTagDto> Tags { get; set; } = [];

    // ==================== 订单统计 ====================
    public int TotalOrderCount { get; set; }
    public decimal TotalOrderAmount { get; set; }
    public int ThisMonthOrderCount { get; set; }
    public decimal ThisMonthOrderAmount { get; set; }
    public DateTime? LastOrderDate { get; set; }
    public int DaysSinceLastOrder { get; set; }

    // ==================== 应收账款 ====================
    public decimal TotalReceivable { get; set; }
    public decimal OverdueReceivable { get; set; }
    public int OverdueOrderCount { get; set; }

    // ==================== 历史订单（最近20条） ====================
    public List<OrderListItem> RecentOrders { get; set; } = [];

    // ==================== 联系记录（最近10条） ====================
    public List<VisitRecordDto> RecentVisits { get; set; } = [];

    // ==================== 企微信息 ====================
    public string? WeChatExternalUserId { get; set; }
    public bool IsWeChatBound { get; set; }
    public DateTime? WeChatBindTime { get; set; }

    // ==================== 风险提示 ====================
    public List<CustomerRisk> Risks { get; set; } = [];
}

/// <summary>
/// 客户标签
/// </summary>
public class CustomerTagDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Color { get; set; }
}

/// <summary>
/// 拜访记录DTO
/// </summary>
public class VisitRecordDto
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public DateTime VisitDate { get; set; }
    public string? Purpose { get; set; }
    public string? Result { get; set; }
    public string? NextAction { get; set; }
    public DateTime? NextVisitDate { get; set; }
    public string CreatedByName { get; set; } = "";
}

/// <summary>
/// 客户风险提示
/// </summary>
public class CustomerRisk
{
    public string Type { get; set; } = ""; // Warning/Error/Info
    public string Message { get; set; } = "";
    public string? Suggestion { get; set; }
}

/// <summary>
/// 客户列表项（增强版）
/// </summary>
public class CustomerListItemEx : CustomerListItem
{
    public new decimal ReceivableAmount { get; set; }
    public int OverdueOrderCount { get; set; }
    public bool IsWeChatBound { get; set; }
    public new string? LastOrderDate { get; set; }
}
