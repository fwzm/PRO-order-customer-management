using Microsoft.EntityFrameworkCore;
using PRO.Domain.Entities;

namespace PRO.Infrastructure.Persistence;

public class ProDbContext : DbContext
{
    public ProDbContext(DbContextOptions<ProDbContext> options) : base(options)
    {
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ConvertLocalDateTimesToUtc();
        return await base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ConvertLocalDateTimesToUtc();
        return base.SaveChanges();
    }

    private void ConvertLocalDateTimesToUtc()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
            {
                foreach (var prop in entry.Properties)
                {
                    if (prop.CurrentValue is DateTime dt)
                    {
                        if (dt.Kind == DateTimeKind.Local)
                        {
                            // 正确地将本地时间转换为 UTC
                            prop.CurrentValue = dt.ToUniversalTime();
                        }
                        else if (dt.Kind == DateTimeKind.Unspecified)
                        {
                            // 对于未指定的时间，假设是 UTC 并明确标记
                            prop.CurrentValue = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                        }
                        // 如果已经是 UTC，则不做处理
                    }
                    else if (prop.CurrentValue is DateTime?)
                    {
                        // 处理可空 DateTime
                        var nullableDt = (DateTime?)prop.CurrentValue;
                        if (nullableDt.HasValue)
                        {
                            var dtValue = nullableDt.Value;
                            if (dtValue.Kind == DateTimeKind.Local)
                            {
                                prop.CurrentValue = dtValue.ToUniversalTime();
                            }
                            else if (dtValue.Kind == DateTimeKind.Unspecified)
                            {
                                prop.CurrentValue = DateTime.SpecifyKind(dtValue, DateTimeKind.Utc);
                            }
                        }
                    }
                }
            }
        }
    }

    // 组织架构
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    // 业务数据
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderModificationRecord> OrderModificationRecords => Set<OrderModificationRecord>();
    public DbSet<DeliveryPerson> DeliveryPersons => Set<DeliveryPerson>();
    public DbSet<Settlement> Settlements => Set<Settlement>();

    // 工作计划
    public DbSet<WorkSchedule> WorkSchedules => Set<WorkSchedule>();
    public DbSet<WorkPlan> WorkPlans => Set<WorkPlan>();
    public DbSet<PlanDraft> PlanDrafts => Set<PlanDraft>();

    // 客户区域数据
    public DbSet<BusinessDistrict> BusinessDistricts => Set<BusinessDistrict>();

    // 系统
    public DbSet<OperationLog> OperationLogs => Set<OperationLog>();
    public DbSet<SyncRecord> SyncRecords => Set<SyncRecord>();
    public DbSet<SyncConfig> SyncConfigs => Set<SyncConfig>();
    public DbSet<LocalSetting> LocalSettings => Set<LocalSetting>();
    public DbSet<WeChatConfig> WeChatConfigs => Set<WeChatConfig>();
    public DbSet<Webhook> Webhooks => Set<Webhook>();
    public DbSet<BackupRecord> BackupRecords => Set<BackupRecord>();
    public DbSet<WeChatSyncLog> WeChatSyncLogs => Set<WeChatSyncLog>();
    public DbSet<WeChatCustomer> WeChatCustomers => Set<WeChatCustomer>();
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<VisitRecord> VisitRecords => Set<VisitRecord>();
    public DbSet<CustomerPrice> CustomerPrices => Set<CustomerPrice>();
    public DbSet<VolumePrice> VolumePrices => Set<VolumePrice>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<InventoryCheck> InventoryChecks => Set<InventoryCheck>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<WeChatVisitRecord> WeChatVisitRecords => Set<WeChatVisitRecord>();
    public DbSet<CustomerTag> CustomerTags => Set<CustomerTag>();
    public DbSet<AppVersion> AppVersions => Set<AppVersion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 全局配置：所有枚举类型存储为整数
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                var propType = property.ClrType;

                // 枚举 → 整数
                var enumType = propType.IsEnum ? propType : Nullable.GetUnderlyingType(propType);
                if (enumType?.IsEnum == true)
                {
                    var converterType = typeof(Microsoft.EntityFrameworkCore.Storage.ValueConversion.EnumToNumberConverter<,>)
                        .MakeGenericType(enumType, typeof(int));
                    var converter = (Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter)
                        Activator.CreateInstance(converterType, new object?[] { null })!;
                    property.SetValueConverter(converter);
                }

                // DateTime → 无时区（解决 Kind=Local 报错）
                if (propType == typeof(DateTime) || Nullable.GetUnderlyingType(propType) == typeof(DateTime))
                {
                    property.SetColumnType("timestamp without time zone");
                }
            }
        }

        // Branch
        modelBuilder.Entity<Branch>(entity =>
        {
            entity.ToTable("Branches");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Code).IsUnique();
        });

        // Department
        modelBuilder.Entity<Department>(entity =>
        {
            entity.ToTable("Departments");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.BranchId);
            entity.HasIndex(e => e.ParentId);
            entity.HasOne(e => e.Branch).WithMany(b => b.Departments).HasForeignKey(e => e.BranchId);
            entity.HasOne(e => e.Parent).WithMany(p => p.Children).HasForeignKey(e => e.ParentId);
        });

        // Employee
        modelBuilder.Entity<Employee>(entity =>
        {
            entity.ToTable("Employees");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.EmployeeNo).IsUnique();
            entity.HasIndex(e => e.BranchId);
            entity.HasIndex(e => e.DepartmentId);
            entity.HasIndex(e => e.WeChatUserId);
            entity.HasOne(e => e.Branch).WithMany(b => b.Employees).HasForeignKey(e => e.BranchId);
            entity.HasOne(e => e.Department).WithMany(d => d.Employees).HasForeignKey(e => e.DepartmentId);
            entity.HasOne(e => e.Role).WithMany(r => r.Employees).HasForeignKey(e => e.RoleId);
        });

        // Role
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Code).IsUnique();
        });

        // Permission
        modelBuilder.Entity<Permission>(entity =>
        {
            entity.ToTable("Permissions");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => e.ParentId);
        });

        // RolePermission
        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.ToTable("RolePermissions");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.RoleId, e.PermissionId }).IsUnique();
            entity.HasOne(e => e.Role).WithMany(r => r.RolePermissions).HasForeignKey(e => e.RoleId);
            entity.HasOne(e => e.Permission).WithMany(p => p.RolePermissions).HasForeignKey(e => e.PermissionId);
        });

        // Customer
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customers");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CustomerNo).IsUnique();
            entity.HasIndex(e => e.Phone);
            entity.HasIndex(e => e.BranchId);
            entity.HasIndex(e => e.ParentCustomerId);
            entity.HasIndex(e => e.WeChatExternalUserId);
            entity.HasOne(e => e.Branch).WithMany(b => b.Customers).HasForeignKey(e => e.BranchId);
            entity.HasOne(e => e.ParentCustomer).WithMany(p => p.SubCustomers).HasForeignKey(e => e.ParentCustomerId);
            entity.HasOne(e => e.Creator).WithMany().HasForeignKey(e => e.CreatedById);
        });

        // Product
        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("Products");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SKU).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasOne(e => e.Category).WithMany(c => c.Products).HasForeignKey(e => e.CategoryId);
        });

        // ProductCategory
        modelBuilder.Entity<ProductCategory>(entity =>
        {
            entity.ToTable("ProductCategories");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ParentId);
            entity.HasOne(e => e.Parent).WithMany(p => p.Children).HasForeignKey(e => e.ParentId);
        });

        // Order
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.OrderNo).IsUnique();
            entity.HasIndex(e => e.CustomerId);
            entity.HasIndex(e => e.BranchId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasOne(e => e.Customer).WithMany(c => c.Orders).HasForeignKey(e => e.CustomerId);
            entity.HasOne(e => e.Branch).WithMany(b => b.Orders).HasForeignKey(e => e.BranchId);
            entity.HasOne(e => e.Creator).WithMany(c => c.CreatedOrders).HasForeignKey(e => e.CreatedById);
            entity.HasOne(e => e.DeliveryPerson).WithMany(d => d.Orders).HasForeignKey(e => e.DeliveryPersonId);
            entity.HasOne(e => e.Settlement).WithMany(s => s.Orders).HasForeignKey(e => e.SettlementId);
        });

        // OrderItem
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.ToTable("OrderItems");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.OrderId);
            entity.HasOne(e => e.Order).WithMany(o => o.Items).HasForeignKey(e => e.OrderId);
            entity.HasOne(e => e.Product).WithMany(p => p.OrderItems).HasForeignKey(e => e.ProductId);
        });

        // OrderModificationRecord
        modelBuilder.Entity<OrderModificationRecord>(entity =>
        {
            entity.ToTable("OrderModificationRecords");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.OrderId);
            entity.HasOne(e => e.Order).WithMany(o => o.ModificationRecords).HasForeignKey(e => e.OrderId);
            entity.HasOne(e => e.ModifiedBy).WithMany().HasForeignKey(e => e.ModifiedById);
        });

        // DeliveryPerson
        modelBuilder.Entity<DeliveryPerson>(entity =>
        {
            entity.ToTable("DeliveryPersons");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.BranchId);
            entity.HasIndex(e => e.Status);
            entity.HasOne(e => e.Branch).WithMany(b => b.DeliveryPersons).HasForeignKey(e => e.BranchId);
        });

        // Settlement
        modelBuilder.Entity<Settlement>(entity =>
        {
            entity.ToTable("Settlements");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SettlementNo).IsUnique();
            entity.HasIndex(e => e.BranchId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId);
            entity.HasOne(e => e.ConfirmedBy).WithMany().HasForeignKey(e => e.ConfirmedById);
        });

        // WorkSchedule
        modelBuilder.Entity<WorkSchedule>(entity =>
        {
            entity.ToTable("WorkSchedules");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.EmployeeId);
            entity.HasIndex(e => e.ScheduleDate);
            entity.HasOne(e => e.Employee).WithMany(e => e.WorkSchedules).HasForeignKey(x => x.EmployeeId);
            entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId);
            entity.HasOne(e => e.Creator).WithMany().HasForeignKey(e => e.CreatedById).OnDelete(DeleteBehavior.NoAction);
        });

        // WorkPlan
        modelBuilder.Entity<WorkPlan>(entity =>
        {
            entity.ToTable("WorkPlans");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.EmployeeId);
            entity.HasIndex(e => e.PlanDate);
            entity.HasOne(e => e.Employee).WithMany(e => e.WorkPlans).HasForeignKey(x => x.EmployeeId);
            entity.HasOne(e => e.Customer).WithMany().HasForeignKey(e => e.CustomerId);
            entity.HasOne(e => e.Order).WithMany().HasForeignKey(e => e.OrderId);
        });

        // PlanDraft
        modelBuilder.Entity<PlanDraft>(entity =>
        {
            entity.ToTable("PlanDrafts");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CreatedById);
        });

        // OperationLog
        modelBuilder.Entity<OperationLog>(entity =>
        {
            entity.ToTable("OperationLogs");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.OperatorId);
            entity.HasIndex(e => e.OperatedAt);
            entity.HasIndex(e => e.SyncStatus);
        });

        // SyncRecord
        modelBuilder.Entity<SyncRecord>(entity =>
        {
            entity.ToTable("SyncRecords");
            entity.HasKey(e => e.Id);
        });

        // SyncConfig
        modelBuilder.Entity<SyncConfig>(entity =>
        {
            entity.ToTable("SyncConfigs");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ConfigKey).IsUnique();
        });

        // LocalSetting
        modelBuilder.Entity<LocalSetting>(entity =>
        {
            entity.ToTable("LocalSettings");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SettingKey).IsUnique();
        });

        // WeChatConfig
        modelBuilder.Entity<WeChatConfig>(entity =>
        {
            entity.ToTable("WeChatConfigs");
            entity.HasKey(e => e.Id);
        });

        // Webhook
        modelBuilder.Entity<Webhook>(entity =>
        {
            entity.ToTable("Webhooks");
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Creator).WithMany().HasForeignKey(e => e.CreatedById).OnDelete(DeleteBehavior.NoAction);
        });

        // BackupRecord
        modelBuilder.Entity<BackupRecord>(entity =>
        {
            entity.ToTable("BackupRecords");
            entity.HasKey(e => e.Id);
        });

        // WeChatSyncLog
        modelBuilder.Entity<WeChatSyncLog>(entity =>
        {
            entity.ToTable("WeChatSyncLogs");
            entity.HasKey(e => e.Id);
        });

        // WeChatCustomer
        modelBuilder.Entity<WeChatCustomer>(entity =>
        {
            entity.ToTable("WeChatCustomers");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ExternalUserId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.AssignedBranchId);
            entity.HasOne(e => e.LinkedCustomer).WithMany().HasForeignKey(e => e.LinkedCustomerId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.AssignedBranch).WithMany().HasForeignKey(e => e.AssignedBranchId).OnDelete(DeleteBehavior.SetNull);
        });

        // Customer (update: add CustomerManagerId + new collections)
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasIndex(e => e.CustomerManagerId);
            entity.HasOne(e => e.CustomerManager).WithMany().HasForeignKey(e => e.CustomerManagerId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.BusinessDistrict).WithMany(b => b.Customers).HasForeignKey(e => e.BusinessDistrictId).OnDelete(DeleteBehavior.SetNull);
        });

        // BusinessDistrict
        modelBuilder.Entity<BusinessDistrict>(entity =>
        {
            entity.ToTable("BusinessDistricts");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.BranchId);
            entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.SetNull);
        });

        // Opportunity
        modelBuilder.Entity<Opportunity>(entity =>
        {
            entity.ToTable("Opportunities");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CustomerId);
            entity.HasIndex(e => e.BranchId);
            entity.Property(e => e.Stage).HasConversion<string>();
            entity.HasOne(e => e.Customer).WithMany(c => c.Opportunities).HasForeignKey(e => e.CustomerId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId);
            entity.HasOne(e => e.Developer).WithMany().HasForeignKey(e => e.DeveloperId);
        });

        // VisitRecord
        modelBuilder.Entity<VisitRecord>(entity =>
        {
            entity.ToTable("VisitRecords");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CustomerId);
            entity.HasIndex(e => e.VisitDate);
            entity.HasOne(e => e.Customer).WithMany(c => c.VisitRecords).HasForeignKey(e => e.CustomerId);
            entity.HasOne(e => e.Visitor).WithMany().HasForeignKey(e => e.VisitorId);
        });

        // CustomerPrice
        modelBuilder.Entity<CustomerPrice>(entity =>
        {
            entity.ToTable("CustomerPrices");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.CustomerId, e.ProductId }).IsUnique();
            entity.HasOne(e => e.Customer).WithMany(c => c.CustomerPrices).HasForeignKey(e => e.CustomerId);
            entity.HasOne(e => e.Product).WithMany().HasForeignKey(e => e.ProductId);
        });

        // VolumePrice
        modelBuilder.Entity<VolumePrice>(entity =>
        {
            entity.ToTable("VolumePrices");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ProductId, e.MinQuantity }).IsUnique();
        });

        // ProductImage
        modelBuilder.Entity<ProductImage>(entity =>
        {
            entity.ToTable("ProductImages");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ProductId);
        });

        // InventoryCheck
        modelBuilder.Entity<InventoryCheck>(entity =>
        {
            entity.ToTable("InventoryChecks");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.BranchId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId);
            entity.HasOne(e => e.Product).WithMany().HasForeignKey(e => e.ProductId);
            entity.HasOne(e => e.CheckedBy).WithMany().HasForeignKey(e => e.CheckedById);
        });

        // CustomerTag
        modelBuilder.Entity<CustomerTag>(entity =>
        {
            entity.ToTable("CustomerTags");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name);
            entity.HasMany(e => e.Customers).WithMany(c => c.CustomerTags).UsingEntity(t => t.ToTable("CustomerTagMappings"));
        });

        // WeChatVisitRecord
        modelBuilder.Entity<WeChatVisitRecord>(entity =>
        {
            entity.ToTable("WeChatVisitRecords");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SourceRecordId).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasOne(e => e.LinkedCustomer).WithMany().HasForeignKey(e => e.LinkedCustomerId).OnDelete(DeleteBehavior.SetNull);
        });

        // AppVersion
        modelBuilder.Entity<AppVersion>(entity =>
        {
            entity.ToTable("app_versions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Version).IsRequired();
            entity.Property(e => e.IsRequired).HasDefaultValue(false);
        });

        // Warehouse
        modelBuilder.Entity<Warehouse>(entity =>
        {
            entity.ToTable("Warehouses");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.BranchId);
            entity.HasIndex(e => e.Name);
            entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId);
            entity.HasMany(e => e.Products).WithOne().HasForeignKey("WarehouseId").OnDelete(DeleteBehavior.SetNull);
        });
    }
}
