using PRO.Application.Interfaces;
using PRO.Domain.Enums;
using Serilog;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 操作审计服务 - 记录关键业务操作
/// </summary>
public class AuditService
{
    private readonly IOperationLogService _logService;

    public AuditService(IOperationLogService logService)
    {
        _logService = logService;
    }

    // ==================== 订单操作 ====================

    public async Task LogOrderCreateAsync(int operatorId, int orderId, string orderNo, decimal amount)
    {
        await _logService.CreateAsync(
            operatorId, "订单", "创建订单",
            $"创建订单 {orderNo}，金额 ¥{amount:N0}",
            "Order", orderId);
    }

    public async Task LogOrderStatusChangeAsync(int operatorId, int orderId, string orderNo,
        OrderStatus fromStatus, OrderStatus toStatus, string? reason = null)
    {
        var fromName = OrderStatusManager.GetStatusName(fromStatus);
        var toName = OrderStatusManager.GetStatusName(toStatus);
        var content = $"订单 {orderNo} 状态变更：{fromName} → {toName}";
        if (!string.IsNullOrEmpty(reason))
            content += $"，原因：{reason}";

        await _logService.CreateAsync(
            operatorId, "订单", "状态变更",
            content, "Order", orderId);
    }

    public async Task LogOrderAssignAsync(int operatorId, int orderId, string orderNo,
        int deliveryPersonId, string deliveryPersonName, string method = "手动")
    {
        await _logService.CreateAsync(
            operatorId, "订单", "分配配送员",
            $"{method}分配：订单 {orderNo} → 配送员 {deliveryPersonName}",
            "Order", orderId);
    }

    public async Task LogOrderBatchAssignAsync(int operatorId, int orderCount, string deliveryPersonName)
    {
        await _logService.CreateAsync(
            operatorId, "订单", "批量分配",
            $"批量分配 {orderCount} 个订单给配送员 {deliveryPersonName}");
    }

    public async Task LogOrderBatchStatusChangeAsync(int operatorId, int successCount, int totalCount,
        OrderStatus toStatus, string? reason = null)
    {
        var toName = OrderStatusManager.GetStatusName(toStatus);
        var content = $"批量变更订单状态：成功 {successCount}/{totalCount} 单，目标状态 {toName}";
        if (!string.IsNullOrWhiteSpace(reason))
            content += $"，原因：{reason}";

        await _logService.CreateAsync(
            operatorId, "订单", "批量状态变更",
            content);
    }

    public async Task LogOrderDeleteAsync(int operatorId, int orderId, string orderNo)
    {
        await _logService.CreateAsync(
            operatorId, "订单", "删除订单",
            $"删除订单 {orderNo}",
            "Order", orderId, "Warning");
    }

    public async Task LogOrderCancelAsync(int operatorId, int orderId, string orderNo, string reason)
    {
        await _logService.CreateAsync(
            operatorId, "订单", "取消订单",
            $"取消订单 {orderNo}，原因：{reason}",
            "Order", orderId, "Warning");
    }

    // ==================== 结算操作 ====================

    public async Task LogSettlementCreateAsync(int operatorId, int settlementId, string settlementNo,
        int orderCount, decimal totalAmount)
    {
        await _logService.CreateAsync(
            operatorId, "结算", "创建结算",
            $"创建结算单 {settlementNo}，包含 {orderCount} 个订单，总金额 ¥{totalAmount:N0}",
            "Settlement", settlementId);
    }

    public async Task LogSettlementBatchAsync(int operatorId, int orderCount, decimal totalAmount)
    {
        await _logService.CreateAsync(
            operatorId, "结算", "批量结算",
            $"批量结算 {orderCount} 个订单，总金额 ¥{totalAmount:N0}");
    }

    // ==================== 客户操作 ====================

    public async Task LogCustomerCreateAsync(int operatorId, int customerId, string customerName)
    {
        await _logService.CreateAsync(
            operatorId, "客户", "创建客户",
            $"创建客户 {customerName}",
            "Customer", customerId);
    }

    public async Task LogCustomerMergeAsync(int operatorId, int mainCustomerId, string mainCustomerName,
        List<string> mergedNames)
    {
        await _logService.CreateAsync(
            operatorId, "客户", "合并客户",
            $"合并客户至 {mainCustomerName}，被合并：{string.Join("、", mergedNames)}",
            "Customer", mainCustomerId);
    }

    public async Task LogCustomerDeleteAsync(int operatorId, int customerId, string customerName)
    {
        await _logService.CreateAsync(
            operatorId, "客户", "删除客户",
            $"删除客户 {customerName}",
            "Customer", customerId, "Warning");
    }

    // ==================== 产品操作 ====================

    public async Task LogProductCreateAsync(int operatorId, int productId, string productName)
    {
        await _logService.CreateAsync(
            operatorId, "产品", "创建产品",
            $"创建产品 {productName}",
            "Product", productId);
    }

    public async Task LogProductDeleteAsync(int operatorId, int productId, string productName)
    {
        await _logService.CreateAsync(
            operatorId, "产品", "删除产品",
            $"删除产品 {productName}",
            "Product", productId, "Warning");
    }

    // ==================== 配送操作 ====================

    public async Task LogAutoAssignAsync(int operatorId, int branchId, int successCount, int totalCount)
    {
        await _logService.CreateAsync(
            operatorId, "物流", "自动分配",
            $"自动分配完成：成功 {successCount}/{totalCount} 单");
    }

    public async Task LogDeliveryStatusChangeAsync(int operatorId, int orderId, string status)
    {
        await _logService.CreateAsync(
            operatorId, "物流", "配送状态变更",
            $"配送状态变更为：{status}",
            "Order", orderId);
    }

    // ==================== 系统操作 ====================

    public async Task LogBackupAsync(int operatorId, string backupType, bool success, string? fileName = null)
    {
        var status = success ? "成功" : "失败";
        var content = $"{backupType}备份{status}";
        if (!string.IsNullOrEmpty(fileName))
            content += $"：{fileName}";

        await _logService.CreateAsync(
            operatorId, "系统", "备份",
            content, result: success ? "Success" : "Failed");
    }

    public async Task LogExportAsync(int operatorId, string exportType, int recordCount)
    {
        await _logService.CreateAsync(
            operatorId, "导出", exportType,
            $"导出 {exportType} {recordCount} 条记录");
    }

    public async Task LogConfigChangeAsync(int operatorId, string configKey, string oldValue, string newValue)
    {
        await _logService.CreateAsync(
            operatorId, "系统", "配置变更",
            $"配置变更：{configKey}，{oldValue} → {newValue}");
    }

    // ==================== 数据质量操作 ====================

    public async Task LogDataCleanupAsync(int operatorId, string cleanupType, int affectedCount)
    {
        await _logService.CreateAsync(
            operatorId, "数据", "数据清理",
            $"数据清理（{cleanupType}）：处理 {affectedCount} 条记录");
    }

    public async Task LogDuplicateResolveAsync(int operatorId, int keptCustomerId, int mergedCount)
    {
        await _logService.CreateAsync(
            operatorId, "数据", "重复处理",
            $"处理重复客户：保留客户ID {keptCustomerId}，合并 {mergedCount} 条",
            "Customer", keptCustomerId);
    }
}
