using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRO.Infrastructure.Persistence;
using PRO.Domain.Entities;
using PRO.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Input;

namespace PRO.Desktop.ViewModels;

/// <summary>
/// 错误统计仪表板 ViewModel
/// 支持按时间范围与错误类型进行多维筛选，展示错误趋势及排名
/// </summary>
public partial class ErrorDashboardViewModel : ViewModelBase
{
    private readonly ProDbContext _dbContext;
    private readonly BusinessMessageService _messageService;

    // ==================== 筛选条件 ====================

    [ObservableProperty]
    private DateTime _dateFrom = DateTime.Today.AddDays(-30);

    [ObservableProperty]
    private DateTime _dateTo = DateTime.Today;

    [ObservableProperty]
    private string? _filterModule;

    [ObservableProperty]
    private string? _filterErrorCode;

    [ObservableProperty]
    private ObservableCollection<string> _availableModules = [];

    [ObservableProperty]
    private ObservableCollection<string> _availableErrorCodes = [];

    // ==================== 统计数据 ====================

    [ObservableProperty]
    private int _totalErrorCount;

    [ObservableProperty]
    private int _resolvedCount;

    [ObservableProperty]
    private int _unresolvedCount;

    [ObservableProperty]
    private double _resolutionRate;

    [ObservableProperty]
    private string _topErrorCode = "";

    [ObservableProperty]
    private int _topErrorCount;

    // ==================== 图表数据 ====================

    [ObservableProperty]
    private ObservableCollection<ErrorTrendPoint> _trendPoints = [];

    [ObservableProperty]
    private ObservableCollection<ErrorCodeSummary> _errorCodeSummary = [];

    [ObservableProperty]
    private ObservableCollection<ModuleSummary> _moduleSummary = [];

    [ObservableProperty]
    private ObservableCollection<RecentErrorItem> _recentErrors = [];

    [ObservableProperty]
    private double _trendMaxValue;

    [ObservableProperty]
    private double _trendBarWidth = 30;

    // ==================== UI 状态 ====================

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private bool _isRefreshing;

    public ErrorDashboardViewModel()
    {
        _dbContext = App.Services.GetService(typeof(ProDbContext)) as ProDbContext
            ?? throw new InvalidOperationException("无法获取数据库上下文");
        _messageService = App.Services.GetService(typeof(BusinessMessageService)) as BusinessMessageService
            ?? throw new InvalidOperationException("无法获取 BusinessMessageService");

        RunInBackground(InitializeAsync(), "加载错误仪表板失败");
    }

    private async Task InitializeAsync()
    {
        await LoadFilterOptionsAsync();
        await RefreshAsync();
    }

    private async Task LoadFilterOptionsAsync()
    {
        try
        {
            var modules = await _dbContext.BusinessErrorMetrics
                .Where(m => m.Module != null)
                .Select(m => m.Module!)
                .Distinct()
                .OrderBy(m => m)
                .ToListAsync();
            AvailableModules = new ObservableCollection<string>(modules);

            var codes = await _dbContext.BusinessErrorMetrics
                .Where(m => m.ErrorCode != null)
                .Select(m => m.ErrorCode!)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();
            AvailableErrorCodes = new ObservableCollection<string>(codes);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "加载筛选选项失败");
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsRefreshing) return;
        IsRefreshing = true;
        ErrorMessage = null;

