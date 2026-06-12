using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PRO.Infrastructure.Configuration;
using PRO.Infrastructure.Persistence;
using Serilog;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 业务规则设置持久化服务 - 读取优先级: 数据库LocalSetting > appsettings.json > 代码默认值
/// 非法值回退到默认值并记录日志
/// 覆盖14项核心配置：草稿超时、批量上限、沉默天数、库存阈值等
/// </summary>
public class BusinessRuleSettingsService
{
    private readonly ProDbContext _dbContext;
    private readonly BusinessRuleOptions _defaults;
    private readonly Dictionary<string, string> _localCache = new();
    private DateTime _lastLoadTime = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public BusinessRuleSettingsService(ProDbContext dbContext, IOptions<BusinessRuleOptions> options)
    {
        _dbContext = dbContext;
        _defaults = options.Value;
    }

    // ==================== 14项核心配置 ====================

    /// <summary>1. 草稿有效期（分钟）</summary>
    public async Task<int> GetDraftExpireMinutesAsync()
        => await GetIntAsync("DraftExpireMinutes", _defaults.DraftExpireMinutes, 1, 1440);

    /// <summary>2. 批量操作上限</summary>
    public async Task<int> GetMaxBatchCountAsync()
        => await GetIntAsync("MaxBatchOperationCount", _defaults.MaxBatchOperationCount, 1, 10000);

    /// <summary>3. 沉默客户天数</summary>
    public async Task<int> GetSilentCustomerDaysAsync()
        => await GetIntAsync("SilentCustomerDays", _defaults.SilentCustomerDays, 1, 365);

    /// <summary>4. 流失风险天数</summary>
    public async Task<int> GetChurnRiskDaysAsync()
        => await GetIntAsync("ChurnRiskDays", _defaults.ChurnRiskDays, 1, 365);

    /// <summary>5. 库存预警阈值</summary>
    public async Task<int> GetStockWarningThresholdAsync()
        => await GetIntAsync("StockWarningThreshold", _defaults.StockWarningThreshold, 0, 99999);

    /// <summary>6. 库存严重不足阈值</summary>
    public async Task<int> GetStockCriticalThresholdAsync()
        => await GetIntAsync("StockCriticalThreshold", _defaults.StockCriticalThreshold, 0, 99999);

    /// <summary>7. 导出最大行数</summary>
    public async Task<int> GetMaxExportRowsAsync()
        => await GetIntAsync("MaxExportRowCount", _defaults.MaxExportRowCount, 1, 100000);

    /// <summary>8. 密码最小长度</summary>
    public async Task<int> GetPasswordMinLengthAsync()
        => await GetIntAsync("PasswordMinLength", _defaults.PasswordMinLength, 6, 64);

    /// <summary>9. 密码过期天数</summary>
    public async Task<int> GetPasswordExpireDaysAsync()
        => await GetIntAsync("PasswordExpireDays", _defaults.PasswordExpireDays, 1, 365);

    /// <summary>10. 最大登录失败次数</summary>
    public async Task<int> GetMaxLoginFailCountAsync()
        => await GetIntAsync("MaxLoginFailCount", _defaults.MaxLoginFailCount, 1, 100);

    /// <summary>11. 账号锁定时间（分钟）</summary>
    public async Task<int> GetAccountLockoutMinutesAsync()
        => await GetIntAsync("AccountLockoutMinutes", _defaults.AccountLockoutMinutes, 1, 1440);

    /// <summary>12. 默认分页大小</summary>
    public async Task<int> GetDefaultPageSizeAsync()
        => await GetIntAsync("DefaultPageSize", _defaults.DefaultPageSize, 10, 500);

    /// <summary>13. 自动保存间隔（秒）</summary>
    public async Task<int> GetAutoSaveIntervalSecondsAsync()
        => await GetIntAsync("AutoSaveIntervalSeconds", _defaults.AutoSaveIntervalSeconds, 5, 600);

    /// <summary>14. 备份保留天数</summary>
    public async Task<int> GetBackupRetentionDaysAsync()
        => await GetIntAsync("BackupRetentionDays", _defaults.BackupRetentionDays, 1, 365);

    // ==================== 通用设置读写 ====================

