using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
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
    public List<string> Errors { get; set; } = [];
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
        Errors = errors ?? []
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
    public List<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount * 1.0 / PageSize);
    public bool HasPrevious => PageIndex > 1;
    public bool HasNext => PageIndex < TotalPages;
}

// ==================== 登录相关 ====================

/// <summary>
/// 登录请求 — 使用 DataAnnotation 在模型绑定层提供字段校验
/// </summary>
public class LoginRequest
{
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "工号不能为空")]
    [System.ComponentModel.DataAnnotations.StringLength(50, MinimumLength = 1, ErrorMessage = "工号长度必须在1-50位之间")]
    public string EmployeeNo { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "密码不能为空")]
    [System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 6, ErrorMessage = "密码长度不能少于6位")]
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
    public List<string> Permissions { get; set; } = [];
}

/// <summary>
/// 修改密码请求 — 使用 DataAnnotation 校验密码强度
/// </summary>
public class ChangePasswordRequest
{
    [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue, ErrorMessage = "无效的员工ID")]
    public int EmployeeId { get; set; }

    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "原密码不能为空")]
    public string OldPassword { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "新密码不能为空")]
    [System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 8, ErrorMessage = "新密码长度不能少于8位")]
    [System.ComponentModel.DataAnnotations.RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$",
        ErrorMessage = "密码必须包含大写字母、小写字母和数字")]
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
    public List<DepartmentTreeNode> Children { get; set; } = [];
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
public class CustomerListItem : INotifyPropertyChanged
{
    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
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

    // 增强字段
    public DateTime? LastOrderDate { get; set; }
    public decimal ReceivableAmount { get; set; }
    public int PendingOrderCount { get; set; }
    public int DaysSinceLastOrder { get; set; }
    public bool IsInactive => DaysSinceLastOrder > 30;
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
    public List<CustomerListItem> SubCustomers { get; set; } = [];
    public List<OrderListItem> RecentOrders { get; set; } = [];
}

/// <summary>
/// 创建客户请求
/// </summary>
public class CreateCustomerRequest
{
    [Required(ErrorMessage = "客户名称不能为空")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "客户名称长度必须在2-100个字符之间")]
    public string Name { get; set; } = string.Empty;

    public CustomerType CustomerType { get; set; }

    [Phone(ErrorMessage = "手机号格式不正确")]
    [StringLength(20, ErrorMessage = "手机号不能超过20个字符")]
    public string? Phone { get; set; }

    [StringLength(500, ErrorMessage = "地址不能超过500个字符")]
    public string? Address { get; set; }

    public double? Longitude { get; set; }
    public double? Latitude { get; set; }

    [StringLength(50, ErrorMessage = "法人代表名称不能超过50个字符")]
    public string? LegalPerson { get; set; }

    [StringLength(500, ErrorMessage = "注册地址不能超过500个字符")]
    public string? RegisterAddress { get; set; }

    public int? ParentCustomerId { get; set; }

    [Required(ErrorMessage = "分公司ID不能为空")]
    [Range(1, int.MaxValue, ErrorMessage = "分公司ID必须大于0")]
    public int BranchId { get; set; }

    [StringLength(1000, ErrorMessage = "备注不能超过1000个字符")]
    public string? Remark { get; set; }

    // 省市区和商圈
    [StringLength(50)]
    public string? Province { get; set; }

    [StringLength(50)]
    public string? City { get; set; }

    [StringLength(50)]
    public string? District { get; set; }

    public int? BusinessDistrictId { get; set; }
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

    // 省市区和商圈
    public string? Province { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }
    public int? BusinessDistrictId { get; set; }
}

/// <summary>
/// 客户查重结果
/// </summary>
public class CustomerDuplicateCheckResult
{
    public bool HasDuplicates { get; set; }
    public List<CustomerDuplicateItem> Duplicates { get; set; } = [];
}

/// <summary>
/// 客户重复项
/// </summary>
public class CustomerDuplicateItem
{
    public int Id { get; set; }
    public int BranchId { get; set; }
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
    public List<int> MergedCustomerIds { get; set; } = [];
}

/// <summary>
/// 批量分配请求
/// </summary>
public class BulkAssignRequest
{
    public List<int> CustomerIds { get; set; } = [];
    public int BranchId { get; set; }
    public int? AssignToEmployeeId { get; set; }
}
