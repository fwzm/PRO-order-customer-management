using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using PRO.Application.Interfaces;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;
using Serilog;
using System.Collections.ObjectModel;

namespace PRO.Desktop.ViewModels;

public partial class WeChatScrmViewModel : ViewModelBase
{
    private readonly ProDbContext _db;
    private readonly IWeChatService? _weChat;

    // Tabs
    [ObservableProperty] private bool _isCustomersTab = true;
    [ObservableProperty] private bool _isTagsTab;
    [ObservableProperty] private bool _isGroupChatsTab;
    [ObservableProperty] private bool _isMessagesTab;
    [ObservableProperty] private bool _isContactWayTab;
    [ObservableProperty] private bool _isTransferTab;

    // WeChat Customers
    [ObservableProperty] private ObservableCollection<WeChatCustomerItem> _customers = [];
    [ObservableProperty] private WeChatCustomerItem? _selectedCustomer;

    // Tags
    [ObservableProperty] private ObservableCollection<WeChatTagDisplay> _tags = [];
    [ObservableProperty] private string _newTagName = "";
    [ObservableProperty] private string _newTagGroup = "默认分组";

    // Group Chats
    [ObservableProperty] private ObservableCollection<WeChatGroupChatItem> _groupChats = [];
    [ObservableProperty] private WeChatGroupChatItem? _selectedGroupChat;
    [ObservableProperty] private ObservableCollection<GroupChatMemberItem> _groupChatMembers = [];

    // Messages
    [ObservableProperty] private string _messageContent = "";
    [ObservableProperty] private string _messageRecipients = "";
    [ObservableProperty] private string _messageResult = "";

    // Contact Way
    [ObservableProperty] private string _contactWayUrl = "";
    [ObservableProperty] private string _contactWayRemark = "";

    // Transfer
    [ObservableProperty] private ObservableCollection<UnassignedCustomerItem> _unassignedCustomers = [];
    [ObservableProperty] private ObservableCollection<EmployeeItem> _takeoverEmployees = [];
    [ObservableProperty] private EmployeeItem? _selectedTakeover;

    // 状态
    [ObservableProperty] private int _customerCount;
    [ObservableProperty] private int _tagCount;
    [ObservableProperty] private int _groupChatCount;
    [ObservableProperty] private int _unassignedCount;

    public WeChatScrmViewModel()
    {
        _db = App.Services.GetService(typeof(ProDbContext)) as ProDbContext ?? throw new();
        _weChat = App.Services.GetService(typeof(IWeChatService)) as IWeChatService
            ?? throw new InvalidOperationException("IWeChatService 未注册，请检查 DI 配置");
        RunInBackground(InitAsync(), "初始化SCRM数据失败");
    }

    private async Task InitAsync()
    {
        IsCustomersTab = true;
        try
        {
            CustomerCount = await _db.WeChatCustomers.AsNoTracking().CountAsync();
            TagCount = await _db.CustomerTags.AsNoTracking().CountAsync();
            UnassignedCount = await _db.WeChatCustomers.AsNoTracking().CountAsync(w => w.Status == "Unidentifiable");
            await LoadCustomersAsync();
            var employees = await _db.Employees.AsNoTracking().Where(e => e.Status == EmployeeStatus.Active && !string.IsNullOrEmpty(e.WeChatUserId)).OrderBy(e => e.Name).ToListAsync();
            TakeoverEmployees = new ObservableCollection<EmployeeItem>(employees.Select(e => new EmployeeItem { Id = e.Id, Name = e.Name }));
        }
        catch (Exception ex) { Log.Error(ex, "SCRM 初始化失败"); }
    }

    private async Task LoadCustomersAsync()
    {
        IsLoading = true;
        try
        {
            var list = await _db.WeChatCustomers.AsNoTracking().Include(w => w.LinkedCustomer).Include(w => w.AssignedBranch).OrderByDescending(w => w.CreatedAt).ToListAsync();
            Customers = new ObservableCollection<WeChatCustomerItem>(list.Select(w => new WeChatCustomerItem
            {
                Id = w.Id,
                Name = w.Name,
                AddUserName = w.AddUserName,
                AddUserDepartmentName = w.AddUserDepartmentName,
                Status = w.Status,
                LinkedCustomerName = w.LinkedCustomer?.Name,
                AssignedBranchName = w.AssignedBranchName,
                CreatedAt = w.CreatedAt,
                AvatarUrl = w.AvatarUrl,
                StatusBadge = w.Status switch { "Linked" => "已关联", "Unlinked" => "未关联", "Unidentifiable" => "未识别", _ => w.Status }
            }));
        }
        catch (Exception ex) { Log.Error(ex, "SCRM 客户数据加载失败"); }
        finally { IsLoading = false; }
    }

