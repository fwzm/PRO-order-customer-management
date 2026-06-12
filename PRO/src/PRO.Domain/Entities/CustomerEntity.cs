using PRO.Domain.Enums;

namespace PRO.Domain.Entities;

/// <summary>
/// 客户实体
/// </summary>
public class Customer : ISyncable
{
    public int Id { get; set; }

    /// <summary>客户名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>客户编号（格式：K+年月日+分公司编号+4位递增）</summary>
    public string CustomerNo { get; set; } = string.Empty;

    /// <summary>客户类型：大客户/细分客户</summary>
    public CustomerType CustomerType { get; set; } = CustomerType.Sub;

    /// <summary>手机号</summary>
    public string? Phone { get; set; }

    /// <summary>省份</summary>
    public string? Province { get; set; }

    /// <summary>城市</summary>
    public string? City { get; set; }

    /// <summary>区/县</summary>
    public string? District { get; set; }

    /// <summary>商圈ID</summary>
    public int? BusinessDistrictId { get; set; }

    /// <summary>详细地址</summary>
    public string? Address { get; set; }

    /// <summary>完整地址（省+市+区+详细地址拼接）</summary>
    public string? FullAddress { get; set; }

    /// <summary>经度</summary>
    public double? Longitude { get; set; }

    /// <summary>纬度</summary>
    public double? Latitude { get; set; }

    /// <summary>法人</summary>
    public string? LegalPerson { get; set; }

    /// <summary>注册地址</summary>
    public string? RegisterAddress { get; set; }

    /// <summary>所属大客户ID（细分客户关联）</summary>
    public int? ParentCustomerId { get; set; }

    /// <summary>所属分公司ID</summary>
    public int BranchId { get; set; }

    /// <summary>企业微信客户ID</summary>
    public string? WeChatCustomerId { get; set; }

    /// <summary>企业微信ExternalUserId</summary>
    public string? WeChatExternalUserId { get; set; }

    /// <summary>状态</summary>
    public CustomerStatus Status { get; set; } = CustomerStatus.Active;

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>创建人ID（也是开发担当）</summary>
    public int CreatedById { get; set; }

    /// <summary>客户担当ID（负责该客户的业务员）</summary>
    public int? CustomerManagerId { get; set; }

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
    public virtual Customer? ParentCustomer { get; set; }
    public virtual BusinessDistrict? BusinessDistrict { get; set; }
    public virtual Employee? CustomerManager { get; set; }
    public virtual ICollection<Customer> SubCustomers { get; set; } = [];
    public virtual ICollection<Order> Orders { get; set; } = [];
    public virtual ICollection<VisitRecord> VisitRecords { get; set; } = [];
    public virtual ICollection<Opportunity> Opportunities { get; set; } = [];
    public virtual ICollection<CustomerPrice> CustomerPrices { get; set; } = [];
    public virtual ICollection<CustomerTag> CustomerTags { get; set; } = [];
    public virtual Employee? Creator { get; set; }
}

/// <summary>
/// 客户标签
/// </summary>
public class CustomerTag
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#007AFF";
    public int? BranchId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public virtual Branch? Branch { get; set; }
    public virtual ICollection<Customer> Customers { get; set; } = [];
}

/// <summary>
/// 客户专属价格
/// </summary>
public class CustomerPrice
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public int ProductId { get; set; }
    public decimal Price { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public virtual Customer? Customer { get; set; }
    public virtual Product? Product { get; set; }
}

/// <summary>
/// 数量阶梯价格
/// </summary>
public class VolumePrice
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int MinQuantity { get; set; }
    public decimal Price { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public virtual Product? Product { get; set; }
}
