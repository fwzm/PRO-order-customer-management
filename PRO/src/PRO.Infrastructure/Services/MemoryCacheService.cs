using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Serilog;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 内存缓存服务 - 用于缓存频繁访问且变化少的数据
/// 支持缓存击穿保护、按前缀失效、命中/失效日志
/// </summary>
public class MemoryCacheService
{
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    /// <summary>跟踪所有缓存 key，按前缀分组，支持 RemoveByPrefix</summary>
    private readonly ConcurrentDictionary<string, HashSet<string>> _keyIndex = new();

    public MemoryCacheService(IMemoryCache cache)
    {
        _cache = cache;
    }

    /// <summary>
    /// 获取或创建缓存（异步，带击穿保护）
    /// </summary>
    public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null)
    {
        if (_cache.TryGetValue(key, out T? cachedValue) && cachedValue != null)
        {
            Log.Debug("[Cache HIT] {Key}", key);
            RecordCacheHit();
            return cachedValue;
        }

        Log.Debug("[Cache MISS] {Key} — 正在加载数据", key);
        RecordCacheMiss();

        // 使用信号量防止缓存击穿
        var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();

        try
        {
            // 双重检查
            if (_cache.TryGetValue(key, out cachedValue) && cachedValue != null)
            {
                Log.Debug("[Cache HIT] {Key} (双重检查)", key);
                RecordCacheHit();
                return cachedValue;
            }

            var value = await factory();

            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(15),
                SlidingExpiration = TimeSpan.FromMinutes(5),
                Priority = CacheItemPriority.Normal
            };

            SetInternal(key, value, cacheOptions);
            Log.Information("[Cache LOADED] {Key}, 过期={Expiration}", key, cacheOptions.AbsoluteExpirationRelativeToNow);
            return value;
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// 获取或创建缓存（同步版本，无击穿保护 — 适合初始化阶段）
    /// </summary>
    public T GetOrCreate<T>(string key, Func<T> factory, TimeSpan? expiration = null)
    {
        if (_cache.TryGetValue(key, out T? cachedValue) && cachedValue != null)
        {
            Log.Debug("[Cache HIT] {Key} (sync)", key);
            return cachedValue;
        }

        Log.Debug("[Cache MISS] {Key} (sync) — 正在加载数据", key);

        var value = factory();

        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(15),
            SlidingExpiration = TimeSpan.FromMinutes(5),
            Priority = CacheItemPriority.Normal
        };

        SetInternal(key, value, cacheOptions);
        Log.Information("[Cache LOADED] {Key} (sync), 过期={Expiration}", key, cacheOptions.AbsoluteExpirationRelativeToNow);
        return value;
    }

    /// <summary>
    /// 获取缓存值（引用类型，不存在返回null）
    /// </summary>
    public T? GetRef<T>(string key) where T : class
    {
        if (_cache.TryGetValue(key, out T? value))
        {
            Log.Debug("[Cache HIT] {Key} (ref)", key);
            RecordCacheHit();
            return value;
        }
        Log.Debug("[Cache MISS] {Key} (ref)", key);
        RecordCacheMiss();
        return null;
    }

    /// <summary>
    /// 设置缓存
    /// </summary>
    public void Set<T>(string key, T value, TimeSpan? expiration = null, CacheItemPriority priority = CacheItemPriority.Normal)
    {
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(15),
            SlidingExpiration = TimeSpan.FromMinutes(5),
            Priority = priority
        };

        SetInternal(key, value, cacheOptions);
        Log.Information("[Cache SET] {Key}, 过期={Expiration}, 优先级={Priority}", key, cacheOptions.AbsoluteExpirationRelativeToNow, priority);
    }

    /// <summary>
    /// 移除缓存
    /// </summary>
    public void Remove(string key)
    {
        _cache.Remove(key);
        RemoveKeyFromIndex(key);
        Log.Information("[Cache REMOVED] {Key}", key);
    }

    /// <summary>
    /// 移除匹配前缀的所有缓存
    /// </summary>
    public void RemoveByPrefix(string prefix)
    {
        var keysToRemove = new List<string>();

        foreach (var kvp in _keyIndex)
        {
            if (kvp.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                keysToRemove.AddRange(kvp.Value);
            }
        }

        // 也检查根 key 本身
        foreach (var kvp in _keyIndex)
        {
            foreach (var key in kvp.Value)
            {
                if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && !keysToRemove.Contains(key))
                    keysToRemove.Add(key);
            }
        }

        foreach (var key in keysToRemove)
        {
            _cache.Remove(key);
        }

        // 清理索引
        foreach (var groupKey in _keyIndex.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList())
        {
            _keyIndex.TryRemove(groupKey, out _);
        }

        Log.Information("[Cache PREFIX REMOVED] Prefix={Prefix}, 移除{Count}项", prefix, keysToRemove.Count);
    }

    /// <summary>
    /// 清空所有缓存
    /// </summary>
    public void Clear()
    {
        if (_cache is MemoryCache memCache)
        {
            memCache.Compact(100);
        }
        _keyIndex.Clear();
        Log.Information("[Cache CLEARED] 所有缓存已清空");
    }

    /// <summary>内部设置方法，同时维护 key 索引</summary>
    private void SetInternal<T>(string key, T value, MemoryCacheEntryOptions options)
    {
        // 注册过期回调以清理索引
        options.RegisterPostEvictionCallback(OnCacheEvicted);

        _cache.Set(key, value, options);

        // 维护索引：按第一个下划线之前的前缀分组
        var prefix = GetCachePrefix(key);
        _keyIndex.AddOrUpdate(prefix,
            _ => new HashSet<string> { key },
            (_, set) => { lock (set) { set.Add(key); } return set; });
    }

    private void OnCacheEvicted(object key, object? value, EvictionReason reason, object? state)
    {
        if (key is string cacheKey)
        {
            RemoveKeyFromIndex(cacheKey);
            if (reason != EvictionReason.Replaced && reason != EvictionReason.None)
            {
                Log.Debug("[Cache EVICTED] {Key}, 原因={Reason}", cacheKey, reason);
            }
        }
    }

    private void RemoveKeyFromIndex(string key)
    {
        var prefix = GetCachePrefix(key);
        if (_keyIndex.TryGetValue(prefix, out var set))
        {
            lock (set) { set.Remove(key); }
        }
    }

    private static string GetCachePrefix(string key)
    {
        var idx = key.IndexOf('_');
        return idx > 0 ? key[..idx] : key;
    }

    // ═══════════════════════════════════════════════════════════
    //  便捷失效方法 — 语义化命名，Service 层调用
    // ═══════════════════════════════════════════════════════════

    /// <summary>失效指定分公司所有产品缓存</summary>
    public void InvalidateProducts(int branchId)
    {
        Remove(CacheKeys.Format(CacheKeys.Products, branchId));
        RemoveByPrefix(CacheKeys.PrefixProducts);
    }

    /// <summary>失效产品分类缓存</summary>
    public void InvalidateCategories()
    {
        RemoveByPrefix(CacheKeys.PrefixCategories);
    }

    /// <summary>失效分公司列表缓存</summary>
    public void InvalidateBranches()
    {
        RemoveByPrefix(CacheKeys.PrefixBranches);
    }

    /// <summary>失效配送员缓存</summary>
    public void InvalidateDeliveryPersons(int? branchId = null)
    {
        if (branchId.HasValue)
            Remove(CacheKeys.Format(CacheKeys.DeliveryPersons, branchId.Value));
        else
            RemoveByPrefix(CacheKeys.PrefixDelivery);
        RemoveByPrefix(CacheKeys.PrefixAvailDelivery);
    }

    /// <summary>失效部门树缓存</summary>
    public void InvalidateDepartmentTree(int? branchId = null)
    {
        if (branchId.HasValue)
            Remove(CacheKeys.Format(CacheKeys.DepartmentTree, branchId.Value));
        else
            RemoveByPrefix(CacheKeys.PrefixDeptTree);
    }

    /// <summary>失效行政区划缓存</summary>
    public void InvalidateRegions()
    {
        RemoveByPrefix(CacheKeys.PrefixRegions);
    }

    /// <summary>失效字典缓存</summary>
    public void InvalidateDictionaries(string? dictType = null)
    {
        if (dictType != null)
            Remove(CacheKeys.Format(CacheKeys.Dictionaries, dictType));
        else
            RemoveByPrefix(CacheKeys.PrefixDictionaries);
    }

    /// <summary>失效 Dashboard 缓存</summary>
    public void InvalidateDashboard(int branchId)
    {
        Remove(CacheKeys.Format(CacheKeys.DashboardData, branchId));
    }

    /// <summary>失效用户权限缓存</summary>
    public void InvalidateUserPermissions(int userId)
    {
        Remove(CacheKeys.Format(CacheKeys.UserPermissions, userId));
    }

    /// <summary>失效角色权限缓存</summary>
    public void InvalidateRolePermissions(int roleId)
    {
        Remove(CacheKeys.Format(CacheKeys.RolePermissions, roleId));
    }

    /// <summary>失效所有权限相关缓存（权限变更时调用）</summary>
    public void InvalidatePermissions()
    {
        RemoveByPrefix(CacheKeys.PrefixPermissions);
        RemoveByPrefix(CacheKeys.PrefixPermTree);
        RemoveByPrefix(CacheKeys.PrefixRoles);
    }

    /// <summary>失效系统配置缓存</summary>
    public void InvalidateSettings(int? employeeId = null)
    {
        if (employeeId.HasValue)
            Remove(CacheKeys.Format(CacheKeys.SystemSettings, employeeId.Value));
        else
            RemoveByPrefix(CacheKeys.PrefixSettings);
    }

    /// <summary>获取缓存命中统计（用于监控）</summary>
    public CacheStats GetStats()
    {
        var totalKeys = _keyIndex.Values.Sum(v => v.Count);
        return new CacheStats
        {
            TotalCachedKeys = totalKeys,
            PrefixGroups = _keyIndex.Count,
            HitCount = _hitCount,
            MissCount = _missCount
        };
    }

    // ═══════════════════════════════════════════════════════════
    //  命中率统计（供 Prometheus 指标采集）
    // ═══════════════════════════════════════════════════════════
    private long _hitCount;
    private long _missCount;

    private void RecordCacheHit()
    {
        Interlocked.Increment(ref _hitCount);
        try { PrometheusMetricsRecorder.RecordCacheHit(); } catch { }
    }

    private void RecordCacheMiss()
    {
        Interlocked.Increment(ref _missCount);
        try { PrometheusMetricsRecorder.RecordCacheMiss(); } catch { }
    }
}