    // Tab切换
    partial void OnIsCustomersTabChanged(bool value) { if (value) RunInBackground(LoadCustomersAsync(), "加载客户列表失败"); }
    partial void OnIsTagsTabChanged(bool value) { if (value) RunInBackground(LoadTagsAsync(), "加载标签列表失败"); }
    partial void OnIsGroupChatsTabChanged(bool value) { if (value) RunInBackground(LoadGroupChatsAsync(), "加载群聊列表失败"); }
    partial void OnIsTransferTabChanged(bool value) { if (value) RunInBackground(LoadUnassignedAsync(), "加载未分配列表失败"); }

    // ═══════════════════ 标签管理 ═══════════════════
    private async Task LoadTagsAsync()
    {
        try
        {
            Tags = new ObservableCollection<WeChatTagDisplay>((await _db.CustomerTags.AsNoTracking().OrderBy(t => t.Name).ToListAsync())
                .Select(t => new WeChatTagDisplay { Id = t.Id, Name = t.Name, Color = t.Color }));
        }
        catch (Exception ex) { Log.Error(ex, "SCRM 标签加载失败"); }
    }

    [RelayCommand]
    private async Task SyncWeChatTagsAsync()
    {
        if (_weChat == null) { ShowError("企业微信未配置"); return; }
        try
        {
            var result = await _weChat.GetTagsAsync();
            if (result.Success && result.Data != null)
            {
                foreach (var tag in result.Data)
                {
                    if (!await _db.CustomerTags.AnyAsync(t => t.Name == tag.Name))
                    {
                        _db.CustomerTags.Add(new CustomerTag { Name = tag.Name, Color = "#007AFF", BranchId = CurrentSession.CurrentBranchId });
                    }
                }
                await _db.SaveChangesAsync();
                ShowSuccess($"同步 {result.Data.Count} 个标签");
                await LoadTagsAsync();
            }
        }
        catch (Exception ex) { ShowError($"同步失败: {ex.Message}"); }
    }

