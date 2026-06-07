using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRO.Domain.Enums;
using PRO.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace PRO.Desktop.ViewModels;

/// <summary>
/// ViewModel基类
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _successMessage;

    partial void OnIsLoadingChanged(bool value)
    {
        if (value)
            App.ShowLoading?.Invoke(null);
        else
            App.HideLoading?.Invoke();
    }

    protected void ShowError(string message)
    {
        ErrorMessage = message;
        SuccessMessage = null;
        App.ShowToast?.Invoke(message, "提示", false);
    }

    protected void ShowSuccess(string message)
    {
        SuccessMessage = message;
        ErrorMessage = null;
        App.ShowToast?.Invoke(message, "成功", true);
    }

    protected void ClearMessages()
    {
        ErrorMessage = null;
        SuccessMessage = null;
    }

    protected void RunInBackground(Task task, string failureMessage)
    {
        _ = ObserveAsync(task, failureMessage);
    }

    private async Task ObserveAsync(Task task, string failureMessage)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            Log.Error(ex, failureMessage);
            ShowError($"{failureMessage}: {ex.Message}");
        }
    }

    /// <summary>
    /// 带自动重试的数据库操作（使用 ConnectionHealthService）
    /// </summary>
    protected async Task<T?> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, string operationName = "")
    {
        try
        {
            var healthService = App.Services.GetService<ConnectionHealthService>();
            if (healthService != null)
            {
                return await healthService.ExecuteWithRetryAsync(async _ => await operation(), operationName);
            }
            return await operation();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "数据库操作失败: {Operation}", operationName);
            ShowError($"操作失败: {ex.Message}");
            return default;
        }
    }

    /// <summary>
    /// 带自动重试的数据库操作（无返回值）
    /// </summary>
    protected async Task ExecuteWithRetryAsync(Func<Task> operation, string operationName = "")
    {
        try
        {
            var healthService = App.Services.GetService<ConnectionHealthService>();
            if (healthService != null)
            {
                await healthService.ExecuteWithRetryAsync(async _ => await operation(), operationName);
            }
            else
            {
                await operation();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "数据库操作失败: {Operation}", operationName);
            ShowError($"操作失败: {ex.Message}");
        }
    }
}

/// <summary>
/// 分页ViewModel基类
/// </summary>
public abstract partial class PagedViewModelBase : ViewModelBase
{
    private bool _suppressPageIndexLoad;

    // 分页缓存：仅当筛选条件变化时重新计算总数
    private string? _lastCountCacheKey;
    private int _cachedTotalCount = -1;

    // 批量选择
    [ObservableProperty]
    private bool _isBatchMode;

    [ObservableProperty]
    private bool _isAllSelected;

    [ObservableProperty]
    private int _selectedCount;

    [ObservableProperty]
    private int _pageIndex = 1;

    [ObservableProperty]
    private int _pageSize = 50;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _totalPages = 1;

    [ObservableProperty]
    private string? _searchKeyword;

    public int ComputedTotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount * 1.0 / Math.Max(1, PageSize)));

    partial void OnPageIndexChanged(int value)
    {
        if (_suppressPageIndexLoad)
        {
            return;
        }

        ClearBatchSelection();
        RunInBackground(LoadDataAsync(), "加载分页数据失败");
    }

    partial void OnPageSizeChanged(int value)
    {
        UpdateTotalPages();
        ClearBatchSelection();
        RunInBackground(ResetToFirstPageAndLoadAsync(), "刷新分页数据失败");
    }

    partial void OnTotalCountChanged(int value)
    {
        UpdateTotalPages();
    }

    partial void OnIsAllSelectedChanged(bool value)
    {
        OnBatchSelectAllChanged(value);
    }

    protected virtual void OnBatchSelectAllChanged(bool value) { }

    protected virtual Task LoadDataAsync() => Task.CompletedTask;

    protected void UpdateTotalPages()
    {
        TotalPages = Math.Max(1, (int)Math.Ceiling(TotalCount * 1.0 / Math.Max(1, PageSize)));
        OnPropertyChanged(nameof(ComputedTotalPages));

        if (PageIndex > TotalPages)
        {
            SetPageIndexWithoutAutoLoad(TotalPages);
        }
        else if (PageIndex < 1)
        {
            SetPageIndexWithoutAutoLoad(1);
        }
    }

    protected void SetPageIndexWithoutAutoLoad(int pageIndex)
    {
        _suppressPageIndexLoad = true;
        try
        {
            PageIndex = pageIndex;
        }
        finally
        {
            _suppressPageIndexLoad = false;
        }
    }

    protected async Task ResetToFirstPageAndLoadAsync()
    {
        ClearBatchSelection();
        if (PageIndex != 1)
        {
            SetPageIndexWithoutAutoLoad(1);
        }

        await LoadDataAsync();
    }

    /// <summary>
    /// 构建 CountAsync 缓存键
    /// </summary>
    protected string BuildCountCacheKey(params object?[] filters)
    {
        return string.Join("|", filters.Select(f => f?.ToString() ?? "null"));
    }

    protected bool TryGetCachedCount(string cacheKey, out int count)
    {
        if (_lastCountCacheKey == cacheKey && _cachedTotalCount >= 0)
        {
            count = _cachedTotalCount;
            return true;
        }
        count = 0;
        return false;
    }

    protected void SetCachedCount(string cacheKey, int count)
    {
        _lastCountCacheKey = cacheKey;
        _cachedTotalCount = count;
    }

    protected void InvalidateCountCache()
    {
        _lastCountCacheKey = null;
        _cachedTotalCount = -1;
    }

    public virtual void ClearBatchSelection()
    {
        IsBatchMode = false;
        IsAllSelected = false;
        SelectedCount = 0;
    }

    protected virtual List<int> GetSelectedIds() => new();

    [RelayCommand]
    protected async Task SearchAsync()
    {
        InvalidateCountCache();
        await ResetToFirstPageAndLoadAsync();
    }

    [RelayCommand]
    protected async Task RefreshAsync()
    {
        InvalidateCountCache();
        await ResetToFirstPageAndLoadAsync();
    }

    [RelayCommand]
    protected async Task PreviousPageAsync()
    {
        if (PageIndex > 1)
        {
            SetPageIndexWithoutAutoLoad(PageIndex - 1);
            await LoadDataAsync();
        }
    }

    [RelayCommand]
    protected async Task NextPageAsync()
    {
        if (PageIndex < TotalPages)
        {
            SetPageIndexWithoutAutoLoad(PageIndex + 1);
            await LoadDataAsync();
        }
    }

    [RelayCommand]
    protected async Task FirstPageAsync()
    {
        if (PageIndex != 1)
        {
            SetPageIndexWithoutAutoLoad(1);
            await LoadDataAsync();
        }
    }

    [RelayCommand]
    protected async Task LastPageAsync()
    {
        if (PageIndex != TotalPages)
        {
            SetPageIndexWithoutAutoLoad(TotalPages);
            await LoadDataAsync();
        }
    }
}

