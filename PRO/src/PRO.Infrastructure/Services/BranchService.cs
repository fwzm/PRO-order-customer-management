using Microsoft.EntityFrameworkCore;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 分公司服务实现 — 分公司列表使用 MemoryCacheService 长缓存
/// </summary>
public class BranchService : IBranchService
{
    private readonly ProDbContext _dbContext;
    private readonly MemoryCacheService _cache;

    private static readonly TimeSpan BranchesCacheTime = TimeSpan.FromMinutes(60);

    public BranchService(ProDbContext dbContext, MemoryCacheService cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public async Task<ApiResponse<PagedResult<BranchListItem>>> GetListAsync(PagedRequest request)
    {
        try
        {
            // 无筛选条件时使用缓存
            if (string.IsNullOrWhiteSpace(request.Keyword))
            {
                var cachedBranches = await _cache.GetOrCreateAsync(CacheKeys.Branches,
                    async () =>
                    {
                        return await _dbContext.Branches.AsNoTracking()
                            .OrderBy(b => b.Code)
                            .Select(b => new BranchListItem
                            {
                                Id = b.Id, Name = b.Name, Code = b.Code,
                                Address = b.Address, Phone = b.Phone, Status = b.Status
                            })
                            .ToListAsync();
                    },
                    BranchesCacheTime);

                return ApiResponse<PagedResult<BranchListItem>>.Ok(new PagedResult<BranchListItem>
                {
                    Items = cachedBranches,
                    TotalCount = cachedBranches.Count,
                    PageIndex = 1,
                    PageSize = cachedBranches.Count
                });
            }

            // 有关键词筛选时不使用缓存
            var query = _dbContext.Branches.AsNoTracking().AsQueryable();
            query = query.Where(b => b.Name.Contains(request.Keyword) || b.Code.Contains(request.Keyword));

            var totalCount = await query.CountAsync();
            var items = await query.OrderBy(b => b.Code)
                .Skip((request.PageIndex - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(b => new BranchListItem
                {
                    Id = b.Id, Name = b.Name, Code = b.Code,
                    Address = b.Address, Phone = b.Phone, Status = b.Status
                })
                .ToListAsync();

            return ApiResponse<PagedResult<BranchListItem>>.Ok(new PagedResult<BranchListItem>
            {
                Items = items, TotalCount = totalCount,
                PageIndex = request.PageIndex, PageSize = request.PageSize
            });
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<BranchListItem>>.Fail($"查询分公司列表失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<BranchListItem>> GetByIdAsync(int id)
    {
        try
        {
            var branch = await _dbContext.Branches.AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == id);

            if (branch == null)
                return ApiResponse<BranchListItem>.Fail("分公司不存在");

            return ApiResponse<BranchListItem>.Ok(new BranchListItem
            {
                Id = branch.Id,
                Name = branch.Name,
                Code = branch.Code,
                Address = branch.Address,
                Phone = branch.Phone,
                Status = branch.Status
            });
        }
        catch (Exception ex)
        {
            return ApiResponse<BranchListItem>.Fail($"查询分公司详情失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<int>> CreateAsync(string name, string code, int? regionId, string? address, string? phone)
    {
        try
        {
            if (await _dbContext.Branches.AnyAsync(b => b.Code == code))
                return ApiResponse<int>.Fail("分公司编号已存在");

            var branch = new Branch
            {
                Name = name,
                Code = code,
                Address = address,
                Phone = phone,
                Status = EntityStatus.Active,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _dbContext.Branches.Add(branch);
            await _dbContext.SaveChangesAsync();
            _cache.InvalidateBranches();

            return ApiResponse<int>.Ok(branch.Id, "分公司创建成功");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"创建分公司失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> UpdateAsync(int id, string name, string code, int? regionId, string? address, string? phone, int status)
    {
        try
        {
            var branch = await _dbContext.Branches.FindAsync(id);
            if (branch == null)
                return ApiResponse<bool>.Fail("分公司不存在");

            branch.Name = name;
            branch.Code = code;
            branch.Address = address;
            branch.Phone = phone;
            branch.Status = (EntityStatus)status;
            branch.UpdatedAt = DateTime.Now;

            await _dbContext.SaveChangesAsync();
            _cache.InvalidateBranches();
            return ApiResponse<bool>.Ok(true, "分公司更新成功");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"更新分公司失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id)
    {
        try
        {
            var branch = await _dbContext.Branches.FindAsync(id);
            if (branch == null)
                return ApiResponse<bool>.Fail("分公司不存在");

            var hasEmployees = await _dbContext.Employees.AnyAsync(e => e.BranchId == id);
            if (hasEmployees)
                return ApiResponse<bool>.Fail("该分公司下有员工，无法删除");

            _dbContext.Branches.Remove(branch);
            await _dbContext.SaveChangesAsync();
            _cache.InvalidateBranches();

            return ApiResponse<bool>.Ok(true, "分公司已删除");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"删除分公司失败: {ex.Message}");
        }
    }
}
