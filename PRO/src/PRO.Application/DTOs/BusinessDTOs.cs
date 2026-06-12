using System.ComponentModel.DataAnnotations;
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

    /// <summary>
    /// 当前状态允许的下一步操作（用于UI动态显示按钮）
    /// </summary>
    public List<OrderStatus> ValidNextStatuses { get; set; } = [];

    /// <summary>
    /// 是否为终态（已完成/已取消）
    /// </summary>
    public bool IsTerminalState => Status == OrderStatus.Completed || Status == OrderStatus.Cancelled;

    /// <summary>
    /// 是否可编辑
    /// </summary>
    public bool IsEditable => Status == OrderStatus.Draft || Status == OrderStatus.Pending;
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
    public List<OrderItemDto> Items { get; set; } = [];
    public List<OrderModificationRecordDto> ModificationRecords { get; set; } = [];
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
    [Required(ErrorMessage = "客户ID不能为空")]
    [Range(1, int.MaxValue, ErrorMessage = "客户ID必须大于0")]
    public int CustomerId { get; set; }

    public int? DeliveryPersonId { get; set; }

    [StringLength(500, ErrorMessage = "配送地址不能超过500个字符")]
    public string? DeliveryAddress { get; set; }

    public double? DeliveryLongitude { get; set; }
    public double? DeliveryLatitude { get; set; }
    public DateTime? DeliveryTime { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "收款金额不能为负数")]
    public decimal ReceivedAmount { get; set; }

    public PaymentStatus PaymentStatus { get; set; }

    [StringLength(1000, ErrorMessage = "备注不能超过1000个字符")]
    public string? Remark { get; set; }

    public bool IsDraft { get; set; }

    [Required(ErrorMessage = "订单明细不能为空")]
    [MinLength(1, ErrorMessage = "订单至少需要一个产品")]
    public List<CreateOrderItemRequest> Items { get; set; } = [];
}

/// <summary>
/// 创建订单明细请求
/// </summary>
public class CreateOrderItemRequest
{
    [Required(ErrorMessage = "产品ID不能为空")]
    [Range(1, int.MaxValue, ErrorMessage = "产品ID必须大于0")]
    public int ProductId { get; set; }

    [Required(ErrorMessage = "数量不能为空")]
    [Range(1, 99999, ErrorMessage = "数量必须在1-99999之间")]
    public int Quantity { get; set; }

    [Required(ErrorMessage = "单价不能为空")]
    [Range(0.01, 999999.99, ErrorMessage = "单价必须大于0")]
    public decimal UnitPrice { get; set; }

    [StringLength(200, ErrorMessage = "备注不能超过200个字符")]
    public string? Remark { get; set; }
}

/// <summary>
/// 更新订单请求
/// </summary>
public class UpdateOrderRequest
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public int? DeliveryPersonId { get; set; }
    public string? DeliveryAddress { get; set; }
    public double? DeliveryLongitude { get; set; }
    public double? DeliveryLatitude { get; set; }
    public DateTime? DeliveryTime { get; set; }
    public decimal ReceivedAmount { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public OrderStatus Status { get; set; }
    public string? CancelReason { get; set; }
    public string? Remark { get; set; }
    public List<CreateOrderItemRequest> Items { get; set; } = [];
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

/// <summary>
/// 批量操作汇总结果
/// </summary>
public class BatchOperationResult
{
    public int TotalCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount => Items.Count(i => !i.Success);
    public List<BatchOperationItemResult> Items { get; set; } = [];
    public bool AllSucceeded => FailedCount == 0;
    public bool HasFailures => FailedCount > 0;
}

/// <summary>
/// 批量操作单项结果（旧版 - 保持向后兼容）
/// </summary>
public class BatchOperationItemResult
{
    public int EntityId { get; set; }
    public string EntityNo { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
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
    public List<ProductCategoryDto> Children { get; set; } = [];
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
    public List<SettlementOrderDto> Orders { get; set; } = [];
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
