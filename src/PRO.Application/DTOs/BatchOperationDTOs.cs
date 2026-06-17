using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PRO.Application.DTOs;

/// <summary>
/// 批量操作进度 - 支持UI实时显示进度条、统计、失败明细
/// </summary>
public class BatchOperationProgress : INotifyPropertyChanged
{
    private string _operationName = "";
    private int _totalCount;
    private int _processedCount;
    private int _successCount;
    private int _failedCount;
    private int _skippedCount;
    private string _currentItemName = "";
    private string _currentItemDetail = "";
    private bool _isRunning;
    private bool _isCancelled;
    private bool _isCompleted;
    private string _statusText = "";
    private double _progressPercent;
    private TimeSpan? _estimatedRemaining;
    private DateTime _startTime;

    public string OperationName { get => _operationName; set => SetProperty(ref _operationName, value); }
    public int TotalCount { get => _totalCount; set => SetProperty(ref _totalCount, value); }
    public int ProcessedCount { get => _processedCount; set => SetProperty(ref _processedCount, value); }
    public int SuccessCount { get => _successCount; set => SetProperty(ref _successCount, value); }
    public int FailedCount { get => _failedCount; set => SetProperty(ref _failedCount, value); }
    public int SkippedCount { get => _skippedCount; set => SetProperty(ref _skippedCount, value); }
    public string CurrentItemName { get => _currentItemName; set => SetProperty(ref _currentItemName, value); }
    public string CurrentItemDetail { get => _currentItemDetail; set => SetProperty(ref _currentItemDetail, value); }
    public bool IsRunning { get => _isRunning; set => SetProperty(ref _isRunning, value); }
    public bool IsCancelled { get => _isCancelled; set => SetProperty(ref _isCancelled, value); }
    public bool IsCompleted { get => _isCompleted; set => SetProperty(ref _isCompleted, value); }
    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }
    public double ProgressPercent { get => _progressPercent; set => SetProperty(ref _progressPercent, value); }
    public TimeSpan? EstimatedRemaining { get => _estimatedRemaining; set => SetProperty(ref _estimatedRemaining, value); }
    public DateTime StartTime { get => _startTime; set => SetProperty(ref _startTime, value); }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>失败项明细</summary>
    public List<BatchOperationFailureDetail> FailedItems { get; set; } = new();

    /// <summary>是否有失败项</summary>
    public bool HasFailures => FailedItems.Count > 0;

    /// <summary>是否全部成功</summary>
    public bool IsAllSuccess => IsCompleted && FailedCount == 0 && !IsCancelled;

    /// <summary>完成摘要</summary>
    public string Summary
    {
        get
        {
            if (!IsCompleted) return "";
            if (IsCancelled) return $"已取消，完成 {ProcessedCount}/{TotalCount} 项";
            if (IsAllSuccess) return $"全部成功，共 {SuccessCount} 项";
            return $"完成 {ProcessedCount} 项：成功 {SuccessCount}，失败 {FailedCount}，跳过 {SkippedCount}";
        }
    }

    /// <summary>耗时文本</summary>
    public string ElapsedText
    {
        get
        {
            if (StartTime == default) return "";
            var elapsed = DateTime.Now - StartTime;
            if (elapsed.TotalSeconds < 60) return $"耗时 {elapsed.TotalSeconds:F0} 秒";
            if (elapsed.TotalMinutes < 60) return $"耗时 {elapsed.TotalMinutes:F0} 分钟";
            return $"耗时 {elapsed.TotalHours:F1} 小时";
        }
    }

    public void UpdateProgress()
    {
        ProcessedCount = SuccessCount + FailedCount + SkippedCount;
        ProgressPercent = TotalCount > 0 ? (double)ProcessedCount / TotalCount * 100 : 0;

        if (ProcessedCount > 0 && IsRunning)
        {
            var elapsed = DateTime.Now - StartTime;
            var avgPerItem = elapsed / ProcessedCount;
            EstimatedRemaining = avgPerItem * (TotalCount - ProcessedCount);
        }

        StatusText = IsRunning
            ? $"处理中 {ProcessedCount}/{TotalCount}..."
            : IsCompleted ? Summary : "准备中...";
    }

    /// <summary>开始操作</summary>
    public void Start(string operationName, int totalCount)
    {
        OperationName = operationName;
        TotalCount = totalCount;
        StartTime = DateTime.Now;
        IsRunning = true;
        IsCompleted = false;
        IsCancelled = false;
        SuccessCount = 0;
        FailedCount = 0;
        SkippedCount = 0;
        ProcessedCount = 0;
        FailedItems.Clear();
        UpdateProgress();
    }

    /// <summary>标记完成</summary>
    public void Complete()
    {
        IsRunning = false;
        IsCompleted = true;
        UpdateProgress();
    }

    /// <summary>标记取消</summary>
    public void Cancel()
    {
        IsRunning = false;
        IsCancelled = true;
        IsCompleted = true;
        UpdateProgress();
    }

    /// <summary>记录成功</summary>
    public void RecordSuccess(string itemName)
    {
        SuccessCount++;
        CurrentItemName = itemName;
        CurrentItemDetail = "成功";
        UpdateProgress();
    }

    /// <summary>记录失败</summary>
    public void RecordFailure(string itemName, string reason, int? itemId = null)
    {
        FailedCount++;
        CurrentItemName = itemName;
        CurrentItemDetail = reason;
        FailedItems.Add(new BatchOperationFailureDetail
        {
            ItemId = itemId,
            ItemName = itemName,
            ErrorMessage = reason,
            FailedAt = DateTime.Now,
            CanRetry = true
        });
        UpdateProgress();
    }

    /// <summary>记录跳过</summary>
    public void RecordSkip(string itemName, string reason = "")
    {
        SkippedCount++;
        CurrentItemName = itemName;
        CurrentItemDetail = reason;
        UpdateProgress();
    }
}

/// <summary>
/// 批量操作上下文 - 封装批量操作的配置和取消支持
/// </summary>
public class BatchOperationContext
{
    public string OperationName { get; set; } = "";
    public CancellationToken CancellationToken { get; set; }
    public IProgress<BatchOperationProgress>? Progress { get; set; }
    public BatchOperationProgress ProgressInfo { get; set; } = new();

    /// <summary>单次最大批量数量</summary>
    public int MaxBatchSize { get; set; } = 500;

    /// <summary>是否允许部分失败</summary>
    public bool AllowPartialFailure { get; set; } = true;

    /// <summary>失败后是否继续</summary>
    public bool ContinueOnError { get; set; } = true;

    /// <summary>是否支持重试失败项</summary>
    public bool SupportRetry { get; set; } = true;
}

/// <summary>
/// 批量操作失败项明细（新增强版）
/// </summary>
public class BatchOperationFailureDetail
{
    /// <summary>项目ID（订单ID、客户ID等）</summary>
    public int? ItemId { get; set; }

    /// <summary>项目名称</summary>
    public string ItemName { get; set; } = "";

    /// <summary>错误信息</summary>
    public string ErrorMessage { get; set; } = "";

    /// <summary>失败时间</summary>
    public DateTime FailedAt { get; set; }

    /// <summary>是否可重试</summary>
    public bool CanRetry { get; set; } = true;
}
