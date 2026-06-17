using PRO.Infrastructure.Services;
using Xunit;

namespace PRO.WebApi.Tests;

/// <summary>
/// 缓存服务测试 — 覆盖命中、未命中、失效、过期、手动刷新
/// </summary>
public class CacheServiceTests
{
    private MemoryCacheService CreateCache()
    {
        var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(
            new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        return new MemoryCacheService(cache);
    }

    [Fact]
    public async Task GetOrCreateAsync_CacheMiss_ShouldCallFactory()
    {
        var cache = CreateCache();
        var callCount = 0;

        var result = await cache.GetOrCreateAsync("test_key", async () =>
        {
            callCount++;
            await Task.Delay(1);
            return "value1";
        });

        Assert.Equal("value1", result);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task GetOrCreateAsync_CacheHit_ShouldReturnCachedValue()
    {
        var cache = CreateCache();
        var callCount = 0;

        // 第一次调用：缓存未命中
        var result1 = await cache.GetOrCreateAsync("hit_test", () =>
        {
            callCount++;
            return Task.FromResult("first");
        });
        Assert.Equal("first", result1);
        Assert.Equal(1, callCount);

        // 第二次调用：缓存命中，不调用工厂
        var result2 = await cache.GetOrCreateAsync("hit_test", () =>
        {
            callCount++;
            return Task.FromResult("second");
        });
        Assert.Equal("first", result2); // 应返回缓存值
        Assert.Equal(1, callCount); // 工厂未被再次调用
    }

    [Fact]
    public void Remove_ShouldInvalidateCache()
    {
        var cache = CreateCache();

        // 设置缓存
        cache.Set("remove_test", "cached_value");
        var ref1 = cache.GetRef<string>("remove_test");
        Assert.Equal("cached_value", ref1);

        // 移除缓存
        cache.Remove("remove_test");
        var ref2 = cache.GetRef<string>("remove_test");
        Assert.Null(ref2);
    }

    [Fact]
    public void RemoveByPrefix_ShouldInvalidateMultipleKeys()
    {
        var cache = CreateCache();

        cache.Set("prefix_test_1", "value1");
        cache.Set("prefix_test_2", "value2");
        cache.Set("prefix_other_1", "value3");

        cache.RemoveByPrefix("prefix_test");

        Assert.Null(cache.GetRef<string>("prefix_test_1"));
        Assert.Null(cache.GetRef<string>("prefix_test_2"));
        Assert.Equal("value3", cache.GetRef<string>("prefix_other_1"));
    }

    [Fact]
    public void Clear_ShouldRemoveAllCache()
    {
        var cache = CreateCache();

        cache.Set("clear_1", "v1");
        cache.Set("clear_2", "v2");

        cache.Clear();

        Assert.Null(cache.GetRef<string>("clear_1"));
        Assert.Null(cache.GetRef<string>("clear_2"));
    }

    [Fact]
    public async Task GetOrCreateAsync_WithShortExpiration_ShouldExpire()
    {
        var cache = CreateCache();
        var callCount = 0;

        var result1 = await cache.GetOrCreateAsync("expire_test",
            () => { callCount++; return Task.FromResult("expiring"); },
            TimeSpan.FromMilliseconds(50));

        Assert.Equal("expiring", result1);
        Assert.Equal(1, callCount);

        // 等待缓存过期
        await Task.Delay(200);

        // 过期后应重新调用工厂
        var result2 = await cache.GetOrCreateAsync("expire_test",
            () => { callCount++; return Task.FromResult("refreshed"); },
            TimeSpan.FromMilliseconds(50));

        Assert.Equal("refreshed", result2);
        Assert.Equal(2, callCount);
    }

    [Fact]
    public void GetStats_ShouldReturnCacheStatistics()
    {
        var cache = CreateCache();

        cache.Set("stats_1", "v1");
        cache.Set("stats_2", "v2");

        var stats = cache.GetStats();
        Assert.True(stats.TotalCachedKeys >= 2);
    }

    [Fact]
    public void InvalidateMethods_ShouldWorkCorrectly()
    {
        var cache = CreateCache();

        // InvalidateCategories
        cache.Set(CacheKeys.ProductCategories, new List<object>());
        cache.InvalidateCategories();
        Assert.Null(cache.GetRef<List<object>>(CacheKeys.ProductCategories));

        // InvalidateBranches
        cache.Set(CacheKeys.Branches, new List<object>());
        cache.InvalidateBranches();
        Assert.Null(cache.GetRef<List<object>>(CacheKeys.Branches));

        // InvalidateDashboard
        cache.Set(CacheKeys.Format(CacheKeys.DashboardData, 1), "dashboard_data");
        cache.InvalidateDashboard(1);
        Assert.Null(cache.GetRef<string>(CacheKeys.Format(CacheKeys.DashboardData, 1)));

        // InvalidatePermissions
        cache.Set(CacheKeys.Format(CacheKeys.UserPermissions, 1), new List<string>());
        cache.InvalidatePermissions();
        Assert.Null(cache.GetRef<List<string>>(CacheKeys.Format(CacheKeys.UserPermissions, 1)));
    }

    [Fact]
    public void CacheKeys_Format_ShouldProduceCorrectKeys()
    {
        Assert.Equal("dashboard_1", CacheKeys.Format(CacheKeys.DashboardData, 1));
        Assert.Equal("delivery_persons_5", CacheKeys.Format(CacheKeys.DeliveryPersons, 5));
        Assert.Equal("permissions_100", CacheKeys.Format(CacheKeys.UserPermissions, 100));
        Assert.Equal("customers_1_2", CacheKeys.Format(CacheKeys.CustomerList, 1, 2));
        Assert.Equal("dept_tree_0", CacheKeys.Format(CacheKeys.DepartmentTree, 0));
        Assert.Equal("dict_status", CacheKeys.Format(CacheKeys.Dictionaries, "status"));
    }
}
