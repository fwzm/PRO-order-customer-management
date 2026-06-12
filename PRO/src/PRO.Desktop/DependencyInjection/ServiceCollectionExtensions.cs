using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRO.Infrastructure.Persistence;
using PRO.Infrastructure.Security;
using PRO.Infrastructure.Database;
using PRO.Infrastructure.Repositories;
using PRO.Infrastructure.WeChat;
using PRO.Infrastructure.Services;
using PRO.Infrastructure.Configuration;
using AppInterfaces = PRO.Application.Interfaces;
using PRO.Desktop.ViewModels;
using PRO.Desktop.Views;
using PRO.Desktop.Services;
using Serilog;

namespace PRO.Desktop.DependencyInjection;

/// <summary>
/// DI容器模块化扩展方法 - 按功能领域组织服务注册
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册数据库上下文
    /// </summary>
    public static IServiceCollection AddProDatabase(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ProDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null);
                npgsql.CommandTimeout(30);
            }));
        return services;
    }

    /// <summary>
    /// 注册基础设施单例服务
    /// </summary>
    public static IServiceCollection AddProInfrastructureServices(this IServiceCollection services)
    {
        services.AddSingleton<ConnectionHealthService>();
        services.AddSingleton<MemoryCacheService>();
        services.AddSingleton(sp => new BusinessMessageService(sp));
        services.AddSingleton<BusinessRuleService>();
        services.AddSingleton<DatabaseBackupService>();
        services.AddSingleton<PRO.Domain.Enums.IOrderStatusManager, PRO.Domain.Enums.OrderStatusTransitionManager>();
        services.AddSingleton<AppInterfaces.IEncryptionService, EncryptionService>();
        services.AddSingleton<DataMaskingService>();
        services.AddScoped<BusinessRuleSettingsService>();
        return services;
    }

    /// <summary>
    /// 注册数据仓储
    /// </summary>
    public static IServiceCollection AddProRepositories(this IServiceCollection services)
    {
        services.AddScoped<AppInterfaces.IBranchRepository, BranchRepository>();
        services.AddScoped<AppInterfaces.IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<AppInterfaces.IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<AppInterfaces.ICustomerRepository, CustomerRepository>();
        services.AddScoped<AppInterfaces.IOrderRepository, OrderRepository>();
        services.AddScoped<AppInterfaces.IProductRepository, ProductRepository>();
        services.AddScoped<AppInterfaces.IDeliveryPersonRepository, DeliveryPersonRepository>();
        return services;
    }

    /// <summary>
    /// 注册核心业务服务
    /// </summary>
    public static IServiceCollection AddProBusinessServices(this IServiceCollection services)
    {
        services.AddScoped<AppInterfaces.IAuthService, AuthService>();
        services.AddScoped<AppInterfaces.IBranchService, BranchService>();
        services.AddScoped<AppInterfaces.IEmployeeService, EmployeeService>();
        services.AddScoped<AppInterfaces.ICustomerService, CustomerService>();
        services.AddScoped<AppInterfaces.IOrderService, OrderService>();
        services.AddScoped<AppInterfaces.IOrderNumberService, OrderNumberService>();
        services.AddScoped<AppInterfaces.IProductService, ProductService>();
        services.AddScoped<AppInterfaces.IDeliveryPersonService, DeliveryPersonService>();
        services.AddScoped<AppInterfaces.ISettlementService, SettlementService>();
        services.AddScoped<AppInterfaces.IWorkScheduleService, WorkScheduleService>();
        services.AddScoped<AppInterfaces.IWorkPlanService, WorkPlanService>();
        services.AddScoped<AppInterfaces.IPlanDraftService, PlanDraftService>();
        services.AddScoped<AppInterfaces.IOperationLogService, OperationLogService>();
        services.AddScoped<AppInterfaces.IOrderDistributionService, DeliveryDistributionService>();
        services.AddScoped<AppInterfaces.IWeChatService, WeChatService>();
        return services;
    }

    /// <summary>
    /// 注册扩展业务服务（Dashboard, Audit, Archive等）
    /// </summary>
    public static IServiceCollection AddProExtendedServices(this IServiceCollection services)
    {
        services.AddScoped<DashboardService>();
        services.AddScoped<CustomerArchiveService>();
        services.AddScoped<ViewMemoryService>();
        services.AddScoped<CustomerTimelineService>();
        services.AddScoped<SettlementOverviewService>();
        services.AddScoped<AuditService>();
        services.AddScoped<DraftService>();
        services.AddScoped<BusinessConfigService>();
        services.AddScoped<VersionCheckService>();
        // 系统设置服务（LocalSettings, WeChatConfig, SyncConfig）
        services.AddScoped<AppInterfaces.ISystemSettingService, SystemSettingService>();
        return services;
    }

    /// <summary>
    /// 注册P3阶段服务
    /// </summary>
    public static IServiceCollection AddProPhase3Services(this IServiceCollection services)
    {
        services.AddScoped<OrderTemplateService>();
        services.AddScoped<DataQualityService>();
        services.AddScoped<InventoryService>();
        services.AddScoped<AppInterfaces.IInventoryService>(sp => sp.GetRequiredService<InventoryService>());
        services.AddScoped<AppInterfaces.IPaymentService, PaymentService>();
        services.AddScoped<AppInterfaces.IReceivableService, ReceivableService>();
        services.AddScoped<AppInterfaces.IAuditTrailService, AuditTrailService>();
        services.AddScoped<UndoService>();
        services.AddScoped<PrintService>();
        services.AddScoped<GlobalShortcutService>();
        services.AddScoped<ApiServiceBase>();
        services.AddScoped<GlobalSearchService>();
        services.AddScoped<AppInterfaces.IImportService, ImportService>();
        return services;
    }

    /// <summary>
    /// 注册HTTP客户端
    /// </summary>
    public static IServiceCollection AddProHttpClients(this IServiceCollection services)
    {
        services.AddHttpClient("WeChatWork", client =>
        {
            client.BaseAddress = new Uri("https://qyapi.weixin.qq.com/");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddHttpClient("TencentMap", client =>
        {
            client.BaseAddress = new Uri("https://apis.map.qq.com");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddScoped<IMapService, TencentMapService>();
        return services;
    }

    /// <summary>
    /// 注册所有ViewModel
    /// </summary>
    public static IServiceCollection AddProViewModels(this IServiceCollection services)
    {
        services.AddTransient<LoginViewModel>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<CustomerListViewModel>();
        services.AddTransient<CustomerEditViewModel>();
        services.AddTransient<CustomerArchiveViewModel>();
        services.AddTransient<OrderListViewModel>();
        services.AddTransient<OrderEditViewModel>();
        services.AddTransient<ProductListViewModel>();
        services.AddTransient<ProductEditViewModel>();
        services.AddTransient<DeliveryPersonListViewModel>();
        services.AddTransient<DeliveryPersonEditViewModel>();
        services.AddTransient<SettlementListViewModel>();
        services.AddTransient<SettlementDetailViewModel>();
        services.AddTransient<WorkScheduleViewModel>();
        services.AddTransient<WorkPlanViewModel>();
        services.AddTransient<DepartmentTreeViewModel>();
        services.AddTransient<EmployeeListViewModel>();
        services.AddTransient<EmployeeEditViewModel>();
        services.AddTransient<SystemSettingsViewModel>();
        services.AddTransient<HeadquartersAdminViewModel>();
        services.AddTransient<SyncStatusViewModel>();
        services.AddTransient<BusinessDistrictViewModel>();
        services.AddTransient<MapPickerViewModel>();
        services.AddTransient<WeChatCustomerViewModel>();
        services.AddTransient<WeChatSyncLogViewModel>();
        services.AddTransient<PredictionDashboardViewModel>();
        services.AddTransient<OpportunityViewModel>();
        services.AddTransient<InventoryViewModel>();
        services.AddTransient<WeChatVisitSyncViewModel>();
        services.AddTransient<AccountsReceivableViewModel>();
        services.AddTransient<TagViewModel>();
        services.AddTransient<ReportCenterViewModel>();
        services.AddTransient<WeChatScrmViewModel>();
        services.AddTransient<VisitOpportunityViewModel>();
        services.AddTransient<DistrictTagViewModel>();
        services.AddTransient<OperationLogViewModel>();
        services.AddTransient<GlobalSearchViewModel>();
        services.AddTransient<WeChatSetupWizardViewModel>();
        services.AddTransient<PaymentRegistrationViewModel>();
        services.AddTransient<DataQualityViewModel>();
        services.AddTransient<CommandPaletteViewModel>();
        services.AddTransient<ErrorDashboardViewModel>();
        return services;
    }

    /// <summary>
    /// 注册View窗口
    /// </summary>
    public static IServiceCollection AddProViews(this IServiceCollection services)
    {
        services.AddTransient<LoginWindow>();
        services.AddTransient<MainWindow>();
        services.AddTransient<DepartmentManagementWindow>();
        services.AddTransient<EmployeeListWindow>();
        return services;
    }

    /// <summary>
    /// 启动期服务解析验证 - 验证关键服务可正常解析
    /// </summary>
    public static void ValidateServices(this IServiceProvider serviceProvider)
    {
        var issues = new List<string>();
        var criticalServices = new (Type ServiceType, string Name)[]
        {
            (typeof(ProDbContext), "数据库上下文"),
            (typeof(ConnectionHealthService), "连接健康服务"),
            (typeof(MemoryCacheService), "内存缓存服务"),
            (typeof(AppInterfaces.IAuthService), "认证服务"),
            (typeof(AppInterfaces.ICustomerService), "客户服务"),
            (typeof(AppInterfaces.IOrderService), "订单服务"),
            (typeof(AppInterfaces.IProductService), "产品服务"),
            (typeof(AppInterfaces.IDeliveryPersonService), "配送员服务"),
            (typeof(AppInterfaces.IEncryptionService), "加密服务"),
            (typeof(PRO.Domain.Enums.IOrderStatusManager), "订单状态管理器"),
        };

        foreach (var (serviceType, name) in criticalServices)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                scope.ServiceProvider.GetRequiredService(serviceType);
            }
            catch (Exception ex)
            {
                issues.Add($"  ⚠ {name} ({serviceType.Name}): {ex.Message}");
            }
        }

        if (issues.Any())
        {
            var message = $"服务解析验证发现 {issues.Count} 个问题：\n{string.Join("\n", issues)}";
            Log.Warning(message);
            // 非致命 - 仅记录警告
        }
        else
        {
            Log.Information("启动期服务解析验证通过，所有关键服务已注册");
        }
    }
}
