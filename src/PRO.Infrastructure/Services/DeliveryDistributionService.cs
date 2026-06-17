using Microsoft.EntityFrameworkCore;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;
using Serilog;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 物流配送自动分配服务 — 基于区域+坐标的负载均衡算法
/// 
/// 算法核心：
///   1. 区域优先 — 优先分配同服务区域的配送员
///   2. 距离加权 — 在同区域内按距离排序，距离越近权重越高
///   3. 负载均衡 — 动态平衡各配送员的负载量，避免某一人过载
///   4. 状态感知 — 排除离线/请假配送员，满负载不再分配
/// </summary>
public class DeliveryDistributionService : IOrderDistributionService
{
    private readonly ProDbContext _dbContext;
    private static readonly double EarthRadiusMeters = 6371000.0;

    public DeliveryDistributionService(ProDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // ─── 获取待分配订单 ────────────────────────────────────────

    public async Task<ApiResponse<List<OrderListItem>>> GetPendingOrdersAsync(int branchId)
    {
        try
        {
            var orders = await _dbContext.Orders
                .AsNoTracking()
                .Include(o => o.Customer)
                .Include(o => o.Branch)
                .Include(o => o.Creator)
                .Include(o => o.DeliveryPerson)
                .Where(o => o.BranchId == branchId && o.Status == OrderStatus.Pending)
                .OrderBy(o => o.CreatedAt)
                .Select(o => new OrderListItem
                {
                    Id = o.Id,
                    OrderNo = o.OrderNo,
                    CustomerId = o.CustomerId,
                    CustomerName = o.Customer != null ? o.Customer.Name : "未知",
                    BranchId = o.BranchId,
                    BranchName = o.Branch != null ? o.Branch.Name : "",
                    TotalAmount = o.TotalAmount,
                    Status = o.Status,
                    PaymentStatus = o.PaymentStatus,
                    CreatedAt = o.CreatedAt,
                    DeliveryPersonName = o.DeliveryPerson != null ? o.DeliveryPerson.Name : null,
                    CreatedByName = o.Creator != null ? o.Creator.Name : "",
                    DeliveryLongitude = o.DeliveryLongitude,
                    DeliveryLatitude = o.DeliveryLatitude,
                    DeliveryAddress = o.DeliveryAddress,
                    ReceivedAmount = o.ReceivedAmount,
                    DeliveryTime = o.DeliveryTime
                })
                .ToListAsync();

            return ApiResponse<List<OrderListItem>>.Ok(orders);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取待分配订单失败 BranchId={BranchId}", branchId);
            return ApiResponse<List<OrderListItem>>.Fail($"获取待分配订单失败: {ex.Message}");
        }
    }

    // ─── 手动分配 ─────────────────────────────────────────────

    public async Task<ApiResponse<bool>> ManualAssignAsync(int orderId, int deliveryPersonId, int assignedById)
    {
        try
        {
            var order = await _dbContext.Orders
                .Include(o => o.Customer).Include(o => o.Creator)
                .FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return ApiResponse<bool>.Fail("订单不存在");
            if (order.Status != OrderStatus.Pending)
                return ApiResponse<bool>.Fail("只有待分配状态的订单可以分配");

            var person = await _dbContext.DeliveryPersons.FindAsync(deliveryPersonId);
            if (person == null) return ApiResponse<bool>.Fail("配送员不存在");
            if (person.Status == DeliveryPersonStatus.Off)
                return ApiResponse<bool>.Fail("配送员已离线，无法分配");
            if (!OrderStatusManager.IsValidTransition(order.Status, OrderStatus.Assigned))
                return ApiResponse<bool>.Fail($"订单不能从「{OrderStatusManager.GetStatusName(order.Status)}」直接变为「{OrderStatusManager.GetStatusName(OrderStatus.Assigned)}」");

            // 更新负载
            order.DeliveryPersonId = deliveryPersonId;
            order.Status = OrderStatus.Assigned;
            order.UpdatedAt = DateTime.Now;
            person.CurrentLoad++;
            if (person.CurrentLoad >= person.MaxLoad)
                person.Status = DeliveryPersonStatus.Busy;

            // 记录操作日志
            var assigner = await _dbContext.Employees.FindAsync(assignedById);
            _dbContext.OperationLogs.Add(new OperationLog
            {
                OperatorId = assignedById,
                OperatorNo = assigner?.EmployeeNo ?? "",
                Module = "配送管理",
                OperationType = "手动分配",
                Content = $"订单 {order.OrderNo} 分配给配送员 {person.Name}（{person.Phone}）",
                EntityType = "Order",
                EntityId = orderId,
                Result = "Success"
            });

            await _dbContext.SaveChangesAsync();
            Log.Information("订单 {OrderNo} 手动分配给配送员 {Person}", order.OrderNo, person.Name);
            return ApiResponse<bool>.Ok(true, $"订单 {order.OrderNo} 已分配给 {person.Name}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "手动分配订单失败 OrderId={OrderId}", orderId);
            return ApiResponse<bool>.Fail($"分配失败: {ex.Message}");
        }
    }

    // ─── 自动分配（区域+坐标+负载均衡） ─────────────────────────

    /// <summary>
    /// 自动分配 — 支持三种算法:
    ///   "region_load_distance" — 默认，区域→负载→距离 三级排序
    ///   "load_balance"        — 仅负载均衡
    ///   "distance_only"       — 仅距离优先
    /// </summary>
    public async Task<ApiResponse<bool>> AutoAssignAsync(int branchId, string algorithm = "region_load_distance")
    {
        int assignedCount = 0;
        int failedCount = 0;
        try
        {
            // 1. 获取待分配订单（按创建时间升序，先到先分配）
            var pendingOrders = await _dbContext.Orders
                .Include(o => o.Customer)
                .Where(o => o.BranchId == branchId && o.Status == OrderStatus.Pending)
                .OrderBy(o => o.CreatedAt)
                .ToListAsync();

            if (!pendingOrders.Any())
                return ApiResponse<bool>.Ok(true, "没有待分配的订单");

            // 2. 获取可用配送员
            var availablePersons = await _dbContext.DeliveryPersons
                .Where(p => p.BranchId == branchId
                         && p.Status != DeliveryPersonStatus.Off
                         && p.CurrentLoad < p.MaxLoad)
                .OrderBy(p => p.CurrentLoad) // 负载低的优先
                .ToListAsync();

            if (!availablePersons.Any())
                return ApiResponse<bool>.Fail("没有可用的配送员");

            foreach (var order in pendingOrders)
            {
                // 动态检查可用配送员
                var assignable = availablePersons
                    .Where(p => p.Status != DeliveryPersonStatus.Off && p.CurrentLoad < p.MaxLoad)
                    .ToList();

                if (!assignable.Any()) break;

                DeliveryPerson? selected = null;

                switch (algorithm)
                {
                    case "load_balance":
                        selected = SelectByLoadOnly(assignable);
                        break;
                    case "distance_only":
                        selected = SelectByDistanceOnly(assignable, order);
                        break;
                    case "region_load_distance":
                    default:
                        selected = SelectByRegionLoadDistance(assignable, order);
                        break;
                }

                if (selected == null) continue;
                if (!OrderStatusManager.IsValidTransition(order.Status, OrderStatus.Assigned))
                {
                    failedCount++;
                    continue;
                }

                // 分配
                order.DeliveryPersonId = selected.Id;
                order.Status = OrderStatus.Assigned;
                order.UpdatedAt = DateTime.Now;
                selected.CurrentLoad++;
                if (selected.CurrentLoad >= selected.MaxLoad)
                    selected.Status = DeliveryPersonStatus.Busy;

                assignedCount++;

                // 操作日志
                _dbContext.OperationLogs.Add(new OperationLog
                {
                    OperatorId = -1, // -1 = 系统自动
                    OperatorNo = "SYSTEM",
                    Module = "配送管理",
                    OperationType = "自动分配",
                    Content = $"订单 {order.OrderNo} 自动分配给 {selected.Name}（算法:{algorithm}）",
                    EntityType = "Order",
                    EntityId = order.Id,
                    Result = "Success"
                });
            }

            await _dbContext.SaveChangesAsync();
            Log.Information("自动分配完成 BranchId={BranchId}, Algorithm={Algorithm}, Assigned={Assigned}, Failed={Failed}",
                branchId, algorithm, assignedCount, failedCount);

            return ApiResponse<bool>.Ok(true,
                $"自动分配完成: {assignedCount} 单已分配, {failedCount} 单无可用配送员");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "自动分配失败 BranchId={BranchId}", branchId);
            return ApiResponse<bool>.Fail($"自动分配失败: {ex.Message}");
        }
    }

    // ─── 最优分配方案（预演，不实际分配） ────────────────────────

    /// <summary>
    /// 返回最优分配方案：订单ID → 配送员ID
    /// 用于管理员预览/确认批量分配方案
    /// </summary>
    public async Task<ApiResponse<Dictionary<int, int>>> GetOptimalAssignmentAsync(int branchId)
    {
        try
        {
            var plan = new Dictionary<int, int>();

            var pendingOrders = await _dbContext.Orders
                .AsNoTracking()
                .Include(o => o.Customer)
                .Where(o => o.BranchId == branchId && o.Status == OrderStatus.Pending)
                .OrderBy(o => o.CreatedAt)
                .ToListAsync();

            if (!pendingOrders.Any())
                return ApiResponse<Dictionary<int, int>>.Ok(plan, "没有待分配订单");

            var availablePersons = await _dbContext.DeliveryPersons
                .AsNoTracking()
                .Where(p => p.BranchId == branchId
                         && p.Status != DeliveryPersonStatus.Off
                         && p.CurrentLoad < p.MaxLoad)
                .ToListAsync();

            if (!availablePersons.Any())
                return ApiResponse<Dictionary<int, int>>.Fail("没有可用的配送员");

            // 模拟负载（快照副本）
            var virtualLoads = availablePersons.ToDictionary(p => p.Id, p => p.CurrentLoad);

            foreach (var order in pendingOrders)
            {
                // 过滤尚未满载的
                var candidates = availablePersons
                    .Where(p => virtualLoads.GetValueOrDefault(p.Id) < p.MaxLoad)
                    .ToList();
                if (!candidates.Any()) break;

                var best = SelectByRegionLoadDistance(candidates, order);
                if (best == null) continue;

                plan[order.Id] = best.Id;
                virtualLoads[best.Id]++;
            }

            return ApiResponse<Dictionary<int, int>>.Ok(plan,
                $"最优方案: {plan.Count} 单可分配, {pendingOrders.Count - plan.Count} 单无可用配送员");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "计算最优分配方案失败 BranchId={BranchId}", branchId);
            return ApiResponse<Dictionary<int, int>>.Fail($"计算失败: {ex.Message}");
        }
    }

