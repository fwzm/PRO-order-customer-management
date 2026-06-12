using Microsoft.Extensions.Diagnostics.HealthChecks;
using PRO.Infrastructure.Services;

namespace PRO.WebApi.HealthChecks;

/// <summary>
/// 缓存服务健康检查
/// </summary>
public class CacheHealthCheck : IHealthCheck
{
    private readonly MemoryCacheService _cache;

    public CacheHealthCheck(MemoryCacheService cache)
    {
        _cache = cache;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = _cache.GetStats();
            var data = new Dictionary<string, object>
            {
                ["cached_keys"] = stats.TotalCachedKeys,
                ["prefix_groups"] = stats.PrefixGroups
            };

            return Task.FromResult(HealthCheckResult.Healthy(
                $"缓存服务正常，{stats.TotalCachedKeys} 个缓存键", data));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("缓存服务异常", ex));
        }
    }
}
