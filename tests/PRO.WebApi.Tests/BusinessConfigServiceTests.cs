using Microsoft.EntityFrameworkCore;
using PRO.Domain.Entities;
using PRO.Infrastructure.Persistence;
using PRO.Infrastructure.Services;
using Xunit;

namespace PRO.WebApi.Tests;

/// <summary>
/// 业务配置服务测试 — 覆盖配置读写、缓存、默认值回退
/// </summary>
public class BusinessConfigServiceTests
{
    [Fact]
    public async Task GetValueAsync_ExistingKey_ReturnsStoredValue()
    {
        await using var db = CreateDbContext();
        db.LocalSettings.Add(new LocalSetting
        {
            SettingKey = "PageSize",
            SettingValue = "100",
            SettingType = "Int",
            UpdatedAt = DateTime.Now
        });
        await db.SaveChangesAsync();

        var service = new BusinessConfigService(db);
        var value = await service.GetValueAsync("PageSize");

        Assert.Equal("100", value);
    }

    [Fact]
    public async Task GetValueAsync_MissingKey_ReturnsDefaultValue()
    {
        await using var db = CreateDbContext();
        var service = new BusinessConfigService(db);
        var value = await service.GetValueAsync("NonExistent", "defaultVal");

        Assert.Equal("defaultVal", value);
    }

    [Fact]
    public async Task GetIntValueAsync_ValidInt_ReturnsParsedValue()
    {
        await using var db = CreateDbContext();
        db.LocalSettings.Add(new LocalSetting
        {
            SettingKey = "MaxPageSize",
            SettingValue = "200",
            SettingType = "Int",
            UpdatedAt = DateTime.Now
        });
        await db.SaveChangesAsync();

        var service = new BusinessConfigService(db);
        var value = await service.GetIntValueAsync("MaxPageSize", 50);

        Assert.Equal(200, value);
    }

    [Fact]
    public async Task GetIntValueAsync_InvalidInt_ReturnsDefaultValue()
    {
        await using var db = CreateDbContext();
        db.LocalSettings.Add(new LocalSetting
        {
            SettingKey = "BrokenSetting",
            SettingValue = "not-a-number",
            SettingType = "Int",
            UpdatedAt = DateTime.Now
        });
        await db.SaveChangesAsync();

        var service = new BusinessConfigService(db);
        var value = await service.GetIntValueAsync("BrokenSetting", 42);

        Assert.Equal(42, value);
    }

    [Fact]
    public async Task GetOrderDraftExpireMinutesAsync_WithConfiguredValue_ReturnsIt()
    {
        await using var db = CreateDbContext();
        db.LocalSettings.Add(new LocalSetting
        {
            SettingKey = "OrderDraftExpireMinutes",
            SettingValue = "60",
            SettingType = "Int",
            UpdatedAt = DateTime.Now
        });
        await db.SaveChangesAsync();

        var service = new BusinessConfigService(db);
        var value = await service.GetOrderDraftExpireMinutesAsync();

        Assert.Equal(60, value);
    }

    [Fact]
    public async Task GetOrderDraftExpireMinutesAsync_NoConfig_ReturnsDefault30()
    {
        await using var db = CreateDbContext();
        var service = new BusinessConfigService(db);
        var value = await service.GetOrderDraftExpireMinutesAsync();

        Assert.Equal(30, value);
    }

    [Fact]
    public async Task SetValueAsync_NewKey_CreatesSetting()
    {
        await using var db = CreateDbContext();
        var service = new BusinessConfigService(db);
        await service.SetValueAsync("NewSetting", "newValue");

        var setting = await db.LocalSettings.FirstOrDefaultAsync(s => s.SettingKey == "NewSetting");
        Assert.NotNull(setting);
        Assert.Equal("newValue", setting.SettingValue);
    }

    [Fact]
    public async Task SetValueAsync_ExistingKey_UpdatesSetting()
    {
        await using var db = CreateDbContext();
        db.LocalSettings.Add(new LocalSetting
        {
            SettingKey = "UpdateMe",
            SettingValue = "old",
            SettingType = "String",
            UpdatedAt = DateTime.Now
        });
        await db.SaveChangesAsync();

        var service = new BusinessConfigService(db);
        await service.SetValueAsync("UpdateMe", "new");

        var setting = await db.LocalSettings.FirstAsync(s => s.SettingKey == "UpdateMe");
        Assert.Equal("new", setting.SettingValue);
    }

    [Fact]
    public async Task GetAllConfigsAsync_ReturnsAllSettings()
    {
        await using var db = CreateDbContext();
        db.LocalSettings.AddRange(
            new LocalSetting { SettingKey = "Key1", SettingValue = "Val1", SettingType = "String", UpdatedAt = DateTime.Now },
            new LocalSetting { SettingKey = "Key2", SettingValue = "Val2", SettingType = "Int", UpdatedAt = DateTime.Now }
        );
        await db.SaveChangesAsync();

        var service = new BusinessConfigService(db);
        var configs = await service.GetAllConfigsAsync();

        Assert.Equal(2, configs.Count);
        Assert.Contains(configs, c => c.Key == "Key1");
        Assert.Contains(configs, c => c.Key == "Key2");
    }

    [Fact]
    public async Task GetBoolValueAsync_TrueValue_ReturnsTrue()
    {
        await using var db = CreateDbContext();
        db.LocalSettings.Add(new LocalSetting
        {
            SettingKey = "EnableFeature",
            SettingValue = "true",
            SettingType = "Bool",
            UpdatedAt = DateTime.Now
        });
        await db.SaveChangesAsync();

        var service = new BusinessConfigService(db);
        var value = await service.GetBoolValueAsync("EnableFeature");

        Assert.True(value);
    }

    [Fact]
    public async Task InvalidateCache_AfterSetValue_ReturnsUpdatedValue()
    {
        await using var db = CreateDbContext();
        var service = new BusinessConfigService(db);
        await service.SetValueAsync("CachedKey", "first");

        var first = await service.GetValueAsync("CachedKey");
        Assert.Equal("first", first);

        // Update via DB directly (bypassing cache)
        var setting = await db.LocalSettings.FirstAsync(s => s.SettingKey == "CachedKey");
        setting.SettingValue = "second";
        await db.SaveChangesAsync();

        // Should still return cached value
        var cached = await service.GetValueAsync("CachedKey");
        Assert.Equal("first", cached);

        // After invalidation, should return new value
        service.InvalidateCache();
        var refreshed = await service.GetValueAsync("CachedKey");
        Assert.Equal("second", refreshed);
    }

    #region Helpers

    private static ProDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ProDbContext>()
            .UseInMemoryDatabase($"BusinessConfig_{Guid.NewGuid():N}")
            .Options;
        return new ProDbContext(options);
    }

    #endregion
}
