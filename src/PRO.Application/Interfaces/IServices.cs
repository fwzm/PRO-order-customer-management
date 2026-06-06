using PRO.Application.DTOs;
using PRO.Domain.Entities;
using PRO.Domain.Enums;

namespace PRO.Application.Interfaces;

// ==================== 认证服务 ====================

public interface IAuthService
{
    Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request);
    Task<ApiResponse<bool>> LogoutAsync(int employeeId);
    Task<ApiResponse<bool>> ChangePasswordAsync(ChangePasswordRequest request);
    Task<ApiResponse<LoginResponse>> RefreshTokenAsync(string token);
    Task<ApiResponse<bool>> ValidateSessionAsync(int employeeId);
}

// ==================== 组织架构服务 ====================

public interface IBranchService
{
    Task<ApiResponse<PagedResult<BranchListItem>>> GetListAsync(PagedRequest request);
    Task<ApiResponse<BranchListItem>> GetByIdAsync(int id);
    Task<ApiResponse<int>> CreateAsync(string name, string code, int? regionId, string? address, string? phone);
    Task<ApiResponse<bool>> UpdateAsync(int id, string name, string code, int? regionId, string? address, string? phone, int status);
    Task<ApiResponse<bool>> DeleteAsync(int id);
}

public interface IDepartmentService
{
    Task<ApiResponse<List<DepartmentTreeNode>>> GetTreeAsync(int? branchId);
    Task<ApiResponse<PagedResult<EmployeeListItem>>> GetEmployeeListAsync(int departmentId, PagedRequest request);
    Task<ApiResponse<int>> CreateAsync(string name, string? code, int? parentId, int branchId, int? managerId);
    Task<ApiResponse<bool>> UpdateAsync(int id, string name, string? code, int? parentId, int? managerId, int sortOrder);
    Task<ApiResponse<bool>> DeleteAsync(int id);
}

public interface IEmployeeService
{
    Task<ApiResponse<PagedResult<EmployeeListItem>>> GetListAsync(PagedRequest request);
    Task<ApiResponse<EmployeeListItem>> GetByIdAsync(int id);
    Task<ApiResponse<int>> CreateAsync(CreateEmployeeRequest request);
    Task<ApiResponse<bool>> UpdateAsync(UpdateEmployeeRequest request);
    Task<ApiResponse<bool>> UpdatePasswordAsync(int id, string newPassword);
    Task<ApiResponse<bool>> UpdateStatusAsync(int id, EmployeeStatus status);
    Task<ApiResponse<bool>> ResetPasswordAsync(int id, string newPassword);
}

// ==================== 客户管理服务 ====================

public interface ICustomerService
{
    Task<ApiResponse<PagedResult<CustomerListItem>>> GetListAsync(PagedRequest request, int? branchId = null, CustomerType? customerType = null, bool showMajorOnly = true);
    Task<ApiResponse<CustomerDetailDto>> GetByIdAsync(int id);
    Task<ApiResponse<CustomerListItem>> GetSubCustomersAsync(int parentCustomerId);
    Task<ApiResponse<int>> CreateAsync(CreateCustomerRequest request);
    Task<ApiResponse<bool>> UpdateAsync(UpdateCustomerRequest request);
    Task<ApiResponse<bool>> DeleteAsync(int id);
    Task<ApiResponse<CustomerDuplicateCheckResult>> CheckDuplicatesAsync(string? phone, string? name, string? address, string? legalPerson);
    Task<ApiResponse<bool>> MergeCustomersAsync(MergeCustomerRequest request);
    Task<ApiResponse<string>> ExportToExcelAsync(PagedRequest request, int? branchId = null);
}

// ==================== 订单管理服务 ====================

public interface IOrderService
{
    Task<ApiResponse<PagedResult<OrderListItem>>> GetListAsync(PagedRequest request, int? branchId = null, OrderStatus? status = null, PaymentStatus? paymentStatus = null);
    Task<ApiResponse<OrderDetailDto>> GetByIdAsync(int id);
    Task<ApiResponse<int>> CreateAsync(CreateOrderRequest request, int createdById);
    Task<ApiResponse<bool>> UpdateAsync(UpdateOrderRequest request, int modifiedById);
    Task<ApiResponse<bool>> DeleteAsync(int id);
    Task<ApiResponse<bool>> AssignAsync(AssignOrderRequest request, int assignedById);
    Task<ApiResponse<bool>> UpdateStatusAsync(UpdateOrderStatusRequest request, int modifiedById);
    Task<ApiResponse<bool>> ConfirmDraftAsync(int orderId, int modifiedById);
    Task<ApiResponse<string>> ExportToExcelAsync(PagedRequest request, int? branchId = null, bool forGaode = false);
    Task<ApiResponse<string>> ExportDeliveryPlanAsync(List<int> orderIds, string groupName);
}

