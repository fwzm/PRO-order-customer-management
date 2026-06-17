using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PRO.Desktop.ViewModels;

/// <summary>
/// 命令面板 ViewModel — Ctrl+Shift+P / Ctrl+K 快速跳转
/// 支持模糊搜索客户/订单/报表/设置入口
/// </summary>
public class CommandPaletteViewModel : INotifyPropertyChanged
{
    private string _searchText = "";
    private List<CommandItem> _allCommands = [];
    private List<CommandItem> _filteredCommands = [];
    private CommandItem? _selectedCommand;

    /// <summary>命令选中时触发（由 View 订阅打开对应窗口）</summary>
    public event Action<string>? CommandExecuted;

    public string SearchText
    {
        get => _searchText;
        set { _searchText = value; OnPropertyChanged(); FilterCommands(); }
    }

    public List<CommandItem> FilteredCommands
    {
        get => _filteredCommands;
        set { _filteredCommands = value; OnPropertyChanged(); }
    }

    public CommandItem? SelectedCommand
    {
        get => _selectedCommand;
        set { _selectedCommand = value; OnPropertyChanged(); }
    }

    public void ExecuteCommand(CommandItem? command)
    {
        if (command == null) return;
        CommandExecuted?.Invoke(command.CommandId);
    }

    public CommandPaletteViewModel()
    {
        InitializeCommands();
        FilterCommands();
    }

    private void InitializeCommands()
    {
        _allCommands =
        [
            // 客户相关
            new() { Name = "客户列表", Category = "客户", Icon = "👥", CommandId = "CustomerList", Shortcut = "Ctrl+K" },
            new() { Name = "新建客户", Category = "客户", Icon = "➕", CommandId = "CustomerNew", Shortcut = "Ctrl+N" },
            new() { Name = "客户详情", Category = "客户", Icon = "📋", CommandId = "CustomerDetail" },
            // 订单相关
            new() { Name = "订单列表", Category = "订单", Icon = "📦", CommandId = "OrderList" },
            new() { Name = "新建订单", Category = "订单", Icon = "📝", CommandId = "OrderNew", Shortcut = "Ctrl+O" },
            // 配送相关
            new() { Name = "配送人员", Category = "配送", Icon = "🚚", CommandId = "DeliveryPersonList" },
            // 库存相关
            new() { Name = "库存管理", Category = "库存", Icon = "🏪", CommandId = "Inventory" },
            // 收款相关
            new() { Name = "应收账款", Category = "收款", Icon = "💰", CommandId = "AccountsReceivable" },
            new() { Name = "收款登记", Category = "收款", Icon = "💳", CommandId = "PaymentRegistration" },
            // 报表相关
            new() { Name = "数据质量", Category = "报表", Icon = "✅", CommandId = "DataQuality" },
            // 设置相关
            new() { Name = "部门管理", Category = "设置", Icon = "⚙️", CommandId = "DepartmentManage" },
            new() { Name = "员工管理", Category = "设置", Icon = "👤", CommandId = "EmployeeList" },
            new() { Name = "修改密码", Category = "设置", Icon = "🔒", CommandId = "ChangePassword" },
            // 导航
            new() { Name = "仪表盘", Category = "导航", Icon = "🏠", CommandId = "Dashboard", Shortcut = "Ctrl+D" },
            new() { Name = "全局搜索", Category = "导航", Icon = "🔍", CommandId = "GlobalSearch", Shortcut = "Ctrl+F" },
            new() { Name = "刷新数据", Category = "导航", Icon = "🔄", CommandId = "Refresh", Shortcut = "F5" },
        ];
    }

    private void FilterCommands()
    {
        if (string.IsNullOrWhiteSpace(_searchText))
        {
            FilteredCommands = _allCommands.Take(12).ToList();
        }
        else
        {
            var keyword = _searchText.ToLower();
            FilteredCommands = _allCommands
                .Where(c => c.Name.ToLower().Contains(keyword) || c.Category.ToLower().Contains(keyword))
                .Take(12)
                .ToList();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class CommandItem
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Icon { get; set; } = "";
    public string CommandId { get; set; } = "";
    public string? Shortcut { get; set; }
}
