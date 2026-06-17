namespace PRO.Application.DTOs;

/// <summary>
/// 打印模板
/// </summary>
public class PrintTemplateDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string TemplateType { get; set; } = ""; // Order/DeliveryNote/Settlement/Invoice
    public string Content { get; set; } = ""; // HTML模板内容
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 打印请求
/// </summary>
public class PrintRequest
{
    public string TemplateType { get; set; } = "";
    public int? TemplateId { get; set; }
    public List<int> EntityIds { get; set; } = [];
    public string Format { get; set; } = "pdf"; // pdf/html/print
    public bool ShowPreview { get; set; } = true;
}

/// <summary>
/// 打印数据
/// </summary>
public class PrintDataDto
{
    public string TemplateName { get; set; } = "";
    public string HtmlContent { get; set; } = "";
    public byte[]? PdfContent { get; set; }
    public string? FilePath { get; set; }
}

/// <summary>
/// 订单打印数据
/// </summary>
public class OrderPrintData
{
    public string OrderNo { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string CustomerPhone { get; set; } = "";
    public string DeliveryAddress { get; set; } = "";
    public DateTime OrderDate { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal ReceivableAmount { get; set; }
    public string? Remark { get; set; }
    public List<OrderItemPrintData> Items { get; set; } = [];
    public string CompanyName { get; set; } = "";
    public string CompanyPhone { get; set; } = "";
    public string CompanyAddress { get; set; } = "";
}

public class OrderItemPrintData
{
    public int Index { get; set; }
    public string ProductName { get; set; } = "";
    public string? Specification { get; set; }
    public int Quantity { get; set; }
    public string Unit { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
}

/// <summary>
/// 结算单打印数据
/// </summary>
public class SettlementPrintData
{
    public string SettlementNo { get; set; } = "";
    public string BranchName { get; set; } = "";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int OrderCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal ReceivedAmount { get; set; }
    public decimal UnpaidAmount { get; set; }
    public string? Remark { get; set; }
    public List<SettlementOrderPrintData> Orders { get; set; } = [];
    public DateTime PrintDate { get; set; }
    public string PrintedBy { get; set; } = "";
}

public class SettlementOrderPrintData
{
    public int Index { get; set; }
    public string OrderNo { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal ReceivedAmount { get; set; }
    public decimal UnpaidAmount { get; set; }
    public string PaymentStatus { get; set; } = "";
}