        try
        {
            await Task.WhenAll(
                LoadTrendDataAsync(),
                LoadErrorCodeSummaryAsync(),
                LoadModuleSummaryAsync(),
                LoadRecentErrorsAsync(),
                LoadSummaryStatsAsync()
            );
        }
        catch (Exception ex)
        {
            ErrorMessage = $"加载数据失败: {ex.Message}";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task FilterByErrorCodeAsync(string? errorCode)
    {
        FilterErrorCode = errorCode;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task FilterByModuleAsync(string? module)
    {
        FilterModule = module;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task ClearFiltersAsync()
    {
        FilterModule = null;
        FilterErrorCode = null;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task ResolveErrorAsync(long errorId)
    {
        try
        {
            var metric = await _dbContext.BusinessErrorMetrics.FindAsync(errorId);
            if (metric != null)
            {
                metric.IsResolved = true;
                await _dbContext.SaveChangesAsync();
                await RefreshAsync();
            }
        }
        catch (Exception ex)
        {
            ShowError($"标记失败: {ex.Message}");
        }
    }

    // ==================== 数据加载 ====================

    private IQueryable<BusinessErrorMetric> BuildQuery()
    {
        var query = _dbContext.BusinessErrorMetrics
            .AsNoTracking()
            .Where(m => m.OccurredAt >= DateFrom && m.OccurredAt <= DateTo.AddDays(1));

        if (!string.IsNullOrWhiteSpace(FilterModule))
            query = query.Where(m => m.Module == FilterModule);
        if (!string.IsNullOrWhiteSpace(FilterErrorCode))
            query = query.Where(m => m.ErrorCode == FilterErrorCode);

        return query;
    }

    private async Task LoadTrendDataAsync()
    {
        var query = BuildQuery();

        var rawData = await query
            .GroupBy(m => m.OccurredAt.Date)
            .Select(g => new
            {
                Date = g.Key,
                Count = g.Count(),
                Unresolved = g.Count(m => !m.IsResolved)
            })
            .OrderBy(x => x.Date)
            .ToListAsync();

        var start = DateFrom.Date;
        var end = DateTo.Date;
        var allDays = new List<ErrorTrendPoint>();

        for (var d = start; d <= end; d = d.AddDays(1))
        {
            var data = rawData.FirstOrDefault(x => x.Date == d);
            allDays.Add(new ErrorTrendPoint
            {
                Date = d,
                TotalCount = data?.Count ?? 0,
                UnresolvedCount = data?.Unresolved ?? 0
            });
        }

        TrendPoints = new ObservableCollection<ErrorTrendPoint>(allDays);
        TrendMaxValue = allDays.Any() ? allDays.Max(x => Math.Max(x.TotalCount, 1)) * 1.2 : 10;
        TrendBarWidth = Math.Max(8, Math.Min(30, 800.0 / Math.Max(allDays.Count, 1)));
    }

    private async Task LoadErrorCodeSummaryAsync()
    {
        var query = BuildQuery();

        var data = await query
            .GroupBy(m => m.ErrorCode)
            .Select(g => new ErrorCodeSummary
            {
                ErrorCode = g.Key,
                Count = g.Count(),
                Percentage = 0,
                LastOccurred = g.Max(m => m.OccurredAt)
            })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync();

        var total = data.Sum(x => x.Count);
        foreach (var item in data)
            item.Percentage = total > 0 ? Math.Round(100.0 * item.Count / total, 1) : 0;

        ErrorCodeSummary = new ObservableCollection<ErrorCodeSummary>(data);

        if (data.Any())
        {
            TopErrorCode = data.First().ErrorCode;
            TopErrorCount = data.First().Count;
        }
    }

    private async Task LoadModuleSummaryAsync()
    {
        var query = BuildQuery();

        var data = await query
            .GroupBy(m => m.Module ?? "未分类")
            .Select(g => new ModuleSummary
            {
                Module = g.Key,
                Count = g.Count(),
                UnresolvedCount = g.Count(m => !m.IsResolved),
                TopError = g.GroupBy(m => m.ErrorCode)
                    .OrderByDescending(g2 => g2.Count())
                    .Select(g2 => g2.Key)
                    .FirstOrDefault() ?? "-"
            })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        ModuleSummary = new ObservableCollection<ModuleSummary>(data);
    }

    private async Task LoadRecentErrorsAsync()
    {
        var query = BuildQuery();

        var data = await query
            .OrderByDescending(m => m.OccurredAt)
            .Take(20)
            .Select(m => new RecentErrorItem
            {
                Id = m.Id,
                ErrorCode = m.ErrorCode,
                Module = m.Module ?? "-",
                Operation = m.Operation ?? "-",
                UserMessage = m.UserMessage ?? "",
                OccurredAt = m.OccurredAt,
                IsResolved = m.IsResolved,
                TraceId = m.TraceId ?? "-"
            })
            .ToListAsync();

        RecentErrors = new ObservableCollection<RecentErrorItem>(data);
    }

    private async Task LoadSummaryStatsAsync()
    {
        var query = BuildQuery();

        TotalErrorCount = await query.CountAsync();
        ResolvedCount = await query.CountAsync(m => m.IsResolved);
        UnresolvedCount = TotalErrorCount - ResolvedCount;
        ResolutionRate = TotalErrorCount > 0
            ? Math.Round(100.0 * ResolvedCount / TotalErrorCount, 1) : 100;
    }
}

// ==================== 视图辅助类型 ====================

/// <summary>错误趋势数据点</summary>
public partial class ErrorTrendPoint : ObservableObject
{
    public DateTime Date { get; set; }
    public string DateLabel => Date.ToString("MM/dd");
    public int TotalCount { get; set; }
    public int UnresolvedCount { get; set; }
    public double BarHeight => TotalCount > 0 ? TotalCount : 1;
    public SolidColorBrush BarColor => UnresolvedCount > 0
        ? new SolidColorBrush(Color.FromRgb(255, 69, 58))  // 红色
        : new SolidColorBrush(Color.FromRgb(48, 209, 88));  // 绿色
}

/// <summary>错误码汇总</summary>
public partial class ErrorCodeSummary : ObservableObject
{
    public string ErrorCode { get; set; } = "";
    public int Count { get; set; }
    public double Percentage { get; set; }
    public DateTime LastOccurred { get; set; }
    public string PercentageText => $"{Percentage:F1}%";
    public string LastOccurredText => LastOccurred.ToString("MM-dd HH:mm");
}

/// <summary>模块汇总</summary>
public partial class ModuleSummary : ObservableObject
{
    public string Module { get; set; } = "";
    public int Count { get; set; }
    public int UnresolvedCount { get; set; }
    public string TopError { get; set; } = "-";
    public SolidColorBrush HealthColor =>
        UnresolvedCount > 5 ? new SolidColorBrush(Color.FromRgb(255, 69, 58)) :
        UnresolvedCount > 0 ? new SolidColorBrush(Color.FromRgb(255, 149, 0)) :
        new SolidColorBrush(Color.FromRgb(48, 209, 88));
}

/// <summary>最近错误项</summary>
public partial class RecentErrorItem : ObservableObject
{
    public long Id { get; set; }
    public string ErrorCode { get; set; } = "";
    public string Module { get; set; } = "-";
    public string Operation { get; set; } = "-";
    public string UserMessage { get; set; } = "";
    public DateTime OccurredAt { get; set; }
    public bool IsResolved { get; set; }
    public string TraceId { get; set; } = "-";
    public string OccurredAtText => OccurredAt.ToString("yyyy-MM-dd HH:mm:ss");
    public string ResolvedBadge => IsResolved ? "已处理" : "未处理";
    public SolidColorBrush ResolvedColor =>
        IsResolved ? new SolidColorBrush(Color.FromRgb(48, 209, 88))
                   : new SolidColorBrush(Color.FromRgb(255, 149, 0));
}