// ==================== 产品管理服务 ====================

public interface IProductService
{
    Task<ApiResponse<PagedResult<ProductListItem>>> GetListAsync(PagedRequest request, int? categoryId = null, ProductStatus? status = null);
    Task<ApiResponse<ProductListItem>> GetByIdAsync(int id);
    Task<ApiResponse<List<ProductCategoryDto>>> GetCategoriesAsync();
    Task<ApiResponse<int>> CreateAsync(CreateProductRequest request);
    Task<ApiResponse<bool>> UpdateAsync(UpdateProductRequest request);
    Task<ApiResponse<bool>> UpdateStockAsync(int id, int quantity);
    Task<ApiResponse<bool>> DeleteAsync(int id);
}

// ==================== 物流管理服务 ====================

public interface IDeliveryPersonService
{
    Task<ApiResponse<PagedResult<DeliveryPersonListItem>>> GetListAsync(PagedRequest request, int? branchId = null, DeliveryPersonStatus? status = null);
    Task<ApiResponse<DeliveryPersonListItem>> GetByIdAsync(int id);
    Task<ApiResponse<List<DeliveryPersonListItem>>> GetAvailableAsync(int branchId);
    Task<ApiResponse<int>> CreateAsync(CreateDeliveryPersonRequest request);
    Task<ApiResponse<bool>> UpdateAsync(UpdateDeliveryPersonRequest request);
    Task<ApiResponse<bool>> UpdateLoadAsync(int id, int currentLoad);
    Task<ApiResponse<bool>> DeleteAsync(int id);
}

public interface IOrderDistributionService
{
    Task<ApiResponse<List<OrderListItem>>> GetPendingOrdersAsync(int branchId);
    Task<ApiResponse<bool>> ManualAssignAsync(int orderId, int deliveryPersonId, int assignedById);
    Task<ApiResponse<bool>> AutoAssignAsync(int branchId, string algorithm = "region_load_distance");
    Task<ApiResponse<Dictionary<int, int>>> GetOptimalAssignmentAsync(int branchId);
}

// ==================== 结算服务 ====================

public interface ISettlementService
{
    Task<ApiResponse<PagedResult<SettlementListItem>>> GetListAsync(PagedRequest request, int? branchId = null);
    Task<ApiResponse<SettlementDetailDto>> GetByIdAsync(int id);
    Task<ApiResponse<SettlementPreviewDto>> PreviewAsync(CreateSettlementRequest request);
    Task<ApiResponse<int>> CreateAsync(CreateSettlementRequest request, int confirmedById);
    Task<ApiResponse<string>> GeneratePdfAsync(int settlementId);
    Task<ApiResponse<string>> DownloadPdfAsync(int settlementId);
    Task<ApiResponse<string>> BatchDownloadPdfAsync(List<int> settlementIds);
}

// ==================== 工作计划服务 ====================

public interface IWorkScheduleService
{
    Task<ApiResponse<PagedResult<WorkScheduleListItem>>> GetListAsync(PagedRequest request, int? employeeId = null);
    Task<ApiResponse<CalendarScheduleItem>> GetCalendarAsync(EmployeeScheduleCalendarRequest request);
    Task<ApiResponse<int>> CreateAsync(CreateWorkScheduleRequest request, int createdById);
    Task<ApiResponse<bool>> BatchCreateAsync(BatchCreateWorkScheduleRequest request, int createdById);
    Task<ApiResponse<bool>> UpdateAsync(int id, CreateWorkScheduleRequest request, int modifiedById);
    Task<ApiResponse<bool>> DeleteAsync(int id);
    Task<ApiResponse<bool>> NotifyEmployeeAsync(int scheduleId);
}

public interface IWorkPlanService
{
    Task<ApiResponse<PagedResult<WorkPlanListItem>>> GetListAsync(PagedRequest request, int? employeeId = null);
    Task<ApiResponse<DailyPlanView>> GetDailyPlanAsync(int employeeId, DateTime date);
    Task<ApiResponse<List<WorkPlanDto>>> GetByDateRangeAsync(int employeeId, DateTime startDate, DateTime endDate);
    Task<ApiResponse<int>> CreateAsync(CreateWorkPlanRequest request);
    Task<ApiResponse<bool>> UpdateAsync(UpdateWorkPlanRequest request);
    Task<ApiResponse<bool>> DeleteAsync(int id);
    Task<ApiResponse<bool>> UpdateExecutionStatusAsync(int id, string status, string? remark);
}

