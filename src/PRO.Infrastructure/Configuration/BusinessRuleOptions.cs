namespace PRO.Infrastructure.Configuration;

/// <summary>
/// 业务规则配置 - 集中管理所有业务阈值和规则
/// </summary>
public class BusinessRuleOptions
{
    /// <summary>配置节名称</summary>
    public const string SectionName = "BusinessRules";

    // ==================== 订单相关 ====================

    /// <summary>草稿有效期（分钟），默认30</summary>
    public int DraftExpireMinutes { get; set; } = 30;

    /// <summary>草稿自动检查间隔（分钟），默认5</summary>
    public int DraftCheckIntervalMinutes { get; set; } = 5;

    /// <summary>订单号序列长度，默认4</summary>
    public int OrderNoSequenceLength { get; set; } = 4;

    /// <summary>批量操作单次最大数量，默认500</summary>
    public int MaxBatchOperationCount { get; set; } = 500;

    // ==================== 客户相关 ====================

    /// <summary>沉默客户天数阈值，默认30天</summary>
    public int SilentCustomerDays { get; set; } = 30;

    /// <summary>流失风险客户天数阈值，默认60天</summary>
    public int ChurnRiskDays { get; set; } = 60;

    /// <summary>客户名称最大长度，默认20</summary>
    public int CustomerNameMaxLength { get; set; } = 20;

    /// <summary>客户编号序列长度，默认4</summary>
    public int CustomerNoSequenceLength { get; set; } = 4;

    // ==================== 库存相关 ====================

    /// <summary>库存预警阈值，默认10</summary>
    public int StockWarningThreshold { get; set; } = 10;

    /// <summary>库存严重不足阈值，默认5</summary>
    public int StockCriticalThreshold { get; set; } = 5;

    /// <summary>负库存是否允许，默认false</summary>
    public bool AllowNegativeStock { get; set; } = false;

    // ==================== 导出相关 ====================

    /// <summary>导出单次最大数量，默认5000</summary>
    public int MaxExportRowCount { get; set; } = 5000;

    /// <summary>导出文件保留天数，默认7天</summary>
    public int ExportFileRetentionDays { get; set; } = 7;

    // ==================== 缓存相关 ====================

    /// <summary>默认缓存过期时间（分钟），默认15</summary>
    public int DefaultCacheExpirationMinutes { get; set; } = 15;

    /// <summary>配置缓存过期时间（分钟），默认60</summary>
    public int ConfigCacheExpirationMinutes { get; set; } = 60;

    /// <summary>字典缓存过期时间（分钟），默认30</summary>
    public int DictionaryCacheExpirationMinutes { get; set; } = 30;

    // ==================== 自动保存相关 ====================

    /// <summary>自动保存间隔（秒），默认30</summary>
    public int AutoSaveIntervalSeconds { get; set; } = 30;

    /// <summary>是否启用自动保存，默认true</summary>
    public bool EnableAutoSave { get; set; } = true;

    // ==================== 数据质量相关 ====================

    /// <summary>客户查重相似度阈值，默认0.7</summary>
    public double DuplicateSimilarityThreshold { get; set; } = 0.7;

    /// <summary>空手机号检测，默认true</summary>
    public bool CheckEmptyPhone { get; set; } = true;

    /// <summary>空地址检测，默认true</summary>
    public bool CheckEmptyAddress { get; set; } = true;

    // ==================== 备份相关 ====================

    /// <summary>自动备份间隔（分钟），默认30</summary>
    public int AutoBackupIntervalMinutes { get; set; } = 30;

    /// <summary>备份文件保留天数，默认30天</summary>
    public int BackupRetentionDays { get; set; } = 30;

    // ==================== 安全相关 ====================

    /// <summary>密码最小长度，默认8</summary>
    public int PasswordMinLength { get; set; } = 8;

    /// <summary>密码过期天数，默认90天</summary>
    public int PasswordExpireDays { get; set; } = 90;

    /// <summary>最大登录失败次数，默认5</summary>
    public int MaxLoginFailCount { get; set; } = 5;

    /// <summary>账号锁定时间（分钟），默认30</summary>
    public int AccountLockoutMinutes { get; set; } = 30;

    // ==================== 脱敏相关 ====================

    /// <summary>手机号脱敏：保留前3后4</summary>
    public int PhoneMaskPrefix { get; set; } = 3;
    public int PhoneMaskSuffix { get; set; } = 4;

    /// <summary>姓名脱敏：保留姓</summary>
    public bool MaskNameKeepFirst { get; set; } = true;

    /// <summary>地址脱敏：保留省市区</summary>
    public int AddressMaskKeepLength { get; set; } = 6;

    // ==================== 分页相关 ====================

    /// <summary>默认分页大小，默认50</summary>
    public int DefaultPageSize { get; set; } = 50;

    /// <summary>最大分页大小，默认200</summary>
    public int MaxPageSize { get; set; } = 200;

    // ==================== 订单状态流转提示 ====================

    /// <summary>草稿确认提示</summary>
    public string DraftConfirmMessage { get; set; } = "确认将草稿转为正式订单？确认后将进入待分配状态。";

    /// <summary>订单取消提示</summary>
    public string OrderCancelMessage { get; set; } = "确定要取消此订单吗？取消后不可恢复。";

    /// <summary>批量分配提示</summary>
    public string BatchAssignMessage { get; set; } = "确定将选中的 {0} 个订单分配给 {1} 吗？";

    /// <summary>批量确认提示</summary>
    public string BatchConfirmMessage { get; set; } = "确定将 {0} 个草稿订单转为待分配状态吗？";

    /// <summary>客户合并提示</summary>
    public string CustomerMergeMessage { get; set; } = "将「{0}」合并至「{1}」？合并后订单将转移到目标客户。";
}

/// <summary>
/// 数据脱敏规则配置
/// </summary>
public class DataMaskingOptions
{
    public const string SectionName = "DataMasking";

    /// <summary>是否启用脱敏，默认true</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>手机号脱敏规则</summary>
    public MaskingRule PhoneRule { get; set; } = new()
    {
        KeepPrefixLength = 3,
        KeepSuffixLength = 4,
        MaskChar = '*'
    };

    /// <summary>身份证脱敏规则</summary>
    public MaskingRule IdCardRule { get; set; } = new()
    {
        KeepPrefixLength = 3,
        KeepSuffixLength = 4,
        MaskChar = '*'
    };

    /// <summary>银行卡脱敏规则</summary>
    public MaskingRule BankCardRule { get; set; } = new()
    {
        KeepPrefixLength = 4,
        KeepSuffixLength = 4,
        MaskChar = '*'
    };
}

/// <summary>
/// 脱敏规则
/// </summary>
public class MaskingRule
{
    public int KeepPrefixLength { get; set; }
    public int KeepSuffixLength { get; set; }
    public char MaskChar { get; set; } = '*';
}
