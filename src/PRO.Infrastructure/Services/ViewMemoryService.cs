using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PRO.Domain.Entities;
using PRO.Infrastructure.Persistence;
using Serilog;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 视图记忆服务 - 保存用户的筛选条件和视图偏好
/// </summary>
public class ViewMemoryService
{
    private readonly ProDbContext _dbContext;

    public ViewMemoryService(ProDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// 保存视图记忆
    /// </summary>
    public async Task SaveViewAsync(string viewKey, object viewData, int employeeId)
    {
        try
        {
            var json = JsonSerializer.Serialize(viewData);
            var key = $"view_{viewKey}_{employeeId}";

            var setting = await _dbContext.LocalSettings
                .FirstOrDefaultAsync(s => s.SettingKey == key);

            if (setting != null)
            {
                setting.SettingValue = json;
                setting.UpdatedAt = DateTime.Now;
            }
            else
            {
                _dbContext.LocalSettings.Add(new LocalSetting
                {
                    SettingKey = key,
                    SettingValue = json,
                    SettingType = "Json",
                    UpdatedAt = DateTime.Now
                });
            }

            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "保存视图记忆失败: {Key}", viewKey);
        }
    }

    /// <summary>
    /// 加载视图记忆
    /// </summary>
    public async Task<T?> LoadViewAsync<T>(string viewKey, int employeeId) where T : class
    {
        try
        {
            var key = $"view_{viewKey}_{employeeId}";
            var setting = await _dbContext.LocalSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SettingKey == key);

            if (setting == null || string.IsNullOrEmpty(setting.SettingValue))
                return null;

            return JsonSerializer.Deserialize<T>(setting.SettingValue);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "加载视图记忆失败: {Key}", viewKey);
            return null;
        }
    }

    /// <summary>
    /// 删除视图记忆
    /// </summary>
    public async Task DeleteViewAsync(string viewKey, int employeeId)
    {
        try
        {
            var key = $"view_{viewKey}_{employeeId}";
            var setting = await _dbContext.LocalSettings
                .FirstOrDefaultAsync(s => s.SettingKey == key);

            if (setting != null)
            {
                _dbContext.LocalSettings.Remove(setting);
                await _dbContext.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "删除视图记忆失败: {Key}", viewKey);
        }
    }
}

/// <summary>
/// 订单列表视图记忆
/// </summary>
public class OrderListViewMemory
{
    public string? FilterStatus { get; set; }
    public string? FilterPaymentStatus { get; set; }
    public DateTime? FilterStartDate { get; set; }
    public DateTime? FilterEndDate { get; set; }
    public string? SearchKeyword { get; set; }
    public int PageSize { get; set; } = 50;
    public string? SortField { get; set; }
    public string? SortDirection { get; set; }
    public bool ShowOnlyDrafts { get; set; }
    public DateTime SavedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// 客户列表视图记忆
/// </summary>
public class CustomerListViewMemory
{
    public string? FilterCustomerType { get; set; }
    public bool ShowMajorOnly { get; set; }
    public string? SearchKeyword { get; set; }
    public int PageSize { get; set; } = 50;
    public DateTime SavedAt { get; set; } = DateTime.Now;
}
