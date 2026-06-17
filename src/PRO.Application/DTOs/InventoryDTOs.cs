namespace PRO.Application.DTOs;

/// <summary>
/// 库存变动日志
/// </summary>
public class InventoryChangeLogDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string ProductSku { get; set; } = "";
    public int BeforeQuantity { get; set; }
    public int AfterQuantity { get; set; }
    public int ChangeQuantity { get; set; }
    public string ChangeType { get; set; } = ""; // OrderCreate/OrderCancel/OrderModify/ManualAdjust/StockIn/StockOut
    public string ChangeReason { get; set; } = "";
    public int? RelatedOrderId { get; set; }
    public string? RelatedOrderNo { get; set; }
    public int OperatorId { get; set; }
    public string OperatorName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 库存盘点
/// </summary>
public class InventoryStockDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string ProductSku { get; set; } = "";
    public int SystemStock { get; set; } // 系统库存
    public int? PhysicalStock { get; set; } // 实际库存
    public int Difference { get; set; } // 差异
    public DateTime LastChangeAt { get; set; }
    public string? LastChangeReason { get; set; }
}

/// <summary>
/// 库存调整请求
/// </summary>
public class InventoryAdjustRequest
{
    public int ProductId { get; set; }
    public int NewQuantity { get; set; }
    public string Reason { get; set; } = "";
}

/// <summary>
/// 库存变动统计
/// </summary>
public class InventoryChangeStatsDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalChanges { get; set; }
    public int OrderCreates { get; set; }
    public int OrderCancels { get; set; }
    public int ManualAdjusts { get; set; }
    public List<ProductChangeSummary> TopChangedProducts { get; set; } = [];
}

public class ProductChangeSummary
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public int TotalChanges { get; set; }
    public int NetChange { get; set; }
}
