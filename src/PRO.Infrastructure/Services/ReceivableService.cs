using Microsoft.EntityFrameworkCore;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 应收账款服务 — 基于订单+折扣+收款+核销的综合计算
/// </summary>
public class ReceivableService : IReceivableService
{
    private readonly ProDbContext _dbContext;

    public ReceivableService(ProDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// 获取客户应收账款列表（服务端聚合，避免前端卡顿）
    /// </summary>
    public async Task<ApiResponse<List<CustomerReceivableDto>>> GetCustomerReceivablesAsync(ReceivableQueryRequest request)
    {
        try
        {
            // 有效订单状态：已确认、已分配、配送中、已完成（排除草稿和取消）
            var validStatuses = new[] { OrderStatus.Pending, OrderStatus.Assigned, OrderStatus.Delivering, OrderStatus.Completed };

            var query = _dbContext.Orders
                .AsNoTracking()
                .Include(o => o.Customer)
                .ThenInclude(c => c!.Branch)
                .Include(o => o.Customer!.CustomerManager)
                .Where(o => validStatuses.Contains(o.Status));

            if (request.CustomerId.HasValue)
                query = query.Where(o => o.CustomerId == request.CustomerId.Value);
            if (request.BranchId.HasValue)
                query = query.Where(o => o.BranchId == request.BranchId.Value);
            if (request.SalespersonId.HasValue)
                query = query.Where(o => o.Customer!.CustomerManagerId == request.SalespersonId.Value);

            // 按客户分组聚合
            var groups = await query
                .GroupBy(o => new
                {
                    o.CustomerId,
                    CustomerName = o.Customer != null ? o.Customer.Name : "",
                    o.BranchId,
                    BranchName = o.Branch != null ? o.Branch.Name : "",
                    o.Customer!.CustomerManagerId,
                    SalespersonName = o.Customer!.CustomerManager != null ? o.Customer!.CustomerManager.Name : ""
                })
                .Select(g => new CustomerReceivableDto
                {
                    CustomerId = g.Key.CustomerId,
                    CustomerName = g.Key.CustomerName,
                    BranchId = g.Key.BranchId,
                    BranchName = g.Key.BranchName,
                    TotalOrderAmount = g.Sum(o => o.TotalAmount),
                    TotalReceivedAmount = g.Sum(o => o.ReceivedAmount),
                    TotalDiscountAmount = g.Sum(o => o.DiscountAmount),
                    ReceivableBalance = g.Sum(o => o.TotalAmount - o.DiscountAmount - o.ReceivedAmount),
                    OrderCount = g.Count(),
                    LastOrderDate = g.Max(o => (DateTime?)o.CreatedAt)
                })
                .ToListAsync();

            // 过滤最小余额
            var result = groups.AsEnumerable();
            if (request.MinBalance.HasValue)
                result = result.Where(r => r.ReceivableBalance >= request.MinBalance.Value);

            // 排序
            if (!string.IsNullOrWhiteSpace(request.SortField))
            {
                var asc = string.Equals(request.SortOrder, "asc", StringComparison.OrdinalIgnoreCase);
                result = request.SortField switch
                {
                    "ReceivableBalance" => asc ? result.OrderBy(r => r.ReceivableBalance) : result.OrderByDescending(r => r.ReceivableBalance),
                    "TotalOrderAmount" => asc ? result.OrderBy(r => r.TotalOrderAmount) : result.OrderByDescending(r => r.TotalOrderAmount),
                    "CustomerName" => asc ? result.OrderBy(r => r.CustomerName) : result.OrderByDescending(r => r.CustomerName),
                    "LastOrderDate" => asc ? result.OrderBy(r => r.LastOrderDate ?? DateTime.MinValue) : result.OrderByDescending(r => r.LastOrderDate ?? DateTime.MinValue),
                    _ => result.OrderByDescending(r => r.ReceivableBalance)
                };
            }
            else
            {
                result = result.OrderByDescending(r => r.ReceivableBalance);
            }

            return ApiResponse<List<CustomerReceivableDto>>.Ok(result.ToList());
        }
        catch (Exception ex)
        {
            return ApiResponse<List<CustomerReceivableDto>>.Fail($"查询应收账款失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取单个客户应收明细
    /// </summary>
    public async Task<ApiResponse<CustomerReceivableDto>> GetCustomerReceivableAsync(int customerId)
    {
        try
        {
            var validStatuses = new[] { OrderStatus.Pending, OrderStatus.Assigned, OrderStatus.Delivering, OrderStatus.Completed };

            var data = await _dbContext.Orders
                .AsNoTracking()
                .Include(o => o.Customer)
                .ThenInclude(c => c!.Branch)
                .Where(o => o.CustomerId == customerId && validStatuses.Contains(o.Status))
                .GroupBy(o => new
                {
                    o.CustomerId,
                    CustomerName = o.Customer != null ? o.Customer.Name : "",
                    o.BranchId,
                    BranchName = o.Branch != null ? o.Branch.Name : ""
                })
                .Select(g => new CustomerReceivableDto
                {
                    CustomerId = g.Key.CustomerId,
                    CustomerName = g.Key.CustomerName,
                    BranchId = g.Key.BranchId,
                    BranchName = g.Key.BranchName,
                    TotalOrderAmount = g.Sum(o => o.TotalAmount),
                    TotalReceivedAmount = g.Sum(o => o.ReceivedAmount),
                    TotalDiscountAmount = g.Sum(o => o.DiscountAmount),
                    ReceivableBalance = g.Sum(o => o.TotalAmount - o.DiscountAmount - o.ReceivedAmount),
                    OrderCount = g.Count(),
                    LastOrderDate = g.Max(o => (DateTime?)o.CreatedAt)
                })
                .FirstOrDefaultAsync();

            return data != null
                ? ApiResponse<CustomerReceivableDto>.Ok(data)
                : ApiResponse<CustomerReceivableDto>.Ok(new CustomerReceivableDto { CustomerId = customerId });
        }
        catch (Exception ex)
        {
            return ApiResponse<CustomerReceivableDto>.Fail($"查询失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 账龄分析 — 按30/60/90天分段统计
    /// </summary>
    public async Task<ApiResponse<AgingAnalysisDto>> GetAgingAnalysisAsync(AgingAnalysisRequest request)
    {
        try
        {
            var asOfDate = request.AsOfDate ?? DateTime.Now.Date;
            var validStatuses = new[] { OrderStatus.Pending, OrderStatus.Assigned, OrderStatus.Delivering, OrderStatus.Completed };

            var query = _dbContext.Orders
                .AsNoTracking()
                .Include(o => o.Customer)
                .ThenInclude(c => c!.Branch)
                .Include(o => o.Customer!.CustomerManager)
                .Where(o => validStatuses.Contains(o.Status)
                    && o.TotalAmount - o.DiscountAmount - o.ReceivedAmount > 0);

            if (request.CustomerId.HasValue)
                query = query.Where(o => o.CustomerId == request.CustomerId.Value);
            if (request.BranchId.HasValue)
                query = query.Where(o => o.BranchId == request.BranchId.Value);
            if (request.SalespersonId.HasValue)
                query = query.Where(o => o.Customer!.CustomerManagerId == request.SalespersonId.Value);

            var orders = await query
                .Select(o => new AgingDetailDto
                {
                    OrderId = o.Id,
                    OrderNo = o.OrderNo,
                    CustomerId = o.CustomerId,
                    CustomerName = o.Customer != null ? o.Customer.Name : "",
                    BranchId = o.BranchId,
                    BranchName = o.Branch != null ? o.Branch.Name : "",
                    SalespersonId = o.Customer != null ? o.Customer.CustomerManagerId : null,
                    SalespersonName = o.Customer != null && o.Customer.CustomerManager != null ? o.Customer.CustomerManager.Name : null,
                    OrderDate = o.CreatedAt,
                    OrderAmount = o.TotalAmount,
                    DiscountAmount = o.DiscountAmount,
                    ReceivedAmount = o.ReceivedAmount,
                    ReceivableBalance = o.TotalAmount - o.DiscountAmount - o.ReceivedAmount
                })
                .ToListAsync();

            // 计算账龄
            foreach (var o in orders)
            {
                o.AgingDays = (int)(asOfDate - o.OrderDate.Date).TotalDays;
                o.AgingBucket = o.AgingDays switch
                {
                    <= 30 => "30天内",
                    <= 60 => "31-60天",
                    <= 90 => "61-90天",
                    _ => "90天以上"
                };
            }

            var within30 = orders.Where(o => o.AgingDays <= 30).ToList();
            var days31To60 = orders.Where(o => o.AgingDays > 30 && o.AgingDays <= 60).ToList();
            var days61To90 = orders.Where(o => o.AgingDays > 60 && o.AgingDays <= 90).ToList();
            var over90 = orders.Where(o => o.AgingDays > 90).ToList();

            var totalReceivable = orders.Sum(o => o.ReceivableBalance);

            return ApiResponse<AgingAnalysisDto>.Ok(new AgingAnalysisDto
            {
                AsOfDate = asOfDate,
                Within30Days = BuildBucket("30天内", within30, totalReceivable),
                Days31To60 = BuildBucket("31-60天", days31To60, totalReceivable),
                Days61To90 = BuildBucket("61-90天", days61To90, totalReceivable),
                Over90Days = BuildBucket("90天以上", over90, totalReceivable),
                TotalReceivable = totalReceivable,
                TotalOrderCount = orders.Count,
                Details = orders.OrderByDescending(o => o.AgingDays).ToList()
            });
        }
        catch (Exception ex)
        {
            return ApiResponse<AgingAnalysisDto>.Fail($"账龄分析失败: {ex.Message}");
        }
    }

    private static AgingBucketDto BuildBucket(string label, List<AgingDetailDto> orders, decimal totalReceivable)
    {
        var amount = orders.Sum(o => o.ReceivableBalance);
        return new AgingBucketDto
        {
            Label = label,
            Amount = amount,
            OrderCount = orders.Count,
            CustomerCount = orders.Select(o => o.CustomerId).Distinct().Count(),
            Percentage = totalReceivable > 0 ? amount / totalReceivable * 100 : 0
        };
    }
}