/// <summary>缓存统计快照</summary>
public class CacheStats
{
    public int TotalCachedKeys { get; set; }
    public int PrefixGroups { get; set; }
    public long HitCount { get; set; }
    public long MissCount { get; set; }
    public double HitRate => (HitCount + MissCount) > 0
        ? (double)HitCount / (HitCount + MissCount) * 100 : 0;
}

/// <summary>
/// Prometheus 指标记录器 — 为 Infrastructure 层提供轻量指标记录能力
/// 通过 Action 委托解耦，避免 Infrastructure 直接依赖 WebApi
/// </summary>
public static class PrometheusMetricsRecorder
{
    /// <summary>记录缓存命中</summary>
    public static Action? OnCacheHit { get; set; }

    /// <summary>记录缓存未命中</summary>
    public static Action? OnCacheMiss { get; set; }

    /// <summary>记录数据库错误</summary>
    public static Action? OnDbError { get; set; }

    /// <summary>记录导出成功</summary>
    public static Action? OnExportSuccess { get; set; }

    /// <summary>记录导出失败</summary>
    public static Action? OnExportFailed { get; set; }

    /// <summary>记录分公司隔离拒绝</summary>
    public static Action? OnBranchIsolationDenied { get; set; }

    internal static void RecordCacheHit() => OnCacheHit?.Invoke();
    internal static void RecordCacheMiss() => OnCacheMiss?.Invoke();
    public static void RecordDbError() => OnDbError?.Invoke();
    public static void RecordExportSuccess() => OnExportSuccess?.Invoke();
    public static void RecordExportFailed() => OnExportFailed?.Invoke();
    public static void RecordBranchIsolationDenied() => OnBranchIsolationDenied?.Invoke();
}