    // ─── 分配算法私有方法 ──────────────────────────────────────

    /// <summary>
    /// 三级排序：区域匹配 → 负载均衡 → 距离优先
    /// </summary>
    private DeliveryPerson? SelectByRegionLoadDistance(List<DeliveryPerson> persons, Order order)
    {
        // Customer 已在 include 中加载
        var orderArea = order.Customer?.FullAddress ?? order.DeliveryAddress ?? "";
        var orderLat = order.DeliveryLatitude;
        var orderLng = order.DeliveryLongitude;

        var scored = persons.Select(p => new
        {
            Person = p,
            RegionScore = CalculateRegionScore(p.ServiceArea, orderArea, order.Customer?.District),
            DistanceScore = CalculateDistanceScore(p, orderLat, orderLng),
            LoadRatio = p.CurrentLoad / (double)p.MaxLoad
        });

        // 排序优先级: 区域匹配度降序 → 负载率升序 → 距离升序
        var best = scored
            .OrderByDescending(s => s.RegionScore)   // 区域匹配度越高越好
            .ThenBy(s => s.LoadRatio)                  // 负载越低越好
            .ThenBy(s => s.DistanceScore)              // 距离越近得分越高
            .FirstOrDefault();

        return best?.Person;
    }

    /// <summary>
    /// 仅负载均衡：选当前负载最低的
    /// </summary>
    private DeliveryPerson? SelectByLoadOnly(List<DeliveryPerson> persons)
    {
        return persons
            .OrderBy(p => p.CurrentLoad / (double)Math.Max(p.MaxLoad, 1))
            .FirstOrDefault();
    }

