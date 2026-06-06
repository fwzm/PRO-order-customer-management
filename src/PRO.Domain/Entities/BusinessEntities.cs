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
    public virtual ICollection<Customer> SubCustomers { get; set; } = new List<Customer>();
    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
    public virtual ICollection<VisitRecord> VisitRecords { get; set; } = new List<VisitRecord>();
    public virtual ICollection<Opportunity> Opportunities { get; set; } = new List<Opportunity>();
    public virtual ICollection<CustomerPrice> CustomerPrices { get; set; } = new List<CustomerPrice>();
    public virtual ICollection<CustomerTag> CustomerTags { get; set; } = new List<CustomerTag>();
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
    public virtual ICollection<Customer> Customers { get; set; } = new List<Customer>();
}

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
    public virtual ICollection<Customer> Customers { get; set; } = new List<Customer>();
}

public class Product : ISyncable
{
    public int Id { get; set; }
    
    /// <summary>SKU编码（唯一）</summary>
    public string SKU { get; set; } = string.Empty;
    
    /// <summary>产品名称</summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>规格</summary>
    public string? Specification { get; set; }

    /// <summary>销售均价（根据已完成订单自动计算）</summary>
    public decimal AverageSalePrice { get; set; }

    /// <summary>参考价（普通用户可见）</summary>
    public decimal? ReferencePrice { get; set; }
    
    /// <summary>库存数量</summary>
    public int Stock { get; set; }
    
    /// <summary>单位</summary>
    public string? Unit { get; set; }
    
    /// <summary>产品分类ID</summary>
    public int? CategoryId { get; set; }

    public int? WarehouseId { get; set; }
    
    /// <summary>状态</summary>
    public ProductStatus Status { get; set; } = ProductStatus.Active;
    
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
    public SyncStatus SyncStatus { get; set; } = SyncStatus.Pending;
    
    // 导航属性
    public virtual ProductCategory? Category { get; set; }
    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}

/// <summary>
/// 产品分类
/// </summary>
public class ProductCategory
{
    public int Id { get; set; }
    
    /// <summary>分类名称</summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>父级分类ID</summary>
    public int? ParentId { get; set; }
    
    /// <summary>排序号</summary>
    public int SortOrder { get; set; }
    
    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    // 导航属性
    public virtual ProductCategory? Parent { get; set; }
    public virtual ICollection<ProductCategory> Children { get; set; } = new List<ProductCategory>();
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}

public class Order : ISyncable
{
    public int Id { get; set; }
    
    /// <summary>订单号</summary>
    public string OrderNo { get; set; } = string.Empty;
    
    /// <summary>客户ID</summary>
    public int CustomerId { get; set; }
    
    /// <summary>分公司ID</summary>
    public int BranchId { get; set; }
    
    /// <summary>创建人ID</summary>
    public int CreatedById { get; set; }
    
    /// <summary>配送员ID</summary>
    public int? DeliveryPersonId { get; set; }
    
    /// <summary>订单状态</summary>
    public OrderStatus Status { get; set; } = OrderStatus.Draft;
    
    /// <summary>收款状态</summary>
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;
    
    /// <summary>总金额</summary>
    public decimal TotalAmount { get; set; }
    
    /// <summary>收款金额</summary>
    public decimal ReceivedAmount { get; set; }

    /// <summary>优惠金额</summary>
    public decimal DiscountAmount { get; set; }
    
    /// <summary>配送地址</summary>
    public string? DeliveryAddress { get; set; }
    
    /// <summary>配送经度</summary>
    public double? DeliveryLongitude { get; set; }
    
    /// <summary>配送纬度</summary>
    public double? DeliveryLatitude { get; set; }
    
    /// <summary>配送时间</summary>
    public DateTime? DeliveryTime { get; set; }
    
    /// <summary>签收时间</summary>
    public DateTime? SignedTime { get; set; }
    
    /// <summary>取消原因</summary>
    public string? CancelReason { get; set; }
    
    /// <summary>备注</summary>
    public string? Remark { get; set; }
    
    /// <summary>草稿自动转正时间</summary>
    public DateTime? DraftExpireTime { get; set; }
    
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
    
    /// <summary>结算ID（结算后关联）</summary>
    public int? SettlementId { get; set; }
    
    // 导航属性
    public virtual Customer? Customer { get; set; }
    public virtual Branch? Branch { get; set; }
    public virtual Employee? Creator { get; set; }
    public virtual DeliveryPerson? DeliveryPerson { get; set; }
    public virtual Settlement? Settlement { get; set; }
    public virtual ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public virtual ICollection<OrderModificationRecord> ModificationRecords { get; set; } = new List<OrderModificationRecord>();
}

/// <summary>
/// 订单明细
/// </summary>
public class OrderItem
{
    public int Id { get; set; }
    
