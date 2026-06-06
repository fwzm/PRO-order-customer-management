using PRO.Domain.Enums;

namespace PRO.Application.DTOs;

// ==================== 订单相关 ====================

/// <summary>
/// 订单列表项
/// </summary>
public class OrderListItem
{
    public int Id { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal ReceivedAmount { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public string PaymentStatusName { get; set; } = string.Empty;
    public OrderStatus Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public string? DeliveryPersonName { get; set; }
    public string? DeliveryAddress { get; set; }
    public double? DeliveryLongitude { get; set; }
    public double? DeliveryLatitude { get; set; }
    public DateTime? DeliveryTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
}

/// <summary>
/// 订单详情
/// </summary>
public class OrderDetailDto
{
    public int Id { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public string? CustomerAddress { get; set; }
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public int CreatedById { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public int? DeliveryPersonId { get; set; }
    public string? DeliveryPersonName { get; set; }
    public OrderStatus Status { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal ReceivedAmount { get; set; }
    public string? DeliveryAddress { get; set; }
    public double? DeliveryLongitude { get; set; }
    public double? DeliveryLatitude { get; set; }
    public DateTime? DeliveryTime { get; set; }
    public DateTime? SignedTime { get; set; }
    public string? CancelReason { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? SettlementId { get; set; }
    public bool IsDraft => Status == OrderStatus.Draft;
    public DateTime? DraftExpireTime { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();
    public List<OrderModificationRecordDto> ModificationRecords { get; set; } = new();
}

/// <summary>
/// 订单明细DTO
/// </summary>
public class OrderItemDto
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductSku { get; set; }
    public string? ProductSpec { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
    public string? DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 订单修改记录DTO
/// </summary>
public class OrderModificationRecordDto
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ModifiedById { get; set; }
    public string ModifiedByName { get; set; } = string.Empty;
    public string ModifiedByNo { get; set; } = string.Empty;
    public DateTime ModifiedAt { get; set; }
    public string Content { get; set; } = string.Empty;
    public string ModificationType { get; set; } = string.Empty;
}

/// <summary>
/// 创建订单请求
/// </summary>
public class CreateOrderRequest
{
    public int CustomerId { get; set; }
    public string? DeliveryAddress { get; set; }
    public double? DeliveryLongitude { get; set; }
    public double? DeliveryLatitude { get; set; }
    public DateTime? DeliveryTime { get; set; }
    public decimal ReceivedAmount { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public string? Remark { get; set; }
    public bool IsDraft { get; set; }
    public List<CreateOrderItemRequest> Items { get; set; } = new();
}

/// <summary>
/// 创建订单明细请求
/// </summary>
public class CreateOrderItemRequest
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 更新订单请求
/// </summary>
public class UpdateOrderRequest
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string? DeliveryAddress { get; set; }
    public double? DeliveryLongitude { get; set; }
    public double? DeliveryLatitude { get; set; }
    public DateTime? DeliveryTime { get; set; }
    public decimal ReceivedAmount { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public OrderStatus Status { get; set; }
    public string? CancelReason { get; set; }
    public string? Remark { get; set; }
    public List<CreateOrderItemRequest> Items { get; set; } = new();
}

/// <summary>
/// 订单分配请求
/// </summary>
public class AssignOrderRequest
{
    public int OrderId { get; set; }
    public int DeliveryPersonId { get; set; }
}

/// <summary>
/// 订单状态更新请求
/// </summary>
public class UpdateOrderStatusRequest
{
    public int OrderId { get; set; }
    public OrderStatus NewStatus { get; set; }
    public string? Reason { get; set; }
    public string? Remark { get; set; }
    public string? SignPhoto { get; set; }
}

// ==================== 产品相关 ====================

/// <summary>
/// 产品列表项
/// </summary>
public class ProductListItem
{
    public int Id { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Specification { get; set; }
    public decimal AverageSalePrice { get; set; }
    public decimal? ReferencePrice { get; set; }
    public int Stock { get; set; }
    public string? Unit { get; set; }
    public string? CategoryName { get; set; }
    public ProductStatus Status { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 创建产品请求
/// </summary>
public class CreateProductRequest
{
    public string SKU { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Specification { get; set; }
    public decimal AverageSalePrice { get; set; }
    public decimal? ReferencePrice { get; set; }
    public int Stock { get; set; }
    public string? Unit { get; set; }
    public int? CategoryId { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 更新产品请求
/// </summary>
public class UpdateProductRequest
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Specification { get; set; }
    public decimal AverageSalePrice { get; set; }
    public decimal? ReferencePrice { get; set; }
    public int Stock { get; set; }
    public string? Unit { get; set; }
    public int? CategoryId { get; set; }
    public ProductStatus Status { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 产品分类DTO
/// </summary>
public class ProductCategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? ParentId { get; set; }
    public int SortOrder { get; set; }
    public List<ProductCategoryDto> Children { get; set; } = new();
}

// ==================== 配送员相关 ====================

/// <summary>
/// 配送员列表项
/// </summary>
public class DeliveryPersonListItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public string? ServiceArea { get; set; }
    public string? VehicleNumber { get; set; }
    public string? WeChatId { get; set; }
    public int CurrentLoad { get; set; }
    public int MaxLoad { get; set; }
    public DeliveryPersonStatus Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
}

/// <summary>
/// 创建配送员请求
/// </summary>
public class CreateDeliveryPersonRequest
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public string? ServiceArea { get; set; }
    public string? VehicleNumber { get; set; }
    public string? WeChatId { get; set; }
    public int MaxLoad { get; set; } = 10;
    public string? Remark { get; set; }
}

/// <summary>
/// 更新配送员请求
/// </summary>
public class UpdateDeliveryPersonRequest
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public string? ServiceArea { get; set; }
    public string? VehicleNumber { get; set; }
    public string? WeChatId { get; set; }
    public int MaxLoad { get; set; }
    public DeliveryPersonStatus Status { get; set; }
    public string? Remark { get; set; }
}

// ==================== 结算相关 ====================

/// <summary>
/// 结算列表项
/// </summary>
public class SettlementListItem
{
    public int Id { get; set; }
    public string SettlementNo { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public string ConfirmedByName { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal ReceivedAmount { get; set; }
    public decimal UnpaidAmount { get; set; }
    public SettlementStatus Status { get; set; }
    public string? PdfPath { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 结算详情
/// </summary>
public class SettlementDetailDto
{
    public int Id { get; set; }
    public string SettlementNo { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public int ConfirmedById { get; set; }
    public string ConfirmedByName { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal ReceivedAmount { get; set; }
    public decimal UnpaidAmount { get; set; }
    public SettlementStatus Status { get; set; }
    public string? Remark { get; set; }
    public string? PdfPath { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<SettlementOrderDto> Orders { get; set; } = new();
}

/// <summary>
/// 结算订单DTO
/// </summary>
public class SettlementOrderDto
{
    public int Id { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal ReceivedAmount { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
}

/// <summary>
/// 创建结算请求
/// </summary>
public class CreateSettlementRequest
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int BranchId { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 结算预览（汇总统计）
/// </summary>
public class SettlementPreviewDto
{
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int OrderCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal ReceivedAmount { get; set; }
    public decimal UnpaidAmount { get; set; }
    public int PaidCount { get; set; }
    public int UnpaidCount { get; set; }
    public int LegalCount { get; set; }
}

// ==================== 企业微信客户相关 ====================

/// <summary>
/// 企微客户列表项
/// </summary>
public class WeChatCustomerListItem
{
    public int Id { get; set; }
    public string ExternalUserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? AddUserName { get; set; }
    public string? AddUserDepartmentName { get; set; }
    public string? CustomerNoInDesc { get; set; }
    public string Status { get; set; } = "Unlinked";
    public string StatusName => Status switch
    {
        "Unlinked" => "未关联",
        "Linked" => "已关联",
        "Unidentifiable" => "无法识别",
        _ => "未知"
    };
    public int? LinkedCustomerId { get; set; }
    public string? LinkedCustomerName { get; set; }
    public int? AssignedBranchId { get; set; }
    public string? AssignedBranchName { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}
