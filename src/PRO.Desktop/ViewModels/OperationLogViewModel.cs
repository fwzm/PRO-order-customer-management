using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Infrastructure.Persistence;
using PRO.Desktop.Controls;
using Serilog;
using System.Collections.ObjectModel;

namespace PRO.Desktop.ViewModels;

/// <summary>
/// 操作日志查看 ViewModel — 内嵌于 SystemSettingsView 的"操作日志"Tab
/// </summary>
public partial class OperationLogViewModel : ViewModelBase
{
    private readonly ProDbContext _dbContext;

    // ─── 查询条件 ────────────────────────────────────────────

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    [ObservableProperty]
    private string? _selectedModule;

    [ObservableProperty]
    private string? _selectedOperationType;

    [ObservableProperty]
    private string? _selectedResult;

    [ObservableProperty]
    private DateTime? _startDate;

    [ObservableProperty]
    private DateTime? _endDate;

    [ObservableProperty]
    private int _retentionDays = 90;

    // 空状态支持
    [ObservableProperty] private EmptyStateViewModel? _emptyState;
    [ObservableProperty] private bool _showEmptyState;

    // ─── 模块/操作类型/结果下拉 ─────────────────────────────────

    [ObservableProperty]
    private List<string> _modules = [];

    [ObservableProperty]
    private List<string> _operationTypes = [];

    public List<string> ResultOptions { get; } = ["全部", "Success", "Failed"];

    // ─── 分页 ────────────────────────────────────────────────

    [ObservableProperty]
    private int _pageIndex = 1;

    [ObservableProperty]
    private int _pageSize = 50;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _totalPages;

    [ObservableProperty]
    private ObservableCollection<OperationLogDetailDto> _logs = [];

    // ─── 统计 ────────────────────────────────────────────────

    [ObservableProperty]
    private OperationLogStatsDto? _stats;

    [ObservableProperty]
    private bool _isLoadingStats;

    // ─── 详情弹窗 ──────────────────────────────────────────────

    [ObservableProperty]
    private OperationLogDetailDto? _selectedLog;

    // ─── 命令 ────────────────────────────────────────────────

    public IAsyncRelayCommand SearchCommand { get; }
    public IAsyncRelayCommand ResetFiltersCommand { get; }
    public IAsyncRelayCommand PreviousPageCommand { get; }
    public IAsyncRelayCommand NextPageCommand { get; }
    public IAsyncRelayCommand FirstPageCommand { get; }
    public IAsyncRelayCommand LastPageCommand { get; }
    public IRelayCommand<string> SelectModuleCommand { get; }
    public IRelayCommand<string> SelectResultCommand { get; }
    public IAsyncRelayCommand ExportCommand { get; }
    public IAsyncRelayCommand CleanupCommand { get; }
    public IRelayCommand CloseDetailCommand { get; }

    public OperationLogViewModel(ProDbContext dbContext, IOperationLogService? _logService = null)
    {
        _dbContext = dbContext;

        SearchCommand = new AsyncRelayCommand(SearchAsync);
        ResetFiltersCommand = new AsyncRelayCommand(ResetFiltersAsync);
        PreviousPageCommand = new AsyncRelayCommand(GoToPreviousPageAsync, () => PageIndex > 1);
        NextPageCommand = new AsyncRelayCommand(GoToNextPageAsync, () => PageIndex < TotalPages);
        FirstPageCommand = new AsyncRelayCommand(GoToFirstPageAsync);
        LastPageCommand = new AsyncRelayCommand(GoToLastPageAsync);
        SelectModuleCommand = new RelayCommand<string>(OnSelectModule);
        SelectResultCommand = new RelayCommand<string>(OnSelectResult);
        ExportCommand = new AsyncRelayCommand(ExportAsync);
        CleanupCommand = new AsyncRelayCommand(CleanupAsync);
        CloseDetailCommand = new RelayCommand(() => SelectedLog = null);
    }

    // ─── 初始化 ───────────────────────────────────────────────

    [RelayCommand]
    public async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            // 加载筛选下拉数据
            var modulesTask = LoadModulesAsync();
            var statsTask = LoadStatsAsync();
            var searchTask = SearchAsync();

