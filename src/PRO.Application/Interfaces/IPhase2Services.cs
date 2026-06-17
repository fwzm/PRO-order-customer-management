using PRO.Application.DTOs;

namespace PRO.Application.Interfaces;

/// <summary>
/// 收款与核销服务接口
/// </summary>
public interface IPaymentService
{
    // 收款登记
    Task<ApiResponse<int>> CreatePaymentAsync(CreatePaymentRequest request, int receivedById);
    Task<ApiResponse<int>> BatchCreatePaymentAsync(BatchPaymentRequest request, int receivedById);
    Task<ApiResponse<bool>> DeletePaymentAsync(int paymentId, int operatorId, string reason);

    // 收款核销（多对多）
    Task<ApiResponse<PaymentAllocationResultDto>> AllocatePaymentAsync(AllocatePaymentRequest request, int operatorId);
    Task<ApiResponse<List<PaymentAllocationDetailDto>>> GetAllocationsAsync(int paymentRecordId);

    // 查询
    Task<ApiResponse<List<PaymentRecordDto>>> GetOrderPaymentsAsync(int orderId);
    Task<ApiResponse<List<PaymentRecordDto>>> GetCustomerPaymentsAsync(int customerId);
    Task<ApiResponse<PagedResult<PaymentRecordDto>>> GetPaymentListAsync(
        PagedRequest request, int? branchId = null, int? customerId = null,
        DateTime? startDate = null, DateTime? endDate = null);
    Task<ApiResponse<PaymentStatsDto>> GetPaymentStatsAsync(int branchId, DateTime startDate, DateTime endDate);
}

/// <summary>
/// 应收账款服务接口 — 基于订单金额+折扣+收款+核销的综合计算
/// </summary>
public interface IReceivableService
{
    /// <summary>获取客户应收账款摘要</summary>
    Task<ApiResponse<List<CustomerReceivableDto>>> GetCustomerReceivablesAsync(ReceivableQueryRequest request);
    /// <summary>获取单个客户应收明细</summary>
    Task<ApiResponse<CustomerReceivableDto>> GetCustomerReceivableAsync(int customerId);
    /// <summary>账龄分析</summary>
    Task<ApiResponse<AgingAnalysisDto>> GetAgingAnalysisAsync(AgingAnalysisRequest request);
}

/// <summary>
/// 状态历史与审计日志服务接口
/// </summary>
public interface IAuditTrailService
{
    // 状态历史
    Task LogOrderStatusChangeAsync(int orderId, string orderNo, int fromStatus, int toStatus,
        string reason, int operatorId);
    Task LogPaymentStatusChangeAsync(string entityType, int entityId, int fromStatus, int toStatus,
        string reason, int operatorId);
    Task<ApiResponse<List<OrderStatusHistoryDto>>> GetOrderStatusHistoryAsync(int orderId);
    Task<ApiResponse<List<PaymentStatusHistoryDto>>> GetPaymentStatusHistoryAsync(string entityType, int entityId);

    // 状态回滚（带审计）
    Task<ApiResponse<bool>> RollbackOrderStatusAsync(StatusRollbackRequest request, int operatorId);
    Task<ApiResponse<bool>> RollbackPaymentStatusAsync(StatusRollbackRequest request, int operatorId);

    // 审计日志详情
    Task LogDetailAsync(string entityType, int entityId, string entityName, string fieldName,
        string? oldValue, string? newValue, string actionType, string reason, int operatorId, int branchId);
    Task<ApiResponse<PagedResult<AuditLogDetailDto>>> QueryAuditLogsAsync(AuditLogQueryRequest request);
}
