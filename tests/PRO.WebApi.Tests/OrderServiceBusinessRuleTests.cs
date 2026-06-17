using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PRO.Application.DTOs;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Configuration;
using PRO.Infrastructure.Persistence;
using PRO.Infrastructure.Services;
using Xunit;

namespace PRO.WebApi.Tests;

public class OrderServiceBusinessRuleTests
{
    [Fact]
    public async Task BatchConfirmDrafts_ReportsSuccessAndInvalidCancelledOrder()
    {
        await using var db = CreateDbContext();
        await SeedBaseDataAsync(db);
        db.Orders.AddRange(
            CreateOrder(1, "D2026061000010001", OrderStatus.Draft),
            CreateOrder(2, "D2026061000010002", OrderStatus.Cancelled));
        db.OrderItems.AddRange(
            new OrderItem { OrderId = 1, ProductId = 1, Quantity = 2, UnitPrice = 10, Amount = 20 },
            new OrderItem { OrderId = 2, ProductId = 1, Quantity = 2, UnitPrice = 10, Amount = 20 });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.BatchConfirmDraftsAsync([1, 2], modifiedById: 1);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data!.SuccessCount);
        Assert.Equal(1, result.Data.FailedCount);
        Assert.Contains(result.Data.Items, i => i.EntityNo == "D2026061000010002" && !i.Success && i.Message.Contains("不允许"));
        Assert.Equal(OrderStatus.Pending, (await db.Orders.FindAsync(1))!.Status);
        Assert.Equal(OrderStatus.Cancelled, (await db.Orders.FindAsync(2))!.Status);
        Assert.Equal(8, (await db.Products.FindAsync(1))!.Stock);
    }

    [Fact]
    public async Task BatchAssign_SkipsCompletedOrderWithoutOverwritingStatus()
    {
        await using var db = CreateDbContext();
        await SeedBaseDataAsync(db);
        db.Orders.AddRange(
            CreateOrder(1, "D2026061000010001", OrderStatus.Pending),
            CreateOrder(2, "D2026061000010002", OrderStatus.Completed));
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.BatchAssignAsync([1, 2], deliveryPersonId: 1, assignedById: 1);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data!.SuccessCount);
        Assert.Equal(1, result.Data.FailedCount);
        Assert.Equal(OrderStatus.Assigned, (await db.Orders.FindAsync(1))!.Status);
        Assert.Equal(OrderStatus.Completed, (await db.Orders.FindAsync(2))!.Status);
    }

    [Fact]
    public async Task CreateDraft_UsesConfiguredExpireMinutes_AndFallsBackForInvalidValue()
    {
        await using var db = CreateDbContext();
        await SeedBaseDataAsync(db);
        db.LocalSettings.Add(new LocalSetting { SettingKey = "OrderDraftExpireMinutes", SettingValue = "60", SettingType = "Int" });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var createResult = await service.CreateAsync(CreateOrderRequest(isDraft: true), createdById: 1);
        Assert.True(createResult.Success);

        var draft = await db.Orders.FindAsync(createResult.Data);
        Assert.NotNull(draft!.DraftExpireTime);
        Assert.InRange((draft.DraftExpireTime!.Value - draft.CreatedAt).TotalMinutes, 59, 61);

        var setting = await db.LocalSettings.FirstAsync(s => s.SettingKey == "OrderDraftExpireMinutes");
        setting.SettingValue = "invalid";
        await db.SaveChangesAsync();

        service = CreateService(db);
        var fallbackResult = await service.CreateAsync(CreateOrderRequest(isDraft: true), createdById: 1);
        var fallbackDraft = await db.Orders.FindAsync(fallbackResult.Data);
        Assert.InRange((fallbackDraft!.DraftExpireTime!.Value - fallbackDraft.CreatedAt).TotalMinutes, 29, 31);
    }

    [Fact]
    public async Task CreateAsync_GeneratesUniqueOrderNumbers_AndDeductsStockWithLog()
    {
        await using var db = CreateDbContext();
        await SeedBaseDataAsync(db);

        var service = CreateService(db);
        var first = await service.CreateAsync(CreateOrderRequest(isDraft: false), createdById: 1);
        var second = await service.CreateAsync(CreateOrderRequest(isDraft: false), createdById: 1);

        Assert.True(first.Success);
        Assert.True(second.Success);
        var orders = await db.Orders.OrderBy(o => o.Id).ToListAsync();
        Assert.Equal(2, orders.Select(o => o.OrderNo).Distinct().Count());
        Assert.All(orders, o => Assert.StartsWith("D", o.OrderNo));
        Assert.Equal(8, (await db.Products.FindAsync(1))!.Stock);
        Assert.Equal(2, await db.InventoryChangeLogs.CountAsync(l => l.ProductId == 1 && l.ChangeType == "OrderCreate"));
    }

    private static ProDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ProDbContext>()
            .UseInMemoryDatabase($"OrderServiceRules_{Guid.NewGuid():N}")
            .Options;
        return new ProDbContext(options);
    }

    private static async Task SeedBaseDataAsync(ProDbContext db)
    {
        db.Branches.Add(new Branch { Id = 1, Name = "测试分公司", Code = "0001", Status = EntityStatus.Active });
        db.Departments.Add(new Department { Id = 1, Name = "测试部门", BranchId = 1, Status = EntityStatus.Active });
        db.Roles.Add(new Role { Id = 1, Name = "测试角色", RoleType = RoleType.HeadquartersAdmin });
        db.Employees.Add(new Employee { Id = 1, Name = "测试员工", EmployeeNo = "E001", BranchId = 1, DepartmentId = 1, RoleId = 1, Status = EmployeeStatus.Active });
        db.Customers.Add(new Customer { Id = 1, Name = "张三", CustomerNo = "C001", BranchId = 1, CustomerType = CustomerType.Major, Status = CustomerStatus.Active });
        db.Products.Add(new Product { Id = 1, Name = "测试产品", SKU = "SKU001", Stock = 10, Status = ProductStatus.Active });
        db.DeliveryPersons.Add(new DeliveryPerson { Id = 1, Name = "配送员A", BranchId = 1, Status = DeliveryPersonStatus.Available, CurrentLoad = 0, MaxLoad = 10 });
        await db.SaveChangesAsync();
    }

    private static OrderService CreateService(ProDbContext db)
    {
        var inventory = new InventoryService(db);
        var config = new BusinessConfigService(db);
        var memoryCache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var cacheService = new MemoryCacheService(memoryCache);
        var maskingOptions = Microsoft.Extensions.Options.Options.Create(new DataMaskingOptions());
        var maskingService = new DataMaskingService(maskingOptions);
        return new OrderService(
            db,
            cacheService,
            inventory,
            new AuditTrailService(db, maskingService),
            config,
            new OrderNumberService(db));
    }

    private static CreateOrderRequest CreateOrderRequest(bool isDraft)
    {
        return new CreateOrderRequest
        {
            CustomerId = 1,
            PaymentStatus = PaymentStatus.Unpaid,
            IsDraft = isDraft,
            Items =
            [
                new CreateOrderItemRequest { ProductId = 1, Quantity = 1, UnitPrice = 10 }
            ]
        };
    }

    private static Order CreateOrder(int id, string orderNo, OrderStatus status)
    {
        return new Order
        {
            Id = id,
            OrderNo = orderNo,
            CustomerId = 1,
            BranchId = 1,
            CreatedById = 1,
            Status = status,
            PaymentStatus = PaymentStatus.Unpaid,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
    }
}
