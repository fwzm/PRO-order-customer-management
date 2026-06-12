using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Configuration;
using PRO.Infrastructure.Persistence;
using PRO.Infrastructure.Services;
using Xunit;

namespace PRO.WebApi.Tests;

/// <summary>
/// OrderService 核心业务测试 — 使用 InMemory 数据库
/// </summary>
public class OrderServiceTests : IDisposable
{
    private readonly ProDbContext _dbContext;
    private readonly OrderService _orderService;

    public OrderServiceTests()
    {
        var options = new DbContextOptionsBuilder<ProDbContext>()
            .UseInMemoryDatabase(databaseName: $"OrderTests_{Guid.NewGuid()}")
            .Options;
        _dbContext = new ProDbContext(options);

        var cache = new MemoryCacheService(new MemoryCache(new MemoryCacheOptions()));
        var inventoryService = new InventoryService(_dbContext);
        var auditTrail = new Mock<IAuditTrailService>();
        var configService = new BusinessConfigService(_dbContext);
        var orderNumberService = new OrderNumberService(_dbContext);

        _orderService = new OrderService(_dbContext, cache, inventoryService, auditTrail.Object, configService, orderNumberService);

        SeedData();
    }

    private void SeedData()
    {
        var branch = new Branch { Id = 1, Name = "北京分公司", Code = "0002", Status = EntityStatus.Active };
        _dbContext.Branches.Add(branch);

        var customer = new Customer
        {
            Id = 1, Name = "测试客户", CustomerNo = "K2026010100020001",
            CustomerType = CustomerType.Major, BranchId = 1,
            Status = CustomerStatus.Active
        };
        _dbContext.Customers.Add(customer);

        _dbContext.Orders.AddRange(
            new Order
            {
                Id = 1, OrderNo = "D2026010100020001", CustomerId = 1, BranchId = 1,
                Status = OrderStatus.Pending, TotalAmount = 100m, CreatedAt = DateTime.Now
            },
            new Order
            {
                Id = 2, OrderNo = "D2026010100020002", CustomerId = 1, BranchId = 1,
                Status = OrderStatus.Draft, TotalAmount = 200m, CreatedAt = DateTime.Now
            },
            new Order
            {
                Id = 3, OrderNo = "D2026010100020003", CustomerId = 1, BranchId = 1,
                Status = OrderStatus.Completed, TotalAmount = 300m, CreatedAt = DateTime.Now
            }
        );
        _dbContext.SaveChanges();
    }

    // ═══ 状态变更测试（核心业务逻辑）═══

    [Fact]
    public async Task UpdateStatusAsync_ValidTransition_Succeeds()
    {
        var request = new UpdateOrderStatusRequest
        {
            OrderId = 1,
            NewStatus = OrderStatus.Assigned,
            Reason = "分配配送员"
        };

        var result = await _orderService.UpdateStatusAsync(request, modifiedById: 1);
        result.Success.Should().BeTrue();

        var order = await _dbContext.Orders.FindAsync(1);
        order!.Status.Should().Be(OrderStatus.Assigned);
    }

    [Fact]
    public async Task UpdateStatusAsync_InvalidTransition_Fails()
    {
        var request = new UpdateOrderStatusRequest
        {
            OrderId = 3,
            NewStatus = OrderStatus.Pending,
            Reason = "试图回退"
        };

        var result = await _orderService.UpdateStatusAsync(request, modifiedById: 1);
        result.Success.Should().BeFalse("已完成的订单不能回退到待分配");
    }

    [Fact]
    public async Task UpdateStatusAsync_CancelWithReason_Succeeds()
    {
        var request = new UpdateOrderStatusRequest
        {
            OrderId = 1,
            NewStatus = OrderStatus.Cancelled,
            Reason = "客户取消订单"
        };

        var result = await _orderService.UpdateStatusAsync(request, modifiedById: 1);
        result.Success.Should().BeTrue();

        var order = await _dbContext.Orders.FindAsync(1);
        order!.Status.Should().Be(OrderStatus.Cancelled);
        order.CancelReason.Should().Be("客户取消订单");
    }

    // ═══ 草稿确认测试 ═══

    [Fact]
    public async Task ConfirmDraftAsync_DraftOrder_Succeeds()
    {
        var result = await _orderService.ConfirmDraftAsync(2, modifiedById: 1);
        result.Success.Should().BeTrue();

        var order = await _dbContext.Orders.FindAsync(2);
        order!.Status.Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public async Task ConfirmDraftAsync_NonDraftOrder_Fails()
    {
        var result = await _orderService.ConfirmDraftAsync(3, modifiedById: 1);
        result.Success.Should().BeFalse("非草稿订单不能确认草稿");
    }

    // ═══ 订单删除测试 ═══

    [Fact]
    public async Task DeleteAsync_DraftOrder_SoftDeletes()
    {
        var result = await _orderService.DeleteAsync(2);
        result.Success.Should().BeTrue();

        var order = await _dbContext.Orders.FindAsync(2);
        order!.Status.Should().Be(OrderStatus.Cancelled, "删除应该将订单状态改为已取消");
    }

    [Fact]
    public async Task DeleteAsync_CompletedOrder_Fails()
    {
        var order = await _dbContext.Orders.FindAsync(3);
        order!.SettlementId = 1;
        await _dbContext.SaveChangesAsync();

        var result = await _orderService.DeleteAsync(3);
        result.Success.Should().BeFalse("已结算的订单不能删除");
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