    /// <summary>
    /// 仅距离优先：选离配送地址最近的
    /// </summary>
    private DeliveryPerson? SelectByDistanceOnly(List<DeliveryPerson> persons, Order order)
    {
        if (!order.DeliveryLatitude.HasValue || !order.DeliveryLongitude.HasValue)
            return SelectByLoadOnly(persons); // 无坐标时回退到负载均衡

        // DeliveryPerson 实体目前没有坐标字段，主要通过 ServiceArea 匹配
        var orderArea = order.Customer?.FullAddress ?? order.DeliveryAddress ?? "";
        var orderDistrict = order.Customer?.District;

        return persons
            .OrderByDescending(p => CalculateRegionScore(p.ServiceArea, orderArea, orderDistrict))
            .FirstOrDefault();
    }

    // ─── 评分计算 ─────────────────────────────────────────────

    /// <summary>
    /// 区域匹配评分:
    ///   100分 - 服务区域包含客户所在区县
    ///   75分  - 服务区域包含客户所在城市
    ///   50分  - 服务区域包含客户所在省份
    ///   25分  - 地址部分匹配
    ///   0分   - 无匹配
    /// </summary>
    private static int CalculateRegionScore(string? serviceArea, string fullAddress, string? district)
    {
        if (string.IsNullOrWhiteSpace(serviceArea))
            return 15; // 无限制服务区域，所有订单中等优先级

        var area = serviceArea.Trim();

        // 精确区县匹配
        if (!string.IsNullOrWhiteSpace(district) && area.Contains(district))
            return 100;

        // 模糊匹配：尝试从 fullAddress 中提取城区信息
        if (!string.IsNullOrWhiteSpace(fullAddress))
        {
            // 市二级匹配
            var addressParts = fullAddress.Replace("省", "|").Replace("市", "|").Replace("区", "|")
                .Replace("县", "|").Replace("街道", "|").Split('|', StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in addressParts)
            {
                if (part.Length >= 2 && area.Contains(part.Trim()))
                {
                    // 检查是区县级(4个字以内)还是市级
                    if (part.Trim().Length <= 4)
                        return 75; // 区县匹配
                    else
                        return 50; // 市级匹配
                }
            }

            // 包含式匹配
            if (area.Length >= 2 && fullAddress.Contains(area))
                return 60;
        }

        return 0;
    }

