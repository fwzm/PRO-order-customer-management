namespace PRO.Domain.Entities;

public class OrderTemplateItem
{
    public int Id { get; set; }
    public int OrderTemplateId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public OrderTemplate? OrderTemplate { get; set; }
    public Product? Product { get; set; }
}
