using Microsoft.EntityFrameworkCore;
using PRO.Application.DTOs;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 客户时间线服务 - 聚合客户所有业务事件
/// </summary>
public class CustomerTimelineService
{
    private readonly ProDbContext _dbContext;

    public CustomerTimelineService(ProDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// 获取客户时间线
    /// </summary>
    public async Task<ApiResponse<CustomerTimelineDto>> GetTimelineAsync(int customerId, int pageIndex = 0, int pageSize = 50)
    {
        try
        {
            var customer = await _dbContext.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == customerId);

            if (customer == null)
                return ApiResponse<CustomerTimelineDto>.Fail("客户不存在");

            var events = new List<CustomerTimelineEvent>();

            // 1. 订单事件
            var orders = await _dbContext.Orders
                .AsNoTracking()
                .Include(o => o.Creator)
                .Include(o => o.DeliveryPerson)
                .Where(o => o.CustomerId == customerId)
                .OrderByDescending(o => o.CreatedAt)
                .Take(200)
                .ToListAsync();

            foreach (var order in orders)
            {
                // 订单创建
                events.Add(new CustomerTimelineEvent
                {
                    EventTime = order.CreatedAt,
                    EventType = TimelineEventTypes.Order,
                    EventIcon = TimelineEventTypes.GetIcon(TimelineEventTypes.Order),
                    EventColor = TimelineEventTypes.GetColor(TimelineEventTypes.Order),
                    Title = "创建订单",
                    Description = $"订单 {order.OrderNo}，金额 ¥{order.TotalAmount:N0}",
                    OperatorName = order.Creator?.Name,
                    RelatedId = order.Id,
                    RelatedType = "Order",
                    Metadata = new Dictionary<string, string>
                    {
                        ["OrderNo"] = order.OrderNo,
                        ["Amount"] = order.TotalAmount.ToString("N0"),
                        ["Status"] = OrderStatusManager.GetStatusName(order.Status)
                    }
                });

                // 订单状态变更记录
                var modifications = await _dbContext.OrderModificationRecords
                    .AsNoTracking()
                    .Include(r => r.ModifiedBy)
                    .Where(r => r.OrderId == order.Id && r.ModificationType == "StatusChange")
                    .OrderByDescending(r => r.ModifiedAt)
                    .Take(5)
                    .ToListAsync();

                foreach (var mod in modifications)
                {
                    events.Add(new CustomerTimelineEvent
                    {
                        EventTime = mod.ModifiedAt,
                        EventType = TimelineEventTypes.OrderStatusChange,
                        EventIcon = TimelineEventTypes.GetIcon(TimelineEventTypes.OrderStatusChange),
                        EventColor = TimelineEventTypes.GetColor(TimelineEventTypes.OrderStatusChange),
                        Title = $"订单状态变更",
                        Description = $"{order.OrderNo}: {mod.Content}",
                        OperatorName = mod.ModifiedBy?.Name,
                        RelatedId = order.Id,
                        RelatedType = "Order"
                    });
                }

                // 配送完成时间
                if (order.DeliveryTime.HasValue && order.Status == OrderStatus.Completed)
                {
                    events.Add(new CustomerTimelineEvent
                    {
                        EventTime = order.DeliveryTime.Value,
                        EventType = TimelineEventTypes.OrderStatusChange,
                        EventIcon = "🚚",
                        EventColor = "#34C759",
                        Title = "配送完成",
                        Description = $"订单 {order.OrderNo} 已送达",
                        OperatorName = order.DeliveryPerson?.Name,
                        RelatedId = order.Id,
                        RelatedType = "Order"
                    });
                }
            }

            // 2. 拜访记录
            var visits = await _dbContext.VisitRecords
                .AsNoTracking()
                .Include(v => v.Visitor)
                .Where(v => v.CustomerId == customerId)
                .OrderByDescending(v => v.VisitDate)
                .Take(100)
                .ToListAsync();

            foreach (var visit in visits)
            {
                events.Add(new CustomerTimelineEvent
                {
                    EventTime = visit.VisitDate,
                    EventType = TimelineEventTypes.Visit,
                    EventIcon = TimelineEventTypes.GetIcon(TimelineEventTypes.Visit),
                    EventColor = TimelineEventTypes.GetColor(TimelineEventTypes.Visit),
                    Title = $"客户拜访",
                    Description = $"目的：{visit.Purpose ?? "未填写"}",
                    OperatorName = visit.Visitor?.Name,
                    RelatedId = visit.Id,
                    RelatedType = "Visit",
                    Metadata = new Dictionary<string, string>
                    {
                        ["Purpose"] = visit.Purpose ?? "",
                        ["Result"] = visit.Result ?? "",
                        ["NextAction"] = visit.NextAction ?? ""
                    }
                });
            }

            // 3. 客户信息变更
            var customerUpdates = await _dbContext.OperationLogs
                .AsNoTracking()
                .Where(l => l.EntityType == "Customer" && l.EntityId == customerId)
                .OrderByDescending(l => l.OperatedAt)
                .Take(50)
                .ToListAsync();

            foreach (var update in customerUpdates)
            {
                events.Add(new CustomerTimelineEvent
                {
                    EventTime = update.OperatedAt,
                    EventType = TimelineEventTypes.CustomerUpdate,
                    EventIcon = TimelineEventTypes.GetIcon(TimelineEventTypes.CustomerUpdate),
                    EventColor = TimelineEventTypes.GetColor(TimelineEventTypes.CustomerUpdate),
                    Title = update.OperationType,
                    Description = update.Content,
                    OperatorName = update.OperatorNo
                });
            }

            // 4. 商机记录
            var opportunities = await _dbContext.Opportunities
                .AsNoTracking()
                .Where(o => o.CustomerId == customerId)
                .OrderByDescending(o => o.CreatedAt)
                .Take(50)
                .ToListAsync();

            foreach (var opp in opportunities)
            {
                events.Add(new CustomerTimelineEvent
                {
                    EventTime = opp.CreatedAt,
                    EventType = TimelineEventTypes.Opportunity,
                    EventIcon = TimelineEventTypes.GetIcon(TimelineEventTypes.Opportunity),
                    EventColor = TimelineEventTypes.GetColor(TimelineEventTypes.Opportunity),
                    Title = $"商机：{opp.Title}",
                    Description = $"阶段：{opp.Stage}，预期金额：¥{opp.ExpectedAmount:N0}",
                    RelatedId = opp.Id,
                    RelatedType = "Opportunity"
                });
            }

            // 按时间排序并分页
            var sortedEvents = events
                .OrderByDescending(e => e.EventTime)
                .ToList();

            var totalCount = sortedEvents.Count;
            var pagedEvents = sortedEvents
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .ToList();

            return ApiResponse<CustomerTimelineDto>.Ok(new CustomerTimelineDto
            {
                CustomerId = customerId,
                CustomerName = customer.Name,
                Events = pagedEvents,
                TotalCount = totalCount,
                HasMore = (pageIndex + 1) * pageSize < totalCount
            });
        }
        catch (Exception ex)
        {
            return ApiResponse<CustomerTimelineDto>.Fail($"获取客户时间线失败: {ex.Message}");
        }
    }
}
