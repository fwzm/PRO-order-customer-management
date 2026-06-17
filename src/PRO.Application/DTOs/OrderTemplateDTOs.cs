using PRO.Domain.Enums;

namespace PRO.Application.DTOs;

/// <summary>
/// 订单模板
/// </summary>
public class OrderTemplateDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string? DeliveryAddress { get; set; }
    public List<OrderTemplateItemDto> Items { get; set; } = [];
    public string? Remark { get; set; }
    public int UseCount { get; set; }
    public DateTime LastUsedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 订单模板明细
/// </summary>
public class OrderTemplateItemDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string? ProductSku { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

/// <summary>
/// 创建订单模板请求
/// </summary>
public class CreateOrderTemplateRequest
{
    public string Name { get; set; } = "";
    public int CustomerId { get; set; }
    public string? DeliveryAddress { get; set; }
    public List<OrderTemplateItemDto> Items { get; set; } = [];
    public string? Remark { get; set; }
}

/// <summary>
/// 从历史订单创建模板
/// </summary>
public class CreateTemplateFromOrderRequest
{
    public int OrderId { get; set; }
    public string TemplateName { get; set; } = "";
}
