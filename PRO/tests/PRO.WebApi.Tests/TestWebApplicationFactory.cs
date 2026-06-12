using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;

namespace PRO.WebApi.Tests;

/// <summary>
/// 测试 Web 应用工厂 — 使用 InMemory 数据库替代 Npgsql。
/// 
/// 修复 IServiceProvider disposed 问题：
///   根因：JwtOptionsValidator 构造函数依赖 Serilog.ILogger，
///   但 Serilog.ILogger 未注册到 DI 容器。Program.cs 中已修复。
///   工厂使用 ConfigureWebHost 重写模式，确保服务替换在正确时机执行。
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"TestDb_{Guid.NewGuid():N}";
    private bool _seeded;

    public TestWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
    }

    static TestWebApplicationFactory()
    {
        // 静态构造中设置 JWT 环境变量，确保任何时机都可读取
        Environment.SetEnvironmentVariable("Jwt__Key", "test-jwt-key-at-least-32-bytes-long-for-testing");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "PRO-System-Test");
        Environment.SetEnvironmentVariable("Jwt__ExpireMinutes", "1440");
    }

    /// <summary>
    /// 重写 ConfigureWebHost：在 Program.Main 注册完所有服务后，
    /// 将 Npgsql DbContext 替换为 InMemory。
    /// </summary>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((ctx, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "test-jwt-key-at-least-32-bytes-long-for-testing",
                ["Jwt:Issuer"] = "PRO-System-Test",
                ["Jwt:ExpireMinutes"] = "1440",
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=test;Port=5432;Database=pro_test;Username=test;Password=test"
            });
        });

        builder.ConfigureServices(services =>
        {
            // 移除 Npgsql DbContext 注册，替换为 InMemory
            services.RemoveAll<DbContextOptions<ProDbContext>>();
            services.RemoveAll<ProDbContext>();

            services.AddDbContext<ProDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName);
            });
        });
    }

    /// <summary>
    /// 获取测试客户端并种子数据（仅首次）。
    /// WebApplicationFactory.CreateClient() 自带懒初始化，线程安全。
    /// </summary>
    public HttpClient GetTestClient()
    {
        var client = CreateClient();

        // 仅在首次调用时种子数据
        if (!_seeded)
        {
            lock (this)
            {
                if (!_seeded)
                {
                    SeedDatabase();
                    _seeded = true;
                }
            }
        }

        return client;
    }

    /// <summary>
    /// 种子测试数据
    /// </summary>
    public async Task SeedAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProDbContext>();

        if (db.Branches.Any())
            return;

        SeedTestData(db);
        await db.SaveChangesAsync();
    }

    private void SeedDatabase()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProDbContext>();

        if (db.Branches.Any())
            return;

        SeedTestData(db);
        db.SaveChanges();
    }

    private static void SeedTestData(ProDbContext db)
    {
        // 分公司
        db.Branches.AddRange(
            new Branch { Id = 1, Name = "分公司A", Code = "0001", Status = EntityStatus.Active },
            new Branch { Id = 2, Name = "分公司B", Code = "0002", Status = EntityStatus.Active }
        );

        // 部门
        db.Departments.Add(new Department
        {
            Id = 1, Name = "总部", BranchId = 1, Status = EntityStatus.Active
        });

        // 角色
        db.Roles.AddRange(
            new Role { Id = 1, Name = "总部管理员", RoleType = RoleType.HeadquartersAdmin },
            new Role { Id = 2, Name = "分公司管理员", RoleType = RoleType.BranchAdmin },
            new Role { Id = 3, Name = "普通员工", RoleType = RoleType.Employee }
        );

        // 权限
        var permissions = new List<Permission>
        {
            new() { Id = 1, Code = "Customer.View", Name = "查看客户" },
            new() { Id = 2, Code = "Customer.Create", Name = "创建客户" },
            new() { Id = 3, Code = "Customer.Edit", Name = "编辑客户" },
            new() { Id = 4, Code = "Customer.Delete", Name = "删除客户" },
            new() { Id = 5, Code = "Customer.Merge", Name = "合并客户" },
            new() { Id = 6, Code = "Order.View", Name = "查看订单" },
            new() { Id = 7, Code = "Order.Create", Name = "创建订单" },
            new() { Id = 8, Code = "Order.Edit", Name = "编辑订单" },
            new() { Id = 9, Code = "Order.Delete", Name = "删除订单" },
            new() { Id = 10, Code = "Delivery.View", Name = "查看配送员" },
            new() { Id = 11, Code = "Delivery.Edit", Name = "编辑配送员" },
            new() { Id = 12, Code = "Delivery.AutoAssign", Name = "自动分配" },
        };
        db.Permissions.AddRange(permissions);

        // 员工
        db.Employees.AddRange(
            new Employee { Id = 1, EmployeeNo = "admin", Name = "总部管理员", PasswordHash = HashPassword("admin123"), BranchId = 1, RoleId = 1, DepartmentId = 1, Status = EmployeeStatus.Active },
            new Employee { Id = 2, EmployeeNo = "branch_a_user", Name = "分公司A用户", PasswordHash = HashPassword("password123"), BranchId = 1, RoleId = 2, DepartmentId = 1, Status = EmployeeStatus.Active },
            new Employee { Id = 3, EmployeeNo = "branch_b_user", Name = "分公司B用户", PasswordHash = HashPassword("password123"), BranchId = 2, RoleId = 2, DepartmentId = 1, Status = EmployeeStatus.Active },
            new Employee { Id = 4, EmployeeNo = "normal_user", Name = "普通用户", PasswordHash = HashPassword("password123"), BranchId = 1, RoleId = 3, DepartmentId = 1, Status = EmployeeStatus.Active },
            new Employee { Id = 5, EmployeeNo = "viewer_user", Name = "查看者", PasswordHash = HashPassword("password123"), BranchId = 1, RoleId = 3, DepartmentId = 1, Status = EmployeeStatus.Active }
        );

        // 角色权限
        var rolePermissions = new List<RolePermission>();
        foreach (var perm in permissions)
        {
            rolePermissions.Add(new RolePermission { RoleId = 1, PermissionId = perm.Id, IsAllowed = true });
            rolePermissions.Add(new RolePermission { RoleId = 2, PermissionId = perm.Id, IsAllowed = true });
        }
        rolePermissions.Add(new RolePermission { RoleId = 3, PermissionId = 1, IsAllowed = true });
        rolePermissions.Add(new RolePermission { RoleId = 3, PermissionId = 6, IsAllowed = true });
        rolePermissions.Add(new RolePermission { RoleId = 3, PermissionId = 10, IsAllowed = true });
        db.RolePermissions.AddRange(rolePermissions);

        // 测试客户
        db.Customers.AddRange(
            new Customer { Id = 1, Name = "分公司A客户1", CustomerNo = "K2026060900010001", BranchId = 1, CustomerType = CustomerType.Major, Status = CustomerStatus.Active, CreatedAt = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc) },
            new Customer { Id = 2, Name = "分公司A客户2", CustomerNo = "K2026060900010002", BranchId = 1, CustomerType = CustomerType.Major, Status = CustomerStatus.Active, CreatedAt = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc) },
            new Customer { Id = 999, Name = "分公司B客户1", CustomerNo = "K2026060900020001", BranchId = 2, CustomerType = CustomerType.Major, Status = CustomerStatus.Active, CreatedAt = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc) }
        );

        // 测试订单
        db.Orders.AddRange(
            new Order { Id = 1, OrderNo = "D2026060900010001", CustomerId = 1, BranchId = 1, CreatedById = 2, Status = OrderStatus.Pending, CreatedAt = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc) },
            new Order { Id = 9999, OrderNo = "D2026060900020001", CustomerId = 999, BranchId = 2, CreatedById = 3, Status = OrderStatus.Pending, CreatedAt = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc) }
        );

        // 测试配送员
        db.DeliveryPersons.AddRange(
            new DeliveryPerson { Id = 1, Name = "分公司A配送员1", BranchId = 1, Status = DeliveryPersonStatus.Available, MaxLoad = 10 },
            new DeliveryPerson { Id = 999, Name = "分公司B配送员1", BranchId = 2, Status = DeliveryPersonStatus.Available, MaxLoad = 10 }
        );
    }

    private static string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 4);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }
}
