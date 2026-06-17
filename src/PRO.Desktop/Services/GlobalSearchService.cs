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
        if (string.IsNullOrWhiteSpace(keyword) || keyword.Trim().Length < 2)
            return results;

        var k = keyword.Trim();
        var isPinyinSearch = k.All(c => char.IsAsciiLetter(c));

        var customers = await _db.Customers
            .AsNoTracking()
            .Where(c => c.BranchId == branchId && c.Status == CustomerStatus.Active)
            .Where(c => isPinyinSearch || c.Name.Contains(k) || c.CustomerNo!.Contains(k) || (c.Phone != null && c.Phone.Contains(k)))
            .OrderByDescending(c => c.CreatedAt)
            .Take(isPinyinSearch ? 300 : 50)
            .ToListAsync();

        results.AddRange(customers
            .Select(c => new { Item = c, Rank = MatchRank(c.Name, k, isPinyinSearch, c.CustomerNo, c.Phone) })
            .Where(x => x.Rank < 100)
            .OrderBy(x => x.Rank)
            .ThenBy(x => x.Item.Name)
            .Take(10)
            .Select(x => new SearchResult
            {
                Title = x.Item.Name,
                Subtitle = $"客户 | {x.Item.CustomerNo} | {x.Item.Phone ?? "无手机号"}",
                Id = x.Item.Id,
                Type = "Customer",
                Icon = "👤",
                Actions =
                [
                    new() { Name = "查看档案", Action = "OpenArchive" },
                    new() { Name = "编辑客户", Action = "EditCustomer" },
                    new() { Name = "查看订单", Action = "ViewOrders" }
                ]
            }));

        var orders = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Where(o => o.BranchId == branchId)
            .Where(o => isPinyinSearch || o.OrderNo.Contains(k) || (o.Customer != null && o.Customer.Name.Contains(k)))
            .OrderByDescending(o => o.CreatedAt)
            .Take(isPinyinSearch ? 300 : 50)
            .ToListAsync();

        results.AddRange(orders
            .Select(o => new { Item = o, Rank = MatchRank(o.Customer?.Name ?? "", k, isPinyinSearch, o.OrderNo) })
            .Where(x => x.Rank < 100)
            .OrderBy(x => x.Rank)
            .ThenByDescending(x => x.Item.CreatedAt)
            .Take(10)
            .Select(x => new SearchResult
            {
                Title = x.Item.OrderNo,
                Subtitle = $"订单 | {x.Item.Customer?.Name} | ¥{x.Item.TotalAmount:N0} | {GetOrderStatusName(x.Item.Status)}",
                Id = x.Item.Id,
                Type = "Order",
                Icon = "📋",
                Actions =
                [
                    new() { Name = "查看订单", Action = "ViewOrder" },
                    new() { Name = "编辑订单", Action = "EditOrder" }
                ]
            }));

        var products = await _db.Products
            .AsNoTracking()
            .Where(p => p.Status == ProductStatus.Active)
            .Where(p => isPinyinSearch || p.Name.Contains(k) || p.SKU!.Contains(k))
            .OrderBy(p => p.Name)
            .Take(isPinyinSearch ? 300 : 50)
            .ToListAsync();

        results.AddRange(products
            .Select(p => new { Item = p, Rank = MatchRank(p.Name, k, isPinyinSearch, p.SKU) })
            .Where(x => x.Rank < 100)
            .OrderBy(x => x.Rank)
            .ThenBy(x => x.Item.Name)
            .Take(10)
            .Select(x => new SearchResult
            {
                Title = x.Item.Name,
                Subtitle = $"产品 | SKU:{x.Item.SKU} | ¥{(x.Item.ReferencePrice ?? 0):N2} | 库存:{x.Item.Stock}",
                Id = x.Item.Id,
                Type = "Product",
                Icon = "📦",
                Actions =
                [
                    new() { Name = "查看产品", Action = "ViewProduct" },
                    new() { Name = "编辑产品", Action = "EditProduct" }
                ]
            }));

        var deliveryPersons = await _db.DeliveryPersons
            .AsNoTracking()
            .Where(d => d.BranchId == branchId && d.Status == DeliveryPersonStatus.Available)
            .Where(d => isPinyinSearch || d.Name.Contains(k) || (d.Phone != null && d.Phone.Contains(k)))
            .OrderBy(d => d.Name)
            .Take(isPinyinSearch ? 200 : 30)
            .ToListAsync();

        results.AddRange(deliveryPersons
            .Select(d => new { Item = d, Rank = MatchRank(d.Name, k, isPinyinSearch, d.Phone) })
            .Where(x => x.Rank < 100)
            .OrderBy(x => x.Rank)
            .ThenBy(x => x.Item.Name)
            .Take(5)
            .Select(x => new SearchResult
            {
                Title = x.Item.Name,
                Subtitle = $"配送员 | {x.Item.Phone ?? "无手机号"} | 负载:{x.Item.CurrentLoad}/{x.Item.MaxLoad}",
                Id = x.Item.Id,
                Type = "DeliveryPerson",
                Icon = "🚚",
                Actions =
                [
                    new() { Name = "查看详情", Action = "ViewDeliveryPerson" }
                ]
            }));

        return results;
    }

    private static int MatchRank(string text, string keyword, bool isPinyinSearch, params string?[] aliases)
    {
        if (text.Equals(keyword, StringComparison.OrdinalIgnoreCase) ||
            aliases.Any(a => !string.IsNullOrWhiteSpace(a) && a.Equals(keyword, StringComparison.OrdinalIgnoreCase)))
            return 0;

        if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            aliases.Any(a => !string.IsNullOrWhiteSpace(a) && a.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            return 1;

        if (isPinyinSearch)
        {
            var initials = GetPinyinInitials(text);
            if (initials.Equals(keyword, StringComparison.OrdinalIgnoreCase))
                return 2;
            if (initials.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return 3;
        }

        return 100;
    }

    private static string GetPinyinInitials(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var result = new System.Text.StringBuilder();
        foreach (var ch in text)
        {
            if (char.IsAsciiLetter(ch))
            {
                result.Append(char.ToLowerInvariant(ch));
                continue;
            }

            if (CommonInitials.TryGetValue(ch, out var initial))
                result.Append(initial);
        }
        return result.ToString();
    }

    private static readonly Dictionary<char, char> CommonInitials = new()
    {
        ['张'] = 'z',
        ['章'] = 'z',
        ['赵'] = 'z',
        ['周'] = 'z',
        ['郑'] = 'z',
        ['朱'] = 'z',
        ['钟'] = 'z',
        ['邹'] = 'z',
        ['三'] = 's',
        ['孙'] = 's',
        ['宋'] = 's',
        ['沈'] = 's',
        ['石'] = 's',
        ['水'] = 's',
        ['食'] = 's',
        ['商'] = 's',
        ['李'] = 'l',
        ['刘'] = 'l',
        ['林'] = 'l',
        ['罗'] = 'l',
        ['梁'] = 'l',
        ['龙'] = 'l',
        ['零'] = 'l',
        ['王'] = 'w',
        ['吴'] = 'w',
        ['魏'] = 'w',
        ['汪'] = 'w',
        ['文'] = 'w',
        ['物'] = 'w',
        ['陈'] = 'c',
        ['程'] = 'c',
        ['曹'] = 'c',
        ['蔡'] = 'c',
        ['常'] = 'c',
        ['成'] = 'c',
        ['车'] = 'c',
        ['杨'] = 'y',
        ['叶'] = 'y',
        ['姚'] = 'y',
        ['余'] = 'y',
        ['袁'] = 'y',
        ['饮'] = 'y',
        ['用'] = 'y',
        ['黄'] = 'h',
        ['何'] = 'h',
        ['胡'] = 'h',
        ['韩'] = 'h',
        ['侯'] = 'h',
        ['货'] = 'h',
        ['钱'] = 'q',
        ['冯'] = 'f',
        ['许'] = 'x',
        ['徐'] = 'x',
        ['谢'] = 'x',
        ['肖'] = 'x',
        ['郭'] = 'g',
        ['高'] = 'g',
        ['顾'] = 'g',
        ['关'] = 'g',
        ['购'] = 'g',
        ['马'] = 'm',
        ['毛'] = 'm',
        ['孟'] = 'm',
        ['米'] = 'm',
        ['丁'] = 'd',
        ['邓'] = 'd',
        ['戴'] = 'd',
        ['段'] = 'd',
        ['单'] = 'd',
        ['彭'] = 'p',
        ['潘'] = 'p',
        ['蒋'] = 'j',
        ['江'] = 'j',
        ['姜'] = 'j',
        ['金'] = 'j',
        ['白'] = 'b',
        ['包'] = 'b',
        ['牛'] = 'n',
        ['倪'] = 'n',
        ['方'] = 'f',
        ['范'] = 'f',
        ['客'] = 'k',
        ['户'] = 'h',
        ['产'] = 'c',
        ['品'] = 'p',
        ['矿'] = 'k',
        ['泉'] = 'q',
        ['可'] = 'k',
        ['口'] = 'k',
        ['乐'] = 'l',
        ['抽'] = 'c',
        ['纸'] = 'z',
        ['充'] = 'c',
        ['电'] = 'd',
        ['宝'] = 'b'
    };

    private static string GetOrderStatusName(OrderStatus status) => status switch
    {
        OrderStatus.Draft => "草稿",
        OrderStatus.Pending => "待分配",
        OrderStatus.Assigned => "已分配",
        OrderStatus.Delivering => "配送中",
        OrderStatus.Completed => "已完成",
        OrderStatus.Failed => "配送失败",
        OrderStatus.Cancelled => "已取消",
        _ => "未知"
    };
}

public class SearchResult
{
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public int Id { get; set; }
    public string Type { get; set; } = "";
    public string Icon { get; set; } = "";
    public List<SearchAction> Actions { get; set; } = [];
}

public class SearchAction
{
    public string Name { get; set; } = "";
    public string Action { get; set; } = "";
}
