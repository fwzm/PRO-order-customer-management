using PRO.Domain.Enums;

namespace PRO.Domain.Entities;

public class Product : ISyncable
{
    public int Id { get; set; }

    /// <summary>SKU编码（唯一）</summary>
    public string SKU { get; set; } = string.Empty;

    /// <summary>产品名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>规格</summary>
    public string? Specification { get; set; }

    /// <summary>销售均价（根据已完成订单自动计算）</summary>
    public decimal AverageSalePrice { get; set; }

    /// <summary>参考价（普通用户可见）</summary>
    public decimal? ReferencePrice { get; set; }

    /// <summary>库存数量</summary>
    public int Stock { get; set; }

    /// <summary>单位</summary>
    public string? Unit { get; set; }

    /// <summary>产品分类ID</summary>
    public int? CategoryId { get; set; }

    public int? WarehouseId { get; set; }

    /// <summary>状态</summary>
    public ProductStatus Status { get; set; } = ProductStatus.Active;

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

    /// <summary>行版本（用于乐观并发控制）</summary>
    [System.ComponentModel.DataAnnotations.Timestamp]
    public byte[]? RowVersion { get; set; }

    // 导航属性
    public virtual ProductCategory? Category { get; set; }
    public virtual ICollection<OrderItem> OrderItems { get; set; } = [];
}

/// <summary>
/// 产品分类
/// </summary>
public class ProductCategory
{
    public int Id { get; set; }

    /// <summary>分类名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>父级分类ID</summary>
    public int? ParentId { get; set; }

    /// <summary>排序号</summary>
    public int SortOrder { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // 导航属性
    public virtual ProductCategory? Parent { get; set; }
    public virtual ICollection<ProductCategory> Children { get; set; } = [];
    public virtual ICollection<Product> Products { get; set; } = [];
}

/// <summary>
/// 产品图片
/// </summary>
public class ProductImage
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public byte[] ImageData { get; set; } = [];
    public bool IsPrimary { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public virtual Product? Product { get; set; }
}

/// <summary>
/// 盘点记录
/// </summary>
public class InventoryCheck
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public int ProductId { get; set; }
    public int SystemStock { get; set; }     // 系统库存
    public int ActualStock { get; set; }     // 实际库存
    public int Variance { get; set; }        // 差异
    public string? Remark { get; set; }
    public int CheckedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public virtual Branch? Branch { get; set; }
    public virtual Product? Product { get; set; }
    public virtual Employee? CheckedBy { get; set; }
}

public class Warehouse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public int BranchId { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public virtual Branch? Branch { get; set; }
    public virtual ICollection<Product> Products { get; set; } = [];
}
