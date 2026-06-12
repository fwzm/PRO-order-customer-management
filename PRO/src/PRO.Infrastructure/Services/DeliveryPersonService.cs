using Microsoft.EntityFrameworkCore;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 配送员服务实现 — 配送员列表使用 MemoryCacheService 缓存
/// </summary>
public class DeliveryPersonService : IDeliveryPersonService
{
    private readonly ProDbContext _dbContext;
    private readonly MemoryCacheService _cache;

    private static readonly TimeSpan DeliveryCacheTime = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan AvailableCacheTime = TimeSpan.FromMinutes(15);

    public DeliveryPersonService(ProDbContext dbContext, MemoryCacheService cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public async Task<ApiResponse<PagedResult<DeliveryPersonListItem>>> GetListAsync(PagedRequest request, int? branchId = null, DeliveryPersonStatus? status = null)
    {
        // 无筛选条件时使用缓存
        if (!branchId.HasValue && !status.HasValue && string.IsNullOrWhiteSpace(request.Keyword))
        {
            var cacheKey = CacheKeys.DeliveryPersons.Replace("{0}", "all");
            var cached = await _cache.GetOrCreateAsync(cacheKey,
                async () =>
                {
                    return await _dbContext.DeliveryPersons.AsNoTracking().Include(d => d.Branch)
                        .OrderBy(d => d.Name)
                        .Select(d => new DeliveryPersonListItem
                        {
                            Id = d.Id, Name = d.Name, Phone = d.Phone,
                            BranchName = d.Branch != null ? d.Branch.Name : "",
                            BranchId = d.BranchId, Status = d.Status,
                            CurrentLoad = d.CurrentLoad, MaxLoad = d.MaxLoad
                        }).ToListAsync();
                }, DeliveryCacheTime);

            return ApiResponse<PagedResult<DeliveryPersonListItem>>.Ok(new PagedResult<DeliveryPersonListItem>
            { Items = cached, TotalCount = cached.Count, PageIndex = 1, PageSize = cached.Count });
        }

        var query = _dbContext.DeliveryPersons.AsNoTracking().Include(d => d.Branch).AsQueryable();
        if (branchId.HasValue) query = query.Where(d => d.BranchId == branchId.Value);
        if (status.HasValue) query = query.Where(d => d.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
            query = query.Where(d => d.Name.Contains(request.Keyword) || (d.Phone != null && d.Phone.Contains(request.Keyword)));

        var totalCount = await query.CountAsync();
        var items = await query.OrderBy(d => d.Name)
            .Skip((request.PageIndex - 1) * request.PageSize).Take(request.PageSize)
            .Select(d => new DeliveryPersonListItem
            {
                Id = d.Id, Name = d.Name, Phone = d.Phone,
                BranchName = d.Branch != null ? d.Branch.Name : "",
                BranchId = d.BranchId, Status = d.Status,
                CurrentLoad = d.CurrentLoad, MaxLoad = d.MaxLoad
            }).ToListAsync();

        return ApiResponse<PagedResult<DeliveryPersonListItem>>.Ok(new PagedResult<DeliveryPersonListItem>
        { Items = items, TotalCount = totalCount, PageIndex = request.PageIndex, PageSize = request.PageSize });
    }

    public async Task<ApiResponse<DeliveryPersonListItem>> GetByIdAsync(int id)
    {
        var d = await _dbContext.DeliveryPersons.AsNoTracking().Include(x => x.Branch).FirstOrDefaultAsync(x => x.Id == id);
        if (d == null) return ApiResponse<DeliveryPersonListItem>.Fail("配送员不存在");
        return ApiResponse<DeliveryPersonListItem>.Ok(new DeliveryPersonListItem
        {
            Id = d.Id, Name = d.Name, Phone = d.Phone,
            BranchName = d.Branch?.Name ?? "", BranchId = d.BranchId,
            Status = d.Status, CurrentLoad = d.CurrentLoad, MaxLoad = d.MaxLoad
        });
    }

    public async Task<ApiResponse<List<DeliveryPersonListItem>>> GetAvailableAsync(int branchId)
    {
        var cacheKey = CacheKeys.Format(CacheKeys.AvailableDeliveryPersons, branchId);
        var persons = await _cache.GetOrCreateAsync(cacheKey,
            async () =>
            {
                return await _dbContext.DeliveryPersons.AsNoTracking()
                    .Where(d => d.BranchId == branchId && d.Status == DeliveryPersonStatus.Available)
                    .Select(d => new DeliveryPersonListItem
                    {
                        Id = d.Id, Name = d.Name, Phone = d.Phone,
                        CurrentLoad = d.CurrentLoad, MaxLoad = d.MaxLoad
                    }).ToListAsync();
            }, AvailableCacheTime);

        return ApiResponse<List<DeliveryPersonListItem>>.Ok(persons);
    }

    public async Task<ApiResponse<int>> CreateAsync(CreateDeliveryPersonRequest request)
    {
        try
        {
            var dp = new DeliveryPerson
            {
                Name = request.Name, Phone = request.Phone, BranchId = request.BranchId,
                Status = DeliveryPersonStatus.Available, CurrentLoad = 0,
                MaxLoad = request.MaxLoad, CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now
            };
            _dbContext.DeliveryPersons.Add(dp);
            await _dbContext.SaveChangesAsync();
            _cache.InvalidateDeliveryPersons();
            return ApiResponse<int>.Ok(dp.Id, "配送员创建成功");
        }
        catch (Exception ex) { return ApiResponse<int>.Fail($"创建配送员失败: {ex.Message}"); }
    }

    public async Task<ApiResponse<bool>> UpdateAsync(UpdateDeliveryPersonRequest request)
    {
        try
        {
            var dp = await _dbContext.DeliveryPersons.FindAsync(request.Id);
            if (dp == null) return ApiResponse<bool>.Fail("配送员不存在");

            dp.Name = request.Name; dp.Phone = request.Phone; dp.MaxLoad = request.MaxLoad;
            dp.Status = request.Status; dp.UpdatedAt = DateTime.Now;
            await _dbContext.SaveChangesAsync();
            _cache.InvalidateDeliveryPersons(dp.BranchId);
            return ApiResponse<bool>.Ok(true, "配送员更新成功");
        }
        catch (Exception ex) { return ApiResponse<bool>.Fail($"更新配送员失败: {ex.Message}"); }
    }

    public async Task<ApiResponse<bool>> UpdateLoadAsync(int id, int currentLoad)
    {
        var dp = await _dbContext.DeliveryPersons.FindAsync(id);
        if (dp == null) return ApiResponse<bool>.Fail("配送员不存在");
        dp.CurrentLoad = currentLoad; dp.UpdatedAt = DateTime.Now;
        await _dbContext.SaveChangesAsync();
        _cache.InvalidateDeliveryPersons(dp.BranchId);
        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id)
    {
        var dp = await _dbContext.DeliveryPersons.FindAsync(id);
        if (dp == null) return ApiResponse<bool>.Fail("配送员不存在");
        var hasOrders = await _dbContext.Orders.AnyAsync(o => o.DeliveryPersonId == id && o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled);
        if (hasOrders) return ApiResponse<bool>.Fail("该配送员有未完成订单，无法删除");
        var branchId = dp.BranchId;
        _dbContext.DeliveryPersons.Remove(dp);
        await _dbContext.SaveChangesAsync();
        _cache.InvalidateDeliveryPersons(branchId);
        return ApiResponse<bool>.Ok(true, "配送员已删除");
    }
}
