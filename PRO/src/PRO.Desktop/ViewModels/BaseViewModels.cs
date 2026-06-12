using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRO.Domain.Enums;
using PRO.Infrastructure.Services;
using PRO.Application.DTOs;
using PRO.Desktop.Controls;
using PRO.Desktop.Validation;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Windows.Input;

namespace PRO.Desktop.ViewModels;

/// <summary>
/// ViewModel基类 - 提供加载状态、错误处理、取消令牌、批量操作进度、权限检查、实时验证等通用能力
/// </summary>
public abstract partial class ViewModelBase : ValidatableViewModelBase
{
    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _successMessage;

    // ==================== 取消令牌 ====================

    /// <summary>全局取消令牌源</summary>
    protected CancellationTokenSource _cts = new();

    /// <summary>当前操作的取消令牌</summary>
    protected CancellationToken CancellationToken => _cts.Token;

    // ==================== 批量操作进度 ====================

    [ObservableProperty]
    private BatchOperationProgress? _batchProgress;

    [ObservableProperty]
    private bool _isBatchOperating;

    /// <summary>
    /// 执行批量操作并追踪进度
    /// </summary>
    /// <param name="operationName">操作名称（如"批量分配"）</param>
    /// <param name="totalCount">总数量</param>
    /// <param name="operation">实际操作委托，接收 (index, item, progress) 参数</param>
    /// <param name="itemNames">项目名称列表（可选，用于进度显示）</param>
    protected async Task<BatchOperationProgress> ExecuteBatchOperationAsync(
        string operationName,
        int totalCount,
        Func<int, CancellationToken, Task<(bool success, string? error)>> operation,
        Func<int, string>? itemNameProvider = null)
    {
        var progress = new BatchOperationProgress();
        BatchProgress = progress;
        IsBatchOperating = true;

        try
        {
            progress.Start(operationName, totalCount);

            for (var i = 0; i < totalCount; i++)
            {
                CancellationToken.ThrowIfCancellationRequested();

                var itemName = itemNameProvider?.Invoke(i) ?? $"#{i + 1}";
                progress.CurrentItemName = itemName;

                try
                {
                    var (success, error) = await operation(i, CancellationToken);
                    if (success)
                        progress.RecordSuccess(itemName);
                    else
                        progress.RecordFailure(itemName, error ?? "未知错误", i);
                }
                catch (OperationCanceledException)
                {
                    progress.Cancel();
                    break;
                }
                catch (Exception ex)
                {
                    progress.RecordFailure(itemName, ex.Message, i);
                }
            }

            if (!progress.IsCancelled)
                progress.Complete();
        }
        catch (OperationCanceledException)
        {
            progress.Cancel();
        }
        catch (Exception ex)
        {
            progress.RecordFailure("批量操作", ex.Message);
            progress.Complete();
        }
        finally
        {
            IsBatchOperating = false;
        }

        return progress;
    }

    /// <summary>
    /// 取消批量操作
    /// </summary>
    [RelayCommand]
    protected void CancelBatchOperation()
    {
        CancelCurrentOperation();
    }

    /// <summary>
    /// 重试批量操作中的失败项
    /// </summary>
    [RelayCommand]
    protected async Task RetryFailedBatchItemsAsync()
    {
        if (BatchProgress?.FailedItems.Count > 0 != true) return;
        // 子类应重写此方法以实现具体重试逻辑
        ShowSuccess("重试功能待实现");
        await Task.CompletedTask;
    }

    partial void OnIsLoadingChanged(bool value)
    {
        if (value)
            App.ShowLoading?.Invoke(null);
        else
            App.HideLoading?.Invoke();
    }

    protected void ShowError(string message)
    {
        // 过滤掉技术性消息中的敏感/技术信息
        var safeMessage = SanitizeErrorMessage(message);
        ErrorMessage = safeMessage;
        SuccessMessage = null;
        App.ShowToast?.Invoke(safeMessage, "提示", false);
    }

    /// <summary>
    /// 显示面向用户的友好错误提示（不暴露技术细节）
    /// </summary>
    protected void ShowUserError(string friendlyMessage)
    {
        ShowError(friendlyMessage);
    }

