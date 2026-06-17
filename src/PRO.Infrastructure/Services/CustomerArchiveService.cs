using Microsoft.EntityFrameworkCore;
using PRO.Application.DTOs;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 客户档案服务 - 聚合客户完整业务数据
/// </summary>
public class CustomerArchiveService
{
    private readonly ProDbContext _dbContext;

    public CustomerArchiveService(ProDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// 获取客户完整档案
    /// </summary>
    public async Task<ApiResponse<CustomerArchiveDto>> GetCustomerArchiveAsync(int customerId)
    {
        try
        {
            var customer = await _dbContext.Customers
                .AsNoTracking()
                .Include(c => c.Branch)
                .Include(c => c.ParentCustomer)
                .Include(c => c.Creator)
                .FirstOrDefaultAsync(c => c.Id == customerId && c.Status != CustomerStatus.Deleted);

            if (customer == null)
                return ApiResponse<CustomerArchiveDto>.Fail("客户不存在");

            var now = DateTime.Now;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);

            // 订单统计
            var orders = await _dbContext.Orders
                .AsNoTracking()
                .Where(o => o.CustomerId == customerId && o.Status != OrderStatus.Draft)
                .ToListAsync();

            var totalOrderCount = orders.Count;
            var totalOrderAmount = orders.Sum(o => o.TotalAmount);
            var thisMonthOrders = orders.Where(o => o.CreatedAt >= startOfMonth).ToList();
            var lastOrder = orders.OrderByDescending(o => o.CreatedAt).FirstOrDefault();

            // 应收账款
            var completedOrders = orders.Where(o => o.Status == OrderStatus.Completed).ToList();
            var totalReceivable = completedOrders.Sum(o => o.TotalAmount - o.ReceivedAmount);
            var overdueDate = now.AddDays(-7);
            var overdueOrders = completedOrders
                .Where(o => o.PaymentStatus != PaymentStatus.Paid && o.CreatedAt < overdueDate)
                .ToList();

            // 子客户
            var subCustomers = await _dbContext.Customers
                .AsNoTracking()
                .Where(c => c.ParentCustomerId == customerId && c.Status == CustomerStatus.Active)
                .Select(c => new CustomerListItem { Id = c.Id, Name = c.Name, Phone = c.Phone })
                .ToListAsync();

            // 最近订单
            var recentOrders = orders
                .OrderByDescending(o => o.CreatedAt)
                .Take(20)
                .Select(o => new OrderListItem
                {
                    Id = o.Id,
                    OrderNo = o.OrderNo,
                    TotalAmount = o.TotalAmount,
                    Status = o.Status,
                    PaymentStatus = o.PaymentStatus,
                    CreatedAt = o.CreatedAt
                })
                .ToList();

            // 拜访记录
            var recentVisits = await _dbContext.VisitRecords
                .AsNoTracking()
                .Include(v => v.Visitor)
                .Where(v => v.CustomerId == customerId)
                .OrderByDescending(v => v.VisitDate)
                .Take(10)
                .Select(v => new VisitRecordDto
                {
                    Id = v.Id,
                    CustomerId = v.CustomerId,
                    VisitDate = v.VisitDate,
                    Purpose = v.Purpose,
                    Result = v.Result,
                    NextAction = v.NextAction,
                    NextVisitDate = v.NextVisitDate,
                    CreatedByName = v.Visitor != null ? v.Visitor.Name : ""
                })
                .ToListAsync();

            // 构建风险提示
            var risks = BuildCustomerRisks(customer, orders, overdueOrders, lastOrder);

            var archive = new CustomerArchiveDto
            {
                // 基本信息
                Id = customer.Id,
                Name = customer.Name,
                CustomerNo = customer.CustomerNo,
                CustomerType = customer.CustomerType,
                CustomerTypeName = customer.CustomerType == CustomerType.Major ? "大客户" : "细分客户",
                Phone = customer.Phone,
                Address = customer.Address,
                FullAddress = customer.FullAddress,
                Longitude = customer.Longitude,
                Latitude = customer.Latitude,
                LegalPerson = customer.LegalPerson,
                Remark = customer.Remark,
                BranchId = customer.BranchId,
                BranchName = customer.Branch?.Name ?? "",
                CreatedAt = customer.CreatedAt,
                CreatedByName = customer.Creator?.Name ?? "",

                // 关联信息
                ParentCustomerId = customer.ParentCustomerId,
                ParentCustomerName = customer.ParentCustomer?.Name,
                SubCustomers = subCustomers,

                // 订单统计
                TotalOrderCount = totalOrderCount,
                TotalOrderAmount = totalOrderAmount,
                ThisMonthOrderCount = thisMonthOrders.Count,
                ThisMonthOrderAmount = thisMonthOrders.Sum(o => o.TotalAmount),
                LastOrderDate = lastOrder?.CreatedAt,
                DaysSinceLastOrder = lastOrder != null ? (int)(now - lastOrder.CreatedAt).TotalDays : -1,

                // 应收账款
                TotalReceivable = totalReceivable,
                OverdueReceivable = overdueOrders.Sum(o => o.TotalAmount - o.ReceivedAmount),
                OverdueOrderCount = overdueOrders.Count,

                // 历史数据
                RecentOrders = recentOrders,
                RecentVisits = recentVisits,

                // 企微信息
                WeChatExternalUserId = customer.WeChatExternalUserId,
                IsWeChatBound = !string.IsNullOrEmpty(customer.WeChatExternalUserId),

                // 风险提示
                Risks = risks
            };

            return ApiResponse<CustomerArchiveDto>.Ok(archive);
        }
        catch (Exception ex)
        {
            return ApiResponse<CustomerArchiveDto>.Fail($"获取客户档案失败: {ex.Message}");
        }
    }

