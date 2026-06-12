namespace PRO.Domain.Entities;

/// <summary>
/// 库存变动记录
/// </summary>
public class InventoryMovement
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int QuantityChange { get; set; }  // 正=入库, 负=出库
    public int StockAfter { get; set; }
    public string MovementType { get; set; } = string.Empty;  // OrderCreate/OrderCancel/ManualAdjust/InventoryCheck
    public int? OrderId { get; set; }
    public int OperatorId { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Product? Product { get; set; }
    public Order? Order { get; set; }
}
