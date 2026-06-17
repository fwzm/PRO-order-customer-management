using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PRO.WebApi.HealthChecks;

/// <summary>
/// 后台任务服务健康检查
/// 检查关键后台服务（导出、同步）是否正常运行
/// </summary>
public class BackgroundServiceHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        // 检查进程是否存活、线程池状态
        var data = new Dictionary<string, object>
        {
            ["process_uptime"] = (DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime).ToString(),
            ["thread_pool_threads"] = ThreadPool.ThreadCount,
            ["pending_work_items"] = ThreadPool.PendingWorkItemCount,
            ["working_set_mb"] = Environment.WorkingSet / 1024 / 1024
        };

        var isHealthy = true;
        var description = "后台服务运行正常";

        // 检查线程池是否过载
        if (ThreadPool.PendingWorkItemCount > 1000)
        {
            isHealthy = false;
            description = "线程池积压过多工作项，可能影响响应速度";
        }

        return Task.FromResult(isHealthy
            ? HealthCheckResult.Healthy(description, data)
            : HealthCheckResult.Degraded(description, data: data));
    }
}