public interface IPlanDraftService
{
    Task<ApiResponse<List<PlanDraftDto>>> GetListAsync(int createdById);
    Task<ApiResponse<int>> SaveAsync(SavePlanDraftRequest request, int createdById);
    Task<ApiResponse<bool>> DeleteAsync(int id);
    Task<ApiResponse<bool>> PublishAsync(int draftId, int publishedById);
}

// ==================== 权限服务 ====================

public interface IPermissionService
{
    Task<ApiResponse<List<PermissionTreeNode>>> GetPermissionTreeAsync();
    Task<ApiResponse<RolePermissionConfig>> GetRolePermissionsAsync(int roleId);
    Task<ApiResponse<bool>> UpdateRolePermissionsAsync(UpdateRolePermissionRequest request);
    Task<ApiResponse<List<string>>> GetUserPermissionsAsync(int employeeId);
    Task<ApiResponse<bool>> HasPermissionAsync(int employeeId, string permissionCode);
    Task<ApiResponse<bool>> HasFieldPermissionAsync(int employeeId, string entity, string field, PermissionType permissionType);
}

public interface IRoleService
{
    Task<ApiResponse<List<Role>>> GetAllAsync();
    Task<ApiResponse<Role>> GetByIdAsync(int id);
    Task<ApiResponse<int>> CreateAsync(string name, string code, RoleType roleType, string? description);
    Task<ApiResponse<bool>> UpdateAsync(int id, string name, string? description);
    Task<ApiResponse<bool>> DeleteAsync(int id);
}

// ==================== 系统配置服务 ====================

public interface ISystemSettingService
{
    Task<ApiResponse<LocalSettingDto>> GetLocalSettingsAsync(int employeeId);
    Task<ApiResponse<bool>> SaveLocalSettingsAsync(SaveLocalSettingRequest request, int employeeId);
    Task<ApiResponse<WeChatConfigDto>> GetWeChatConfigAsync();
    Task<ApiResponse<bool>> SaveWeChatConfigAsync(SaveWeChatConfigRequest request);
    Task<ApiResponse<bool>> TestWeChatConnectionAsync();
    Task<ApiResponse<SyncConfigDto>> GetSyncConfigAsync();
    Task<ApiResponse<bool>> SaveSyncConfigAsync(SyncConfigDto config);
}

public interface IWebhookService
{
    Task<ApiResponse<PagedResult<WebhookListItem>>> GetListAsync(PagedRequest request);
    Task<ApiResponse<int>> CreateAsync(string name, string webhookUrl, string? triggerCondition, string? remark);
    Task<ApiResponse<bool>> UpdateAsync(int id, string name, string webhookUrl, string? triggerCondition, string? remark, bool isEnabled);
    Task<ApiResponse<bool>> DeleteAsync(int id);
    Task<ApiResponse<bool>> TestAsync(int id);
}

public interface IBackupService
{
    Task<ApiResponse<PagedResult<BackupRecordDto>>> GetListAsync(PagedRequest request);
    Task<ApiResponse<bool>> CreateManualBackupAsync();
    Task<ApiResponse<bool>> RestoreAsync(int backupId);
    Task<ApiResponse<bool>> DeleteAsync(int backupId);
    Task<ApiResponse<bool>> CleanupExpiredBackupsAsync();
}

public interface IOperationLogService
{
    Task<ApiResponse<PagedResult<OperationLogDto>>> GetListAsync(PagedRequest request, int? moduleId = null);
    Task<ApiResponse<bool>> CreateAsync(int operatorId, string module, string operationType, string content, string? entityType = null, int? entityId = null, string? result = null, string? errorMessage = null);
    Task<ApiResponse<int>> SyncPendingLogsAsync();
}

// ==================== 数据同步服务 ====================

public interface ISyncService
{
    Task<ApiResponse<SyncResult>> SyncAllAsync(CancellationToken ct = default);
    Task<ApiResponse<SyncResult>> SyncEntityAsync<T>(int id, CancellationToken ct = default) where T : class;
    Task<ApiResponse<bool>> SyncPendingChangesAsync(CancellationToken ct = default);
    Task<ApiResponse<bool>> SyncFromRemoteAsync(string tableName, CancellationToken ct = default);
    Task<ApiResponse<bool>> ResolveConflictAsync(int syncRecordId, string resolution);
    Task<ApiResponse<SyncStatusInfo>> GetSyncStatusAsync();
}

public class SyncResult
{
    public bool Success { get; set; }
    public int UploadedCount { get; set; }
    public int DownloadedCount { get; set; }
    public int ConflictCount { get; set; }
    public int FailedCount { get; set; }
    public TimeSpan Duration { get; set; }
    public string? ErrorMessage { get; set; }
}

