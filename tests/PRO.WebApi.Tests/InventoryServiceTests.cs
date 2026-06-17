using Microsoft.EntityFrameworkCore;
using PRO.Application.DTOs;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;
using PRO.Infrastructure.Services;
using Xunit;

namespace PRO.WebApi.Tests;

/// <summary>
/// 库存服务测试 — 覆盖库存扣减、并发冲突检测、盘点等场景
/// </summary>
public class InventoryServiceTests
{
    [Fact]
    public async Task LogChangeAsync_ValidChange_CreatesChangeLog()
    {
        await using var db = CreateDbContext();
        await SeedProductsAsync(db);

        var service = new InventoryService(db);
        await service.LogChangeAsync(1, 100, 90, "OrderCreate", "订单创建扣减", 1, 1);

        var logs = await db.InventoryChangeLogs.ToListAsync();
        Assert.Single(logs);
        Assert.Equal(100, logs[0].BeforeQuantity);
        Assert.Equal(90, logs[0].AfterQuantity);
        Assert.Equal(-10, logs[0].ChangeQuantity);
        Assert.Equal("OrderCreate", logs[0].ChangeType);
    }

    [Fact]
    public async Task AdjustStockAsync_ProductNotFound_ReturnsFail()
    {
        await using var db = CreateDbContext();

        var service = new InventoryService(db);
        var result = await service.AdjustStockAsync(
            new InventoryAdjustRequest { ProductId = 999, NewQuantity = 50, Reason = "测试" },
            operatorId: 1);

        Assert.False(result.Success);
        Assert.Contains("不存在", result.Message);
    }

    [Fact]
    public async Task AdjustStockAsync_ValidAdjustment_UpdatesStockAndLogs()
    {
        await using var db = CreateDbContext();
        await SeedProductsAsync(db);

        var service = new InventoryService(db);
        var result = await service.AdjustStockAsync(
            new InventoryAdjustRequest { ProductId = 1, NewQuantity = 50, Reason = "盘点调整" },
            operatorId: 1);

        Assert.True(result.Success);
        var product = await db.Products.FindAsync(1);
        Assert.Equal(50, product!.Stock);

        var log = await db.InventoryChangeLogs.FirstAsync();
        Assert.Equal("ManualAdjust", log.ChangeType);
        Assert.Equal("盘点调整", log.ChangeReason);
    }

    [Fact]
    public async Task GetChangeLogsAsync_WithDateFilter_ReturnsFilteredLogs()
    {
        await using var db = CreateDbContext();
        await SeedProductsAsync(db);

        // Add logs with different dates
        var service = new InventoryService(db);
        await service.LogChangeAsync(1, 100, 90, "OrderCreate", "订单1", 1, 1);
        await service.LogChangeAsync(1, 90, 80, "OrderCreate", "订单2", 2, 1);

        var result = await service.GetChangeLogsAsync(
            new PagedRequest { PageIndex = 1, PageSize = 20 },
            productId: 1);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.TotalCount);
    }

    [Fact]
    public async Task GetStockInventoryAsync_WithActiveProducts_ReturnsInventoryList()
    {
        await using var db = CreateDbContext();
        await SeedProductsAsync(db);

        var service = new InventoryService(db);
        var result = await service.GetStockInventoryAsync();

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Count);
        Assert.Contains(result.Data, s => s.ProductSku == "SKU001");
        Assert.Contains(result.Data, s => s.ProductSku == "SKU002");
    }

    [Fact]
    public async Task GetChangeStatsAsync_WithDateRange_ReturnsCorrectStats()
    {
        await using var db = CreateDbContext();
        await SeedProductsAsync(db);

        var service = new InventoryService(db);
        await service.LogChangeAsync(1, 100, 80, "OrderCreate", "订单1", 1, 1);
        await service.LogChangeAsync(1, 80, 90, "ManualAdjust", "调整", orderId: null, operatorId: 1);
        await service.LogChangeAsync(2, 50, 60, "ManualAdjust", "补货", orderId: null, operatorId: 1);

        var result = await service.GetChangeStatsAsync(DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1));

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(3, result.Data.TotalChanges);
        Assert.Equal(1, result.Data.OrderCreates);
        Assert.Equal(2, result.Data.ManualAdjusts);
    }

    [Fact]
    public async Task BatchStockTakeAsync_MultipleAdjustments_ReturnsSuccessCount()
    {
        await using var db = CreateDbContext();
        await SeedProductsAsync(db);

        var service = new InventoryService(db);
        var adjustments = new List<InventoryAdjustRequest>
        {
            new() { ProductId = 1, NewQuantity = 100, Reason = "盘点" },
            new() { ProductId = 2, NewQuantity = 200, Reason = "盘点" }
        };

        var result = await service.BatchStockTakeAsync(adjustments, operatorId: 1);

        Assert.True(result.Success);
        Assert.Equal(2, result.Data);
    }

    #region Helpers

    private static ProDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ProDbContext>()
            .UseInMemoryDatabase($"InventoryService_{Guid.NewGuid():N}")
            .Options;
        return new ProDbContext(options);
    }

    private static async Task SeedProductsAsync(ProDbContext db)
    {
        db.Products.AddRange(
            new Product { Id = 1, Name = "产品A", SKU = "SKU001", Stock = 100, Status = ProductStatus.Active, Unit = "个" },
            new Product { Id = 2, Name = "产品B", SKU = "SKU002", Stock = 50, Status = ProductStatus.Active, Unit = "箱" },
            new Product { Id = 3, Name = "已下架产品", SKU = "SKU003", Stock = 30, Status = ProductStatus.Inactive, Unit = "件" }
        );
        await db.SaveChangesAsync();
    }

    #endregion
}