    /// <summary>
    /// 清理错误消息中的技术细节（隐藏数据库字段名、堆栈信息等）
    /// </summary>
    private static string SanitizeErrorMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return message;
        // 截断包含堆栈跟踪的消息
        var stackIndex = message.IndexOf("   at ", StringComparison.Ordinal);
        if (stackIndex > 0)
            message = message[..stackIndex].Trim();
        // 隐藏 PostgreSQL 内部表名
        message = System.Text.RegularExpressions.Regex.Replace(message, @"relation ""\w+""", "数据表");
        message = System.Text.RegularExpressions.Regex.Replace(message, @"column ""\w+""", "字段");
        return message;
    }

    /// <summary>
    /// 显示技术性错误：日志记录完整详情，用户仅看到通用友好提示（隐藏数据库字段和堆栈）
    /// </summary>
    protected void ShowTechnicalError(Exception ex, string operationName = "操作")
    {
        Log.Error(ex, "{Operation}异常", operationName);
        // 使用 BusinessMessageService 翻译技术异常为业务提示，避免暴露 ex.Message
        var msgService = App.Services.GetService<BusinessMessageService>();
        if (msgService != null)
        {
            var bizMsg = msgService.Translate(ex, operationName);
            ErrorMessage = bizMsg.Format();
            SuccessMessage = null;
            App.ShowToast?.Invoke(bizMsg.Format(), bizMsg.Title, false);
        }
        else
        {
            ErrorMessage = $"{operationName}失败，请稍后重试";
            SuccessMessage = null;
            App.ShowToast?.Invoke($"{operationName}失败，请稍后重试或联系技术支持", "操作异常", false);
        }
    }

    /// <summary>
    /// 翻译技术异常为业务提示并展示给用户
    /// </summary>
    protected void ShowBusinessException(Exception ex, string? context = null)
    {
        var msgService = App.Services.GetService<BusinessMessageService>();
        if (msgService != null)
        {
            var bizMsg = msgService.Translate(ex, context);
            ErrorMessage = bizMsg.Format();
            SuccessMessage = null;
            App.ShowToast?.Invoke(bizMsg.Format(), bizMsg.Title, false);
        }
        else
        {
            ShowTechnicalError(ex, context ?? "操作");
        }
    }

    protected void ShowSuccess(string message)
    {
        SuccessMessage = message;
        ErrorMessage = null;
        App.ShowToast?.Invoke(message, "成功", true);
    }

    /// <summary>
    /// 显示业务错误（带建议和可执行操作）
    /// </summary>
    protected void ShowBusinessError(BusinessError error)
    {
        var message = error.Format();
        ErrorMessage = message;
        SuccessMessage = null;

        var toastMessage = error.Message;
        if (!string.IsNullOrEmpty(error.Suggestion))
            toastMessage += $"\n💡 {error.Suggestion}";

        App.ShowToast?.Invoke(toastMessage, error.Title, false);
    }

    /// <summary>
    /// 显示业务错误（带重试操作）
    /// </summary>
    protected void ShowBusinessErrorWithRetry(BusinessError error, Action _retryAction)
    {
        ShowBusinessError(error);
    }

    protected void ClearMessages()
    {
        ErrorMessage = null;
        SuccessMessage = null;
    }

    /// <summary>
    /// 取消当前操作
    /// </summary>
    protected void CancelCurrentOperation()
    {
        if (!_cts.IsCancellationRequested)
        {
            _cts.Cancel();
        }
        _cts = new CancellationTokenSource();
    }

    /// <summary>
    /// 检查是否已取消
    /// </summary>
    protected void ThrowIfCancelled()
    {
        CancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// 检查权限并显示提示
    /// </summary>
    protected bool CheckPermission(string permission)
    {
        var result = PermissionService.CheckPermission(CurrentSession.Current, permission);
        if (!result.IsAllowed)
        {
            ShowBusinessError(BusinessMessages.PermissionDenied(result.DenyReason ?? permission));
            return false;
        }
        return true;
    }

    /// <summary>
    /// 检查订单操作权限
    /// </summary>
    protected bool CheckOrderPermission(string action)
    {
        var result = PermissionService.CheckOrderPermission(CurrentSession.Current, action);
        if (!result.IsAllowed)
        {
            ShowBusinessError(BusinessMessages.PermissionDenied(result.DenyReason ?? action));
            return false;
        }
        return true;
    }

    /// <summary>
    /// 检查结算操作权限
    /// </summary>
    protected bool CheckSettlementPermission(string action)
    {
        var result = PermissionService.CheckSettlementPermission(CurrentSession.Current, action);
        if (!result.IsAllowed)
        {
            ShowBusinessError(BusinessMessages.PermissionDenied(result.DenyReason ?? action));
            return false;
        }
        return true;
    }

    protected bool CheckCustomerPermission(string action)
    {
        var result = PermissionService.CheckCustomerPermission(CurrentSession.Current, action);
        if (!result.IsAllowed)
        {
            ShowBusinessError(BusinessMessages.PermissionDenied(result.DenyReason ?? action));
            return false;
        }
        return true;
    }

    protected bool CheckDeliveryPermission(string action)
    {
        var result = PermissionService.CheckDeliveryPermission(CurrentSession.Current, action);
        if (!result.IsAllowed)
        {
            ShowBusinessError(BusinessMessages.PermissionDenied(result.DenyReason ?? action));
            return false;
        }
        return true;
    }

    /// <summary>
    /// 显示确认对话框
    /// </summary>
    protected bool ConfirmAction(string title, string message, string _confirmText = "确定", string _cancelText = "取消")
    {
        var result = System.Windows.MessageBox.Show(
            message,
            title,
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);
        return result == System.Windows.MessageBoxResult.Yes;
    }

    /// <summary>
    /// 显示危险操作确认对话框
    /// </summary>
    protected bool ConfirmDangerousAction(string title, string message)
    {
        return ConfirmAction(title, $"⚠️ {message}\n\n此操作不可撤销，是否继续？", "确认执行", "取消");
    }

    /// <summary>
    /// 带重试的操作执行
    /// </summary>
    protected async Task<bool> ExecuteWithRetryAsync(Func<Task> operation, string operationName, bool showSuccess = true)
    {
        var result = await RetryService.ExecuteWithRetryAsync(operation, operationName);

        if (result.IsSuccess)
        {
            if (showSuccess)
                ShowSuccess($"{operationName}成功");
            return true;
        }
        else
        {
            ShowBusinessError(new BusinessError
            {
                Title = $"{operationName}失败",
                Message = result.ErrorMessage ?? "未知错误",
                Suggestion = "请检查网络连接后重试，或联系管理员",
                Actions = new[] { "重试", "查看详情" }
            });
            return false;
        }
    }

    /// <summary>
    /// 带重试和确认的操作执行
    /// </summary>
    protected async Task<bool> ExecuteWithRetryAndConfirmAsync(
        Func<Task> operation,
        string operationName,
        string confirmMessage,
        bool isDangerous = false)
    {
        // 显示确认对话框
        bool confirmed = isDangerous
            ? ConfirmDangerousAction(operationName, confirmMessage)
            : ConfirmAction(operationName, confirmMessage);

        if (!confirmed) return false;

        return await ExecuteWithRetryAsync(operation, operationName, showSuccess: true);
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
/// 分页ViewModel基类 - 提供分页、批量选择、空状态支持
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

    // ==================== 空状态支持 ====================

    [ObservableProperty]
    private EmptyStateViewModel? _emptyState;

    [ObservableProperty]
    private bool _showEmptyState;

    /// <summary>当前空状态类型</summary>
    protected EmptyStateType CurrentEmptyStateType { get; set; } = EmptyStateType.Empty;

    /// <summary>实体类型名称（如"订单"、"客户"），子类需设置</summary>
    protected virtual string EntityTypeName => "数据";

    /// <summary>
    /// 根据当前列表状态更新空状态显示
    /// </summary>
    protected virtual void UpdateEmptyState()
    {
        if (IsLoading)
        {
            ShowEmptyState = false;
            return;
        }

        if (TotalCount > 0)
        {
            ShowEmptyState = false;
            return;
        }

        ShowEmptyState = true;

        if (!string.IsNullOrWhiteSpace(SearchKeyword))
        {
            EmptyState = EmptyStateViewModel.CreateForSearchNoResults(SearchKeyword, SearchCommand);
        }
        else if (HasActiveFilters())
        {
            EmptyState = EmptyStateViewModel.CreateForNoResults(EntityTypeName, ClearFiltersCommand);
        }
        else
        {
            EmptyState = EmptyStateViewModel.CreateForEmpty(EntityTypeName, CreateNewCommand);
        }
    }

    /// <summary>
    /// 显示加载失败空状态
    /// </summary>
    protected void ShowLoadFailedState()
    {
        IsLoading = false;
        ShowEmptyState = true;
        EmptyState = EmptyStateViewModel.CreateForLoadFailed(RefreshCommand);
    }

    /// <summary>
    /// 显示网络异常空状态
    /// </summary>
    protected void ShowNetworkErrorState()
    {
        IsLoading = false;
        ShowEmptyState = true;
        EmptyState = EmptyStateViewModel.CreateForNetworkError(RefreshCommand);
    }

    /// <summary>
    /// 显示无权限空状态
    /// </summary>
    protected void ShowNoPermissionState()
    {
        IsLoading = false;
        ShowEmptyState = true;
        EmptyState = EmptyStateViewModel.CreateForNoPermission(EntityTypeName);
    }

    /// <summary>
    /// 子类重写：是否有活跃的筛选条件
    /// </summary>
    protected virtual bool HasActiveFilters() => false;

    /// <summary>
    /// 子类重写：清除筛选命令
    /// </summary>
    protected virtual ICommand? ClearFiltersCommand => null;

    /// <summary>
    /// 子类重写：新建实体命令
    /// </summary>
    protected virtual ICommand? CreateNewCommand => null;

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

    protected virtual List<int> GetSelectedIds() => [];

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
    public List<NavigationItem> Children { get; set; } = [];
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
public class UserSession : IPermissionSession
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
    public List<string> Permissions { get; set; } = [];
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
