using PRO.Application.DTOs;

namespace PRO.Application.Interfaces;

/// <summary>
/// 数据质量检测服务
/// </summary>
public interface IDataQualityService
{
    /// <summary>
    /// 获取数据质量报告
    /// </summary>
    /// <param name="branchId">分公司ID，null表示全部</param>
    /// <returns>数据质量报告</returns>
    Task<ApiResponse<DataQualitySummaryReportDto>> GetDataQualityReportAsync(int? branchId);
}

/// <summary>
/// 数据质量汇总报告（P3-P4简化版）
/// </summary>
public class DataQualitySummaryReportDto
{
    /// <summary>重复客户数</summary>
    public int DuplicateCustomerCount { get; set; }

    /// <summary>缺少手机号的客户数</summary>
    public int MissingPhoneCustomerCount { get; set; }

    /// <summary>缺少地址的客户数</summary>
    public int MissingAddressCustomerCount { get; set; }

    /// <summary>未关联企微的客户数</summary>
    public int UnlinkedWeChatCustomerCount { get; set; }

    /// <summary>负库存产品数</summary>
    public int NegativeStockProductCount { get; set; }

    /// <summary>超期订单数</summary>
    public int OverdueOrderCount { get; set; }

    /// <summary>不活跃客户数（超过90天无订单）</summary>
    public int InactiveCustomerCount { get; set; }

    /// <summary>超负载配送员数</summary>
    public int OverloadedDeliveryPersonCount { get; set; }

    /// <summary>问题详情列表</summary>
    public List<DataQualityIssue> Issues { get; set; } = [];

    /// <summary>报告生成时间</summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 数据质量问题项
/// </summary>
public class DataQualityIssue
{
    /// <summary>分类：Customer/Product/Order/Delivery</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>严重程度：Critical/Warning/Info</summary>
    public string Severity { get; set; } = string.Empty;

    /// <summary>问题描述</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>影响数量</summary>
    public int AffectedCount { get; set; }

    /// <summary>建议操作</summary>
    public string? SuggestedAction { get; set; }
}
