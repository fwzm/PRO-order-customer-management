using PRO.Application.DTOs;
using PRO.Domain.Enums;

namespace PRO.Application.Interfaces;

/// <summary>
/// 工作台服务接口
/// </summary>
public interface IDashboardService
{
    Task<ApiResponse<BusinessDashboardDto>> GetDashboardDataAsync(int branchId);
    Task<ApiResponse<List<DashboardAlert>>> GetAlertsAsync(int branchId);
    Task<ApiResponse<List<DashboardTask>>> GetTodayTasksAsync(int branchId);
}

/// <summary>
/// 审计服务接口
/// </summary>
public interface IAuditService
{
    Task LogOrderCreateAsync(int operatorId, int orderId, string orderNo, decimal amount);
    Task LogOrderStatusChangeAsync(int operatorId, int orderId, string orderNo, OrderStatus fromStatus, OrderStatus toStatus, string? reason = null);
    Task LogOrderAssignAsync(int operatorId, int orderId, string orderNo, int deliveryPersonId, string deliveryPersonName, string method = "手动");
    Task LogOrderBatchAssignAsync(int operatorId, int orderCount, string deliveryPersonName);
    Task LogOrderBatchStatusChangeAsync(int operatorId, int successCount, int totalCount, OrderStatus toStatus, string? reason = null);
    Task LogOrderDeleteAsync(int operatorId, int orderId, string orderNo);
    Task LogOrderCancelAsync(int operatorId, int orderId, string orderNo, string reason);
    Task LogSettlementCreateAsync(int operatorId, int settlementId, string settlementNo, int orderCount, decimal totalAmount);
    Task LogSettlementBatchAsync(int operatorId, int orderCount, decimal totalAmount);
    Task LogCustomerCreateAsync(int operatorId, int customerId, string customerName);
    Task LogCustomerMergeAsync(int operatorId, int mainCustomerId, string mainCustomerName, List<string> mergedNames);
    Task LogCustomerDeleteAsync(int operatorId, int customerId, string customerName);
    Task LogProductCreateAsync(int operatorId, int productId, string productName);
    Task LogProductDeleteAsync(int operatorId, int productId, string productName);
    Task LogExportAsync(int operatorId, string exportType, int recordCount);
    Task LogConfigChangeAsync(int operatorId, string configKey, string oldValue, string newValue);
}

/// <summary>
/// 客户档案服务接口
/// </summary>
public interface ICustomerArchiveService
{
    Task<ApiResponse<CustomerArchiveDto>> GetCustomerArchiveAsync(int customerId);
    Task<ApiResponse<CustomerTimelineDto>> GetCustomerTimelineAsync(int customerId, int pageIndex = 0, int pageSize = 50);
}

/// <summary>
/// 结算概览服务接口
/// </summary>
public interface ISettlementOverviewService
{
    Task<ApiResponse<SettlementOverviewDto>> GetOverviewAsync(int branchId);
    Task<ApiResponse<bool>> BatchSettleAsync(List<int> orderIds, int confirmedById, string? remark = null);
    Task<ApiResponse<bool>> MarkAbnormalAsync(int orderId, string abnormalType, string remark);
}

/// <summary>
/// 撤销服务接口
/// </summary>
public interface IUndoService
{
    Task RecordOperationAsync(string operationType, string entityType, int entityId, string entityName, OperationSnapshot snapshot, int operatorId, string description);
    Task<ApiResponse<List<UndoableOperationDto>>> GetUndoableOperationsAsync(int operatorId);
    Task<ApiResponse<bool>> UndoAsync(int operationId, int operatorId);
    Task CleanupExpiredAsync();
}

/// <summary>
/// 打印服务接口
/// </summary>
public interface IPrintService
{
    Task<ApiResponse<PrintDataDto>> GenerateOrderPrintAsync(int orderId, int? templateId = null);
    Task<ApiResponse<PrintDataDto>> GenerateSettlementPrintAsync(int settlementId);
    Task<ApiResponse<List<PrintDataDto>>> BatchGeneratePrintAsync(string type, List<int> ids);
}
