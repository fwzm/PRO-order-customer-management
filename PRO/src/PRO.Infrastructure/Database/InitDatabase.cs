using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using PRO.Application.Interfaces;
using Microsoft.Extensions.Hosting;

namespace PRO.Infrastructure.Database;

public static class DatabaseInitializer
{
    /// <summary>
    /// 数据库初始化 — 生产环境使用 MigrateAsync，开发/测试环境允许快速初始化
    /// </summary>
    public static async Task InitializeAsync(
        ProDbContext context,
        IEncryptionService encryptionService,
        IHostEnvironment? environment = null)
    {
        var canConnect = await context.Database.CanConnectAsync();
        if (!canConnect)
        {
            Serilog.Log.Warning("无法连接到 PostgreSQL 数据库，请检查连接配置");
            return;
        }

        var isProduction = environment?.IsProduction() ?? false;

        if (isProduction)
        {
            Serilog.Log.Information("生产环境：正在应用数据库迁移...");
            var pendingMigrations = (await context.Database.GetPendingMigrationsAsync()).ToList();
            if (pendingMigrations.Count > 0)
            {
                Serilog.Log.Information("发现 {Count} 个待应用的迁移: {Migrations}",
                    pendingMigrations.Count, string.Join(", ", pendingMigrations));
                await context.Database.MigrateAsync();
                Serilog.Log.Information("数据库迁移已成功应用");
            }
            else
            {
                Serilog.Log.Information("数据库已是最新版本，无需迁移");
            }
        }
        else
        {
            var hasMigrationHistory = await context.Database.CanConnectAsync()
                && await HasMigrationHistoryTableAsync(context);

            if (hasMigrationHistory)
            {
                Serilog.Log.Information("开发环境：检测到迁移历史，正在应用迁移...");
                var pendingMigrations = (await context.Database.GetPendingMigrationsAsync()).ToList();
                if (pendingMigrations.Count > 0)
                {
                    Serilog.Log.Information("发现 {Count} 个待应用的迁移: {Migrations}",
                        pendingMigrations.Count, string.Join(", ", pendingMigrations));
                }
                await context.Database.MigrateAsync();
            }
            else
            {
                Serilog.Log.Information("开发环境：首次初始化，使用 EnsureCreated 快速创建表结构...");
                await context.Database.EnsureCreatedAsync();
                Serilog.Log.Warning("开发环境使用 EnsureCreated 初始化，生产环境请使用 Migration");
            }
        }

        if (await context.Roles.AnyAsync())
        {
            await EnsureDefaultPermissionsAsync(context);
            await EnsureDefaultLocalSettingsAsync(context);
            return;
        }

        var roles = new List<Role>
        {
            new() { Id = 1, Name = "总部管理员", Code = "HEADQUARTERS_ADMIN", RoleType = RoleType.HeadquartersAdmin, Description = "拥有系统最高权限", IsSystem = true },
            new() { Id = 2, Name = "分公司管理员", Code = "BRANCH_ADMIN", RoleType = RoleType.BranchAdmin, Description = "管理本公司数据", IsSystem = true },
            new() { Id = 3, Name = "区域管理员", Code = "REGION_ADMIN", RoleType = RoleType.RegionAdmin, Description = "管理管辖区域数据", IsSystem = true },
            new() { Id = 4, Name = "普通员工", Code = "EMPLOYEE", RoleType = RoleType.Employee, Description = "普通员工权限", IsSystem = true }
        };
        context.Roles.AddRange(roles);

        var permissions = CreateDefaultPermissions();
        context.Permissions.AddRange(permissions);

        var allPermissionIds = permissions.Select(p => p.Id).ToList();
        var headquartersPermissions = allPermissionIds.Select(pid => new RolePermission
        {
            RoleId = 1,
            PermissionId = pid,
            IsAllowed = true
        }).ToList();
        context.RolePermissions.AddRange(headquartersPermissions);

        var branchAdminPermissions = CreateBranchAdminPermissions(permissions, 2);
        context.RolePermissions.AddRange(branchAdminPermissions);

        var branches = new List<Branch>
        {
            new() { Id = 1, Name = "总部", Code = "0001", Status = EntityStatus.Active },
            new() { Id = 2, Name = "北京分公司", Code = "0002", Status = EntityStatus.Active },
            new() { Id = 3, Name = "上海分公司", Code = "0003", Status = EntityStatus.Active },
            new() { Id = 4, Name = "广州分公司", Code = "0004", Status = EntityStatus.Active },
            new() { Id = 5, Name = "深圳分公司", Code = "0005", Status = EntityStatus.Active }
        };
        context.Branches.AddRange(branches);

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

        var localSettings = new List<LocalSetting>
        {
            new() { SettingKey = "CloseBehavior", SettingValue = "0", SettingType = "Int" },
            new() { SettingKey = "EnableNotification", SettingValue = "true", SettingType = "Bool" },
            new() { SettingKey = "EnableSound", SettingValue = "true", SettingType = "Bool" },
            new() { SettingKey = "AutoSyncOnStartup", SettingValue = "true", SettingType = "Bool" },
            new() { SettingKey = "SyncIntervalMinutes", SettingValue = "30", SettingType = "Int" },
            new() { SettingKey = "OrderDraftExpireMinutes", SettingValue = "30", SettingType = "Int" },
            new() { SettingKey = "DraftExpireMinutes", SettingValue = "30", SettingType = "Int" }
        };
        context.LocalSettings.AddRange(localSettings);

        var categories = new List<ProductCategory>
        {
            new() { Id = 1, Name = "食品饮料", SortOrder = 1 },
            new() { Id = 2, Name = "日用百货", SortOrder = 2 },
            new() { Id = 3, Name = "电子产品", SortOrder = 3 }
        };
        context.ProductCategories.AddRange(categories);

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
        var id = 1;

        foreach (var seed in GetDefaultPermissionSeeds())
        {
            permissions.Add(new Permission
            {
                Id = id,
                Code = seed.Code,
                Name = seed.Name,
                Module = seed.Module,
                PermissionType = seed.PermissionType,
                SortOrder = id
            });
            id++;
        }

        return permissions;
    }

