using PRO.Domain.Enums;

namespace PRO.Domain.Entities;

/// <summary>
/// 商圈
/// </summary>
public class BusinessDistrict
{
    public int Id { get; set; }

    /// <summary>商圈名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>所在城市</summary>
    public string City { get; set; } = string.Empty;

    /// <summary>所属分公司ID</summary>
    public int? BranchId { get; set; }

    /// <summary>状态</summary>
    public string Status { get; set; } = "Active";

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // 导航属性
    public virtual Branch? Branch { get; set; }
    public virtual ICollection<Customer> Customers { get; set; } = [];
}

/// <summary>
/// 企业微信客户（从企业微信同步过来的客户）
/// </summary>
public class WeChatCustomer : ISyncable
{
    public int Id { get; set; }

    /// <summary>企业微信ExternalUserId</summary>
    public string ExternalUserId { get; set; } = string.Empty;

    /// <summary>客户名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>添加人微信UserId</summary>
    public string? AddUserId { get; set; }

    /// <summary>添加人姓名</summary>
    public string? AddUserName { get; set; }

    /// <summary>添加人部门ID（企业微信部门ID）</summary>
    public long? AddUserDepartmentId { get; set; }

    /// <summary>添加人部门名称</summary>
    public string? AddUserDepartmentName { get; set; }

    /// <summary>系统客户编号（从企业微信描述中解析）</summary>
    public string? CustomerNoInDesc { get; set; }

    /// <summary>状态：Unlinked/已同步未关联, Linked/已关联, Unidentifiable/无法识别</summary>
    public string Status { get; set; } = "Unlinked";

    /// <summary>关联的系统客户ID</summary>
    public int? LinkedCustomerId { get; set; }

    /// <summary>自动归属的分公司ID</summary>
    public int? AssignedBranchId { get; set; }

    /// <summary>自动归属的分公司名称</summary>
    public string? AssignedBranchName { get; set; }

    /// <summary>企业微信头像URL</summary>
    public string? AvatarUrl { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>更新时间</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    /// <summary>本地更新时间戳</summary>
    public long LocalTimestamp { get; set; }

    /// <summary>远程更新时间戳</summary>
    public long? RemoteTimestamp { get; set; }

    /// <summary>同步状态</summary>
    public SyncStatus SyncStatus { get; set; } = SyncStatus.Synced;

    // 导航属性
    public virtual Customer? LinkedCustomer { get; set; }
    public virtual Branch? AssignedBranch { get; set; }
}

/// <summary>
/// 商机实体
/// </summary>
public class Opportunity
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public int BranchId { get; set; }
    public int DeveloperId { get; set; }  // 开发担当
    public OpportunityStage Stage { get; set; } = OpportunityStage.Trial;
    public DateTime? ExpectedCloseDate { get; set; }
    public decimal? ExpectedAmount { get; set; }
    public string? Requirements { get; set; }    // 客户需求
    public string? TrialProducts { get; set; }   // 试用产品（JSON product id array）
    public string? IntendedProducts { get; set; } // 意向产品
    public string? CompetitorInfo { get; set; }    // 竞品信息
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public virtual Customer? Customer { get; set; }
    public virtual Branch? Branch { get; set; }
    public virtual Employee? Developer { get; set; }
}

/// <summary>
/// 企业微信智能表格同步的拜访记录（未关联客户前暂存）
/// </summary>
public class WeChatVisitRecord
{
    public int Id { get; set; }
    public string SourceRecordId { get; set; } = string.Empty;  // 智能表格 record_id
    public string? CustomerName { get; set; }
    public string? VisitorName { get; set; }
    public DateTime? VisitDate { get; set; }
    public string? VisitType { get; set; }
    public string? Content { get; set; }
    public string? Purpose { get; set; }
    public string? CustomerDemand { get; set; }
    public string? NextAction { get; set; }
    public string? RawData { get; set; }  // JSON原始数据
    public string Status { get; set; } = "Unlinked";  // Unlinked/Linked
    public int? LinkedCustomerId { get; set; }
    public DateTime SyncedAt { get; set; } = DateTime.Now;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public virtual Customer? LinkedCustomer { get; set; }
}

/// <summary>
/// 拜访记录
/// </summary>
public class VisitRecord
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public int VisitorId { get; set; }
    public DateTime VisitDate { get; set; } = DateTime.Now;
    public string VisitType { get; set; } = "电话";  // 电话/微信/上门拜访
    public string? Purpose { get; set; }
    public string? Content { get; set; }     // 拜访内容
    public string? CustomerDemand { get; set; } // 客户需求
    public string? CustomerProfile { get; set; } // 客户画像更新
    public string? Result { get; set; }      // 拜访结果
    public string? NextAction { get; set; }  // 下次行动计划
    public DateTime? NextVisitDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public virtual Customer? Customer { get; set; }
    public virtual Employee? Visitor { get; set; }
}
