using Microsoft.EntityFrameworkCore;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;
using Serilog;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 订单自动分配服务 - 基于区域+坐标+负载均衡的智能调度
/// </summary>
public class OrderDistributionService : IOrderDistributionService
{
    private readonly ProDbContext _dbContext;
    private readonly IOperationLogService _logService;

    // 算法参数配置
    private const double MaxDeliveryDistanceKm = 30.0;  // 最大配送距离
    private const double LoadWeightFactor = 0.4;         // 负载权重
    private const double DistanceWeightFactor = 0.4;     // 距离权重
    private const double RegionWeightFactor = 0.2;       // 区域匹配权重

    public OrderDistributionService(ProDbContext dbContext, IOperationLogService logService)
    {
        _dbContext = dbContext;
        _logService = logService;
    }

    /// <summary>
    /// 获取待分配订单列表
    /// </summary>
    public async Task<ApiResponse<List<OrderListItem>>> GetPendingOrdersAsync(int branchId)
    {
        try
        {
            var orders = await _dbContext.Orders
                .AsNoTracking()
                .Include(o => o.Customer)
                .Where(o => o.BranchId == branchId && o.Status == OrderStatus.Pending)
                .OrderBy(o => o.CreatedAt)
                .Select(o => new OrderListItem
                {
                    Id = o.Id,
                    OrderNo = o.OrderNo,
                    CustomerId = o.CustomerId,
                    CustomerName = o.Customer != null ? o.Customer.Name : "",
                    TotalAmount = o.TotalAmount,
                    DeliveryAddress = o.DeliveryAddress,
                    DeliveryLongitude = o.DeliveryLongitude,
                    DeliveryLatitude = o.DeliveryLatitude,
                    CreatedAt = o.CreatedAt
                })
                .ToListAsync();

            return ApiResponse<List<OrderListItem>>.Ok(orders);
        }
        catch (Exception ex)
        {
            return ApiResponse<List<OrderListItem>>.Fail($"获取待分配订单失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 手动分配订单
    /// </summary>
    public async Task<ApiResponse<bool>> ManualAssignAsync(int orderId, int deliveryPersonId, int assignedById)
    {
        try
        {
            var order = await _dbContext.Orders.FindAsync(orderId);
            if (order == null)
                return ApiResponse<bool>.Fail("订单不存在");

            var person = await _dbContext.DeliveryPersons.FindAsync(deliveryPersonId);
            if (person == null)
                return ApiResponse<bool>.Fail("配送员不存在");

            if (person.Status != DeliveryPersonStatus.Available)
                return ApiResponse<bool>.Fail("配送员不可用");

            if (person.CurrentLoad >= person.MaxLoad)
                return ApiResponse<bool>.Fail("配送员已满载");

            if (!OrderStatusManager.IsValidTransition(order.Status, OrderStatus.Assigned))
                return ApiResponse<bool>.Fail($"订单不能从「{OrderStatusManager.GetStatusName(order.Status)}」直接变为「{OrderStatusManager.GetStatusName(OrderStatus.Assigned)}」");

            order.DeliveryPersonId = deliveryPersonId;
            order.Status = OrderStatus.Assigned;
            order.UpdatedAt = DateTime.Now;
            order.LocalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            order.SyncStatus = SyncStatus.Pending;

            person.CurrentLoad++;

            _dbContext.OrderModificationRecords.Add(new OrderModificationRecord
            {
                OrderId = orderId,
                ModifiedById = assignedById,
                ModifiedAt = DateTime.Now,
                Content = $"手动分配配送员: {person.Name}",
                ModificationType = "Assign"
            });

            await _dbContext.SaveChangesAsync();

            await _logService.CreateAsync(assignedById, "物流", "手动分配",
                $"订单 {order.OrderNo} 分配给 {person.Name}", "Order", orderId);

            return ApiResponse<bool>.Ok(true, "分配成功");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"分配失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 自动分配订单（核心算法）
    /// </summary>
    public async Task<ApiResponse<bool>> AutoAssignAsync(int branchId, string algorithm = "region_load_distance")
    {
        try
        {
            // 1. 获取待分配订单
            var pendingOrders = await _dbContext.Orders
                .Include(o => o.Customer)
                .Where(o => o.BranchId == branchId && o.Status == OrderStatus.Pending)
                .OrderBy(o => o.CreatedAt)
                .ToListAsync();

            if (!pendingOrders.Any())
                return ApiResponse<bool>.Ok(true, "没有待分配的订单");

            // 2. 获取可用配送员
            var availablePersons = await _dbContext.DeliveryPersons
                .Where(d => d.BranchId == branchId && d.Status == DeliveryPersonStatus.Available)
                .ToListAsync();

            if (!availablePersons.Any())
                return ApiResponse<bool>.Fail("没有可用的配送员");

            // 3. 从配送员服务区域字段解析负责区域，避免引入额外表结构
            var personRegions = BuildPersonRegions(availablePersons);

            // 4. 执行分配算法
            var assignments = new Dictionary<int, int>(); // orderId -> deliveryPersonId
            var personLoads = availablePersons.ToDictionary(p => p.Id, p => p.CurrentLoad);

            foreach (var order in pendingOrders)
            {
                var bestPerson = FindBestDeliveryPerson(order, availablePersons, personLoads, personRegions);
                if (bestPerson != null)
                {
                    assignments[order.Id] = bestPerson.Id;
                    personLoads[bestPerson.Id]++;
                }
            }

            // 5. 应用分配结果
            var successCount = 0;
            foreach (var assignment in assignments)
            {
                var order = await _dbContext.Orders.FindAsync(assignment.Key);
                var person = await _dbContext.DeliveryPersons.FindAsync(assignment.Value);

                if (order != null && person != null)
                {
                    if (!OrderStatusManager.IsValidTransition(order.Status, OrderStatus.Assigned))
                        continue;

                    order.DeliveryPersonId = assignment.Value;
                    order.Status = OrderStatus.Assigned;
                    order.UpdatedAt = DateTime.Now;
                    order.LocalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    order.SyncStatus = SyncStatus.Pending;

                    person.CurrentLoad = personLoads[assignment.Value];

                    _dbContext.OrderModificationRecords.Add(new OrderModificationRecord
                    {
                        OrderId = assignment.Key,
                        ModifiedById = 0, // 系统自动
                        ModifiedAt = DateTime.Now,
                        Content = $"自动分配配送员: {person.Name} (算法: {algorithm})",
                        ModificationType = "AutoAssign"
                    });

                    successCount++;
                }
            }

            await _dbContext.SaveChangesAsync();

            await _logService.CreateAsync(0, "物流", "自动分配",
                $"自动分配完成: {successCount}/{pendingOrders.Count} 单", "Order");

            return ApiResponse<bool>.Ok(true, $"自动分配完成: 成功 {successCount}/{pendingOrders.Count} 单");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "自动分配失败");
            return ApiResponse<bool>.Fail($"自动分配失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取最优分配方案（预览）
    /// </summary>
    public async Task<ApiResponse<Dictionary<int, int>>> GetOptimalAssignmentAsync(int branchId)
    {
        try
        {
            var pendingOrders = await _dbContext.Orders
                .Include(o => o.Customer)
                .Where(o => o.BranchId == branchId && o.Status == OrderStatus.Pending)
                .ToListAsync();

            var availablePersons = await _dbContext.DeliveryPersons
                .Where(d => d.BranchId == branchId && d.Status == DeliveryPersonStatus.Available)
                .ToListAsync();

            var personRegions = BuildPersonRegions(availablePersons);

            var assignments = new Dictionary<int, int>();
            var personLoads = availablePersons.ToDictionary(p => p.Id, p => p.CurrentLoad);

            foreach (var order in pendingOrders)
            {
                var bestPerson = FindBestDeliveryPerson(order, availablePersons, personLoads, personRegions);
                if (bestPerson != null)
                {
                    assignments[order.Id] = bestPerson.Id;
                    personLoads[bestPerson.Id]++;
                }
            }

            return ApiResponse<Dictionary<int, int>>.Ok(assignments);
        }
        catch (Exception ex)
        {
            return ApiResponse<Dictionary<int, int>>.Fail($"获取分配方案失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 核心算法：为订单找到最优配送员
    /// </summary>
    private DeliveryPerson? FindBestDeliveryPerson(
        Order order,
        List<DeliveryPerson> availablePersons,
        Dictionary<int, int> personLoads,
        List<DeliveryPersonRegion> personRegions)
    {
        DeliveryPerson? bestPerson = null;
        double bestScore = double.MinValue;

        foreach (var person in availablePersons)
        {
            // 跳过满载配送员
            if (personLoads[person.Id] >= person.MaxLoad)
                continue;

            var score = CalculateAssignmentScore(order, person, personLoads, personRegions);
            if (score > bestScore)
            {
                bestScore = score;
                bestPerson = person;
            }
        }

        return bestPerson;
    }

    /// <summary>
    /// 计算分配评分（越高越好）
    /// </summary>
    private double CalculateAssignmentScore(
        Order order,
        DeliveryPerson person,
        Dictionary<int, int> personLoads,
        List<DeliveryPersonRegion> personRegions)
    {
        double score = 100.0; // 基础分

        // 1. 负载评分（负载越低分数越高）
        var loadRatio = (double)personLoads[person.Id] / person.MaxLoad;
        var loadScore = (1.0 - loadRatio) * 100;

        // 2. 距离评分：当前配送员模型暂无实时坐标，先按中性分处理
        double distanceScore = order.DeliveryLongitude.HasValue && order.DeliveryLatitude.HasValue ? 70.0 : 60.0;

        // 3. 区域匹配评分
        double regionScore = 50.0; // 默认中等分数
        var personRegionList = personRegions.Where(r => r.DeliveryPersonId == person.Id).ToList();
        if (personRegionList.Any())
        {
            // 检查订单地址是否在配送员负责区域内
            var isInRange = personRegionList.Any(r =>
                !string.IsNullOrEmpty(order.DeliveryAddress) &&
                !string.IsNullOrEmpty(r.RegionName) &&
                order.DeliveryAddress.Contains(r.RegionName));

            regionScore = isInRange ? 100.0 : 30.0;
        }

        // 加权计算总分
        score = (loadScore * LoadWeightFactor) +
                (distanceScore * DistanceWeightFactor) +
                (regionScore * RegionWeightFactor);

        return score;
    }

    private static List<DeliveryPersonRegion> BuildPersonRegions(IEnumerable<DeliveryPerson> persons)
    {
        var separators = new[] { ',', '，', ';', '；', '/', '|', '、' };
        return persons
            .Where(p => !string.IsNullOrWhiteSpace(p.ServiceArea))
            .SelectMany(p => p.ServiceArea!
                .Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(region => new DeliveryPersonRegion(p.Id, region)))
            .ToList();
    }
}

internal sealed record DeliveryPersonRegion(int DeliveryPersonId, string RegionName);

/// <summary>
/// 地理坐标计算器
/// </summary>
public static class GeoCalculator
{
    private const double EarthRadiusKm = 6371.0;

    /// <summary>
    /// 计算两点之间的距离（Haversine公式）
    /// </summary>
    public static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return EarthRadiusKm * c;
    }

    /// <summary>
    /// 批量计算距离矩阵
    /// </summary>
    public static double[,] CalculateDistanceMatrix(List<GeoPoint> points)
    {
        var n = points.Count;
        var matrix = new double[n, n];

        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                var distance = CalculateDistance(
                    points[i].Latitude, points[i].Longitude,
                    points[j].Latitude, points[j].Longitude);
                matrix[i, j] = distance;
                matrix[j, i] = distance;
            }
        }

        return matrix;
    }

    /// <summary>
    /// 查找最近的点
    /// </summary>
    public static (int Index, double Distance) FindNearest(GeoPoint target, List<GeoPoint> points)
    {
        var minDistance = double.MaxValue;
        var minIndex = -1;

        for (int i = 0; i < points.Count; i++)
        {
            var distance = CalculateDistance(
                target.Latitude, target.Longitude,
                points[i].Latitude, points[i].Longitude);

            if (distance < minDistance)
            {
                minDistance = distance;
                minIndex = i;
            }
        }

        return (minIndex, minDistance);
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}

public class GeoPoint
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