            await Task.WhenAll(modulesTask, statsTask, searchTask);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "操作日志页面初始化失败");
            ShowError($"加载失败: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ─── 查询 ─────────────────────────────────────────────────

    private bool HasActiveFilters() =>
        SelectedModule != null || SelectedOperationType != null || SelectedResult != null ||
        StartDate.HasValue || EndDate.HasValue;

    private async Task SearchAsync()
    {
        IsLoading = true;
        try
        {
            var query = _dbContext.OperationLogs
                .AsNoTracking()
                .Include(l => l.Operator)
                .AsQueryable();

            // 关键词搜索 — 内容/工号/操作人
            if (!string.IsNullOrWhiteSpace(SearchKeyword))
                query = query.Where(l =>
                    l.Content.Contains(SearchKeyword) ||
                    l.OperatorNo.Contains(SearchKeyword) ||
                    (l.Operator != null && l.Operator.Name.Contains(SearchKeyword)));

            // 模块筛选
            if (!string.IsNullOrWhiteSpace(SelectedModule) && SelectedModule != "全部")
                query = query.Where(l => l.Module == SelectedModule);

            // 操作类型筛选
            if (!string.IsNullOrWhiteSpace(SelectedOperationType) && SelectedOperationType != "全部")
                query = query.Where(l => l.OperationType == SelectedOperationType);

            // 结果筛选
            if (!string.IsNullOrWhiteSpace(SelectedResult) && SelectedResult != "全部")
                query = query.Where(l => l.Result == SelectedResult);

            // 时间范围
            if (StartDate.HasValue)
                query = query.Where(l => l.OperatedAt >= StartDate.Value);
            if (EndDate.HasValue)
                query = query.Where(l => l.OperatedAt < EndDate.Value.AddDays(1));

            // 先获取总数
            TotalCount = await query.CountAsync();
            TotalPages = (int)Math.Ceiling(TotalCount * 1.0 / PageSize);

            if (PageIndex > TotalPages && TotalPages > 0)
                PageIndex = TotalPages;

            // 分页查询
            var items = await query
                .OrderByDescending(l => l.OperatedAt)
                .Skip((PageIndex - 1) * PageSize)
                .Take(PageSize)
                .Select(l => new OperationLogDetailDto
                {
                    Id = l.Id,
                    OperatorId = l.OperatorId,
                    OperatorName = l.Operator != null ? l.Operator.Name : "",
                    OperatorNo = l.OperatorNo,
                    Module = l.Module,
                    OperationType = l.OperationType,
                    Content = l.Content,
                    EntityType = l.EntityType,
                    EntityId = l.EntityId,
                    Result = l.Result,
                    ErrorMessage = l.ErrorMessage,
                    OperatedAt = l.OperatedAt,
                    SyncStatus = l.SyncStatus
                })
                .ToListAsync();

            Logs = new ObservableCollection<OperationLogDetailDto>(items);

            // 空状态
            if (TotalCount == 0)
            {
                ShowEmptyState = true;
                if (!string.IsNullOrWhiteSpace(SearchKeyword))
                    EmptyState = EmptyStateViewModel.CreateForSearchNoResults(SearchKeyword, SearchCommand);
                else if (HasActiveFilters())
                    EmptyState = EmptyStateViewModel.CreateForNoResults("操作日志", ResetFiltersCommand);
                else
                    EmptyState = EmptyStateViewModel.CreateForEmpty("操作日志");
            }
            else { ShowEmptyState = false; }

            // 更新导航按钮
            NotifyNavigationChanged();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "操作日志查询失败");
            ShowBusinessException(ex, "查询操作日志");
            ShowEmptyState = true;
            EmptyState = EmptyStateViewModel.CreateForLoadFailed(SearchCommand);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ResetFiltersAsync()
    {
        SearchKeyword = string.Empty;
        SelectedModule = null;
        SelectedOperationType = null;
        SelectedResult = null;
        StartDate = null;
        EndDate = null;
        PageIndex = 1;
        await SearchAsync();
    }

    // ─── 统计 ─────────────────────────────────────────────────

    private async Task LoadStatsAsync()
    {
        IsLoadingStats = true;
        try
        {
            var cutoffDate = DateTime.Now.AddDays(-30);
            var query = _dbContext.OperationLogs.AsNoTracking()
                .Where(l => l.OperatedAt >= cutoffDate);

            var totalCount = await query.CountAsync();
            var todayCount = await query.CountAsync(l => l.OperatedAt >= DateTime.Today);
            var weekStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
            var weekCount = await query.CountAsync(l => l.OperatedAt >= weekStart);
            var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var monthCount = await query.CountAsync(l => l.OperatedAt >= monthStart);
            var failedCount = await query.CountAsync(l => l.Result == "Failed");

            var moduleStats = await query
                .GroupBy(l => l.Module)
                .Select(g => new ModuleStats { Module = g.Key, Count = g.Count() })
                .OrderByDescending(m => m.Count)
                .Take(10)
                .ToListAsync();
            var total = moduleStats.Sum(m => m.Count);
            foreach (var ms in moduleStats)
                ms.Percentage = total > 0 ? Math.Round(ms.Count * 100.0 / total, 1) : 0;

            var dailyStats = await query
                .GroupBy(l => l.OperatedAt.Date)
                .Select(g => new DailyStats { Date = g.Key, Count = g.Count() })
                .OrderBy(d => d.Date)
                .ToListAsync();

            Stats = new OperationLogStatsDto
            {
                TotalCount = totalCount,
                TodayCount = todayCount,
                WeekCount = weekCount,
                MonthCount = monthCount,
                FailedCount = failedCount,
                ModuleStats = moduleStats,
                DailyStats = dailyStats
            };
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "操作日志统计加载失败");
        }
        finally
        {
            IsLoadingStats = false;
            OnPropertyChanged(nameof(IsLoadingStats));
        }
    }

    // ─── 筛选数据 ─────────────────────────────────────────────

    private async Task LoadModulesAsync()
    {
        try
        {
            var list = await _dbContext.OperationLogs
                .AsNoTracking()
                .Select(l => l.Module)
                .Distinct()
                .OrderBy(m => m)
                .ToListAsync();
            Modules = ["全部", .. list];
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "模块列表加载失败");
            Modules = ["全部"];
        }
    }

    private async Task LoadOperationTypesAsync()
    {
        try
        {
            var query = _dbContext.OperationLogs.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(SelectedModule) && SelectedModule != "全部")
                query = query.Where(l => l.Module == SelectedModule);

            var list = await query
                .Select(l => l.OperationType)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync();

            OperationTypes = ["全部", .. list];
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "操作类型列表加载失败");
            OperationTypes = ["全部"];
        }
    }

    private void OnSelectModule(string? module)
    {
        SelectedModule = module;
        RunInBackground(OnSelectModuleAsync(), "筛选操作类型失败");
    }

    private async Task OnSelectModuleAsync()
    {
        await LoadOperationTypesAsync();
        await SearchAsync();
    }

    private void OnSelectResult(string? result)
    {
        SelectedResult = result;
        RunInBackground(SearchAsync(), "筛选结果失败");
    }

    // ─── 导出 ─────────────────────────────────────────────────

    private async Task ExportAsync()
    {
        try
        {
            var query = _dbContext.OperationLogs
                .AsNoTracking()
                .Include(l => l.Operator)
                .AsQueryable();

            if (StartDate.HasValue) query = query.Where(l => l.OperatedAt >= StartDate.Value);
            if (EndDate.HasValue) query = query.Where(l => l.OperatedAt < EndDate.Value.AddDays(1));
            if (!string.IsNullOrWhiteSpace(SelectedModule) && SelectedModule != "全部")
                query = query.Where(l => l.Module == SelectedModule);
            if (!string.IsNullOrWhiteSpace(SelectedResult) && SelectedResult != "全部")
                query = query.Where(l => l.Result == SelectedResult);

            var logs = await query
                .OrderByDescending(l => l.OperatedAt)
                .Take(10000)
                .ToListAsync();

            // 构建CSV
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("\uFEFF时间,操作人,工号,模块,操作类型,内容,关联实体,结果,错误信息");
            foreach (var log in logs)
            {
                var entity = string.IsNullOrEmpty(log.EntityType) ? "" : $"{log.EntityType}({log.EntityId})";
                sb.AppendLine($"{log.OperatedAt:yyyy-MM-dd HH:mm:ss},{log.Operator?.Name ?? ""},{log.OperatorNo},{log.Module},{log.OperationType},\"{log.Content?.Replace("\"", "\"\"")}\",{entity},{log.Result},{log.ErrorMessage?.Replace("\"", "\"\"")}");
            }

            // 保存到文件
            var fileName = $"操作日志_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var filePath = System.IO.Path.Combine(desktopPath, fileName);
            await System.IO.File.WriteAllTextAsync(filePath, sb.ToString(), System.Text.Encoding.UTF8);

            ShowSuccess($"已导出 {logs.Count} 条日志到桌面: {fileName}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "导出操作日志失败");
            ShowError($"导出失败: {ex.Message}");
        }
    }

    // ─── 清理 ─────────────────────────────────────────────────

    private async Task CleanupAsync()
    {
        try
        {
            var cutoffDate = DateTime.Now.AddDays(-RetentionDays);
            var oldLogs = await _dbContext.OperationLogs
                .Where(l => l.OperatedAt < cutoffDate)
                .ToListAsync();

            if (oldLogs.Count == 0)
            {
                ShowSuccess($"没有 {RetentionDays} 天之前的日志需要清理");
                return;
            }

            _dbContext.OperationLogs.RemoveRange(oldLogs);
            await _dbContext.SaveChangesAsync();

            ShowSuccess($"已清理 {oldLogs.Count} 条 {RetentionDays} 天前的日志");
            await SearchAsync();
            await LoadStatsAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "清理操作日志失败");
            ShowError($"清理失败: {ex.Message}");
        }
    }

    // ─── 分页 ─────────────────────────────────────────────────

    private async Task GoToPreviousPageAsync()
    {
        if (PageIndex > 1) { PageIndex--; await SearchAsync(); }
    }

    private async Task GoToNextPageAsync()
    {
        if (PageIndex < TotalPages) { PageIndex++; await SearchAsync(); }
    }

    private async Task GoToFirstPageAsync()
    {
        PageIndex = 1; await SearchAsync();
    }

    private async Task GoToLastPageAsync()
    {
        PageIndex = TotalPages; await SearchAsync();
    }

    private void NotifyNavigationChanged()
    {
        (PreviousPageCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (NextPageCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(PageIndex));
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(TotalCount));
    }

    // ─── 查看详情 ─────────────────────────────────────────────

    [RelayCommand]
    public void ViewDetail(OperationLogDetailDto? log)
    {
        SelectedLog = log;
    }
}