    /// <summary>订单ID</summary>
    public int OrderId { get; set; }
    
    /// <summary>产品ID</summary>
    public int ProductId { get; set; }
    
    /// <summary>数量</summary>
    public int Quantity { get; set; }
    
    /// <summary>单价</summary>
    public decimal UnitPrice { get; set; }
    
    /// <summary>金额</summary>
    public decimal Amount { get; set; }

    /// <summary>优惠类型（Discount/PriceChange）</summary>
    public string? DiscountType { get; set; }

    /// <summary>优惠值（折扣率0-1或改价金额）</summary>
    public decimal DiscountValue { get; set; }
    
    /// <summary>备注</summary>
    public string? Remark { get; set; }
    
    // 导航属性
    public virtual Order? Order { get; set; }
    public virtual Product? Product { get; set; }
}

/// <summary>
/// 订单修改记录
/// </summary>
public class OrderModificationRecord
{
    public int Id { get; set; }
    
    /// <summary>订单ID</summary>
    public int OrderId { get; set; }
    
    /// <summary>修改人ID</summary>
    public int ModifiedById { get; set; }
    
    /// <summary>修改时间</summary>
    public DateTime ModifiedAt { get; set; } = DateTime.Now;
    
    /// <summary>修改内容（JSON）</summary>
    public string Content { get; set; } = string.Empty;
    
    /// <summary>修改类型</summary>
    public string ModificationType { get; set; } = string.Empty;
    
    // 导航属性
    public virtual Order? Order { get; set; }
    public virtual Employee? ModifiedBy { get; set; }
}

/// <summary>
/// 配送员实体
/// </summary>
public class DeliveryPerson : ISyncable
{
    public int Id { get; set; }
    
    /// <summary>姓名</summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>手机号</summary>
    public string Phone { get; set; } = string.Empty;
    
    /// <summary>分公司ID</summary>
    public int BranchId { get; set; }
    
    /// <summary>服务区域</summary>
    public string? ServiceArea { get; set; }
    
    /// <summary>车辆牌号</summary>
    public string? VehicleNumber { get; set; }
    
    /// <summary>微信号</summary>
    public string? WeChatId { get; set; }
    
    /// <summary>当前负载（当前配送订单数）</summary>
    public int CurrentLoad { get; set; }
    
    /// <summary>最大负载</summary>
    public int MaxLoad { get; set; } = 10;
    
    /// <summary>状态</summary>
    public DeliveryPersonStatus Status { get; set; } = DeliveryPersonStatus.Available;
    
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
    public SyncStatus SyncStatus { get; set; } = SyncStatus.Pending;
    
    // 导航属性
    public virtual Branch? Branch { get; set; }
    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}

/// <summary>
/// 结算实体
/// </summary>
public class Settlement : ISyncable
{
    public int Id { get; set; }
    
    /// <summary>结算单号</summary>
    public string SettlementNo { get; set; } = string.Empty;
    
    /// <summary>开始时间</summary>
    public DateTime StartDate { get; set; }
    
    /// <summary>结束时间</summary>
    public DateTime EndDate { get; set; }
    
    /// <summary>分公司ID</summary>
    public int BranchId { get; set; }
    
    /// <summary>确认人ID</summary>
    public int ConfirmedById { get; set; }
    
    /// <summary>订单总数</summary>
    public int OrderCount { get; set; }
    
    /// <summary>订单总金额</summary>
    public decimal TotalAmount { get; set; }
    
    /// <summary>收款总金额</summary>
    public decimal ReceivedAmount { get; set; }
    
    /// <summary>未收款金额</summary>
    public decimal UnpaidAmount { get; set; }
    
    /// <summary>状态</summary>
    public SettlementStatus Status { get; set; } = SettlementStatus.Completed;
    
    /// <summary>备注</summary>
    public string? Remark { get; set; }
    
    /// <summary>PDF文件路径</summary>
    public string? PdfPath { get; set; }
    
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
    public virtual Employee? ConfirmedBy { get; set; }
    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
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

/// <summary>
/// 产品图片
/// </summary>
public class ProductImage
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public byte[] ImageData { get; set; } = Array.Empty<byte>();
    public bool IsPrimary { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public virtual Product? Product { get; set; }
}

/// <summary>
/// 盘点记录
/// </summary>
public class InventoryCheck
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public int ProductId { get; set; }
    public int SystemStock { get; set; }     // 系统库存
    public int ActualStock { get; set; }     // 实际库存
    public int Variance { get; set; }        // 差异
    public string? Remark { get; set; }
    public int CheckedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public virtual Branch? Branch { get; set; }
    public virtual Product? Product { get; set; }
    public virtual Employee? CheckedBy { get; set; }
}

public class Warehouse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public int BranchId { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public virtual Branch? Branch { get; set; }
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
