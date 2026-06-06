namespace PRO.Domain.Enums;

/// <summary>
/// 通用实体状态
/// </summary>
public enum EntityStatus
{
    /// <summary>活跃/启用</summary>
    Active = 1,
    
    /// <summary>非活跃/停用</summary>
    Inactive = 0
}

/// <summary>
/// 员工状态
/// </summary>
public enum EmployeeStatus
{
    /// <summary>在职</summary>
    Active = 1,
    
    /// <summary>离职</summary>
    Inactive = 0
}

/// <summary>
/// 角色类型
/// </summary>
public enum RoleType
{
    /// <summary>总部管理员</summary>
    HeadquartersAdmin = 1,
    
    /// <summary>分公司管理员</summary>
    BranchAdmin = 2,
    
    /// <summary>区域管理员</summary>
    RegionAdmin = 3,
    
    /// <summary>普通员工</summary>
    Employee = 4
}

/// <summary>
/// 权限类型
/// </summary>
public enum PermissionType
{
    /// <summary>查看</summary>
    View = 1,
    
    /// <summary>新增</summary>
    Create = 2,
    
    /// <summary>编辑</summary>
    Edit = 3,
    
    /// <summary>删除</summary>
    Delete = 4,
    
    /// <summary>导出</summary>
    Export = 5,
    
    /// <summary>打印</summary>
    Print = 6
}

/// <summary>
/// 同步状态
/// </summary>
public enum SyncStatus
{
    /// <summary>未同步</summary>
    Pending = 0,
    
    /// <summary>同步中</summary>
    Syncing = 1,
    
    /// <summary>已同步</summary>
    Synced = 2,
    
    /// <summary>有冲突</summary>
    Conflict = 3,
    
    /// <summary>同步失败</summary>
    Failed = 4
}

/// <summary>
/// 客户类型
/// </summary>
public enum CustomerType
{
    /// <summary>大客户</summary>
    Major = 1,
    
    /// <summary>细分客户</summary>
    Sub = 2
}

/// <summary>
/// 客户状态
/// </summary>
public enum CustomerStatus
{
    /// <summary>正常</summary>
    Active = 1,
    
    /// <summary>已合并</summary>
    Merged = 0,
    
    /// <summary>已删除</summary>
    Deleted = -1
}

/// <summary>
/// 订单状态
/// </summary>
public enum OrderStatus
{
    /// <summary>待分配</summary>
    Pending = 1,
    
    /// <summary>已分配</summary>
    Assigned = 2,
    
    /// <summary>配送中</summary>
    Delivering = 3,
    
    /// <summary>已完成</summary>
    Completed = 4,
    
    /// <summary>配送失败</summary>
    Failed = 5,
    
    /// <summary>已取消</summary>
    Cancelled = 6,
    
    /// <summary>草稿</summary>
    Draft = 0
}

/// <summary>
/// 收款状态
/// </summary>
public enum PaymentStatus
{
    /// <summary>未收款</summary>
    Unpaid = 0,
    
    /// <summary>部分收款</summary>
    PartialPaid = 1,
    
    /// <summary>已收款</summary>
    Paid = 2,
    
    /// <summary>移交法务处理</summary>
    Legal = 3
}

/// <summary>
/// 产品状态
/// </summary>
public enum ProductStatus
{
    /// <summary>上架</summary>
    Active = 1,
    
    /// <summary>下架</summary>
    Inactive = 0
}

/// <summary>
/// 配送员状态
/// </summary>
public enum DeliveryPersonStatus
{
    /// <summary>可用</summary>
    Available = 1,
    
    /// <summary>忙碌</summary>
    Busy = 2,
    
    /// <summary>休息</summary>
    Off = 0
}

/// <summary>
/// 结算状态
/// </summary>
public enum SettlementStatus
{
    /// <summary>已结算</summary>
    Completed = 1,
    
    /// <summary>已取消</summary>
    Cancelled = 0
}

/// <summary>
/// 关闭行为
/// </summary>
public enum CloseBehavior
{
    /// <summary>关闭最小化至托盘</summary>
    MinimizeToTray = 0,
    
    /// <summary>关闭即退出</summary>
    Exit = 1
}

/// <summary>
/// 冲突解决策略
/// </summary>
public enum ConflictResolution
{
    /// <summary>时间优先</summary>
    TimestampFirst = 0,
    
    /// <summary>总部优先</summary>
    HeadquartersFirst = 1,
    
    /// <summary>手动处理</summary>
    Manual = 2
}

/// <summary>
/// 性别
/// </summary>
public enum Gender
{
    /// <summary>未知</summary>
    Unknown = 0,
    
    /// <summary>男</summary>
    Male = 1,
    
    /// <summary>女</summary>
    Female = 2
}

/// <summary>
/// 登录方式
/// </summary>
public enum LoginMethod
{
    /// <summary>工号密码登录</summary>
    AccountPassword = 0,
    
    /// <summary>企业微信登录</summary>
    WeChatWork = 1
}

/// <summary>商机阶段</summary>
public enum OpportunityStage
{
    Trial,          // 试用
    Communication,  // 沟通
    Quotation,      // 报价
    Negotiation,    // 谈判
    Won,            // 成交
    Lost            // 流失
}

/// <summary>盘点状态</summary>
public enum InventoryCheckStatus
{
    InProgress,     // 盘点中
    Completed,      // 已完成
    Confirmed       // 已确认
}