/// <summary>
/// 支持批量选择的可选列表项
/// </summary>
public partial class SelectableItem<T> : ObservableObject
{
    public T Data { get; set; } = default!;

    [ObservableProperty]
    private bool _isSelected;

    public int Id { get; set; }
}


/// <summary>
/// 导航项
/// </summary>
public partial class NavigationItem : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? ParentId { get; set; }
    public Type? ViewModelType { get; set; }
    public string? Permission { get; set; }
    public List<NavigationItem> Children { get; set; } = new();
    public bool IsVisible { get; set; } = true;
    public int SortOrder { get; set; }

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isHovered;

    [ObservableProperty]
    private int _badgeCount;

    /// <summary>是否分类标题（有子项的父菜单）</summary>
    public bool IsCategory => Children.Count > 0;

    /// <summary>搜索匹配得分（0=不匹配，值越高匹配越好）</summary>
    public int SearchScore { get; set; }

    /// <summary>是否被搜索过滤掉</summary>
    public bool IsSearchMatch => SearchScore > 0;
}

/// <summary>
/// 标签页项
/// </summary>
public partial class TabItem : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public object? Data { get; set; }
    public ViewModelBase? ViewModel { get; set; }
    
    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isClosable = true;
}

/// <summary>
/// 用户会话信息
/// </summary>
public class UserSession
{
    public int EmployeeId { get; set; }
    public string EmployeeNo { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public int? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public RoleType RoleType { get; set; }
    public List<string> Permissions { get; set; } = new();
    public DateTime LoginTime { get; set; }
    public string? Token { get; set; }

    public bool IsHeadquartersAdmin => RoleType == RoleType.HeadquartersAdmin;
    public bool IsBranchAdmin => RoleType == RoleType.BranchAdmin;
    public bool IsRegionAdmin => RoleType == RoleType.RegionAdmin;
    public bool IsEmployee => RoleType == RoleType.Employee;
    
    public bool CanAccessHeadquartersAdmin => IsHeadquartersAdmin;
    public bool CanManageOrganization => IsHeadquartersAdmin || IsBranchAdmin;
    public bool CanManageOrders => IsHeadquartersAdmin || IsBranchAdmin || IsRegionAdmin;
    public bool CanSettlement => IsHeadquartersAdmin || IsBranchAdmin;
    public bool CanManageWorkPlan => IsHeadquartersAdmin || IsBranchAdmin;

    public bool HasPermission(string permission)
    {
        return Permissions.Contains(permission) || IsHeadquartersAdmin;
    }
}

/// <summary>
/// 当前会话
/// </summary>
public static class CurrentSession
{
    private static UserSession? _current;
    private static readonly object _lock = new();
    public static UserSession Current => _current ?? throw new InvalidOperationException("用户未登录");

    public static void SetSession(UserSession session)
    {
        lock (_lock)
        {
            _current = session;
        }
    }

    public static void ClearSession()
    {
        lock (_lock)
        {
            _current = null;
        }
    }

    public static int CurrentBranchId => _current?.BranchId ?? 0;
    public static int CurrentEmployeeId => _current?.EmployeeId ?? 0;
    public static int CurrentRoleId => _current?.RoleId ?? 0;
    public static bool IsAdmin => _current?.IsHeadquartersAdmin == true || _current?.IsBranchAdmin == true;
}
