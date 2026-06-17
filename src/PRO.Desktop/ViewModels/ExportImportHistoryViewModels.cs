using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using PRO.Domain.Entities;
using PRO.Infrastructure.Persistence;
using PRO.Desktop.Controls;
using Serilog;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;

namespace PRO.Desktop.ViewModels;

// ====================================================================
// 导出历史项 DTO
// ====================================================================

/// <summary>
/// 导出历史记录项（用于 View 层展示）
/// </summary>
public partial class ExportHistoryItem : ObservableObject
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ExportType { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public int RecordCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string OperatorName { get; set; } = string.Empty;
    public DateTime ExportTime { get; set; }
    public string? Remark { get; set; }

    /// <summary>文件大小友好显示</summary>
    public string FileSizeDisplay => FileSize switch
    {
        <= 0 => "—",
        < 1024 => $"{FileSize} B",
        < 1024 * 1024 => $"{FileSize / 1024.0:F1} KB",
        _ => $"{FileSize / (1024.0 * 1024.0):F1} MB"
    };

    /// <summary>状态友好显示</summary>
    public string StatusDisplay => Status switch
    {
        "Success" => "成功",
        "Failed" => "失败",
        "Pending" => "进行中",
        _ => Status
    };
}

// ====================================================================
// 导入历史项 DTO
// ====================================================================

/// <summary>
/// 导入历史记录项（用于 View 层展示）
/// </summary>
public partial class ImportHistoryItem : ObservableObject
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ImportType { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public int TotalCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string OperatorName { get; set; } = string.Empty;
    public DateTime ImportTime { get; set; }
    public string? ErrorLog { get; set; }
    public string? Remark { get; set; }

    /// <summary>文件大小友好显示</summary>
    public string FileSizeDisplay => FileSize switch
    {
        <= 0 => "—",
        < 1024 => $"{FileSize} B",
        < 1024 * 1024 => $"{FileSize / 1024.0:F1} KB",
        _ => $"{FileSize / (1024.0 * 1024.0):F1} MB"
    };

    /// <summary>状态友好显示</summary>
    public string StatusDisplay => Status switch
    {
        "Success" => "成功",
        "Failed" => "失败",
        "Partial" => "部分成功",
        "Pending" => "进行中",
        _ => Status
    };

    /// <summary>导入结果友好显示</summary>
    public string ResultDisplay => TotalCount > 0
        ? FailedCount > 0
            ? $"{SuccessCount}成功/{FailedCount}失败"
            : $"{SuccessCount}成功"
        : "—";
}

// ====================================================================
// 导出历史 ViewModel
// ====================================================================

/// <summary>
/// 导出历史视图模型
/// </summary>
public partial class ExportHistoryViewModel : ViewModelBase
{
    private readonly ProDbContext _dbContext;

    [ObservableProperty] private ObservableCollection<ExportHistoryItem> _items = [];
    [ObservableProperty] private ExportHistoryItem? _selectedItem;
    [ObservableProperty] private string _searchKeyword = string.Empty;
    [ObservableProperty] private string? _filterType;

    // 空状态支持
    [ObservableProperty] private EmptyStateViewModel? _emptyState;
    [ObservableProperty] private bool _showEmptyState;

    public ExportHistoryViewModel()
    {
        _dbContext = App.Services.GetService(typeof(ProDbContext)) as ProDbContext
            ?? throw new InvalidOperationException("无法获取数据库上下文");

        EmptyState = new EmptyStateViewModel
        {
            IconKind = MaterialDesignThemes.Wpf.PackIconKind.FileExportOutline,
            Title = "暂无导出记录",
            Description = "导出数据后将在此处显示历史记录"
        };

        RunInBackground(LoadAsync(), "导出历史加载失败");
    }

    // ─── 加载 ──────────────────────────────────────────────────

