using System.Collections.Concurrent;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 轻量级内存缓存服务 - 用于缓存不频繁变化的数据
/// </summary>
public class MemoryCacheService
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

    /// <summary>
    /// 获取缓存值，如果不存在或已过期则返回默认值
    /// </summary>
    public T? Get<T>(string key) where T : struct
    {
        if (_cache.TryGetValue(key, out var entry) && entry.Expiry > DateTime.UtcNow)
        {
            return (T)entry.Value;
        }
        return null;
    }

    /// <summary>
    /// 获取缓存值（引用类型），如果不存在或已过期则返回null
    /// </summary>
    public T? GetRef<T>(string key) where T : class
    {
        if (_cache.TryGetValue(key, out var entry) && entry.Expiry > DateTime.UtcNow)
        {
            return entry.Value as T;
        }
        return null;
    }

    /// <summary>
    /// 设置缓存值
    /// </summary>
    public void Set<T>(string key, T value, TimeSpan? expiry = null)
    {
        var entry = new CacheEntry
        {
            Value = value!,
            Expiry = DateTime.UtcNow.Add(expiry ?? TimeSpan.FromMinutes(5))
        };
        _cache[key] = entry;
    }

    /// <summary>
    /// 移除指定缓存
    /// </summary>
    public void Remove(string key)
    {
        _cache.TryRemove(key, out _);
    }

    /// <summary>
    /// 移除匹配前缀的所有缓存
    /// </summary>
    public void RemoveByPrefix(string prefix)
    {
        var keysToRemove = _cache.Keys.Where(k => k.StartsWith(prefix)).ToList();
        foreach (var key in keysToRemove)
        {
            _cache.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// 清除所有缓存
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
    }

    /// <summary>
    /// 获取或创建缓存值
    /// </summary>
    public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null) where T : struct
    {
        var cached = Get<T>(key);
        if (cached.HasValue)
            return cached.Value;

        var value = await factory();
        Set(key, value, expiry);
        return value;
    }

    private class CacheEntry
    {
        public object Value { get; set; } = null!;
        public DateTime Expiry { get; set; }
    }
}