    private List<CustomerRisk> BuildCustomerRisks(
        Domain.Entities.Customer customer,
        List<Domain.Entities.Order> orders,
        List<Domain.Entities.Order> overdueOrders,
        Domain.Entities.Order? lastOrder)
    {
        var risks = new List<CustomerRisk>();

        // 超期应收风险
        if (overdueOrders.Any())
        {
            var overdueAmount = overdueOrders.Sum(o => o.TotalAmount - o.ReceivedAmount);
            risks.Add(new CustomerRisk
            {
                Type = "Error",
                Message = $"有 {overdueOrders.Count} 笔超期应收，金额 ¥{overdueAmount:N0}",
                Suggestion = "建议尽快联系客户催收"
            });
        }

        // 长期未下单风险
        if (lastOrder != null)
        {
            var daysSinceLastOrder = (int)(DateTime.Now - lastOrder.CreatedAt).TotalDays;
            if (daysSinceLastOrder > 30 && daysSinceLastOrder <= 60)
            {
                risks.Add(new CustomerRisk
                {
                    Type = "Warning",
                    Message = $"已 {daysSinceLastOrder} 天未下单",
                    Suggestion = "建议安排客户回访"
                });
            }
            else if (daysSinceLastOrder > 60)
            {
                risks.Add(new CustomerRisk
                {
                    Type = "Error",
                    Message = $"已 {daysSinceLastOrder} 天未下单，存在流失风险",
                    Suggestion = "建议立即联系客户了解情况"
                });
            }
        }

        // 企微未绑定
        if (string.IsNullOrEmpty(customer.WeChatExternalUserId))
        {
            risks.Add(new CustomerRisk
            {
                Type = "Info",
                Message = "客户未绑定企业微信",
                Suggestion = "建议引导客户添加企微，便于日常沟通"
            });
        }

        // 信息不完整
        if (string.IsNullOrEmpty(customer.Phone))
        {
            risks.Add(new CustomerRisk
            {
                Type = "Warning",
                Message = "客户手机号未填写",
                Suggestion = "建议补充手机号，便于联系和查重"
            });
        }

        return risks;
    }
}
