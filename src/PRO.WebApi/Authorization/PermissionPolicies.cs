using Microsoft.AspNetCore.Authorization;

namespace PRO.WebApi.Authorization;

/// <summary>
/// 权限策略定义
/// </summary>
public static class PermissionPolicies
{
    // ==================== 订单权限 ====================
    public const string OrderView = "Order.View";
    public const string OrderCreate = "Order.Create";
    public const string OrderEdit = "Order.Edit";
    public const string OrderDelete = "Order.Delete";
    public const string OrderCancel = "Order.Cancel";
    public const string OrderExport = "Order.Export";
    public const string OrderAutoAssign = "Order.AutoAssign";
    public const string OrderBatchAssign = "Order.BatchAssign";
    public const string OrderBatchStatusChange = "Order.BatchStatusChange";

    // ==================== 客户权限 ====================
    public const string CustomerView = "Customer.View";
    public const string CustomerCreate = "Customer.Create";
    public const string CustomerEdit = "Customer.Edit";
    public const string CustomerDelete = "Customer.Delete";
    public const string CustomerMerge = "Customer.Merge";
    public const string CustomerExport = "Customer.Export";

    // ==================== 产品权限 ====================
    public const string ProductView = "Product.View";
    public const string ProductCreate = "Product.Create";
    public const string ProductEdit = "Product.Edit";
    public const string ProductDelete = "Product.Delete";

    // ==================== 结算权限 ====================
    public const string SettlementView = "Settlement.View";
    public const string SettlementCreate = "Settlement.Create";
    public const string SettlementDelete = "Settlement.Delete";

    // ==================== 配送权限 ====================
    public const string DeliveryView = "Delivery.View";
    public const string DeliveryEdit = "Delivery.Edit";
    public const string DeliveryAutoAssign = "Delivery.AutoAssign";
    public const string DeliveryManualAssign = "Delivery.ManualAssign";

    // ==================== 系统权限 ====================
    public const string SystemSettings = "System.Settings";
    public const string SystemBackup = "System.Backup";
    public const string SystemLogs = "System.Logs";
    public const string SystemWeChat = "System.WeChat";

    /// <summary>
    /// 注册所有权限策略
    /// </summary>
    public static void RegisterPolicies(AuthorizationOptions options)
    {
        // 订单权限
        AddPermissionPolicy(options, OrderView, OrderEdit, "Order.*");
        AddPermissionPolicy(options, OrderCreate, OrderEdit, "Order.*");
        AddPermissionPolicy(options, OrderEdit, "Order.*");
        AddPermissionPolicy(options, OrderDelete, "Order.*");
        AddPermissionPolicy(options, OrderCancel, OrderEdit, "Order.*");
        AddPermissionPolicy(options, OrderExport, OrderEdit, "Order.*");
        AddPermissionPolicy(options, OrderAutoAssign, DeliveryAutoAssign, DeliveryEdit, "Order.*");
        AddPermissionPolicy(options, OrderBatchAssign, DeliveryEdit, "Order.*");
        AddPermissionPolicy(options, OrderBatchStatusChange, OrderEdit, "Order.*");

        // 客户权限
        AddPermissionPolicy(options, CustomerView, CustomerEdit, "Customer.*");
        AddPermissionPolicy(options, CustomerCreate, CustomerEdit, "Customer.*");
        AddPermissionPolicy(options, CustomerEdit, "Customer.*");
        AddPermissionPolicy(options, CustomerDelete, "Customer.*");
        AddPermissionPolicy(options, CustomerMerge, "Customer.*");
        AddPermissionPolicy(options, CustomerExport, CustomerEdit, "Customer.*");

        // 产品权限
        AddPermissionPolicy(options, ProductView, ProductEdit, "Product.*");
        AddPermissionPolicy(options, ProductCreate, ProductEdit, "Product.*");
        AddPermissionPolicy(options, ProductEdit, "Product.*");
        AddPermissionPolicy(options, ProductDelete, "Product.*");

        // 结算权限
        AddPermissionPolicy(options, SettlementView, SettlementCreate, "Settlement.*");
        AddPermissionPolicy(options, SettlementCreate, "Settlement.*");
        AddPermissionPolicy(options, SettlementDelete, "Settlement.*");

        // 配送权限
        AddPermissionPolicy(options, DeliveryView, DeliveryEdit, "Delivery.*");
        AddPermissionPolicy(options, DeliveryEdit, "Delivery.*");
        AddPermissionPolicy(options, DeliveryAutoAssign, DeliveryEdit, "Delivery.*");
        AddPermissionPolicy(options, DeliveryManualAssign, DeliveryEdit, "Delivery.*");

        // 系统权限
        AddPermissionPolicy(options, SystemSettings);
        AddPermissionPolicy(options, SystemBackup, SystemSettings);
        AddPermissionPolicy(options, SystemLogs, SystemSettings);
        AddPermissionPolicy(options, SystemWeChat, SystemSettings);
    }

    private static void AddPermissionPolicy(AuthorizationOptions options, string policy, params string[] compatiblePermissions)
    {
        options.AddPolicy(policy, builder =>
        {
            builder.RequireAuthenticatedUser();
            builder.Requirements.Add(new PermissionRequirement(policy, compatiblePermissions));
        });
    }
}