    private static IEnumerable<PermissionSeed> GetDefaultPermissionSeeds()
    {
        var modules = new[] { "System", "Customer", "Order", "Product", "Delivery", "Settlement", "WorkPlan" };
        var types = new[] { "View", "Create", "Edit", "Delete", "Export" };

        foreach (var module in modules)
        {
            foreach (var type in types)
            {
                yield return new PermissionSeed(
                    $"{module}.{type}",
                    $"{GetModuleName(module)}{GetTypeName(type)}",
                    module,
                    GetPermissionType(type));
            }
        }

        yield return new PermissionSeed("Order.AutoAssign", "订单自动分配", "Order", PermissionType.Edit);
        yield return new PermissionSeed("Order.BatchAssign", "订单批量分配", "Order", PermissionType.Edit);
        yield return new PermissionSeed("Order.BatchStatusChange", "订单批量改状态", "Order", PermissionType.Edit);
        yield return new PermissionSeed("Order.Cancel", "订单取消", "Order", PermissionType.Edit);
        yield return new PermissionSeed("Customer.Merge", "客户合并", "Customer", PermissionType.Edit);
        yield return new PermissionSeed("Delivery.AutoAssign", "配送自动分配", "Delivery", PermissionType.Edit);
        yield return new PermissionSeed("Delivery.ManualAssign", "配送手动分配", "Delivery", PermissionType.Edit);
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

    private static List<RolePermission> CreateBranchAdminPermissions(IEnumerable<Permission> modulePermissions, int roleId)
    {
        var permissions = new List<RolePermission>();

        foreach (var perm in modulePermissions.Where(IsBranchAdminDefaultPermission))
        {
            permissions.Add(new RolePermission
            {
                RoleId = roleId,
                PermissionId = perm.Id,
                IsAllowed = true
            });
        }

        return permissions;
    }

    private static async Task EnsureDefaultPermissionsAsync(ProDbContext context)
    {
        var seeds = GetDefaultPermissionSeeds().ToList();
        var existingCodes = (await context.Permissions
                .AsNoTracking()
                .Select(p => p.Code)
                .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var nextSortOrder = await context.Permissions.AnyAsync()
            ? await context.Permissions.MaxAsync(p => p.SortOrder) + 1
            : 1;

        var missingPermissions = seeds
            .Where(seed => !existingCodes.Contains(seed.Code))
            .Select(seed => new Permission
            {
                Code = seed.Code,
                Name = seed.Name,
                Module = seed.Module,
                PermissionType = seed.PermissionType,
                SortOrder = nextSortOrder++
            })
            .ToList();

        if (missingPermissions.Count > 0)
        {
            context.Permissions.AddRange(missingPermissions);
            await context.SaveChangesAsync();
        }

        await EnsureSystemRolePermissionsAsync(context);
    }

    private static async Task EnsureSystemRolePermissionsAsync(ProDbContext context)
    {
        var systemRoles = await context.Roles
            .AsNoTracking()
            .Where(r => r.Code == "HEADQUARTERS_ADMIN" || r.Code == "BRANCH_ADMIN")
            .Select(r => new { r.Id, r.Code })
            .ToListAsync();

        if (systemRoles.Count == 0)
            return;

        var permissions = await context.Permissions
            .AsNoTracking()
            .ToListAsync();

        var existingRolePermissions = (await context.RolePermissions
                .AsNoTracking()
                .Select(rp => new { rp.RoleId, rp.PermissionId })
                .ToListAsync())
            .Select(rp => $"{rp.RoleId}:{rp.PermissionId}")
            .ToHashSet(StringComparer.Ordinal);

        var rolePermissionsToAdd = new List<RolePermission>();

        foreach (var role in systemRoles)
        {
            var allowedPermissions = role.Code == "HEADQUARTERS_ADMIN"
                ? permissions
                : permissions.Where(IsBranchAdminDefaultPermission);

            foreach (var permission in allowedPermissions)
            {
                var key = $"{role.Id}:{permission.Id}";
                if (existingRolePermissions.Contains(key))
                    continue;

                rolePermissionsToAdd.Add(new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permission.Id,
                    IsAllowed = true
                });
                existingRolePermissions.Add(key);
            }
        }

        if (rolePermissionsToAdd.Count > 0)
        {
            context.RolePermissions.AddRange(rolePermissionsToAdd);
            await context.SaveChangesAsync();
        }
    }

    private static bool IsBranchAdminDefaultPermission(Permission permission)
    {
        var modules = new[] { "Customer", "Order", "Product", "Delivery", "Settlement", "WorkPlan" };
        return modules.Contains(permission.Module)
            && !(permission.Module == "Settlement" && permission.Code.EndsWith(".Delete", StringComparison.OrdinalIgnoreCase));
    }

    private sealed record PermissionSeed(string Code, string Name, string Module, PermissionType PermissionType);

    private static async Task EnsureDefaultLocalSettingsAsync(ProDbContext context)
    {
        var defaults = new Dictionary<string, (string Value, string Type)>
        {
            // 分页
            ["PageSize"] = ("50", "Int"),
            ["MaxPageSize"] = ("500", "Int"),
            // 备份
            ["AutoBackupInterval"] = ("30", "Int"),
            // 草稿
            ["OrderDraftExpireMinutes"] = ("30", "Int"),
            ["DraftExpireMinutes"] = ("30", "Int"),
            // 日志
            ["LogRetentionDays"] = ("90", "Int"),
            // 关闭行为
            ["CloseBehavior"] = ("0", "Int"),
            // 安全
            ["MaxLoginFailures"] = ("5", "Int"),
            ["LockoutMinutes"] = ("30", "Int"),
            ["PasswordExpireDays"] = ("90", "Int"),
            // 自动保存
            ["EnableAutoSave"] = ("true", "Bool"),
            ["AutoSaveInterval"] = ("30", "Int"),
            // 缓存
            ["DefaultCacheExpirationMinutes"] = ("15", "Int"),
            ["ConfigCacheExpirationMinutes"] = ("60", "Int"),
            // 库存
            ["StockWarningThreshold"] = ("20", "Int"),
            ["StockCriticalThreshold"] = ("5", "Int"),
            ["AllowNegativeStock"] = ("false", "Bool"),
            // 导出
            ["MaxExportRowCount"] = ("5000", "Int"),
            ["ExportFileRetentionDays"] = ("30", "Int"),
            // 客户
            ["SilentCustomerDays"] = ("30", "Int"),
            ["ChurnRiskDays"] = ("60", "Int"),
            // 通知
            ["EnableNotification"] = ("true", "Bool"),
            ["EnableSound"] = ("true", "Bool"),
            // 同步
            ["AutoSyncOnStartup"] = ("true", "Bool"),
            ["SyncIntervalMinutes"] = ("30", "Int"),
        };

        var existingKeys = await context.LocalSettings
            .Where(s => defaults.Keys.Contains(s.SettingKey))
            .Select(s => s.SettingKey)
            .ToListAsync();

        foreach (var (key, setting) in defaults)
        {
            if (existingKeys.Contains(key))
                continue;

            context.LocalSettings.Add(new LocalSetting
            {
                SettingKey = key,
                SettingValue = setting.Value,
                SettingType = setting.Type,
                UpdatedAt = DateTime.Now
            });
        }

        await context.SaveChangesAsync();
    }

    private static async Task<bool> HasMigrationHistoryTableAsync(ProDbContext context)
    {
        try
        {
            var migrations = await context.Database.GetAppliedMigrationsAsync();
            return migrations.Any();
        }
        catch
        {
            return false;
        }
    }
}