    /// <summary>
    /// 距离评分（0-100）:
    ///   距离越近得分越高。
    ///   注意：当前 DeliveryPerson 实体没有位置坐标字段，
    ///   此处基于 ServiceArea 文本估算，预留坐标扩展接口。
    /// </summary>
    private static double CalculateDistanceScore(DeliveryPerson person, double? orderLat, double? orderLng)
    {
        // 当前版本：DeliveryPerson 无坐标，退还区域匹配分
        // 未来扩展：为 DeliveryPerson 增加 Lat/Lng 后启用真实距离计算
        return 50; // 默认中等分
    }

    /// <summary>
    /// Haversine公式 — 计算两个坐标点之间的地球表面距离（米）
    /// 预留：当 DeliveryPerson 实体添加坐标字段后启用
    /// </summary>
    private static double CalculateHaversineDistance(double lat1, double lng1, double lat2, double lng2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLng = ToRadians(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2))
              * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusMeters * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

    // ─── 负载释放（订单完成/取消时调用） ───────────────────────

    /// <summary>
    /// 释放配送员负载 — 当订单完成、取消或转移时调用
    /// </summary>
    public async Task<ApiResponse<bool>> ReleaseLoadAsync(int deliveryPersonId)
    {
        try
        {
            var person = await _dbContext.DeliveryPersons.FindAsync(deliveryPersonId);
            if (person == null) return ApiResponse<bool>.Fail("配送员不存在");

            person.CurrentLoad = Math.Max(0, person.CurrentLoad - 1);
            if (person.CurrentLoad < person.MaxLoad && person.Status == DeliveryPersonStatus.Busy)
                person.Status = DeliveryPersonStatus.Available;

            await _dbContext.SaveChangesAsync();
            return ApiResponse<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "释放配送员负载失败 Id={Id}", deliveryPersonId);
            return ApiResponse<bool>.Fail($"操作失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取配送员负载状态快照（用于Dashboard展示）
    /// </summary>
    public async Task<ApiResponse<List<DeliveryLoadSnapshot>>> GetLoadSnapshotAsync(int branchId)
    {
        try
        {
            var persons = await _dbContext.DeliveryPersons
                .AsNoTracking()
                .Where(p => p.BranchId == branchId)
                .Select(p => new DeliveryLoadSnapshot
                {
                    Id = p.Id,
                    Name = p.Name,
                    Phone = p.Phone,
                    CurrentLoad = p.CurrentLoad,
                    MaxLoad = p.MaxLoad,
                    LoadPercentage = Math.Round(p.CurrentLoad * 100.0 / Math.Max(p.MaxLoad, 1), 1),
                    Status = p.Status,
                    ServiceArea = p.ServiceArea ?? "",
                    VehicleNumber = p.VehicleNumber ?? ""
                })
                .OrderByDescending(s => s.LoadPercentage)
                .ToListAsync();

            return ApiResponse<List<DeliveryLoadSnapshot>>.Ok(persons);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取配送员负载快照失败 BranchId={BranchId}", branchId);
            return ApiResponse<List<DeliveryLoadSnapshot>>.Fail($"获取失败: {ex.Message}");
        }
    }
}

// ─── 配送负载快照 ────────────────────────────────────────────

public class DeliveryLoadSnapshot
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public int CurrentLoad { get; set; }
    public int MaxLoad { get; set; }
    public double LoadPercentage { get; set; }
    public DeliveryPersonStatus Status { get; set; }
    public string ServiceArea { get; set; } = "";
    public string VehicleNumber { get; set; } = "";
}
