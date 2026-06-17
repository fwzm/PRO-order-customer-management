namespace PRO.Domain.Entities;

public class InventoryChangeLog
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductSku { get; set; }
    public int BeforeQuantity { get; set; }
    public int AfterQuantity { get; set; }
    public int ChangeQuantity { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public string ChangeReason { get; set; } = string.Empty;
    public int? RelatedOrderId { get; set; }
    public string? RelatedOrderNo { get; set; }
    public int OperatorId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public Product? Product { get; set; }
    public Order? RelatedOrder { get; set; }
}