    [RelayCommand]
    private async Task CreateTagAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTagName)) return;
        try
        {
            var tag = new CustomerTag
            {
                Name = NewTagName,
                Color = "#007AFF",
                BranchId = CurrentSession.CurrentBranchId
            };
            _db.CustomerTags.Add(tag);
            await _db.SaveChangesAsync();
            ShowSuccess($"标签 \"{NewTagName}\" 已创建");
            NewTagName = string.Empty;
            NewTagGroup = string.Empty;
            await LoadTagsAsync();
        }
        catch (Exception ex)
        {
            ShowError($"创建标签失败: {ex.Message}");
        }
    }

    // ═══════════════════ 客户群 ═══════════════════
    private async Task LoadGroupChatsAsync()
    {
        GroupChats = [];
        if (_weChat == null) { ShowError("企业微信未配置"); return; }
        try
        {
            var result = await _weChat.GetGroupChatsAsync();
            if (result.Success && result.Data != null)
            {
                GroupChats = new ObservableCollection<WeChatGroupChatItem>(result.Data.Select(g => new WeChatGroupChatItem
                { ChatId = g.ChatId, Name = g.Name, MemberCount = g.MemberCount, CreateTime = g.CreateTime }));
                GroupChatCount = GroupChats.Count;
            }
        }
        catch (Exception ex) { ShowError($"加载群聊失败: {ex.Message}"); }
    }

    [RelayCommand]
    private async Task ViewGroupChatDetailAsync(WeChatGroupChatItem? item)
    {
        if (item == null || _weChat == null) return;
        try
        {
            var result = await _weChat.GetGroupChatDetailAsync(item.ChatId);
            if (result.Success && result.Data != null)
            {
                GroupChatMembers = new ObservableCollection<GroupChatMemberItem>(result.Data.Members.Select(m => new GroupChatMemberItem
                { UserId = m.UserId, Name = m.Name, Type = m.Type == "user" ? "员工" : "客户" }));
            }
        }
        catch (Exception ex) { Log.Error(ex, "群聊详情加载失败"); }
    }

    // ═══════════════════ 消息群发 ═══════════════════
    [RelayCommand]
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(MessageContent)) { ShowError("请输入消息内容"); return; }
        if (_weChat == null) { ShowError("企业微信未配置"); return; }
        try
        {
            var result = await _weChat.SendMessageAsync(MessageRecipients, MessageContent);
            if (result.Success) { ShowSuccess("消息发送成功"); MessageContent = ""; }
            else ShowError(result.Message);
        }
        catch (Exception ex) { ShowError($"发送失败: {ex.Message}"); }
    }

    [RelayCommand]
    private async Task CreateMassMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(MessageContent)) { ShowError("请输入消息内容"); return; }
        if (_weChat == null) { ShowError("企业微信未配置"); return; }
        try
        {
            var result = await _weChat.CreateMassMessageAsync(MessageContent, sendToAll: true);
            if (result.Success) MessageResult = $"群发任务已创建，消息ID: {result.Data}";
            else MessageResult = $"失败: {result.Message}";
        }
        catch (Exception ex) { MessageResult = $"失败: {ex.Message}"; }
    }

    // ═══════════════════ 联系我 ═══════════════════
    [RelayCommand]
    private async Task CreateContactWayAsync()
    {
        if (_weChat == null) { ShowError("企业微信未配置"); return; }
        try
        {
            var currentUserId = (await _db.Employees.FindAsync(CurrentSession.CurrentEmployeeId))?.WeChatUserId;
            if (string.IsNullOrEmpty(currentUserId)) { ShowError("当前员工未绑定企业微信"); return; }
            var result = await _weChat.CreateContactWayAsync(1, currentUserId, ContactWayRemark);
            if (result.Success) { ContactWayUrl = result.Data ?? ""; ShowSuccess("「联系我」二维码已生成"); }
            else ShowError(result.Message);
        }
        catch (Exception ex) { ShowError($"创建失败: {ex.Message}"); }
    }

    // ═══════════════════ 离职分配 ═══════════════════
    private async Task LoadUnassignedAsync()
    {
        UnassignedCount = 0;
        if (_weChat == null) return;
        try
        {
            var result = await _weChat.GetUnassignedCustomersAsync();
            if (result.Success && result.Data != null)
            {
                var externalIds = result.Data;
                var customers = await _db.WeChatCustomers.AsNoTracking().Where(w => externalIds.Contains(w.ExternalUserId)).ToListAsync();
                UnassignedCustomers = new ObservableCollection<UnassignedCustomerItem>(customers.Select(c => new UnassignedCustomerItem
                { CustomerName = c.Name, ExternalUserId = c.ExternalUserId }));
                UnassignedCount = UnassignedCustomers.Count;
            }
        }
        catch (Exception ex) { Log.Error(ex, "未分配客户加载失败"); }
    }

    [RelayCommand]
    private async Task TransferCustomerAsync(UnassignedCustomerItem? item)
    {
        if (item == null || SelectedTakeover == null || _weChat == null) return;
        try
        {
            var handoverUser = (await _db.Employees.FindAsync(CurrentSession.CurrentEmployeeId))?.WeChatUserId;
            var takeoverUser = (await _db.Employees.FindAsync(SelectedTakeover.Id))?.WeChatUserId;
            if (string.IsNullOrEmpty(handoverUser) || string.IsNullOrEmpty(takeoverUser)) { ShowError("员工未绑定企业微信"); return; }
            var result = await _weChat.TransferCustomerAsync(item.ExternalUserId, handoverUser, takeoverUser);
            if (result.Success) { ShowSuccess("客户已分配"); item.Status = "已分配"; }
            else ShowError(result.Message);
        }
        catch (Exception ex) { ShowError($"分配失败: {ex.Message}"); }
    }
}

public class WeChatCustomerItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? AddUserName { get; set; }
    public string? AddUserDepartmentName { get; set; }
    public string Status { get; set; } = "Unlinked";
    public string StatusBadge { get; set; } = "未关联";
    public string? LinkedCustomerName { get; set; }
    public string? AssignedBranchName { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class WeChatTagDisplay
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#007AFF";
}

public class WeChatGroupChatItem
{
    public string ChatId { get; set; } = "";
    public string Name { get; set; } = "";
    public int MemberCount { get; set; }
    public DateTime CreateTime { get; set; }
}

public class GroupChatMemberItem
{
    public string UserId { get; set; } = "";
    public string? Name { get; set; }
    public string Type { get; set; } = "";
}

public class UnassignedCustomerItem
{
    public string CustomerName { get; set; } = "";
    public string ExternalUserId { get; set; } = "";
    public string Status { get; set; } = "待分配";
}
