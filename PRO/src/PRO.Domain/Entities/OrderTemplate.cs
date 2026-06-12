using System.Collections.Generic;

namespace PRO.Domain.Entities;

public class OrderTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public string? DeliveryAddress { get; set; }
    public string? Remark { get; set; }
    public int CreatedById { get; set; }
    public bool IsPublic { get; set; }
    public bool IsActive { get; set; } = true;
    public int UseCount { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public Customer? Customer { get; set; }
    public Employee? CreatedBy { get; set; }
    public ICollection<OrderTemplateItem> Items { get; set; } = [];
}
