using Microsoft.EntityFrameworkCore;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;

namespace PRO.Desktop.Services;

public class GlobalSearchService
{
    private readonly ProDbContext _db;

    public GlobalSearchService(ProDbContext db) { _db = db; }

    public async Task<List<SearchResult>> SearchAsync(string keyword, int branchId)
    {
        var results = new List<SearchResult>();
        if (string.IsNullOrWhiteSpace(keyword) || keyword.Length < 2) return results;

        var k = keyword.Trim();

        // 客户
        var customers = await _db.Customers
            .Where(c => c.BranchId == branchId && c.Status == CustomerStatus.Active &&
                (c.Name.Contains(k) || c.CustomerNo!.Contains(k) || (c.Phone != null && c.Phone.Contains(k))))
            .Take(10).ToListAsync();
        results.AddRange(customers.Select(c => new SearchResult { Title = c.Name, Subtitle = $"客户 | {c.CustomerNo}", Id = c.Id, Type = "Customer", Icon = "👤" }));

        // 订单
        var orders = await _db.Orders
            .Include(o => o.Customer)
            .Where(o => o.BranchId == branchId && (o.OrderNo.Contains(k) || o.Customer!.Name.Contains(k)))
            .Take(10).ToListAsync();
        results.AddRange(orders.Select(o => new SearchResult { Title = o.OrderNo, Subtitle = $"订单 | {o.Customer?.Name} ¥{o.TotalAmount:N0}", Id = o.Id, Type = "Order", Icon = "📋" }));

        // 产品
        var products = await _db.Products
            .Where(p => p.Status == ProductStatus.Active && (p.Name.Contains(k) || p.SKU!.Contains(k)))
            .Take(10).ToListAsync();
        results.AddRange(products.Select(p => new SearchResult { Title = p.Name, Subtitle = $"产品 | SKU:{p.SKU} ¥{(p.ReferencePrice ?? 0):N2}", Id = p.Id, Type = "Product", Icon = "📦" }));

        // 商机
        var opps = await _db.Opportunities
            .Include(o => o.Customer)
            .Where(o => o.Title.Contains(k) || (o.Customer != null && o.Customer.Name.Contains(k)))
            .Take(5).ToListAsync();
        results.AddRange(opps.Select(o => new SearchResult { Title = o.Title, Subtitle = $"商机 | {o.Customer?.Name} 阶段:{o.Stage}", Id = o.Id, Type = "Opportunity", Icon = "💼" }));

        return results;
    }
}

public class SearchResult
{
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public int Id { get; set; }
    public string Type { get; set; } = "";
    public string Icon { get; set; } = "";
}
