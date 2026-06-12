using PRO.Domain.Enums;

namespace PRO.Domain.Entities;

/// <summary>
/// 分公司实体
/// </summary>
public class Branch
{
    public int Id { get; set; }

    /// <summary>分公司名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>分公司编码</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>地址</summary>
    public string? Address { get; set; }

    /// <summary>联系电话</summary>
    public string? Phone { get; set; }

    /// <summary>区域ID（用于区域经理管辖）</summary>
    public int? RegionId { get; set; }

    /// <summary>所属总部ID</summary>
    public int? HeadquartersId { get; set; }

    /// <summary>状态</summary>
    public EntityStatus Status { get; set; } = EntityStatus.Active;

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>更新时间</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    /// <summary>本地更新时间戳</summary>
    public long LocalTimestamp { get; set; }

    /// <summary>远程更新时间戳</summary>
    public long? RemoteTimestamp { get; set; }

    /// <summary>同步状态</summary>
    public SyncStatus SyncStatus { get; set; } = SyncStatus.Pending;

    // 导航属性
    public virtual ICollection<Department> Departments { get; set; } = [];
    public virtual ICollection<Employee> Employees { get; set; } = [];
    public virtual ICollection<Customer> Customers { get; set; } = [];
    public virtual ICollection<Order> Orders { get; set; } = [];
    public virtual ICollection<DeliveryPerson> DeliveryPersons { get; set; } = [];
}

/// <summary>
/// 部门实体（支持树形结构）
/// </summary>
public class Department
{
    public int Id { get; set; }

    /// <summary>部门名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>部门编码</summary>
    public string? Code { get; set; }

    /// <summary>上级部门ID（顶级为null）</summary>
    public int? ParentId { get; set; }

    /// <summary>分公司ID</summary>
    public int BranchId { get; set; }

    /// <summary>负责人ID</summary>
    public int? ManagerId { get; set; }

    /// <summary>排序号</summary>
    public int SortOrder { get; set; }

    /// <summary>状态</summary>
    public EntityStatus Status { get; set; } = EntityStatus.Active;

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>更新时间</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    /// <summary>本地更新时间戳</summary>
    public long LocalTimestamp { get; set; }

    /// <summary>远程更新时间戳</summary>
    public long? RemoteTimestamp { get; set; }

    /// <summary>同步状态</summary>
    public SyncStatus SyncStatus { get; set; } = SyncStatus.Pending;

    // 导航属性
    public virtual Branch? Branch { get; set; }
    public virtual Department? Parent { get; set; }
    public virtual ICollection<Department> Children { get; set; } = [];
    public virtual ICollection<Employee> Employees { get; set; } = [];
}

/// <summary>
/// 员工实体
/// </summary>
public class Employee
{
    public int Id { get; set; }

    /// <summary>姓名</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>工号（登录凭证）</summary>
    public string EmployeeNo { get; set; } = string.Empty;

    /// <summary>密码哈希</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>连续登录失败次数</summary>
    public int LoginFailCount { get; set; }

    /// <summary>账号锁定截止时间</summary>
    public DateTime? LockedUntil { get; set; }

    /// <summary>密码最后修改时间（null表示从未修改过默认密码）</summary>
    public DateTime? PasswordChangedAt { get; set; }

    /// <summary>部门ID</summary>
    public int DepartmentId { get; set; }

    /// <summary>分公司ID</summary>
    public int BranchId { get; set; }

    /// <summary>区域ID（用于区域经理）</summary>
    public int? RegionId { get; set; }

    /// <summary>角色ID</summary>
    public int RoleId { get; set; }

    /// <summary>手机号</summary>
    public string? Phone { get; set; }

    /// <summary>邮箱</summary>
    public string? Email { get; set; }

    /// <summary>性别</summary>
    public Gender Gender { get; set; } = Gender.Unknown;

    /// <summary>状态</summary>
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;

    /// <summary>企业微信UserId</summary>
    public string? WeChatUserId { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>更新时间</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    /// <summary>本地更新时间戳</summary>
    public long LocalTimestamp { get; set; }

    /// <summary>远程更新时间戳</summary>
    public long? RemoteTimestamp { get; set; }

    /// <summary>同步状态</summary>
    public SyncStatus SyncStatus { get; set; } = SyncStatus.Pending;

    // 导航属性
    public virtual Department? Department { get; set; }
    public virtual Branch? Branch { get; set; }
    public virtual Role? Role { get; set; }
    public virtual ICollection<WorkSchedule> WorkSchedules { get; set; } = [];
    public virtual ICollection<WorkPlan> WorkPlans { get; set; } = [];
    public virtual ICollection<Order> CreatedOrders { get; set; } = [];
}

/// <summary>
/// 角色实体
/// </summary>
public class Role
{
    public int Id { get; set; }

    /// <summary>角色名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>角色编码</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>角色类型</summary>
    public RoleType RoleType { get; set; }

    /// <summary>描述</summary>
    public string? Description { get; set; }

    /// <summary>是否系统角色（不可删除）</summary>
    public bool IsSystem { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>更新时间</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    // 导航属性
    public virtual ICollection<Employee> Employees { get; set; } = [];
    public virtual ICollection<RolePermission> RolePermissions { get; set; } = [];
}

/// <summary>
/// 权限实体
/// </summary>
public class Permission
{
    public int Id { get; set; }

    /// <summary>权限编码</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>权限名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>所属模块</summary>
    public string Module { get; set; } = string.Empty;

    /// <summary>权限类型</summary>
    public PermissionType PermissionType { get; set; }

    /// <summary>父级权限ID</summary>
    public int? ParentId { get; set; }

    /// <summary>排序号</summary>
    public int SortOrder { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // 导航属性
    public virtual Permission? Parent { get; set; }
    public virtual ICollection<Permission> Children { get; set; } = [];
    public virtual ICollection<RolePermission> RolePermissions { get; set; } = [];
}

/// <summary>
/// 角色权限关联
/// </summary>
public class RolePermission
{
    public int Id { get; set; }

    public int RoleId { get; set; }

    public int PermissionId { get; set; }

    /// <summary>是否允许访问</summary>
    public bool IsAllowed { get; set; } = true;

    /// <summary>是否字段级权限</summary>
    public bool IsFieldLevel { get; set; }

    /// <summary>关联的字段（用于字段级权限）</summary>
    public string? FieldName { get; set; }

    // 导航属性
    public virtual Role? Role { get; set; }
    public virtual Permission? Permission { get; set; }
}
