using Serilog;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 操作重试服务 - 支持关键操作失败重试
/// </summary>
public class RetryService
{
    /// <summary>
    /// 带重试的异步操作
    /// </summary>
    public static async Task<RetryResult<T>> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        int maxRetries = 3,
        TimeSpan? delay = null)
    {
        var retryDelay = delay ?? TimeSpan.FromSeconds(1);
        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var result = await operation();
                if (attempt > 1)
                {
                    Log.Information("操作 {Operation} 在第 {Attempt} 次尝试成功", operationName, attempt);
                }
                return RetryResult<T>.Success(result, attempt);
            }
            catch (Exception ex)
            {
                lastException = ex;
                Log.Warning(ex, "操作 {Operation} 第 {Attempt}/{MaxRetries} 次失败", operationName, attempt, maxRetries);

                if (attempt < maxRetries)
                {
                    await Task.Delay(retryDelay * attempt); // 指数退避
                }
            }
        }

        return RetryResult<T>.Failure(lastException!, maxRetries);
    }

    /// <summary>
    /// 带重试的无返回值异步操作
    /// </summary>
    public static async Task<RetryResult<bool>> ExecuteWithRetryAsync(
        Func<Task> operation,
        string operationName,
        int maxRetries = 3,
        TimeSpan? delay = null)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            await operation();
            return true;
        }, operationName, maxRetries, delay);
    }

    /// <summary>
    /// 带重试和进度回调的异步操作
    /// </summary>
    public static async Task<RetryResult<T>> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        Action<int, int, string>? onProgress,
        int maxRetries = 3)
    {
        var delay = TimeSpan.FromSeconds(1);
        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                onProgress?.Invoke(attempt, maxRetries, $"正在执行 {operationName}...");
                var result = await operation();

                onProgress?.Invoke(attempt, maxRetries, "操作成功");
                return RetryResult<T>.Success(result, attempt);
            }
            catch (Exception ex)
            {
                lastException = ex;
                var message = $"第 {attempt} 次尝试失败：{ex.Message}";
                onProgress?.Invoke(attempt, maxRetries, message);

                if (attempt < maxRetries)
                {
                    onProgress?.Invoke(attempt, maxRetries, $"{delay.TotalSeconds * attempt}秒后重试...");
                    await Task.Delay(delay * attempt);
                }
            }
        }

        return RetryResult<T>.Failure(lastException!, maxRetries);
    }

    public static async Task<RetryResult<bool>> ExecuteWithRetryAsync(
        Func<Task> operation,
        string operationName,
        Action<int, int, string>? onProgress,
        int maxRetries = 3)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            await operation();
            return true;
        }, operationName, onProgress, maxRetries);
    }
}

/// <summary>
/// 重试结果
/// </summary>
public class RetryResult<T>
{
    public bool IsSuccess { get; set; }
    public T? Data { get; set; }
    public int AttemptCount { get; set; }
    public Exception? LastException { get; set; }
    public string? ErrorMessage { get; set; }

    public static RetryResult<T> Success(T data, int attemptCount) => new()
    {
        IsSuccess = true,
        Data = data,
        AttemptCount = attemptCount
    };

    public static RetryResult<T> Failure(Exception ex, int attemptCount) => new()
    {
        IsSuccess = false,
        LastException = ex,
        AttemptCount = attemptCount,
        ErrorMessage = $"操作失败（已重试 {attemptCount} 次）：{ex.Message}"
    };
}

/// <summary>
/// 带重试的操作命令封装
/// </summary>
public class RetriableOperation
{
    public string Name { get; set; } = "";
    public Func<Task> Operation { get; set; } = () => Task.CompletedTask;
    public int MaxRetries { get; set; } = 3;
    public bool RequiresConfirmation { get; set; }
    public string? ConfirmationMessage { get; set; }

    /// <summary>
    /// 执行操作（带重试）
    /// </summary>
    public async Task<RetryResult<bool>> ExecuteAsync(Action<int, int, string>? onProgress = null)
    {
        return await RetryService.ExecuteWithRetryAsync(Operation, Name, onProgress, MaxRetries);
    }
}
