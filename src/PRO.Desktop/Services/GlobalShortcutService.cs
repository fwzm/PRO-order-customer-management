using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace PRO.Desktop.Services;

/// <summary>
/// 全局快捷键管理器
/// 注册 Ctrl+N(新建), Ctrl+F(搜索), F5(刷新), Ctrl+S(保存), Esc(关闭/取消)
/// 通过 Action 委托注入实际执行逻辑，避免对 ViewModel 的直接耦合
/// 处理输入法作用域冲突（仅当焦点不在 TextBox 等输入控件时响应 Ctrl+F/N）
/// </summary>
public class GlobalShortcutService
{
    private readonly Window _mainWindow;
    private Action? _onNewCustomer;
    private Action? _onNewOrder;
    private Action? _onSearch;
    private Action? _onRefresh;
    private Action? _onSave;
    private Action? _onOpenDashboard;
    private Action? _onOpenReports;
    private Action? _onCommandPalette;

    public GlobalShortcutService(Window mainWindow)
    {
        _mainWindow = mainWindow;
    }

    /// <summary>
    /// 配置快捷键对应的操作
    /// </summary>
    public void Configure(
        Action? onNewCustomer = null,
        Action? onNewOrder = null,
        Action? onSearch = null,
        Action? onRefresh = null,
        Action? onSave = null,
        Action? onOpenDashboard = null,
        Action? onOpenReports = null,
        Action? onCommandPalette = null)
    {
        _onNewCustomer = onNewCustomer;
        _onNewOrder = onNewOrder;
        _onSearch = onSearch;
        _onRefresh = onRefresh;
        _onSave = onSave;
        _onOpenDashboard = onOpenDashboard;
        _onOpenReports = onOpenReports;
        _onCommandPalette = onCommandPalette;
    }

    /// <summary>
    /// 注册全局快捷键到主窗口 InputBindings
    /// </summary>
    public void RegisterShortcuts()
    {
        // Ctrl+N - 新建客户
        _mainWindow.InputBindings.Add(new KeyBinding(
            CreateCommand(() => _onNewCustomer?.Invoke(), IsNotInTextBox),
            Key.N, ModifierKeys.Control));

        // Ctrl+O - 新建订单
        _mainWindow.InputBindings.Add(new KeyBinding(
            CreateCommand(() => _onNewOrder?.Invoke(), IsNotInTextBox),
            Key.O, ModifierKeys.Control));

        // Ctrl+F - 全局搜索（仅当焦点不在输入控件时）
        _mainWindow.InputBindings.Add(new KeyBinding(
            CreateCommand(() => _onSearch?.Invoke(), IsNotInTextBox),
            Key.F, ModifierKeys.Control));

        // Ctrl+S - 保存当前编辑
        _mainWindow.InputBindings.Add(new KeyBinding(
            CreateCommand(() => _onSave?.Invoke()),
            Key.S, ModifierKeys.Control));

        // F5 - 刷新当前列表
        _mainWindow.InputBindings.Add(new KeyBinding(
            CreateCommand(() => _onRefresh?.Invoke()),
            Key.F5, ModifierKeys.None));

        // Ctrl+D - 仪表盘
        _mainWindow.InputBindings.Add(new KeyBinding(
            CreateCommand(() => _onOpenDashboard?.Invoke(), IsNotInTextBox),
            Key.D, ModifierKeys.Control));

        // Ctrl+R - 报表中心
        _mainWindow.InputBindings.Add(new KeyBinding(
            CreateCommand(() => _onOpenReports?.Invoke(), IsNotInTextBox),
            Key.R, ModifierKeys.Control));

        // Ctrl+K / Ctrl+Shift+P - 命令面板
        _mainWindow.InputBindings.Add(new KeyBinding(
            CreateCommand(() => _onCommandPalette?.Invoke(), IsNotInTextBox),
            Key.K, ModifierKeys.Control));

        _mainWindow.InputBindings.Add(new KeyBinding(
            CreateCommand(() => _onCommandPalette?.Invoke(), IsNotInTextBox),
            Key.P, ModifierKeys.Control | ModifierKeys.Shift));

        // Esc - 关闭当前活动弹窗（非主窗口）
        _mainWindow.InputBindings.Add(new KeyBinding(
            CreateCommand(() =>
            {
                var activeWindow = System.Windows.Application.Current.Windows
                    .OfType<Window>()
                    .FirstOrDefault(w => w.IsActive && w != _mainWindow);
                activeWindow?.Close();
            }),
            Key.Escape, ModifierKeys.None));
    }

    /// <summary>
    /// 检查当前焦点是否不在文本输入控件中
    /// 避免在 TextBox/PasswordBox 中按 Ctrl+F 时触发全局搜索
    /// </summary>
    private static bool IsNotInTextBox()
    {
        var focused = Keyboard.FocusedElement;
        if (focused is System.Windows.Controls.TextBox) return false;
        if (focused is System.Windows.Controls.PasswordBox) return false;
        if (focused is System.Windows.Controls.RichTextBox) return false;
        if (focused is System.Windows.Controls.ComboBox) return false;
        // 检查是否在 DataGrid 编辑模式
        if (focused is System.Windows.Controls.DataGridCell) return false;
        return true;
    }

    private static ICommand CreateCommand(Action execute, Func<bool>? canExecute = null)
    {
        return new RelayCommand(execute, canExecute);
    }
}

/// <summary>
/// 简单 RelayCommand 实现（用于全局快捷键）
/// </summary>
internal class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => _execute();
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}
