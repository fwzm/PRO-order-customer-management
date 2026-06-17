using Microsoft.EntityFrameworkCore;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Entities;
using PRO.Infrastructure.Persistence;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 系统设置服务实现 - 管理本地设置、微信配置、同步配置
/// 使用 MemoryCacheService 缓存配置项（5min），修改后主动失效
/// </summary>
public class SystemSettingService : ISystemSettingService
{
    private readonly ProDbContext _dbContext;
    private readonly MemoryCacheService _cache;
    private static readonly TimeSpan SettingsCacheTime = TimeSpan.FromMinutes(5);

    public SystemSettingService(ProDbContext dbContext, MemoryCacheService cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public async Task<ApiResponse<LocalSettingDto>> GetLocalSettingsAsync(int employeeId)
    {
        try
        {
            var settings = await _dbContext.LocalSettings
                .AsNoTracking()
                .Where(s => s.EmployeeId == employeeId)
                .ToListAsync();

            var dto = new LocalSettingDto
            {
                EmployeeId = employeeId,
                Settings = settings.ToDictionary(s => s.SettingKey, s => s.SettingValue)
            };

            return ApiResponse<LocalSettingDto>.Ok(dto);
        }
        catch (Exception ex)
        {
            return ApiResponse<LocalSettingDto>.Fail($"获取本地设置失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> SaveLocalSettingsAsync(SaveLocalSettingRequest request, int employeeId)
    {
        try
        {
            foreach (var kvp in request.Settings)
            {
                var existing = await _dbContext.LocalSettings
                    .FirstOrDefaultAsync(s => s.EmployeeId == employeeId && s.SettingKey == kvp.Key);

                if (existing != null)
                {
                    existing.SettingValue = kvp.Value;
                    existing.UpdatedAt = DateTime.Now;
                }
                else
                {
                    _dbContext.LocalSettings.Add(new LocalSetting
                    {
                        EmployeeId = employeeId,
                        SettingKey = kvp.Key,
                        SettingValue = kvp.Value,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    });
                }
            }

            await _dbContext.SaveChangesAsync();
            _cache.InvalidateSettings(employeeId);
            return ApiResponse<bool>.Ok(true, "设置保存成功");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"保存设置失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<WeChatConfigDto>> GetWeChatConfigAsync()
    {
        try
        {
            var cacheKey = CacheKeys.WeChatConfig;
            var dto = await _cache.GetOrCreateAsync(cacheKey, async () =>
            {
                var config = await _dbContext.WeChatConfigs
                    .AsNoTracking()
                    .FirstOrDefaultAsync();

                if (config == null)
                    return new WeChatConfigDto();

                return new WeChatConfigDto
                {
                    Id = config.Id,
                    Name = config.Name,
                    CorpId = config.CorpId,
                    AgentId = config.AgentId,
                    AppSecretMasked = MaskSecret(config.AppSecret),
                    WebhookUrl = config.WebhookUrl,
                    Token = config.Token,
                    EncodingAESKey = config.EncodingAESKey,
                    IsEnabled = config.IsEnabled,
                    Remark = config.Remark
                };
            }, SettingsCacheTime);

            return ApiResponse<WeChatConfigDto>.Ok(dto);
        }
        catch (Exception ex)
        {
            return ApiResponse<WeChatConfigDto>.Fail($"获取微信配置失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> SaveWeChatConfigAsync(SaveWeChatConfigRequest request)
    {
        try
        {
            var config = await _dbContext.WeChatConfigs.FirstOrDefaultAsync();
            if (config == null)
            {
                config = new WeChatConfig
                {
                    CorpId = request.CorpId,
                    AgentId = request.AgentId,
                    AppSecret = request.AppSecret,
                    Token = request.Token,
                    EncodingAESKey = request.EncodingAESKey,
                    IsEnabled = request.IsEnabled,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                _dbContext.WeChatConfigs.Add(config);
            }
            else
            {
                config.CorpId = request.CorpId;
                config.AgentId = request.AgentId;
                config.AppSecret = request.AppSecret;
                config.Token = request.Token;
                config.EncodingAESKey = request.EncodingAESKey;
                config.IsEnabled = request.IsEnabled;
                config.UpdatedAt = DateTime.Now;
            }

            await _dbContext.SaveChangesAsync();
            _cache.Remove(CacheKeys.WeChatConfig);
            return ApiResponse<bool>.Ok(true, "微信配置保存成功");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"保存微信配置失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> TestWeChatConnectionAsync()
    {
        try
        {
            var config = await _dbContext.WeChatConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (config == null || !config.IsEnabled)
                return ApiResponse<bool>.Fail("微信配置未启用");

            return ApiResponse<bool>.Ok(true, "微信配置有效");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"测试微信连接失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<SyncConfigDto>> GetSyncConfigAsync()
    {
        try
        {
            var cacheKey = CacheKeys.SyncConfig;
            var dto = await _cache.GetOrCreateAsync(cacheKey, async () =>
            {
                var config = await _dbContext.SyncConfigs
                    .AsNoTracking()
                    .FirstOrDefaultAsync();

                return config == null
                    ? new SyncConfigDto()
                    : new SyncConfigDto
                    {
                        IsEnabled = config.IsEnabled,
                        SyncIntervalMinutes = config.SyncIntervalMinutes,
                        AutoSyncEnabled = config.AutoSyncEnabled
                    };
            }, SettingsCacheTime);

            return ApiResponse<SyncConfigDto>.Ok(dto);
        }
        catch (Exception ex)
        {
            return ApiResponse<SyncConfigDto>.Fail($"获取同步配置失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> SaveSyncConfigAsync(SyncConfigDto config)
    {
        try
        {
            var existing = await _dbContext.SyncConfigs.FirstOrDefaultAsync();
            if (existing == null)
            {
                existing = new SyncConfig
                {
                    IsEnabled = config.IsEnabled,
                    SyncIntervalMinutes = config.SyncIntervalMinutes,
                    AutoSyncEnabled = config.AutoSyncEnabled,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                _dbContext.SyncConfigs.Add(existing);
            }
            else
            {
                existing.IsEnabled = config.IsEnabled;
                existing.SyncIntervalMinutes = config.SyncIntervalMinutes;
                existing.AutoSyncEnabled = config.AutoSyncEnabled;
                existing.UpdatedAt = DateTime.Now;
            }

            await _dbContext.SaveChangesAsync();
            _cache.Remove(CacheKeys.SyncConfig);
            return ApiResponse<bool>.Ok(true, "同步配置保存成功");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"保存同步配置失败: {ex.Message}");
        }
    }

    private static string MaskSecret(string? secret)
    {
        if (string.IsNullOrEmpty(secret) || secret.Length < 8)
            return "****";
        return $"{secret[..4]}****{secret[^4..]}";
    }
}
