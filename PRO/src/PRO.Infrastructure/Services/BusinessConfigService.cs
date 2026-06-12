using Microsoft.EntityFrameworkCore;
using PRO.Domain.Entities;
using PRO.Infrastructure.Persistence;
using Serilog;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 业务配置服务 - 管理可配置的业务参数
/// </summary>
public class BusinessConfigService
{
    private readonly ProDbContext _dbContext;
    private readonly Dictionary<string, string> _cache = new();
    private DateTime _cacheExpiry = DateTime.MinValue;

    public BusinessConfigService(ProDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// 获取配置值
    /// </summary>
    public async Task<string> GetValueAsync(string key, string defaultValue = "")
    {
        await EnsureCacheLoadedAsync();
        return _cache.GetValueOrDefault(key, defaultValue);
    }

    /// <summary>
    /// 获取整数配置值
    /// </summary>
    public async Task<int> GetIntValueAsync(string key, int defaultValue = 0)
    {
        var value = await GetValueAsync(key, defaultValue.ToString());
        return int.TryParse(value, out var result) ? result : defaultValue;
    }

    public async Task<int> GetPositiveIntValueAsync(string key, int defaultValue)
    {
        var value = await GetIntValueAsync(key, defaultValue);
        return value > 0 ? value : defaultValue;
    }

    public async Task<int> GetOrderDraftExpireMinutesAsync()
    {
        var value = await GetPositiveIntValueAsync(ConfigKeys.OrderDraftExpireMinutes, 30);
        if (value != 30)
            return value;

        return await GetPositiveIntValueAsync(ConfigKeys.DraftExpireMinutes, 30);
    }

    /// <summary>
    /// 获取布尔配置值
    /// </summary>
    public async Task<bool> GetBoolValueAsync(string key, bool defaultValue = false)
    {
        var value = await GetValueAsync(key, defaultValue.ToString());
        return bool.TryParse(value, out var result) ? result : defaultValue;
    }

    /// <summary>
    /// 设置配置值
    /// </summary>
    public async Task SetValueAsync(string key, string value, string? description = null)
    {
        try
        {
            var setting = await _dbContext.LocalSettings
                .FirstOrDefaultAsync(s => s.SettingKey == key);

            if (setting != null)
            {
                setting.SettingValue = value;
                setting.UpdatedAt = DateTime.Now;
            }
            else
            {
                _dbContext.LocalSettings.Add(new LocalSetting
                {
                    SettingKey = key,
                    SettingValue = value,
                    SettingType = "String",
                    UpdatedAt = DateTime.Now
                });
            }

            await _dbContext.SaveChangesAsync();

            // 更新缓存
            _cache[key] = value;

            Log.Information("配置已更新: {Key} = {Value}", key, value);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "更新配置失败: {Key}", key);
            throw;
        }
    }

    /// <summary>
    /// 获取所有配置
    /// </summary>
    public async Task<List<BusinessConfigItem>> GetAllConfigsAsync()
    {
        var settings = await _dbContext.LocalSettings
            .AsNoTracking()
            .OrderBy(s => s.SettingKey)
            .ToListAsync();

        return settings.Select(s => new BusinessConfigItem
        {
            Key = s.SettingKey,
            Value = s.SettingValue,
            Description = GetDefaultDescription(s.SettingKey),
            Type = s.SettingType ?? "String",
            UpdatedAt = s.UpdatedAt
        }).ToList();
    }

    /// <summary>
    /// 刷新缓存
    /// </summary>
    public void InvalidateCache()
    {
        _cacheExpiry = DateTime.MinValue;
    }

    private async Task EnsureCacheLoadedAsync()
    {
        if (_cacheExpiry > DateTime.Now)
            return;

        var settings = await _dbContext.LocalSettings.AsNoTracking().ToListAsync();
        _cache.Clear();
        foreach (var s in settings)
        {
            _cache[s.SettingKey] = s.SettingValue;
        }
        _cacheExpiry = DateTime.Now.AddMinutes(5);
    }

    private static string GetDefaultDescription(string key) => key switch
    {
        "PageSize" => "列表每页显示条数",
        "AutoBackupInterval" => "自动备份间隔（分钟）",
        "OrderDraftExpireMinutes" => "订单草稿有效期（分钟）",
        "DraftExpireMinutes" => "订单草稿有效期（分钟，兼容旧配置）",
        "LogRetentionDays" => "日志保留天数",
        "CloseBehavior" => "关闭行为（0=退出，1=最小化到托盘）",
        "DefaultPaymentStatus" => "默认收款状态",
        "MaxLoginFailures" => "最大登录失败次数",
        "LockoutMinutes" => "锁定时间（分钟）",
        "PasswordExpireDays" => "密码过期天数",
        "EnableAutoSave" => "启用自动保存",
        "AutoSaveInterval" => "自动保存间隔（秒）",
        _ => key
    };
}

public class BusinessConfigItem
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public string Description { get; set; } = "";
    public string Type { get; set; } = "String";
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 预定义的业务配置键
/// </summary>
public static class ConfigKeys
{
    // 分页
    public const string PageSize = "PageSize";
    public const string MaxPageSize = "MaxPageSize";

    // 备份
    public const string AutoBackupInterval = "AutoBackupInterval";

    // 草稿
    public const string OrderDraftExpireMinutes = "OrderDraftExpireMinutes";
    public const string DraftExpireMinutes = "DraftExpireMinutes";

    // 日志
    public const string LogRetentionDays = "LogRetentionDays";

    // 关闭行为
    public const string CloseBehavior = "CloseBehavior";

    // 安全
    public const string MaxLoginFailures = "MaxLoginFailures";
    public const string LockoutMinutes = "LockoutMinutes";
    public const string PasswordExpireDays = "PasswordExpireDays";

    // 自动保存
    public const string EnableAutoSave = "EnableAutoSave";
    public const string AutoSaveInterval = "AutoSaveInterval";

    // 缓存
    public const string DefaultCacheExpirationMinutes = "DefaultCacheExpirationMinutes";
    public const string ConfigCacheExpirationMinutes = "ConfigCacheExpirationMinutes";

    // 库存
    public const string StockWarningThreshold = "StockWarningThreshold";
    public const string StockCriticalThreshold = "StockCriticalThreshold";
    public const string AllowNegativeStock = "AllowNegativeStock";

    // 导出
    public const string MaxExportRowCount = "MaxExportRowCount";
    public const string ExportFileRetentionDays = "ExportFileRetentionDays";

    // 客户
    public const string SilentCustomerDays = "SilentCustomerDays";
    public const string ChurnRiskDays = "ChurnRiskDays";
}