    /// <summary>
    /// 获取整型配置值
    /// </summary>
    private async Task<int> GetIntAsync(string key, int defaultValue, int min, int max)
    {
        try
        {
            var strValue = await GetSettingValueAsync(key);
            if (string.IsNullOrEmpty(strValue))
                return defaultValue;

            if (!int.TryParse(strValue, out var value))
            {
                Log.Warning("配置 {Key} 值 '{Value}' 无法解析为整数，使用默认值 {Default}", key, strValue, defaultValue);
                return defaultValue;
            }

            if (value < min || value > max)
            {
                Log.Warning("配置 {Key} 值 {Value} 超出范围 [{Min}, {Max}]，使用默认值 {Default}", key, value, min, max, defaultValue);
                return defaultValue;
            }

            return value;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "读取配置 {Key} 失败，使用默认值 {Default}", key, defaultValue);
            return defaultValue;
        }
    }

    /// <summary>
    /// 获取设置字符串值（带本地缓存）
    /// </summary>
    private async Task<string?> GetSettingValueAsync(string key)
    {
        try
        {
            // 检查缓存
            if (_localCache.TryGetValue(key, out var cached) &&
                (DateTime.Now - _lastLoadTime) < CacheDuration)
            {
                return cached;
            }

            // 从数据库读取
            var setting = await _dbContext.LocalSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SettingKey == key);

            if (setting != null)
            {
                _localCache[key] = setting.SettingValue;
                _lastLoadTime = DateTime.Now;
                return setting.SettingValue;
            }

            return null;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "从数据库读取设置 {Key} 失败", key);
            return null;
        }
    }

    /// <summary>
    /// 保存设置值（同时更新数据库和缓存）
    /// </summary>
    public async Task<bool> SaveSettingAsync(string key, string value, int employeeId = 0)
    {
        try
        {
            var existing = await _dbContext.LocalSettings
                .FirstOrDefaultAsync(s => s.SettingKey == key);

            if (existing != null)
            {
                existing.SettingValue = value;
                existing.UpdatedAt = DateTime.Now;
            }
            else
            {
                _dbContext.LocalSettings.Add(new Domain.Entities.LocalSetting
                {
                    SettingKey = key,
                    SettingValue = value,
                    SettingType = "BusinessRule",
                    UpdatedAt = DateTime.Now
                });
            }

            await _dbContext.SaveChangesAsync();

            // 更新缓存
            _localCache[key] = value;
            _lastLoadTime = DateTime.Now;

            Log.Information("业务规则配置 {Key} 已更新为 {Value}", key, value);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "保存设置 {Key} 失败", key);
            return false;
        }
    }

    /// <summary>
    /// 批量获取所有业务规则配置
    /// </summary>
    public async Task<Dictionary<string, string>> GetAllSettingsAsync()
    {
        var result = new Dictionary<string, string>();
        var keys = new[]
        {
            "DraftExpireMinutes", "MaxBatchOperationCount", "SilentCustomerDays",
            "ChurnRiskDays", "StockWarningThreshold", "StockCriticalThreshold",
            "MaxExportRowCount", "PasswordMinLength", "PasswordExpireDays",
            "MaxLoginFailCount", "AccountLockoutMinutes", "DefaultPageSize",
            "AutoSaveIntervalSeconds", "BackupRetentionDays"
        };

        try
        {
            var settings = await _dbContext.LocalSettings
                .AsNoTracking()
                .Where(s => keys.Contains(s.SettingKey))
                .ToListAsync();

            foreach (var key in keys)
            {
                var setting = settings.FirstOrDefault(s => s.SettingKey == key);
                result[key] = setting?.SettingValue ?? GetDefaultValueForDisplay(key);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "批量获取业务规则配置失败");
            foreach (var key in keys)
                result[key] = GetDefaultValueForDisplay(key);
        }

        return result;
    }

    private string GetDefaultValueForDisplay(string key) => key switch
    {
        "DraftExpireMinutes" => _defaults.DraftExpireMinutes.ToString(),
        "MaxBatchOperationCount" => _defaults.MaxBatchOperationCount.ToString(),
        "SilentCustomerDays" => _defaults.SilentCustomerDays.ToString(),
        "ChurnRiskDays" => _defaults.ChurnRiskDays.ToString(),
        "StockWarningThreshold" => _defaults.StockWarningThreshold.ToString(),
        "StockCriticalThreshold" => _defaults.StockCriticalThreshold.ToString(),
        "MaxExportRowCount" => _defaults.MaxExportRowCount.ToString(),
        "PasswordMinLength" => _defaults.PasswordMinLength.ToString(),
        "PasswordExpireDays" => _defaults.PasswordExpireDays.ToString(),
        "MaxLoginFailCount" => _defaults.MaxLoginFailCount.ToString(),
        "AccountLockoutMinutes" => _defaults.AccountLockoutMinutes.ToString(),
        "DefaultPageSize" => _defaults.DefaultPageSize.ToString(),
        "AutoSaveIntervalSeconds" => _defaults.AutoSaveIntervalSeconds.ToString(),
        "BackupRetentionDays" => _defaults.BackupRetentionDays.ToString(),
        _ => "0"
    };

    /// <summary>
    /// 刷新缓存
    /// </summary>
    public void InvalidateCache()
    {
        _localCache.Clear();
        _lastLoadTime = DateTime.MinValue;
    }
}
