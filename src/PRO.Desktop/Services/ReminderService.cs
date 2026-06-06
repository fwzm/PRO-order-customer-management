using Microsoft.EntityFrameworkCore;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;

namespace PRO.Desktop.Services;

public class ReminderService
{
    private readonly ProDbContext _db;

    public ReminderService(ProDbContext db) { _db = db; }

    public async Task<List<ReminderItem>> GetRemindersAsync(int branchId)
    {
        var reminders = new List<ReminderItem>();
        var now = DateTime.Now;

        // 1. 收款提醒（超过15天未收款的订单）
        var overdueOrders = await _db.Orders
            .Include(o => o.Customer)
            .Where(o => o.BranchId == branchId && o.PaymentStatus != PaymentStatus.Paid && o.CreatedAt < now.AddDays(-15))
            .OrderBy(o => o.CreatedAt)
            .Take(5).ToListAsync();

        reminders.AddRange(overdueOrders.Select(o => new ReminderItem
        {
            Title = $"收款提醒：{o.Customer?.Name}",
            Detail = $"订单 {o.OrderNo} 已 {Math.Max(0, (int)(now - o.CreatedAt).TotalDays)} 天未收款，金额 ¥{(o.TotalAmount - o.ReceivedAmount):N0}",
            Type = "Payment", Priority = (int)(now - o.CreatedAt).TotalDays >= 60 ? "高" : "中",
            CreatedAt = o.CreatedAt
        }));

        // 2. 拜访提醒（有NextVisitDate的客户）
        var upcomingVisits = await _db.VisitRecords
            .Include(v => v.Customer)
            .Where(v => v.NextVisitDate != null && v.NextVisitDate <= now.AddDays(3) && v.NextVisitDate > now)
            .Take(5).ToListAsync();

        reminders.AddRange(upcomingVisits.Select(v => new ReminderItem
        {
            Title = $"拜访提醒：{v.Customer?.Name}",
            Detail = $"计划于 {v.NextVisitDate:MM-dd} 进行拜访，目的：{v.Purpose}",
            Type = "Visit", Priority = "中",
            CreatedAt = v.NextVisitDate!.Value
        }));

        // 3. 订单预警（超过30天没下单的客户）
        var customers = await _db.Customers
            .Where(c => c.BranchId == branchId && c.Status == CustomerStatus.Active)
            .ToListAsync();

        foreach (var c in customers)
        {
            var lastOrder = await _db.Orders
                .Where(o => o.CustomerId == c.Id && o.Status == OrderStatus.Completed)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();
            if (lastOrder != null)
            {
                var days = (int)(now - lastOrder.CreatedAt).TotalDays;
                if (days >= 30 && days < 60)
                {
                    reminders.Add(new ReminderItem
                    {
                        Title = $"客户预警：{c.Name}",
                        Detail = $"已 {days} 天未下单，上次订单 {lastOrder.CreatedAt:MM-dd}",
                        Type = "Churn", Priority = "低", CreatedAt = lastOrder.CreatedAt
                    });
                }
            }
        }

        return reminders.OrderByDescending(r => r.Priority == "高" ? 3 : r.Priority == "中" ? 2 : 1).ThenBy(r => r.CreatedAt).Take(10).ToList();
    }
}

public class ReminderItem
{
    public string Title { get; set; } = "";
    public string Detail { get; set; } = "";
    public string Type { get; set; } = "";
    public string Priority { get; set; } = "低";
    public DateTime CreatedAt { get; set; }
}
