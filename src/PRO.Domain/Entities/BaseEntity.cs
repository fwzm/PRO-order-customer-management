namespace PRO.Domain.Entities;

/// <summary>
/// 实体基类 - 提供通用主键和时间戳
/// </summary>
public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