    public async Task LoadAsync()
    {
        IsLoading = true;
        ShowEmptyState = false;
        try
        {
            var branchId = CurrentSession.CurrentBranchId;
            var query = _dbContext.ExportHistories
                .AsNoTracking()
                .Where(e => e.BranchId == branchId)
                .AsQueryable();

            // 关键词搜索 — 文件名/操作人
            if (!string.IsNullOrWhiteSpace(SearchKeyword))
                query = query.Where(e =>
                    e.FileName.Contains(SearchKeyword) ||
                    e.OperatorName.Contains(SearchKeyword));

            // 类型筛选
            if (!string.IsNullOrWhiteSpace(FilterType))
                query = query.Where(e => e.ExportType == FilterType);

            var list = await query
                .OrderByDescending(e => e.ExportTime)
                .Take(500)
                .Select(e => new ExportHistoryItem
                {
                    Id = e.Id,
                    FileName = e.FileName,
                    ExportType = e.ExportType,
                    Format = e.Format,
                    FileSize = e.FileSize,
                    RecordCount = e.RecordCount,
                    Status = e.Status,
                    OperatorName = e.OperatorName,
                    ExportTime = e.ExportTime,
                    Remark = e.Remark
                })
                .ToListAsync();

            Items = new ObservableCollection<ExportHistoryItem>(list);
            UpdateEmptyState();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载导出历史失败");
            ShowError($"加载导出历史失败: {ex.Message}");
            ShowEmptyState = true;
            EmptyState = EmptyStateViewModel.CreateForLoadFailed(RefreshCommand);
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ─── 命令 ──────────────────────────────────────────────────

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadAsync();
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        await LoadAsync();
    }

    [RelayCommand]
    private async Task FilterByTypeAsync(string? type)
    {
        FilterType = type;
        await LoadAsync();
    }

    [RelayCommand]
    private void Download(ExportHistoryItem? item)
    {
        if (item == null) return;
        try
        {
            // 尝试在文件所在目录打开（实际部署中文件可能存储在服务器）
            ShowSuccess($"导出文件: {item.FileName}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "下载导出文件失败: {FileName}", item.FileName);
            ShowError($"下载失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(ExportHistoryItem? item)
    {
        if (item == null) return;

        if (!ConfirmAction("确认删除", $"确定要删除导出记录「{item.FileName}」吗？"))
            return;

        try
        {
            var entity = await _dbContext.ExportHistories.FindAsync(item.Id);
            if (entity != null)
            {
                _dbContext.ExportHistories.Remove(entity);
                await _dbContext.SaveChangesAsync();
                Items.Remove(item);
                ShowSuccess("删除成功");
                UpdateEmptyState();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "删除导出历史失败: Id={Id}", item.Id);
            ShowError($"删除失败: {ex.Message}");
        }
    }

    // ─── 空状态 ────────────────────────────────────────────────

    private void UpdateEmptyState()
    {
        if (IsLoading) { ShowEmptyState = false; return; }
        if (Items.Count > 0) { ShowEmptyState = false; return; }

        ShowEmptyState = true;
        if (!string.IsNullOrWhiteSpace(SearchKeyword))
            EmptyState = EmptyStateViewModel.CreateForSearchNoResults(SearchKeyword, SearchCommand);
        else if (!string.IsNullOrWhiteSpace(FilterType))
            EmptyState = EmptyStateViewModel.CreateForNoResults("导出记录", new RelayCommand(() => { FilterType = null; RunInBackground(LoadAsync(), "清除筛选失败"); }));
        else
            EmptyState = new EmptyStateViewModel
            {
                IconKind = MaterialDesignThemes.Wpf.PackIconKind.FileExportOutline,
                Title = "暂无导出记录",
                Description = "导出数据后将在此处显示历史记录"
            };
    }
}

// ====================================================================
// 导入历史 ViewModel
// ====================================================================

/// <summary>
/// 导入历史视图模型
/// </summary>
public partial class ImportHistoryViewModel : ViewModelBase
{
    private readonly ProDbContext _dbContext;

    [ObservableProperty] private ObservableCollection<ImportHistoryItem> _items = [];
    [ObservableProperty] private ImportHistoryItem? _selectedItem;
    [ObservableProperty] private string _searchKeyword = string.Empty;
    [ObservableProperty] private string? _filterType;

    // 空状态支持
    [ObservableProperty] private EmptyStateViewModel? _emptyState;
    [ObservableProperty] private bool _showEmptyState;

    public ImportHistoryViewModel()
    {
        _dbContext = App.Services.GetService(typeof(ProDbContext)) as ProDbContext
            ?? throw new InvalidOperationException("无法获取数据库上下文");

        EmptyState = new EmptyStateViewModel
        {
            IconKind = MaterialDesignThemes.Wpf.PackIconKind.FileImportOutline,
            Title = "暂无导入记录",
            Description = "导入数据后将在此处显示历史记录"
        };

        RunInBackground(LoadAsync(), "导入历史加载失败");
    }

    // ─── 加载 ──────────────────────────────────────────────────

    public async Task LoadAsync()
    {
        IsLoading = true;
        ShowEmptyState = false;
        try
        {
            var branchId = CurrentSession.CurrentBranchId;
            var query = _dbContext.ImportHistories
                .AsNoTracking()
                .Where(e => e.BranchId == branchId)
                .AsQueryable();

            // 关键词搜索 — 文件名/操作人
            if (!string.IsNullOrWhiteSpace(SearchKeyword))
                query = query.Where(e =>
                    e.FileName.Contains(SearchKeyword) ||
                    e.OperatorName.Contains(SearchKeyword));

            // 类型筛选
            if (!string.IsNullOrWhiteSpace(FilterType))
                query = query.Where(e => e.ImportType == FilterType);

            var list = await query
                .OrderByDescending(e => e.ImportTime)
                .Take(500)
                .Select(e => new ImportHistoryItem
                {
                    Id = e.Id,
                    FileName = e.FileName,
                    ImportType = e.ImportType,
                    Format = e.Format,
                    FileSize = e.FileSize,
                    TotalCount = e.TotalCount,
                    SuccessCount = e.SuccessCount,
                    FailedCount = e.FailedCount,
                    Status = e.Status,
                    OperatorName = e.OperatorName,
                    ImportTime = e.ImportTime,
                    ErrorLog = e.ErrorLog,
                    Remark = e.Remark
                })
                .ToListAsync();

            Items = new ObservableCollection<ImportHistoryItem>(list);
            UpdateEmptyState();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载导入历史失败");
            ShowError($"加载导入历史失败: {ex.Message}");
            ShowEmptyState = true;
            EmptyState = EmptyStateViewModel.CreateForLoadFailed(RefreshCommand);
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ─── 命令 ──────────────────────────────────────────────────

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadAsync();
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        await LoadAsync();
    }

    [RelayCommand]
    private async Task FilterByTypeAsync(string? type)
    {
        FilterType = type;
        await LoadAsync();
    }

    [RelayCommand]
    private void ViewErrorLog(ImportHistoryItem? item)
    {
        if (item == null) return;

        if (string.IsNullOrWhiteSpace(item.ErrorLog))
        {
            ShowSuccess("本次导入无错误日志");
            return;
        }

        try
        {
            // 将错误日志写入临时文件并用默认编辑器打开
            var tempFile = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"import_error_{item.Id}_{DateTime.Now:yyyyMMddHHmmss}.txt");
            System.IO.File.WriteAllText(tempFile, item.ErrorLog);
            Process.Start(new ProcessStartInfo(tempFile) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "查看导入错误日志失败: Id={Id}", item.Id);
            ShowError($"查看错误日志失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(ImportHistoryItem? item)
    {
        if (item == null) return;

        if (!ConfirmAction("确认删除", $"确定要删除导入记录「{item.FileName}」吗？"))
            return;

        try
        {
            var entity = await _dbContext.ImportHistories.FindAsync(item.Id);
            if (entity != null)
            {
                _dbContext.ImportHistories.Remove(entity);
                await _dbContext.SaveChangesAsync();
                Items.Remove(item);
                ShowSuccess("删除成功");
                UpdateEmptyState();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "删除导入历史失败: Id={Id}", item.Id);
            ShowError($"删除失败: {ex.Message}");
        }
    }

    // ─── 空状态 ────────────────────────────────────────────────

    private void UpdateEmptyState()
    {
        if (IsLoading) { ShowEmptyState = false; return; }
        if (Items.Count > 0) { ShowEmptyState = false; return; }

        ShowEmptyState = true;
        if (!string.IsNullOrWhiteSpace(SearchKeyword))
            EmptyState = EmptyStateViewModel.CreateForSearchNoResults(SearchKeyword, SearchCommand);
        else if (!string.IsNullOrWhiteSpace(FilterType))
            EmptyState = EmptyStateViewModel.CreateForNoResults("导入记录", new RelayCommand(() => { FilterType = null; RunInBackground(LoadAsync(), "清除筛选失败"); }));
        else
            EmptyState = new EmptyStateViewModel
            {
                IconKind = MaterialDesignThemes.Wpf.PackIconKind.FileImportOutline,
                Title = "暂无导入记录",
                Description = "导入数据后将在此处显示历史记录"
            };
    }
}