/// <summary>
/// 缓存键常量 — 统一管理所有缓存键，便于按前缀失效
/// </summary>
public static class CacheKeys
{
    // ═══════════════════════════════════════════════════════════
    //  低变更数据（长缓存 60min）
    //  产品分类、分公司、部门树、行政区划、常用字典
    // ═══════════════════════════════════════════════════════════
    public const string ProductCategories = "categories_all";
    public const string Branches = "branches_all";
    public const string DeliveryPersons = "delivery_persons_{0}";     // {0} = branchId
    public const string Products = "products_all_{0}";               // {0} = branchId
    public const string DepartmentTree = "dept_tree_{0}";            // {0} = branchId (0=all)
    public const string Regions = "regions_all";
    public const string RegionChildren = "regions_children_{0}";     // {0} = parentCode
    public const string Dictionaries = "dict_{0}";                   // {0} = dictType

    // ═══════════════════════════════════════════════════════════
    //  中变更数据（中缓存 15min）
    //  客户列表、订单计数、配送员可用列表
    // ═══════════════════════════════════════════════════════════
    public const string CustomerList = "customers_{0}_{1}";          // {0} = branchId, {1} = page
    public const string OrderCount = "order_count_{0}";              // {0} = branchId
    public const string AvailableDeliveryPersons = "avail_delivery_{0}"; // {0} = branchId