public class SyncStatusInfo
{
    public bool IsOnline { get; set; }
    public DateTime? LastSyncTime { get; set; }
    public SyncStatus Status { get; set; }
    public int PendingCount { get; set; }
    public int ConflictCount { get; set; }
}

// ==================== 企业微信服务 ====================

public interface IWeChatService
{
    Task<ApiResponse<bool>> SyncOrganizationAsync();
    Task<ApiResponse<bool>> SyncEmployeeAsync(int employeeId);
    Task<ApiResponse<bool>> SyncCustomerAsync(int customerId);
    Task<ApiResponse<bool>> SendMessageAsync(string toUser, string content, string? agentId = null);
    Task<ApiResponse<bool>> SendTemplateMessageAsync(string toUser, string templateId, Dictionary<string, string> data);
    Task<ApiResponse<WeChatUserInfo>> GetUserInfoAsync(string code);
    Task<ApiResponse<bool>> PullNewCustomersAsync();
    Task<ApiResponse<List<WeChatSyncLogDto>>> GetSyncLogsAsync(int days = 7);

    // ---- SCRM 扩展 API ----

    /// <summary>获取企业微信客户标签</summary>
    Task<ApiResponse<List<WeChatTagDto>>> GetTagsAsync();
    /// <summary>创建企业微信标签</summary>
    Task<ApiResponse<bool>> CreateTagAsync(string groupName, string tagName);
    /// <summary>为客户打企业微信标签</summary>
    Task<ApiResponse<bool>> TagCustomerAsync(string externalUserId, List<string> tagIds);
    /// <summary>取消客户标签</summary>
    Task<ApiResponse<bool>> UntagCustomerAsync(string externalUserId, List<string> tagIds);

    /// <summary>获取客户群列表</summary>
    Task<ApiResponse<List<WeChatGroupChatDto>>> GetGroupChatsAsync(int offset = 0, int limit = 100);
    /// <summary>获取客户群详情</summary>
    Task<ApiResponse<WeChatGroupChatDetail>> GetGroupChatDetailAsync(string chatId);

    /// <summary>创建「联系我」二维码</summary>
    Task<ApiResponse<string>> CreateContactWayAsync(int type, string userId, string? remark = null);
    /// <summary>更新「联系我」配置</summary>
    Task<ApiResponse<bool>> UpdateContactWayAsync(string configId, string? remark = null);
    /// <summary>删除「联系我」</summary>
    Task<ApiResponse<bool>> DeleteContactWayAsync(string configId);

    /// <summary>创建群发任务</summary>
    Task<ApiResponse<string>> CreateMassMessageAsync(string content, string? tagIds = null, bool sendToAll = false);
    /// <summary>查询群发结果</summary>
    Task<ApiResponse<MassMessageResult>> GetMassMessageResultAsync(string msgId);

    /// <summary>获取员工离职客户列表</summary>
    Task<ApiResponse<List<string>>> GetUnassignedCustomersAsync(int offset = 0, int limit = 100);
    /// <summary>分配离职客户</summary>
    Task<ApiResponse<bool>> TransferCustomerAsync(string externalUserId, string handoverUserId, string takeoverUserId);
}

/// <summary>企业微信标签</summary>
public class WeChatTagDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string GroupName { get; set; } = "";
    public int Type { get; set; }
}

/// <summary>客户群</summary>
public class WeChatGroupChatDto
{
    public string ChatId { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Owner { get; set; }
    public int MemberCount { get; set; }
    public int MaxMemberCount { get; set; }
    public DateTime CreateTime { get; set; }
}

/// <summary>客户群详情</summary>
public class WeChatGroupChatDetail
{
    public string ChatId { get; set; } = "";
    public string Name { get; set; } = "";
    public List<GroupChatMember> Members { get; set; } = new();
    public int MemberCount { get; set; }
}

public class GroupChatMember
{
    public string UserId { get; set; } = "";
    public string? Name { get; set; }
    public string Type { get; set; } = "";
    public long JoinTime { get; set; }
}

/// <summary>群发结果</summary>
public class MassMessageResult
{
    public string MsgId { get; set; } = "";
    public string Status { get; set; } = "";
    public int TotalCount { get; set; }
    public int SentCount { get; set; }
    public int FailedCount { get; set; }
}

public class WeChatUserInfo
{
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Avatar { get; set; }
    public string? Phone { get; set; }
}

public class WeChatSyncLogDto
{
    public int Id { get; set; }
    public string SyncType { get; set; } = string.Empty;
    public string SyncDirection { get; set; } = string.Empty;
    public int RecordCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime SyncTime { get; set; }
}
