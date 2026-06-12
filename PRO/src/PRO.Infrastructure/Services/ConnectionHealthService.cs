using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Retry;
using PRO.Infrastructure.Persistence;
using Serilog;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 数据库连接健康监控与自动重连服务
/// </summary>
public class ConnectionHealthService
{
    private readonly IServiceProvider _serviceProvider;
    private bool _isConnected = true;
    private int _consecutiveFailures;
    private DateTime _lastFailureTime;
    private readonly ResiliencePipeline _retryPipeline;

    /// <summary>连接状态变更事件：参数为 (isConnected, message)</summary>
    public event Action<bool, string>? ConnectionStatusChanged;

    /// <summary>当前是否已连接</summary>
    public bool IsConnected => _isConnected;

    public ConnectionHealthService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;

        // 使用 Polly v8 ResiliencePipeline 构建重试策略
        _retryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                OnRetry = args =>
                {
                    Log.Warning("数据库操作重试 {Attempt}/3，延迟 {Delay}s",
                        args.AttemptNumber + 1, args.RetryDelay.TotalSeconds);
                    return ValueTask.CompletedTask;
                }
            })
            .AddTimeout(TimeSpan.FromSeconds(30))
            .Build();
    }

    /// <summary>
    /// 带自动重试的数据库操作执行
    /// </summary>
    public async Task<T> ExecuteWithRetryAsync<T>(Func<ProDbContext, Task<T>> operation, string operationName = "")
    {
        return await _retryPipeline.ExecuteAsync(async ct =>
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ProDbContext>();

            try
            {
                var result = await operation(dbContext);
                await OnConnectionRecoveredAsync();
                return result;
            }
            catch (Npgsql.NpgsqlException ex)
            {
                await OnConnectionFailedAsync(ex);
                throw;
            }
        });
    }

    /// <summary>
    /// 带自动重试的无返回值数据库操作执行
    /// </summary>
    public async Task ExecuteWithRetryAsync(Func<ProDbContext, Task> operation, string operationName = "")
    {
        await _retryPipeline.ExecuteAsync(async ct =>
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ProDbContext>();

            try
            {
                await operation(dbContext);
                await OnConnectionRecoveredAsync();
            }
            catch (Npgsql.NpgsqlException ex)
            {
                await OnConnectionFailedAsync(ex);
                throw;
            }
        });
    }

    /// <summary>
    /// 检测数据库连接状态
    /// </summary>
    public async Task<bool> CheckConnectionAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ProDbContext>();
            var canConnect = await dbContext.Database.CanConnectAsync();

            if (canConnect && !_isConnected)
            {
                await OnConnectionRecoveredAsync();
            }
            else if (!canConnect && _isConnected)
            {
                await OnConnectionFailedAsync(null);
            }

            return canConnect;
        }
        catch (Exception ex)
        {
            await OnConnectionFailedAsync(ex);
            return false;
        }
    }

    private Task OnConnectionFailedAsync(Exception? ex)
    {
        _consecutiveFailures++;
        _lastFailureTime = DateTime.Now;

        if (_isConnected)
        {
            _isConnected = false;
            var message = _consecutiveFailures >= 3
                ? "数据库连接已断开，请检查网络"
                : "网络连接不稳定，正在重连...";
            ConnectionStatusChanged?.Invoke(false, message);
            Log.Warning(ex, "数据库连接失败 (第{Count}次)", _consecutiveFailures);
        }
        return Task.CompletedTask;
    }

    private Task OnConnectionRecoveredAsync()
    {
        if (!_isConnected)
        {
            _isConnected = true;
            _consecutiveFailures = 0;
            ConnectionStatusChanged?.Invoke(true, "数据库连接已恢复");
            Log.Information("数据库连接已恢复");
        }
        return Task.CompletedTask;
    }
}
