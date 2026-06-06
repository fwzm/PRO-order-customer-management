using PRO.Domain.Enums;

namespace PRO.Application.DTOs;

// ==================== 通用响应 ====================

/// <summary>
/// 统一API响应
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public List<string> Errors { get; set; } = new();
    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public static ApiResponse<T> Ok(T data, string message = "操作成功") => new()
    {
        Success = true,
        Data = data,
        Message = message
    };

    public static ApiResponse<T> Fail(string message, List<string>? errors = null) => new()
    {
        Success = false,
        Message = message,
        Errors = errors ?? new List<string>()
    };
}

/// <summary>
/// 分页请求
/// </summary>
public class PagedRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 100;
    public string? Keyword { get; set; }
    public string? SortField { get; set; }
    public string? SortOrder { get; set; } // asc/desc
}

/// <summary>
/// 分页响应
/// </summary>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount * 1.0 / PageSize);
    public bool HasPrevious => PageIndex > 1;
    public bool HasNext => PageIndex < TotalPages;
}

// ==================== 登录相关 ====================

/// <summary>
/// 登录请求
/// </summary>
public class LoginRequest
{
    public string EmployeeNo { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public LoginMethod LoginMethod { get; set; } = LoginMethod.AccountPassword;
    public string? WeChatCode { get; set; }
}

/// <summary>
/// 登录响应
/// </summary>
public class LoginResponse
{
    public int EmployeeId { get; set; }
    public string EmployeeNo { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public RoleType RoleType { get; set; }
    public string Token { get; set; } = string.Empty;
    public List<string> Permissions { get; set; } = new();
}

/// <summary>
/// 修改密码请求
/// </summary>
public class ChangePasswordRequest
{
    public int EmployeeId { get; set; }
    public string OldPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

// ==================== 组织架构相关 ====================

/// <summary>
/// 部门树节点
/// </summary>
public class DepartmentTreeNode
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public int? ParentId { get; set; }
    public int ManagerId { get; set; }
    public string? ManagerName { get; set; }
    public int SortOrder { get; set; }
    public List<DepartmentTreeNode> Children { get; set; } = new();
}

/// <summary>
/// 员工列表项
/// </summary>
public class EmployeeListItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string EmployeeNo { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public int DepartmentId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public EmployeeStatus Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 创建员工请求
/// </summary>
public class CreateEmployeeRequest
{
    public string Name { get; set; } = string.Empty;
    public string EmployeeNo { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int DepartmentId { get; set; }
    public int BranchId { get; set; }
    public int RoleId { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public Gender Gender { get; set; }
}

/// <summary>
/// 更新员工请求
/// </summary>
public class UpdateEmployeeRequest
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DepartmentId { get; set; }
    public int RoleId { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public Gender Gender { get; set; }
    public EmployeeStatus Status { get; set; }
}

/// <summary>
/// 分公司列表项
/// </summary>
public class BranchListItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public int? RegionId { get; set; }
    public EntityStatus Status { get; set; }
    public int DepartmentCount { get; set; }
    public int EmployeeCount { get; set; }
}

// ==================== 客户相关 ====================

/// <summary>
/// 客户列表项
/// </summary>
public class CustomerListItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CustomerNo { get; set; } = string.Empty;
    public CustomerType CustomerType { get; set; }
    public string CustomerTypeName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public int? ParentCustomerId { get; set; }
    public string? ParentCustomerName { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public CustomerStatus Status { get; set; }
    public int OrderCount { get; set; }
    public decimal TotalOrderAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CustomerManagerName { get; set; }
    public string? CreatorName { get; set; }
}

/// <summary>
/// 客户详情
/// </summary>
public class CustomerDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CustomerNo { get; set; } = string.Empty;
    public CustomerType CustomerType { get; set; }
    public string CustomerTypeName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public double? Longitude { get; set; }
    public double? Latitude { get; set; }
    public string? LegalPerson { get; set; }
    public string? RegisterAddress { get; set; }
    public int? ParentCustomerId { get; set; }
    public string? ParentCustomerName { get; set; }
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string? WeChatCustomerId { get; set; }
    public CustomerStatus Status { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedAt { get; set; }
    public int CreatedById { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public string CustomerManagerName { get; set; } = string.Empty;
    public List<CustomerListItem> SubCustomers { get; set; } = new();
    public List<OrderListItem> RecentOrders { get; set; } = new();
}

/// <summary>
/// 创建客户请求
/// </summary>
public class CreateCustomerRequest
{
    public string Name { get; set; } = string.Empty;
    public CustomerType CustomerType { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public double? Longitude { get; set; }
    public double? Latitude { get; set; }
    public string? LegalPerson { get; set; }
    public string? RegisterAddress { get; set; }
    public int? ParentCustomerId { get; set; }
    public int BranchId { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 更新客户请求
/// </summary>
public class UpdateCustomerRequest
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public CustomerType CustomerType { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public double? Longitude { get; set; }
    public double? Latitude { get; set; }
    public string? LegalPerson { get; set; }
    public string? RegisterAddress { get; set; }
    public int? ParentCustomerId { get; set; }
    public int BranchId { get; set; }
    public CustomerStatus Status { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 客户查重结果
/// </summary>
public class CustomerDuplicateCheckResult
{
    public bool HasDuplicates { get; set; }
    public List<CustomerDuplicateItem> Duplicates { get; set; } = new();
}

/// <summary>
/// 客户重复项
/// </summary>
public class CustomerDuplicateItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? LegalPerson { get; set; }
    public string MatchType { get; set; } = string.Empty; // phone/name_address/legal_phone
    public double Similarity { get; set; }
}

/// <summary>
/// 客户合并请求
/// </summary>
public class MergeCustomerRequest
{
    public int MainCustomerId { get; set; }
    public List<int> MergedCustomerIds { get; set; } = new();
}
