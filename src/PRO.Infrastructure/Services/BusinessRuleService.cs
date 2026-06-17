using Microsoft.Extensions.Options;
using PRO.Infrastructure.Configuration;
using Serilog;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 业务规则服务 - 统一提供业务规则值，支持配置覆盖和安全回退
/// 优先级：数据库配置 > appsettings.json > 代码默认值
/// </summary>
public class BusinessRuleService
{
    private readonly BusinessRuleOptions _defaults;
    private readonly BusinessConfigService? _configService;

    public BusinessRuleService(
        IOptions<BusinessRuleOptions> options,
        BusinessConfigService? configService = null)
    {
        _defaults = options.Value;
        _configService = configService;
    }

    // ==================== 订单规则 ====================

    public async Task<int> GetDraftExpireMinutesAsync()
    {
        if (_configService != null)
        {
            try
            {
                return await _configService.GetOrderDraftExpireMinutesAsync();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "获取草稿有效期配置失败，使用默认值 {Default}", _defaults.DraftExpireMinutes);
            }
        }
        return _defaults.DraftExpireMinutes;
    }

    public int GetDraftCheckIntervalMinutes()
    {
        try
        {
            return _defaults.DraftCheckIntervalMinutes;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "获取草稿检查间隔配置失败，使用默认值 5");
            return 5;
        }
    }

    public int GetMaxBatchOperationCount()
    {
        try
        {
            return _defaults.MaxBatchOperationCount;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "获取批量操作上限配置失败，使用默认值 500");
            return 500;
        }
    }

    // ==================== 客户规则 ====================

    public async Task<int> GetSilentCustomerDaysAsync()
    {
        try
        {
            if (_configService != null)
            {
                var val = await _configService.GetPositiveIntValueAsync("SilentCustomerDays", _defaults.SilentCustomerDays);
                return val;
            }
        }
        catch (Exception ex) { Log.Warning(ex, "获取沉默客户天数配置失败"); }
        return _defaults.SilentCustomerDays;
    }

    public int GetChurnRiskDays() => GetSafeInt(() => _defaults.ChurnRiskDays, 60);

    // ==================== 库存规则 ====================

    public int GetStockWarningThreshold() => GetSafeInt(() => _defaults.StockWarningThreshold, 10);
    public int GetStockCriticalThreshold() => GetSafeInt(() => _defaults.StockCriticalThreshold, 5);
    public bool GetAllowNegativeStock() => _defaults.AllowNegativeStock;

    // ==================== 导出规则 ====================

    public int GetMaxExportRowCount() => GetSafeInt(() => _defaults.MaxExportRowCount, 5000);
    public int GetExportFileRetentionDays() => GetSafeInt(() => _defaults.ExportFileRetentionDays, 7);

    // ==================== 缓存规则 ====================

    public int GetDefaultCacheExpirationMinutes() => GetSafeInt(() => _defaults.DefaultCacheExpirationMinutes, 15);
    public int GetConfigCacheExpirationMinutes() => GetSafeInt(() => _defaults.ConfigCacheExpirationMinutes, 60);
    public int GetDictionaryCacheExpirationMinutes() => GetSafeInt(() => _defaults.DictionaryCacheExpirationMinutes, 30);

    // ==================== 安全规则 ====================

    public int GetPasswordMinLength() => GetSafeInt(() => _defaults.PasswordMinLength, 8);
    public int GetPasswordExpireDays() => GetSafeInt(() => _defaults.PasswordExpireDays, 90);
    public int GetMaxLoginFailCount() => GetSafeInt(() => _defaults.MaxLoginFailCount, 5);
    public int GetAccountLockoutMinutes() => GetSafeInt(() => _defaults.AccountLockoutMinutes, 30);

    // ==================== 分页规则 ====================

    public int GetDefaultPageSize() => GetSafeInt(() => _defaults.DefaultPageSize, 50);
    public int GetMaxPageSize() => GetSafeInt(() => _defaults.MaxPageSize, 200);

    // ==================== 脱敏规则 ====================

    public int GetPhoneMaskPrefix() => GetSafeInt(() => _defaults.PhoneMaskPrefix, 3);
    public int GetPhoneMaskSuffix() => GetSafeInt(() => _defaults.PhoneMaskSuffix, 4);
    public bool GetMaskNameKeepFirst() => _defaults.MaskNameKeepFirst;
    public int GetAddressMaskKeepLength() => GetSafeInt(() => _defaults.AddressMaskKeepLength, 6);

    // ==================== 自动保存规则 ====================

    public int GetAutoSaveIntervalSeconds() => GetSafeInt(() => _defaults.AutoSaveIntervalSeconds, 30);
    public bool GetEnableAutoSave() => _defaults.EnableAutoSave;

    // ==================== 数据质量规则 ====================

    public double GetDuplicateSimilarityThreshold() => _defaults.DuplicateSimilarityThreshold;

    // ==================== 备份规则 ====================

    public int GetAutoBackupIntervalMinutes() => GetSafeInt(() => _defaults.AutoBackupIntervalMinutes, 30);
    public int GetBackupRetentionDays() => GetSafeInt(() => _defaults.BackupRetentionDays, 30);

    // ==================== 状态流转文案 ====================

    public string GetDraftConfirmMessage() =>
        GetSafeString(() => _defaults.DraftConfirmMessage, "确认将草稿转为正式订单？确认后将进入待分配状态。");

    public string GetOrderCancelMessage() =>
        GetSafeString(() => _defaults.OrderCancelMessage, "确定要取消此订单吗？取消后不可恢复。");

    public string GetBatchAssignMessage(int count, string targetName) =>
        string.Format(GetSafeString(() => _defaults.BatchAssignMessage, "确定将选中的 {0} 个订单分配给 {1} 吗？"), count, targetName);

    public string GetBatchConfirmMessage(int count) =>
        string.Format(GetSafeString(() => _defaults.BatchConfirmMessage, "确定将 {0} 个草稿订单转为待分配状态吗？"), count);

    public string GetCustomerMergeMessage(string source, string target) =>
        string.Format(GetSafeString(() => _defaults.CustomerMergeMessage, "将「{0}」合并至「{1}」？合并后订单将转移到目标客户。"), source, target);

    // ==================== 辅助方法 ====================

    private int GetSafeInt(Func<int> getter, int fallback)
    {
        try { return getter(); }
        catch (Exception ex)
        {
            Log.Warning(ex, "获取配置值失败，使用默认值 {Fallback}", fallback);
            return fallback;
        }
    }

    private string GetSafeString(Func<string> getter, string fallback)
    {
        try { return getter(); }
        catch (Exception ex)
        {
            Log.Warning(ex, "获取配置值失败，使用默认值");
            return fallback;
        }
    }
}
