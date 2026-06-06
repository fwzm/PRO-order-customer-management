using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using PRO.Application.Interfaces;

namespace PRO.Infrastructure.Database;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(ProDbContext context, IEncryptionService encryptionService)
    {
        var canConnect = await context.Database.CanConnectAsync();
        if (!canConnect)
        {
            Serilog.Log.Warning("无法连接到 PostgreSQL 数据库，请检查连接配置");
            return;
        }

        // 自动创建所有表（如果不存在）
        Serilog.Log.Information("确保数据库表结构已创建...");
        await context.Database.EnsureCreatedAsync();

        // 检查是否已有数据
        if (await context.Roles.AnyAsync())
            return;

        // 创建初始角色
        var roles = new List<Role>
        {
            new() { Id = 1, Name = "总部管理员", Code = "HEADQUARTERS_ADMIN", RoleType = RoleType.HeadquartersAdmin, Description = "拥有系统最高权限", IsSystem = true },
            new() { Id = 2, Name = "分公司管理员", Code = "BRANCH_ADMIN", RoleType = RoleType.BranchAdmin, Description = "管理本公司数据", IsSystem = true },
            new() { Id = 3, Name = "区域管理员", Code = "REGION_ADMIN", RoleType = RoleType.RegionAdmin, Description = "管理管辖区域数据", IsSystem = true },
            new() { Id = 4, Name = "普通员工", Code = "EMPLOYEE", RoleType = RoleType.Employee, Description = "普通员工权限", IsSystem = true }
        };
        context.Roles.AddRange(roles);

        // 创建初始权限
        var permissions = CreateDefaultPermissions();
        context.Permissions.AddRange(permissions);

        // 创建总部管理员角色权限（全部权限）
        var allPermissionIds = permissions.Select(p => p.Id).ToList();
        var headquartersPermissions = allPermissionIds.Select(pid => new RolePermission
        {
            RoleId = 1,
            PermissionId = pid,
            IsAllowed = true
        }).ToList();
        context.RolePermissions.AddRange(headquartersPermissions);

        // 创建分公司管理员默认权限
        var branchAdminPermissions = CreateBranchAdminPermissions(context, 2);
        context.RolePermissions.AddRange(branchAdminPermissions);

        // 创建初始分公司
        var branches = new List<Branch>
        {
            new() { Id = 1, Name = "总部", Code = "0001", Status = EntityStatus.Active },
            new() { Id = 2, Name = "北京分公司", Code = "0002", Status = EntityStatus.Active },
            new() { Id = 3, Name = "上海分公司", Code = "0003", Status = EntityStatus.Active },
            new() { Id = 4, Name = "广州分公司", Code = "0004", Status = EntityStatus.Active },
            new() { Id = 5, Name = "深圳分公司", Code = "0005", Status = EntityStatus.Active }
        };
        context.Branches.AddRange(branches);

        // 创建初始部门
        var departments = new List<Department>
        {
            new() { Id = 1, Name = "总经办", BranchId = 1, SortOrder = 1, Status = EntityStatus.Active },
            new() { Id = 2, Name = "销售部", BranchId = 2, SortOrder = 1, Status = EntityStatus.Active },
            new() { Id = 3, Name = "客服部", BranchId = 2, SortOrder = 2, Status = EntityStatus.Active },
            new() { Id = 4, Name = "配送部", BranchId = 2, SortOrder = 3, Status = EntityStatus.Active },
            new() { Id = 5, Name = "销售部", BranchId = 3, SortOrder = 1, Status = EntityStatus.Active },
            new() { Id = 6, Name = "客服部", BranchId = 3, SortOrder = 2, Status = EntityStatus.Active },
            new() { Id = 7, Name = "配送部", BranchId = 3, SortOrder = 3, Status = EntityStatus.Active }
        };
        context.Departments.AddRange(departments);

        // 创建初始员工（默认密码：admin123）
        var defaultPasswordHash = encryptionService.HashPassword("admin123");
        var employees = new List<Employee>
        {
            new() { Id = 1, Name = "系统管理员", EmployeeNo = "ADMIN", PasswordHash = defaultPasswordHash,
                    DepartmentId = 1, BranchId = 1, RoleId = 1, Status = EmployeeStatus.Active },
            new() { Id = 2, Name = "北京管理员", EmployeeNo = "BJ001", PasswordHash = defaultPasswordHash,
                    DepartmentId = 2, BranchId = 2, RoleId = 2, Status = EmployeeStatus.Active },
            new() { Id = 3, Name = "张三", EmployeeNo = "BJ002", PasswordHash = defaultPasswordHash,
                    DepartmentId = 2, BranchId = 2, RoleId = 4, Status = EmployeeStatus.Active },
            new() { Id = 4, Name = "李四", EmployeeNo = "SH001", PasswordHash = defaultPasswordHash,
                    DepartmentId = 5, BranchId = 3, RoleId = 2, Status = EmployeeStatus.Active },
            new() { Id = 5, Name = "王五", EmployeeNo = "SH002", PasswordHash = defaultPasswordHash,
                    DepartmentId = 5, BranchId = 3, RoleId = 4, Status = EmployeeStatus.Active }
        };
        context.Employees.AddRange(employees);

        // 创建默认本地设置
        var localSettings = new List<LocalSetting>
        {
            new() { SettingKey = "CloseBehavior", SettingValue = "0", SettingType = "Int" },
            new() { SettingKey = "EnableNotification", SettingValue = "true", SettingType = "Bool" },
            new() { SettingKey = "EnableSound", SettingValue = "true", SettingType = "Bool" },
            new() { SettingKey = "AutoSyncOnStartup", SettingValue = "true", SettingType = "Bool" },
            new() { SettingKey = "SyncIntervalMinutes", SettingValue = "30", SettingType = "Int" }
        };
        context.LocalSettings.AddRange(localSettings);

        // 创建产品分类
        var categories = new List<ProductCategory>
        {
            new() { Id = 1, Name = "食品饮料", SortOrder = 1 },
            new() { Id = 2, Name = "日用百货", SortOrder = 2 },
            new() { Id = 3, Name = "电子产品", SortOrder = 3 }
        };
        context.ProductCategories.AddRange(categories);

        // 创建示例产品
        var products = new List<Product>
        {
            new() { SKU = "SKU001", Name = "矿泉水", Specification = "500ml/瓶", AverageSalePrice = 2.00m, ReferencePrice = 2.50m, Stock = 1000, Unit = "瓶", CategoryId = 1, Status = ProductStatus.Active },
            new() { SKU = "SKU002", Name = "可口可乐", Specification = "330ml/罐", AverageSalePrice = 3.00m, ReferencePrice = 3.50m, Stock = 500, Unit = "罐", CategoryId = 1, Status = ProductStatus.Active },
            new() { SKU = "SKU003", Name = "抽纸", Specification = "3层/包", AverageSalePrice = 5.00m, ReferencePrice = 6.00m, Stock = 300, Unit = "包", CategoryId = 2, Status = ProductStatus.Active },
            new() { SKU = "SKU004", Name = "充电宝", Specification = "10000mAh", AverageSalePrice = 89.00m, ReferencePrice = 99.00m, Stock = 50, Unit = "个", CategoryId = 3, Status = ProductStatus.Active }
        };
        context.Products.AddRange(products);

        await context.SaveChangesAsync();
    }

    private static List<Permission> CreateDefaultPermissions()
    {
        var permissions = new List<Permission>();
        var modules = new[] { "System", "Customer", "Order", "Product", "Delivery", "Settlement", "WorkPlan" };
        var types = new[] { "View", "Create", "Edit", "Delete", "Export" };
        var id = 1;

        foreach (var module in modules)
        {
            foreach (var type in types)
            {
                permissions.Add(new Permission
                {
                    Id = id++,
                    Code = $"{module}.{type}",
                    Name = $"{GetModuleName(module)}{GetTypeName(type)}",
                    Module = module,
                    PermissionType = GetPermissionType(type),
                    SortOrder = id
                });
            }
        }

        return permissions;
    }

    private static string GetModuleName(string module) => module switch
    {
        "System" => "系统",
        "Customer" => "客户",
        "Order" => "订单",
        "Product" => "产品",
        "Delivery" => "配送",
        "Settlement" => "结算",
        "WorkPlan" => "计划",
        _ => module
    };

    private static string GetTypeName(string type) => type switch
    {
        "View" => "查看",
        "Create" => "新增",
        "Edit" => "编辑",
        "Delete" => "删除",
        "Export" => "导出",
        _ => type
    };

    private static PermissionType GetPermissionType(string type) => type switch
    {
        "View" => PermissionType.View,
        "Create" => PermissionType.Create,
        "Edit" => PermissionType.Edit,
        "Delete" => PermissionType.Delete,
        "Export" => PermissionType.Export,
        _ => PermissionType.View
    };

    private static List<RolePermission> CreateBranchAdminPermissions(ProDbContext context, int roleId)
    {
        var permissions = new List<RolePermission>();
        var modules = new[] { "Customer", "Order", "Product", "Delivery", "Settlement", "WorkPlan" };

        foreach (var module in modules)
        {
            var modulePermissions = context.Permissions.Where(p => p.Module == module).ToList();
            foreach (var perm in modulePermissions)
            {
                if (module == "Settlement" && perm.Code.EndsWith(".Delete"))
                    continue;

                permissions.Add(new RolePermission
                {
                    RoleId = roleId,
                    PermissionId = perm.Id,
                    IsAllowed = true
                });
            }
        }

        return permissions;
    }
}
