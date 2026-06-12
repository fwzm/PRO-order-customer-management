using Microsoft.Extensions.DependencyInjection;
using PRO.Application.Interfaces;
using PRO.Infrastructure.Services;
using PRO.Infrastructure.WeChat;

namespace PRO.Infrastructure;

/// <summary>
/// DI 注册扩展方法 - 按模块组织服务注册
/// 注意：Desktop项目使用PRO.Desktop.DependencyInjection下的扩展方法；
/// 此文件供WebApi或其他宿主使用。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册客户服务
    /// </summary>
    public static IServiceCollection AddCustomerServices(this IServiceCollection services)
    {
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<CustomerArchiveService>();
        services.AddScoped<CustomerTimelineService>();
        return services;
    }

    /// <summary>
    /// 注册订单服务
    /// </summary>
    public static IServiceCollection AddOrderServices(this IServiceCollection services)
    {
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IOrderNumberService, OrderNumberService>();
        services.AddScoped<OrderTemplateService>();
        return services;
    }

    /// <summary>
    /// 注册产品服务
    /// </summary>
    public static IServiceCollection AddProductServices(this IServiceCollection services)
    {
        services.AddScoped<IProductService, ProductService>();
        return services;
    }

    /// <summary>
    /// 注册配送服务
    /// </summary>
    public static IServiceCollection AddDeliveryServices(this IServiceCollection services)
    {
        services.AddScoped<IDeliveryPersonService, DeliveryPersonService>();
        services.AddScoped<IOrderDistributionService, DeliveryDistributionService>();
        return services;
    }

    /// <summary>
    /// 注册结算服务
    /// </summary>
    public static IServiceCollection AddSettlementServices(this IServiceCollection services)
    {
        services.AddScoped<ISettlementService, SettlementService>();
        services.AddScoped<SettlementOverviewService>();
        return services;
    }

    /// <summary>
    /// 注册审计服务
    /// </summary>
    public static IServiceCollection AddAuditServices(this IServiceCollection services)
    {
        services.AddScoped<IOperationLogService, OperationLogService>();
        services.AddScoped<AuditService>();
        return services;
    }

    /// <summary>
    /// 注册工作计划服务
    /// </summary>
    public static IServiceCollection AddWorkPlanServices(this IServiceCollection services)
    {
        services.AddScoped<IWorkScheduleService, WorkScheduleService>();
        services.AddScoped<IWorkPlanService, WorkPlanService>();
        services.AddScoped<IPlanDraftService, PlanDraftService>();
        return services;
    }

    /// <summary>
    /// 注册企业微信服务
    /// </summary>
    public static IServiceCollection AddWeChatServices(this IServiceCollection services)
    {
        services.AddScoped<IWeChatService, WeChatService>();
        return services;
    }

    /// <summary>
    /// 注册基础设施服务
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // 通用服务
        services.AddSingleton<ConnectionHealthService>();
        services.AddSingleton<DatabaseBackupService>();
        services.AddSingleton<MemoryCacheService>();
        services.AddSingleton<BusinessConfigService>();
        services.AddScoped<DraftService>();
        services.AddScoped<ViewMemoryService>();
        services.AddScoped<DataQualityService>();
        services.AddScoped<InventoryService>();
        services.AddScoped<PaymentService>();
        services.AddScoped<UndoService>();
        services.AddScoped<PrintService>();
        services.AddScoped<ExportService>();

        // 组织架构服务（注意：DepartmentService/RoleService 尚未实现，标记为待补充）
        services.AddScoped<IBranchService, BranchService>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        // TODO: 实现 IDepartmentService 后取消注释
        // services.AddScoped<IDepartmentService, DepartmentService>();
        // TODO: 实现 IRoleService 后取消注释
        // services.AddScoped<IRoleService, RoleService>();
        // TODO: PermissionService 需实现 IPermissionService 接口后取消注释
        // services.AddScoped<IPermissionService, PermissionService>();

        return services;
    }

    /// <summary>
    /// 注册所有业务服务
    /// </summary>
    public static IServiceCollection AddAllBusinessServices(this IServiceCollection services)
    {
        services.AddCustomerServices();
        services.AddOrderServices();
        services.AddProductServices();
        services.AddDeliveryServices();
        services.AddSettlementServices();
        services.AddAuditServices();
        services.AddWorkPlanServices();
        services.AddWeChatServices();
        services.AddInfrastructureServices();
        return services;
    }
}
