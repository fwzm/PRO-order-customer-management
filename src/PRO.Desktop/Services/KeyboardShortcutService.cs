using System.Windows;
using System.Windows.Input;

namespace PRO.Desktop.Services;

/// <summary>
/// 快捷键服务 - 管理全局键盘快捷键
/// </summary>
public class KeyboardShortcutService
{
    private readonly Dictionary<string, ShortcutDefinition> _shortcuts = [];
    private readonly Dictionary<string, Action> _handlers = [];

    public KeyboardShortcutService()
    {
        RegisterDefaultShortcuts();
    }

    private void RegisterDefaultShortcuts()
    {
        // 导航快捷键
        RegisterShortcut("Ctrl+N", "新建订单", "导航");
        RegisterShortcut("Ctrl+Shift+N", "新建客户", "导航");
        RegisterShortcut("Ctrl+F", "搜索", "导航");
        RegisterShortcut("Ctrl+P", "命令面板", "导航");
        RegisterShortcut("F5", "刷新", "导航");
        RegisterShortcut("Escape", "关闭/取消", "导航");

        // 订单快捷键
        RegisterShortcut("Ctrl+S", "保存", "订单");
        RegisterShortcut("Ctrl+D", "复制订单", "订单");
        RegisterShortcut("Ctrl+Enter", "确认", "订单");
        RegisterShortcut("Delete", "删除选中", "订单");

        // 列表快捷键
        RegisterShortcut("Ctrl+A", "全选", "列表");
        RegisterShortcut("Ctrl+Shift+A", "取消全选", "列表");
        RegisterShortcut("Ctrl+E", "导出", "列表");

        // 窗口快捷键
        RegisterShortcut("Ctrl+W", "关闭标签页", "窗口");
        RegisterShortcut("Ctrl+Tab", "下一个标签页", "窗口");
        RegisterShortcut("Ctrl+Shift+Tab", "上一个标签页", "窗口");
    }

    public void RegisterShortcut(string keyCombination, string description, string category)
    {
        _shortcuts[keyCombination] = new ShortcutDefinition
        {
            KeyCombination = keyCombination,
            Description = description,
            Category = category
        };
    }

    public void RegisterHandler(string keyCombination, Action handler)
    {
        _handlers[keyCombination] = handler;
    }

    public bool HandleKeyDown(KeyEventArgs e)
    {
        var keyCombination = GetKeyCombination(e);
        if (_handlers.TryGetValue(keyCombination, out var handler))
        {
            handler.Invoke();
            e.Handled = true;
            return true;
        }
        return false;
    }

    private string GetKeyCombination(KeyEventArgs e)
    {
        var parts = new List<string>();

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            parts.Add("Ctrl");
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            parts.Add("Shift");
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
            parts.Add("Alt");

        parts.Add(e.Key.ToString());

        return string.Join("+", parts);
    }

    public List<ShortcutDefinition> GetAllShortcuts()
    {
        return _shortcuts.Values.ToList();
    }

    public List<ShortcutDefinition> GetShortcutsByCategory(string category)
    {
        return _shortcuts.Values.Where(s => s.Category == category).ToList();
    }
}

public class ShortcutDefinition
{
    public string KeyCombination { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
}
