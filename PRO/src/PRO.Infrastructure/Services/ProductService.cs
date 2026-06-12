using Microsoft.EntityFrameworkCore;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 产品服务实现 — 分类数据使用 MemoryCacheService 长缓存
/// </summary>
public class ProductService : IProductService
{
    private readonly ProDbContext _dbContext;
    private readonly MemoryCacheService _cache;

    private static readonly TimeSpan CategoriesCacheTime = TimeSpan.FromMinutes(60);

    public ProductService(ProDbContext dbContext, MemoryCacheService cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public async Task<ApiResponse<PagedResult<ProductListItem>>> GetListAsync(PagedRequest request, int? categoryId = null, ProductStatus? status = null)
    {
        try
        {
            var query = _dbContext.Products.AsNoTracking().Include(p => p.Category).AsQueryable();
            if (categoryId.HasValue) query = query.Where(p => p.CategoryId == categoryId.Value);
            if (status.HasValue) query = query.Where(p => p.Status == status.Value);
            if (!string.IsNullOrWhiteSpace(request.Keyword))
                query = query.Where(p => p.Name.Contains(request.Keyword) || p.SKU.Contains(request.Keyword));

            var totalCount = await query.CountAsync();
            var items = await query.OrderBy(p => p.SKU)
                .Skip((request.PageIndex - 1) * request.PageSize).Take(request.PageSize)
                .Select(p => new ProductListItem
                {
                    Id = p.Id,
                    SKU = p.SKU,
                    Name = p.Name,
                    Specification = p.Specification,
                    AverageSalePrice = p.AverageSalePrice,
                    ReferencePrice = p.ReferencePrice,
                    Stock = p.Stock,
                    Unit = p.Unit,
                    Status = p.Status,
                    CategoryName = p.Category != null ? p.Category.Name : null
                }).ToListAsync();

            return ApiResponse<PagedResult<ProductListItem>>.Ok(new PagedResult<ProductListItem>
            { Items = items, TotalCount = totalCount, PageIndex = request.PageIndex, PageSize = request.PageSize });
        }
        catch (Exception ex) { return ApiResponse<PagedResult<ProductListItem>>.Fail($"查询产品列表失败: {ex.Message}"); }
    }

    public async Task<ApiResponse<ProductListItem>> GetByIdAsync(int id)
    {
        var p = await _dbContext.Products.AsNoTracking().Include(x => x.Category).FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return ApiResponse<ProductListItem>.Fail("产品不存在");
        return ApiResponse<ProductListItem>.Ok(new ProductListItem
        {
            Id = p.Id,
            SKU = p.SKU,
            Name = p.Name,
            Specification = p.Specification,
            AverageSalePrice = p.AverageSalePrice,
            ReferencePrice = p.ReferencePrice,
            Stock = p.Stock,
            Unit = p.Unit,
            Status = p.Status,
            CategoryName = p.Category?.Name
        });
    }

    public async Task<ApiResponse<List<ProductCategoryDto>>> GetCategoriesAsync()
    {
        var cats = await _cache.GetOrCreateAsync(CacheKeys.ProductCategories,
            async () =>
            {
                var categories = await _dbContext.ProductCategories
                    .AsNoTracking()
                    .OrderBy(c => c.SortOrder)
                    .ToListAsync();
                return categories.Select(c => new ProductCategoryDto { Id = c.Id, Name = c.Name }).ToList();
            },
            CategoriesCacheTime);

        return ApiResponse<List<ProductCategoryDto>>.Ok(cats);
    }

    public async Task<ApiResponse<int>> CreateAsync(CreateProductRequest request)
    {
        try
        {
            if (await _dbContext.Products.AnyAsync(p => p.SKU == request.SKU))
                return ApiResponse<int>.Fail("SKU编码已存在");

            var product = new Product
            {
                SKU = request.SKU,
                Name = request.Name,
                Specification = request.Specification,
                AverageSalePrice = request.AverageSalePrice,
                ReferencePrice = request.ReferencePrice,
                Stock = request.Stock,
                Unit = request.Unit,
                CategoryId = request.CategoryId,
                Status = ProductStatus.Active,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                LocalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            _dbContext.Products.Add(product);
            await _dbContext.SaveChangesAsync();
            _cache.InvalidateProducts(0);
            _cache.InvalidateCategories();
            return ApiResponse<int>.Ok(product.Id, "产品创建成功");
        }
        catch (Exception ex) { return ApiResponse<int>.Fail($"创建产品失败: {ex.Message}"); }
    }

    public async Task<ApiResponse<bool>> UpdateAsync(UpdateProductRequest request)
    {
        try
        {
            var product = await _dbContext.Products.FindAsync(request.Id);
            if (product == null) return ApiResponse<bool>.Fail("产品不存在");

            product.Name = request.Name; product.Specification = request.Specification;
            product.AverageSalePrice = request.AverageSalePrice; product.ReferencePrice = request.ReferencePrice;
            product.Unit = request.Unit; product.CategoryId = request.CategoryId;
            product.Status = request.Status; product.UpdatedAt = DateTime.Now;
            product.LocalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            product.SyncStatus = SyncStatus.Pending;

            await _dbContext.SaveChangesAsync();
            _cache.InvalidateProducts(0);
            _cache.InvalidateCategories();
            return ApiResponse<bool>.Ok(true, "产品更新成功");
        }
        catch (Exception ex) { return ApiResponse<bool>.Fail($"更新产品失败: {ex.Message}"); }
    }

    public async Task<ApiResponse<bool>> UpdateStockAsync(int id, int quantity)
    {
        var product = await _dbContext.Products.FindAsync(id);
        if (product == null) return ApiResponse<bool>.Fail("产品不存在");
        product.Stock = quantity; product.UpdatedAt = DateTime.Now; product.SyncStatus = SyncStatus.Pending;
        await _dbContext.SaveChangesAsync();
        _cache.InvalidateProducts(0);
        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id)
    {
        var product = await _dbContext.Products.FindAsync(id);
        if (product == null) return ApiResponse<bool>.Fail("产品不存在");
        var hasOrders = await _dbContext.OrderItems.AnyAsync(i => i.ProductId == id);
        if (hasOrders) return ApiResponse<bool>.Fail("该产品已关联订单，无法删除");
        _dbContext.Products.Remove(product);
        await _dbContext.SaveChangesAsync();
        _cache.InvalidateProducts(0);
        _cache.InvalidateCategories();
        return ApiResponse<bool>.Ok(true, "产品已删除");
    }
}
