using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;

namespace PRO.Desktop.Controls;

/// <summary>
/// 列表空状态控件 - 统一处理各种空数据场景
/// 支持5种状态：首次无数据、筛选无结果、无权限、加载失败、网络异常
/// 兼容深浅主题
/// </summary>
public partial class EmptyStateControl : UserControl
{
    public EmptyStateControl()
    {
        InitializeComponent();
    }
}

/// <summary>
/// 空状态类型枚举
/// </summary>
public enum EmptyStateType
{
    /// <summary>首次使用，无任何数据</summary>
    Empty,
    /// <summary>筛选后无结果</summary>
    NoResults,
    /// <summary>搜索无结果</summary>
    SearchNoResults,
    /// <summary>无权限访问</summary>
    NoPermission,
    /// <summary>加载失败</summary>
    LoadFailed,
    /// <summary>网络连接异常</summary>
    NetworkError,
    /// <summary>服务器错误</summary>
    ServerError,
    /// <summary>自定义</summary>
    Custom
}

/// <summary>
/// 空状态ViewModel - 提供丰富的工厂方法创建不同场景的空状态
/// </summary>
public partial class EmptyStateViewModel : ObservableObject
{
    [ObservableProperty]
    private PackIconKind _iconKind = PackIconKind.InboxOutline;

    [ObservableProperty]
    private string _title = "暂无数据";

    [ObservableProperty]
    private string _description = "";

    [ObservableProperty]
    private string _primaryButtonText = "";

    [ObservableProperty]
    private string _secondaryButtonText = "";

    [ObservableProperty]
    private string _hintText = "";

    [ObservableProperty]
    private bool _hasPrimaryAction;

    [ObservableProperty]
    private bool _hasSecondaryAction;

    [ObservableProperty]
    private bool _hasHint;

    private ICommand? _primaryCommand;
    private ICommand? _secondaryCommand;

    public ICommand? PrimaryCommand
    {
        get => _primaryCommand;
        set
        {
            _primaryCommand = value;
            HasPrimaryAction = value != null;
        }
    }

    public ICommand? SecondaryCommand
    {
        get => _secondaryCommand;
        set
        {
            _secondaryCommand = value;
            HasSecondaryAction = value != null;
        }
    }

    partial void OnHintTextChanged(string value)
    {
        HasHint = !string.IsNullOrEmpty(value);
    }

    // ==================== 工厂方法 ====================

    /// <summary>
    /// 首次使用暂无数据
    /// </summary>
    public static EmptyStateViewModel CreateForEmpty(string entityType, ICommand? createCommand = null)
    {
        return new EmptyStateViewModel
        {
            IconKind = PackIconKind.InboxOutline,
            Title = $"暂无{entityType}",
            Description = $"您还没有创建任何{entityType}，点击下方按钮开始创建",
            PrimaryButtonText = $"新建{entityType}",
            PrimaryCommand = createCommand,
            HintText = "提示：您可以通过左侧菜单随时访问此页面"
        };
    }

    /// <summary>
    /// 筛选无结果
    /// </summary>
    public static EmptyStateViewModel CreateForNoResults(string entityType, ICommand? clearFilterCommand = null)
    {
        return new EmptyStateViewModel
        {
            IconKind = PackIconKind.FilterOffOutline,
            Title = "筛选无结果",
            Description = $"当前筛选条件下没有找到{entityType}，请尝试调整筛选条件",
            PrimaryButtonText = "清除筛选",
            PrimaryCommand = clearFilterCommand,
            SecondaryButtonText = "刷新",
            HintText = "提示：您可以尝试扩大时间范围或清空关键词"
        };
    }

    /// <summary>
    /// 搜索无结果
    /// </summary>
    public static EmptyStateViewModel CreateForSearchNoResults(string keyword, ICommand? clearSearchCommand = null)
    {
        return new EmptyStateViewModel
        {
            IconKind = PackIconKind.MagnifyClose,
            Title = "搜索无结果",
            Description = $"未找到与「{keyword}」相关的内容",
            PrimaryButtonText = "清除搜索",
            PrimaryCommand = clearSearchCommand,
            HintText = "提示：请尝试使用不同的关键词或检查输入是否正确"
        };
    }

    /// <summary>
    /// 权限不足
    /// </summary>
    public static EmptyStateViewModel CreateForNoPermission(string entityType)
    {
        return new EmptyStateViewModel
        {
            IconKind = PackIconKind.LockOutline,
            Title = "暂无访问权限",
            Description = $"您当前账号没有查看{entityType}的权限",
            HintText = "如需访问，请联系管理员分配相应权限"
        };
    }

    /// <summary>
    /// 加载失败
    /// </summary>
    public static EmptyStateViewModel CreateForLoadFailed(ICommand? retryCommand = null)
    {
        return new EmptyStateViewModel
        {
            IconKind = PackIconKind.AlertCircleOutline,
            Title = "数据加载失败",
            Description = "加载数据时发生错误，请检查网络连接后重试",
            PrimaryButtonText = "重新加载",
            PrimaryCommand = retryCommand,
            HintText = "如果问题持续存在，请联系技术支持"
        };
    }

    /// <summary>
    /// 网络连接异常
    /// </summary>
    public static EmptyStateViewModel CreateForNetworkError(ICommand? retryCommand = null)
    {
        return new EmptyStateViewModel
        {
            IconKind = PackIconKind.WifiOff,
            Title = "网络连接异常",
            Description = "无法连接到服务器，请检查网络设置",
            PrimaryButtonText = "重新连接",
            PrimaryCommand = retryCommand,
            HintText = "请确保您的设备已连接到网络"
        };
    }

    /// <summary>
    /// 服务器错误
    /// </summary>
    public static EmptyStateViewModel CreateForServerError(ICommand? retryCommand = null)
    {
        return new EmptyStateViewModel
        {
            IconKind = PackIconKind.ServerNetworkOff,
            Title = "服务器暂时不可用",
            Description = "服务器正在维护或遇到问题，请稍后重试",
            PrimaryButtonText = "重新加载",
            PrimaryCommand = retryCommand,
            HintText = "如果问题持续存在，请联系技术支持"
        };
    }

    /// <summary>
    /// 自定义空状态
    /// </summary>
    public static EmptyStateViewModel CreateCustom(
        PackIconKind icon,
        string title,
        string description,
        string? primaryButtonText = null,
        ICommand? primaryCommand = null,
        string? secondaryButtonText = null,
        ICommand? secondaryCommand = null,
        string? hintText = null)
    {
        return new EmptyStateViewModel
        {
            IconKind = icon,
            Title = title,
            Description = description,
            PrimaryButtonText = primaryButtonText ?? "",
            PrimaryCommand = primaryCommand,
            SecondaryButtonText = secondaryButtonText ?? "",
            SecondaryCommand = secondaryCommand,
            HintText = hintText ?? ""
        };
    }
}
