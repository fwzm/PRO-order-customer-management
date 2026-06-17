using PRO.Domain.Enums;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 权限检查服务 - 统一权限校验
/// </summary>
public class PermissionService
{
    /// <summary>
    /// 检查是否允许执行操作
    /// </summary>
    public static PermissionResult CheckPermission(IPermissionSession session, string permission)
    {
        // 总部管理员拥有所有权限
        if (session.IsHeadquartersAdmin)
            return PermissionResult.Allowed();

        // 检查具体权限
        if (session.HasPermission(permission))
            return PermissionResult.Allowed();

        // 兼容旧库：新增的细粒度动作权限可回退到原模块编辑权限
        var fallbackPermission = GetFallbackPermission(permission);
        if (!string.IsNullOrEmpty(fallbackPermission) && session.HasPermission(fallbackPermission))
            return PermissionResult.Allowed();

        return PermissionResult.Denied(GetPermissionMessage(permission));
    }

    /// <summary>
    /// 检查订单操作权限
    /// </summary>
    public static PermissionResult CheckOrderPermission(IPermissionSession session, string action)
    {
        return action switch
        {
            "AutoAssign" => CheckPermission(session, "Order.AutoAssign"),
            "BatchAssign" => CheckPermission(session, "Order.BatchAssign"),
            "BatchStatusChange" => CheckPermission(session, "Order.BatchStatusChange"),
            "Delete" => CheckPermission(session, "Order.Delete"),
            "Cancel" => CheckPermission(session, "Order.Cancel"),
            "Export" => CheckPermission(session, "Order.Export"),
            _ => PermissionResult.Allowed()
        };
    }

    /// <summary>
    /// 检查结算操作权限
    /// </summary>
    public static PermissionResult CheckSettlementPermission(IPermissionSession session, string action)
    {
        // 结算操作需要更高权限
        if (!session.CanSettlement)
            return PermissionResult.Denied("结算操作需要管理员权限");

        return action switch
        {
            "Create" => PermissionResult.Allowed(),
            "BatchSettle" => PermissionResult.Allowed(),
            "Delete" => CheckPermission(session, "Settlement.Delete"),
            _ => PermissionResult.Allowed()
        };
    }

    /// <summary>
    /// 检查客户操作权限
    /// </summary>
    public static PermissionResult CheckCustomerPermission(IPermissionSession session, string action)
    {
        return action switch
        {
            "Delete" => CheckPermission(session, "Customer.Delete"),
            "Merge" => CheckPermission(session, "Customer.Merge"),
            "Export" => CheckPermission(session, "Customer.Export"),
            _ => PermissionResult.Allowed()
        };
    }

    /// <summary>
    /// 检查配送分配权限
    /// </summary>
    public static PermissionResult CheckDeliveryPermission(IPermissionSession session, string action)
    {
        return action switch
        {
            "AutoAssign" => CheckPermission(session, "Delivery.AutoAssign"),
            "ManualAssign" => CheckPermission(session, "Delivery.ManualAssign"),
            _ => PermissionResult.Allowed()
        };
    }

    private static string GetPermissionMessage(string permission) => permission switch
    {
        "Order.AutoAssign" => "自动分配订单",
        "Order.BatchAssign" => "批量分配订单",
        "Order.BatchStatusChange" => "批量修改订单状态",
        "Order.Delete" => "删除订单",
        "Order.Cancel" => "取消订单",
        "Order.Export" => "导出订单",
        "Settlement.Delete" => "删除结算单",
        "Customer.Delete" => "删除客户",
        "Customer.Merge" => "合并客户",
        "Customer.Export" => "导出客户",
        "Delivery.AutoAssign" => "自动分配配送",
        "Delivery.ManualAssign" => "手动分配配送",
        _ => permission
    };

    private static string? GetFallbackPermission(string permission) => permission switch
    {
        "Order.AutoAssign" => "Order.Edit",
        "Order.BatchAssign" => "Order.Edit",
        "Order.BatchStatusChange" => "Order.Edit",
        "Order.Cancel" => "Order.Edit",
        "Customer.Merge" => "Customer.Edit",
        "Delivery.AutoAssign" => "Delivery.Edit",
        "Delivery.ManualAssign" => "Delivery.Edit",
        _ => null
    };
}

public interface IPermissionSession
{
    bool IsHeadquartersAdmin { get; }
    bool CanSettlement { get; }
    bool HasPermission(string permission);
}

/// <summary>
/// 权限检查结果
/// </summary>
public class PermissionResult
{
    public bool IsAllowed { get; set; }
    public string? DenyReason { get; set; }

    public static PermissionResult Allowed() => new() { IsAllowed = true };
    public static PermissionResult Denied(string reason) => new() { IsAllowed = false, DenyReason = reason };
}
