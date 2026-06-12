using Microsoft.EntityFrameworkCore;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 工作台数据服务 - 聚合业务关键指标
/// 使用 MemoryCacheService 短缓存（2min），支持 forceRefresh 手动刷新
/// </summary>
public class DashboardService
{
    private readonly ProDbContext _dbContext;
    private readonly MemoryCacheService _cache;
    private static readonly TimeSpan DashboardCacheTime = TimeSpan.FromMinutes(2);

    public DashboardService(ProDbContext dbContext, MemoryCacheService cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    /// <summary>
    /// 获取工作台完整数据（带缓存，支持强制刷新）
    /// </summary>
    public async Task<ApiResponse<BusinessDashboardDto>> GetDashboardDataAsync(int branchId, bool forceRefresh = false)
    {
        try
        {
            var cacheKey = string.Format(CacheKeys.DashboardData, branchId);

            if (forceRefresh)
            {
                _cache.Remove(cacheKey);
                Serilog.Log.Information("Dashboard 缓存已手动刷新: BranchId={BranchId}", branchId);
            }

            var result = await _cache.GetOrCreateAsync(cacheKey,
                () => LoadDashboardDataAsync(branchId),
                DashboardCacheTime);

            return ApiResponse<BusinessDashboardDto>.Ok(result);
        }
        catch (Exception ex)
        {
            return ApiResponse<BusinessDashboardDto>.Fail($"获取工作台数据失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 被动失效 Dashboard 缓存（例如订单变更后调用）
    /// </summary>
    public void InvalidateDashboardCache(int branchId)
    {
        _cache.RemoveByPrefix(string.Format(CacheKeys.DashboardData, branchId));
    }

    private async Task<BusinessDashboardDto> LoadDashboardDataAsync(int branchId)
    {
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        var result = new BusinessDashboardDto();

        // 今日概览
        var todayOrders = await _dbContext.Orders
            .Where(o => o.BranchId == branchId && o.CreatedAt >= today && o.CreatedAt < tomorrow)
            .ToListAsync();

        result.TodayOrderCount = todayOrders.Count;
        result.TodayOrderAmount = todayOrders.Sum(o => o.TotalAmount);
        result.PendingOrderCount = await _dbContext.Orders
            .CountAsync(o => o.BranchId == branchId && o.Status == OrderStatus.Pending);
        result.DeliveringOrderCount = await _dbContext.Orders
            .CountAsync(o => o.BranchId == branchId && o.Status == OrderStatus.Delivering);
        result.CompletedOrderCount = await _dbContext.Orders
            .CountAsync(o => o.BranchId == branchId && o.Status == OrderStatus.Completed && o.CreatedAt >= today);

        // 待处理事项
        result.UnassignedOrderCount = await _dbContext.Orders
            .CountAsync(o => o.BranchId == branchId && o.Status == OrderStatus.Pending);
        result.DraftOrderCount = await _dbContext.Orders
            .CountAsync(o => o.BranchId == branchId && o.Status == OrderStatus.Draft);

        var overdueDate = DateTime.Now.AddDays(-7);
        var overdueOrders = await _dbContext.Orders
            .Where(o => o.BranchId == branchId && o.PaymentStatus != PaymentStatus.Paid
                && o.Status == OrderStatus.Completed && o.CreatedAt < overdueDate)
            .ToListAsync();
        result.OverduePaymentCount = overdueOrders.Count;
        result.OverduePaymentAmount = overdueOrders.Sum(o => o.TotalAmount - o.ReceivedAmount);

        result.PendingSettlementCount = await _dbContext.Orders
            .CountAsync(o => o.BranchId == branchId && o.Status == OrderStatus.Completed && o.SettlementId == null);

        // 配送相关
        result.TodayDeliveryCount = await _dbContext.Orders
            .CountAsync(o => o.BranchId == branchId && o.Status == OrderStatus.Completed && o.DeliveryTime >= today);
        result.DeliveryFailedCount = await _dbContext.Orders
            .CountAsync(o => o.BranchId == branchId && o.Status == OrderStatus.Failed && o.UpdatedAt >= today);
        result.OverloadedDeliveryPersons = await _dbContext.DeliveryPersons
            .CountAsync(d => d.BranchId == branchId && d.CurrentLoad >= d.MaxLoad);

        // 客户相关
        result.NewCustomerCount = await _dbContext.Customers
            .CountAsync(c => c.BranchId == branchId && c.CreatedAt >= today && c.Status == CustomerStatus.Active);
        result.VisitReminderCount = await _dbContext.VisitRecords
            .CountAsync(v => v.NextVisitDate != null && v.NextVisitDate <= DateTime.Now.AddDays(3) && v.NextVisitDate > DateTime.Now);

        // 异常/告警
        result.RecentErrorCount = await _dbContext.OperationLogs
            .CountAsync(l => l.Result == "Failed" && l.OperatedAt >= today);

        result.Alerts = await BuildAlertsAsync(branchId);
        result.TodayTasks = BuildTodayTasks(result);
        result.Shortcuts = BuildShortcuts();

        result.RecentOrders = await _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Where(o => o.BranchId == branchId)
            .OrderByDescending(o => o.CreatedAt)
            .Take(10)
            .Select(o => new OrderListItem
            {
                Id = o.Id, OrderNo = o.OrderNo,
                CustomerName = o.Customer != null ? o.Customer.Name : "",
                TotalAmount = o.TotalAmount, Status = o.Status,
                PaymentStatus = o.PaymentStatus, CreatedAt = o.CreatedAt
            })
            .ToListAsync();

        return result;
    }

    private async Task<List<DashboardAlert>> BuildAlertsAsync(int branchId)
    {
        var alerts = new List<DashboardAlert>();

        var pendingCount = await _dbContext.Orders
            .CountAsync(o => o.BranchId == branchId && o.Status == OrderStatus.Pending);
        if (pendingCount > 10)
            alerts.Add(new DashboardAlert { Type = "Warning", Title = "待分配订单积压", Message = $"当前有 {pendingCount} 个订单待分配配送员", ActionText = "去分配", ActionRoute = "order" });

        var overdueAmount = await _dbContext.Orders
            .Where(o => o.BranchId == branchId && o.PaymentStatus != PaymentStatus.Paid
                && o.Status == OrderStatus.Completed && o.CreatedAt < DateTime.Now.AddDays(-7))
            .SumAsync(o => o.TotalAmount - o.ReceivedAmount);
        if (overdueAmount > 0)
            alerts.Add(new DashboardAlert { Type = "Error", Title = "超期应收提醒", Message = $"有 ¥{overdueAmount:N0} 应收账款超过7天未收回", ActionText = "查看应收", ActionRoute = "ar" });

        return alerts;
    }

    private List<DashboardTask> BuildTodayTasks(BusinessDashboardDto data)
    {
        var tasks = new List<DashboardTask>();
        if (data.UnassignedOrderCount > 0)
            tasks.Add(new DashboardTask { Category = "订单", Title = "待分配订单", Description = $"{data.UnassignedOrderCount} 个订单等待分配", Count = data.UnassignedOrderCount, Priority = data.UnassignedOrderCount > 10 ? "High" : "Normal", ActionRoute = "order" });
        if (data.OverduePaymentCount > 0)
            tasks.Add(new DashboardTask { Category = "财务", Title = "超期应收", Description = $"{data.OverduePaymentCount} 笔应收超期", Count = data.OverduePaymentCount, Priority = "High", ActionRoute = "ar" });
        return tasks;
    }

    private List<DashboardShortcut> BuildShortcuts()
    {
        return
        [
            new() { Title = "新建订单", Icon = "➕", Route = "order_new" },
            new() { Title = "客户查询", Icon = "🔍", Route = "customer" },
            new() { Title = "配送分配", Icon = "🚚", Route = "logistics" },
            new() { Title = "收款登记", Icon = "💰", Route = "ar" }
        ];
    }
}