    // ═══════════════════════════════════════════════════════════
    //  短缓存（2min，支持手动刷新）
    //  Dashboard 非实时统计项
    // ═══════════════════════════════════════════════════════════
    public const string DashboardData = "dashboard_{0}";             // {0} = branchId

    // ═══════════════════════════════════════════════════════════
    //  系统配置缓存（短缓存 5min，主动失效）
    // ═══════════════════════════════════════════════════════════
    public const string SystemSettings = "settings_{0}";             // {0} = employeeId (0=global)
    public const string WeChatConfig = "settings_wechat";
    public const string SyncConfig = "settings_sync";
    public const string BusinessRules = "settings_business_rules";

    // ═══════════════════════════════════════════════════════════
    //  权限菜单缓存（配置变更时主动失效）
    // ═══════════════════════════════════════════════════════════
    public const string UserPermissions = "permissions_{0}";         // {0} = userId
    public const string RolePermissions = "role_perms_{0}";          // {0} = roleId
    public const string PermissionTree = "perm_tree";
    public const string RoleList = "roles_all";

    // ═══════════════════════════════════════════════════════════
    //  前缀常量（供 RemoveByPrefix 批量失效）
    // ═══════════════════════════════════════════════════════════
    public const string PrefixCategories = "categories";
    public const string PrefixBranches = "branches";
    public const string PrefixDelivery = "delivery";
    public const string PrefixAvailDelivery = "avail_delivery";
    public const string PrefixProducts = "products";
    public const string PrefixCustomers = "customers";
    public const string PrefixDashboard = "dashboard";
    public const string PrefixOrderCount = "order_count";
    public const string PrefixPermissions = "permissions";
    public const string PrefixPermTree = "perm_tree";
    public const string PrefixRoles = "roles";
    public const string PrefixSettings = "settings";
    public const string PrefixRegions = "regions";
    public const string PrefixDeptTree = "dept";
    public const string PrefixDictionaries = "dict";

    // ═══════════════════════════════════════════════════════════
    //  辅助方法
    // ═══════════════════════════════════════════════════════════
    /// <summary>格式化带参数的缓存Key</summary>
    public static string Format(string keyTemplate, params object[] args)
        => string.Format(keyTemplate, args);
}
