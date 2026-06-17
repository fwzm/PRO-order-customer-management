using Microsoft.EntityFrameworkCore;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 库存服务 - 记录库存变动和盘点
/// </summary>
public class InventoryService : IInventoryService
{
    private readonly ProDbContext _dbContext;

    public InventoryService(ProDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// 记录库存变动
    /// </summary>
    public async Task LogChangeAsync(int productId, int beforeQty, int afterQty, string changeType, string reason,
        int? orderId = null, int operatorId = 0)
    {
        await AddChangeLogAsync(productId, beforeQty, afterQty, changeType, reason, orderId, null, operatorId);
        await _dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// 在当前 DbContext 中追加库存日志，但不立即保存；用于订单事务内保证库存和日志一致。
    /// </summary>
    public async Task AddChangeLogAsync(int productId, int beforeQty, int afterQty, string changeType, string reason,
        int? orderId = null, string? orderNo = null, int operatorId = 0)
    {
        var product = await _dbContext.Products.FindAsync(productId);
        orderNo ??= orderId.HasValue
            ? await _dbContext.Orders.Where(o => o.Id == orderId.Value).Select(o => o.OrderNo).FirstOrDefaultAsync()
            : null;

        _dbContext.InventoryChangeLogs.Add(new InventoryChangeLog
        {
            ProductId = productId,
            ProductName = product?.Name ?? "",
            ProductSku = product?.SKU ?? "",
            BeforeQuantity = beforeQty,
            AfterQuantity = afterQty,
            ChangeQuantity = afterQty - beforeQty,
            ChangeType = changeType,
            ChangeReason = reason,
            RelatedOrderId = orderId,
            RelatedOrderNo = orderNo,
            OperatorId = operatorId,
            CreatedAt = DateTime.Now
        });
    }

    /// <summary>
    /// 获取库存变动日志
    /// </summary>
    public async Task<ApiResponse<PagedResult<InventoryChangeLogDto>>> GetChangeLogsAsync(
        PagedRequest request, int? productId = null, string? changeType = null,
        DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            var query = _dbContext.InventoryChangeLogs.AsNoTracking().AsQueryable();

            if (productId.HasValue)
                query = query.Where(l => l.ProductId == productId.Value);
            if (!string.IsNullOrEmpty(changeType))
                query = query.Where(l => l.ChangeType == changeType);
            if (startDate.HasValue)
                query = query.Where(l => l.CreatedAt >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(l => l.CreatedAt < endDate.Value.AddDays(1));

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(l => l.CreatedAt)
                .Skip((request.PageIndex - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(l => new InventoryChangeLogDto
                {
                    Id = l.Id,
                    ProductId = l.ProductId,
                    ProductName = l.ProductName,
                    ProductSku = l.ProductSku ?? "",
                    BeforeQuantity = l.BeforeQuantity,
                    AfterQuantity = l.AfterQuantity,
                    ChangeQuantity = l.ChangeQuantity,
                    ChangeType = l.ChangeType,
                    ChangeReason = l.ChangeReason,
                    RelatedOrderId = l.RelatedOrderId,
                    RelatedOrderNo = l.RelatedOrderNo,
                    OperatorId = l.OperatorId,
                    CreatedAt = l.CreatedAt
                })
                .ToListAsync();

            return ApiResponse<PagedResult<InventoryChangeLogDto>>.Ok(new PagedResult<InventoryChangeLogDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageIndex = request.PageIndex,
                PageSize = request.PageSize
            });
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<InventoryChangeLogDto>>.Fail($"查询失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取库存盘点数据
    /// </summary>
    public async Task<ApiResponse<List<InventoryStockDto>>> GetStockInventoryAsync()
    {
        try
        {
            var products = await _dbContext.Products
                .AsNoTracking()
                .Where(p => p.Status == ProductStatus.Active)
                .OrderBy(p => p.SKU)
                .ToListAsync();

            var productIds = products.Select(p => p.Id).ToList();
            var lastChanges = await _dbContext.InventoryChangeLogs
                .AsNoTracking()
                .Where(l => productIds.Contains(l.ProductId))
                .GroupBy(l => l.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    LastChangeAt = g.Max(l => l.CreatedAt),
                    LastChangeReason = g.OrderByDescending(l => l.CreatedAt).First().ChangeReason
                })
                .ToDictionaryAsync(x => x.ProductId);

            var inventory = products.Select(p =>
            {
                var lastChange = lastChanges.GetValueOrDefault(p.Id);
                return new InventoryStockDto
                {
                    ProductId = p.Id,
                    ProductName = p.Name,
                    ProductSku = p.SKU,
                    SystemStock = p.Stock,
                    LastChangeAt = lastChange?.LastChangeAt ?? p.CreatedAt,
                    LastChangeReason = lastChange?.LastChangeReason
                };
            }).ToList();

            return ApiResponse<List<InventoryStockDto>>.Ok(inventory);
        }
        catch (Exception ex)
        {
            return ApiResponse<List<InventoryStockDto>>.Fail($"获取库存失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 手动调整库存
    /// </summary>
    public async Task<ApiResponse<bool>> AdjustStockAsync(InventoryAdjustRequest request, int operatorId)
    {
        try
        {
            var product = await _dbContext.Products.FindAsync(request.ProductId);
            if (product == null)
                return ApiResponse<bool>.Fail("产品不存在");

            var beforeQty = product.Stock;
            product.Stock = request.NewQuantity;
            product.UpdatedAt = DateTime.Now;
            product.SyncStatus = SyncStatus.Pending;

            await AddChangeLogAsync(
                request.ProductId,
                beforeQty,
                request.NewQuantity,
                "ManualAdjust",
                request.Reason,
                operatorId: operatorId);

            await _dbContext.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "库存已调整");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"调整失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 批量盘点
    /// </summary>
    public async Task<ApiResponse<int>> BatchStockTakeAsync(List<InventoryAdjustRequest> adjustments, int operatorId)
    {
        try
        {
            int successCount = 0;
            foreach (var adjustment in adjustments)
            {
                var result = await AdjustStockAsync(adjustment, operatorId);
                if (result.Success) successCount++;
            }

            return ApiResponse<int>.Ok(successCount, $"已调整 {successCount} 个产品的库存");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"批量盘点失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取库存变动统计
    /// </summary>
    public async Task<ApiResponse<InventoryChangeStatsDto>> GetChangeStatsAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            var logs = await _dbContext.InventoryChangeLogs
                .AsNoTracking()
                .Where(l => l.CreatedAt >= startDate && l.CreatedAt < endDate.AddDays(1))
                .ToListAsync();

            var stats = new InventoryChangeStatsDto
            {
                StartDate = startDate,
                EndDate = endDate,
                TotalChanges = logs.Count,
                OrderCreates = logs.Count(l => l.ChangeType == "OrderCreate"),
                OrderCancels = logs.Count(l => l.ChangeType == "OrderCancel"),
                ManualAdjusts = logs.Count(l => l.ChangeType == "ManualAdjust"),
                TopChangedProducts = logs
                    .GroupBy(l => new { l.ProductId, l.ProductName })
                    .Select(g => new ProductChangeSummary
                    {
                        ProductId = g.Key.ProductId,
                        ProductName = g.Key.ProductName,
                        TotalChanges = g.Count(),
                        NetChange = g.Sum(l => l.ChangeQuantity)
                    })
                    .OrderByDescending(p => p.TotalChanges)
                    .Take(10)
                    .ToList()
            };

            return ApiResponse<InventoryChangeStatsDto>.Ok(stats);
        }
        catch (Exception ex)
        {
            return ApiResponse<InventoryChangeStatsDto>.Fail($"获取统计失败: {ex.Message}");
        }
    }
}
