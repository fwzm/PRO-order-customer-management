using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;
using PRO.Infrastructure.Services;
using PRO.Infrastructure.WeChat;
using PRO.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Windows;

namespace PRO.Desktop.ViewModels;

public partial class HeadquartersAdminViewModel : ViewModelBase
{
    private readonly ProDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private WeChatService? _weChatService;

    [ObservableProperty]
    private ObservableCollection<BranchListItem> _branches = [];

    [ObservableProperty]
    private ObservableCollection<EmployeeListItem> _allEmployees = [];

    [ObservableProperty]
    private WeChatConfigDto? _weChatConfig;

    [ObservableProperty]
    private SyncConfigDto? _syncConfig;

    // 企业微信配置属性
    [ObservableProperty] private string _weChatCorpId = string.Empty;
    [ObservableProperty] private string _weChatCorpSecret = string.Empty;
    [ObservableProperty] private string _weChatAgentId = string.Empty;
    [ObservableProperty] private string _weChatWebhookUrl = string.Empty;
    [ObservableProperty] private bool _isWeChatEnabled;
    [ObservableProperty] private bool _isWeChatConnected;
    [ObservableProperty] private string _weChatStatus = "未连接";
    [ObservableProperty] private bool _isWeChatConnecting;
    [ObservableProperty] private bool _isWeChatHardcoded;

    // Webhook 管理属性
    [ObservableProperty] private ObservableCollection<WebhookListItem> _webhooks = [];
    [ObservableProperty] private WebhookListItem? _selectedWebhook;
    [ObservableProperty] private string _newWebhookName = string.Empty;
    [ObservableProperty] private string _newWebhookUrl = string.Empty;
    [ObservableProperty] private string _newWebhookTrigger = string.Empty;
    [ObservableProperty] private string _newWebhookRemark = string.Empty;
    [ObservableProperty] private bool _isEditingWebhook;
    [ObservableProperty] private int _editingWebhookId;

    [ObservableProperty] private string _syncResult = "";
    [ObservableProperty] private bool _isSyncing;
    [ObservableProperty] private ObservableCollection<PRO.Application.DTOs.BackupRecordDto> _backupRecords = [];
    [ObservableProperty] private ObservableCollection<OperationLogDto> _operationLogs = [];

    public HeadquartersAdminViewModel()
    {
        _dbContext = App.Services.GetService(typeof(ProDbContext)) as ProDbContext
            ?? throw new InvalidOperationException("无法获取数据库上下文");
        _httpClientFactory = App.Services.GetService(typeof(IHttpClientFactory)) as IHttpClientFactory
            ?? throw new InvalidOperationException("无法获取 HttpClientFactory");
        SyncConfig = new SyncConfigDto();

        if (CurrentSession.Current.IsHeadquartersAdmin)
        {
            RunInBackground(InitializeAsync(), "初始化总部管理失败");
        }
    }

    private WeChatService GetWeChatService()
    {
        if (_weChatService == null)
        {
            var encryptionService = App.Services.GetService(typeof(IEncryptionService)) as IEncryptionService
                ?? throw new InvalidOperationException("无法获取 EncryptionService");
            _weChatService = new WeChatService(_httpClientFactory, encryptionService, _dbContext);
        }
        return _weChatService;
    }

    private async Task InitializeAsync()
    {
        await LoadWeChatConfigAsync();
        await LoadWebhooksAsync();
        await LoadBranchesAsync();
        await LoadEmployeesAsync();
        await LoadBackupRecordsAsync();
        await LoadSyncConfigAsync();
        await LoadOperationLogsAsync();
    }

    private async Task LoadOperationLogsAsync()
    {
        try
        {
            var logs = await _dbContext.OperationLogs
                .AsNoTracking()
                .OrderByDescending(l => l.OperatedAt)
                .Take(200)
                .Select(l => new OperationLogDto
                {
                    Id = l.Id,
                    OperatorId = l.OperatorId,
                    OperatorName = l.Operator != null ? l.Operator.Name : l.OperatorNo,
                    OperatorNo = l.OperatorNo,
                    Module = l.Module,
                    OperationType = l.OperationType,
                    Content = l.Content,
                    Result = l.Result,
                    OperatedAt = l.OperatedAt
                })
                .ToListAsync();

            OperationLogs = new ObservableCollection<OperationLogDto>(logs);
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "加载操作日志失败"); }
    }

    private async Task LoadWeChatConfigAsync()
    {
        if (HardcodedConfig.HasWeChatConfig)
        {
            WeChatCorpId = HardcodedConfig.WeChatCorpId;
            WeChatCorpSecret = HardcodedConfig.WeChatCorpSecret;
            WeChatAgentId = HardcodedConfig.WeChatAgentId;
            IsWeChatEnabled = HardcodedConfig.WeChatEnabled;
            IsWeChatHardcoded = true;
            return;
        }

        try
        {
            var service = GetWeChatService();
            var config = await service.GetWeChatConfigAsync();
            if (config != null)
            {
                WeChatCorpId = config.CorpId;
                WeChatCorpSecret = config.CorpSecret;
                WeChatAgentId = config.AgentId;
                WeChatWebhookUrl = config.DefaultWebhookUrl ?? "";
                IsWeChatEnabled = config.IsEnabled;
            }
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "加载企业微信配置失败"); }
        IsWeChatHardcoded = false;
    }

    [RelayCommand]
    private async Task SaveWeChatConfigAsync()
    {
        try
        {
            var service = GetWeChatService();
            var config = new PRO.Infrastructure.WeChat.WeChatConfig
            {
                CorpId = WeChatCorpId,
                CorpSecret = WeChatCorpSecret,
                AgentId = WeChatAgentId,
                DefaultWebhookUrl = WeChatWebhookUrl,
                IsEnabled = IsWeChatEnabled
            };
            await service.SaveWeChatConfigAsync(config);
            ShowSuccess("企业微信配置已保存");
        }
        catch (Exception ex) { ShowError($"保存失败: {ex.Message}"); }
    }

    [RelayCommand]
    private async Task TestWeChatConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(WeChatCorpId) || string.IsNullOrWhiteSpace(WeChatCorpSecret))
        {
            ShowError("请先填写企业ID和应用Secret");
            return;
        }

        IsWeChatConnecting = true;
        WeChatStatus = "正在连接...";

        try
        {
            var service = GetWeChatService();
            var config = new PRO.Infrastructure.WeChat.WeChatConfig { CorpId = WeChatCorpId, CorpSecret = WeChatCorpSecret };
            await service.SaveWeChatConfigAsync(config);

            var success = await service.TestConnectionAsync();
            if (success)
            {
                IsWeChatConnected = true;
                WeChatStatus = "连接成功";
                ShowSuccess("企业微信连接成功！");
            }
            else
            {
                IsWeChatConnected = false;
                WeChatStatus = "连接失败";
                ShowError("企业微信连接失败，请检查配置");
            }
        }
        catch (Exception ex)
        {
            IsWeChatConnected = false;
            WeChatStatus = "连接失败";
            ShowError($"连接失败: {ex.Message}");
        }
        finally { IsWeChatConnecting = false; }
    }

    [RelayCommand]
    private async Task SyncOrganizationAsync()
    {
        if (!IsWeChatConnected) { ShowError("请先测试企业微信连接"); return; }
        IsSyncing = true;
        SyncResult = "正在同步组织架构...";
        try
        {
            var service = GetWeChatService();
            var result = await service.SyncOrganizationAsync();
            SyncResult = result.Success ? result.Message ?? "同步成功" : "同步失败";
            if (result.Success) ShowSuccess($"组织架构同步完成：{result.Message}");
            else ShowError($"同步失败：{result.Message}");
        }
        catch (Exception ex) { SyncResult = "同步失败"; ShowError($"同步失败: {ex.Message}"); }
        finally { IsSyncing = false; }
    }

    private async Task LoadWebhooksAsync()
    {
        try
        {
            var result = await _dbContext.Webhooks.AsNoTracking().OrderByDescending(w => w.CreatedAt).Take(50).ToListAsync();
            Webhooks = new ObservableCollection<WebhookListItem>(result.Select(w => new WebhookListItem
            {
                Id = w.Id,
                Name = w.Name,
                WebhookUrl = w.WebhookUrl,
                TriggerCondition = w.TriggerCondition,
                Remark = w.Remark,
                IsEnabled = w.IsEnabled,
                LastTestTime = w.LastTestTime,
                LastTestResult = w.LastTestResult
            }));
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "加载Webhooks失败"); }
    }

    private async Task LoadBranchesAsync()
    {
        var branches = await _dbContext.Branches.AsNoTracking().ToListAsync();
        Branches = new ObservableCollection<BranchListItem>(branches.Select(b => new BranchListItem
        {
            Id = b.Id,
            Name = b.Name,
            Code = b.Code,
            Address = b.Address,
            Phone = b.Phone,
            Status = b.Status
        }));
    }

    private async Task LoadEmployeesAsync()
    {
        var employees = await _dbContext.Employees.AsNoTracking()
            .Include(e => e.Branch).Include(e => e.Department).Include(e => e.Role)
            .ToListAsync();
        AllEmployees = new ObservableCollection<EmployeeListItem>(employees.Select(e => new EmployeeListItem
        {
            Id = e.Id,
            Name = e.Name,
            EmployeeNo = e.EmployeeNo,
            BranchName = e.Branch?.Name ?? "",
            DepartmentName = e.Department?.Name ?? "",
            RoleName = e.Role?.Name ?? "",
            Status = e.Status,
            CreatedAt = e.CreatedAt
        }));
    }

    private async Task LoadBackupRecordsAsync()
    {
        var records = await _dbContext.BackupRecords.AsNoTracking()
            .OrderByDescending(r => r.BackupTime).Take(30).ToListAsync();
        BackupRecords = new ObservableCollection<PRO.Application.DTOs.BackupRecordDto>(records.Select(r => new PRO.Application.DTOs.BackupRecordDto
        {
            Id = r.Id,
            FileName = r.FileName,
            BackupType = r.BackupType,
            FileSize = r.FileSize,
            BackupTime = r.BackupTime,
            ExpireTime = r.ExpireTime,
            Status = r.Status
        }));
    }

    private async Task LoadSyncConfigAsync()
    {
        var settings = await _dbContext.LocalSettings.AsNoTracking()
            .Where(s => s.SettingKey == "HeadquartersAutoSyncEnabled"
                || s.SettingKey == "HeadquartersSyncIntervalMinutes"
                || s.SettingKey == "HeadquartersConflictResolution")
            .ToListAsync();

        var autoSync = settings.FirstOrDefault(s => s.SettingKey == "HeadquartersAutoSyncEnabled");
        var interval = settings.FirstOrDefault(s => s.SettingKey == "HeadquartersSyncIntervalMinutes");
        var conflict = settings.FirstOrDefault(s => s.SettingKey == "HeadquartersConflictResolution");

        var conflictResolution = ConflictResolution.TimestampFirst;
        if (conflict != null && Enum.TryParse<ConflictResolution>(conflict.SettingValue, out var parsedConflict))
            conflictResolution = parsedConflict;

        SyncConfig = new SyncConfigDto
        {
            AutoSyncEnabled = autoSync != null && bool.TryParse(autoSync.SettingValue, out var autoSyncEnabled) && autoSyncEnabled,
            SyncIntervalMinutes = interval != null && int.TryParse(interval.SettingValue, out var syncInterval) ? syncInterval : 30,
            ConflictResolution = conflictResolution
        };
    }

    private async Task SaveLocalSettingAsync(string key, string value)
    {
        var setting = await _dbContext.LocalSettings.FirstOrDefaultAsync(s => s.SettingKey == key);
        if (setting == null)
        {
            setting = new LocalSetting { SettingKey = key, SettingType = "String" };
            _dbContext.LocalSettings.Add(setting);
        }
        setting.SettingValue = value;
        setting.UpdatedAt = DateTime.Now;
        await _dbContext.SaveChangesAsync();
    }

    [RelayCommand]
    private async Task SaveSyncConfigAsync()
    {
        try
        {
            SyncConfig ??= new SyncConfigDto();
            await SaveLocalSettingAsync("HeadquartersAutoSyncEnabled", SyncConfig.AutoSyncEnabled.ToString());
            await SaveLocalSettingAsync("HeadquartersSyncIntervalMinutes", SyncConfig.SyncIntervalMinutes.ToString());
            await SaveLocalSettingAsync("HeadquartersConflictResolution", SyncConfig.ConflictResolution.ToString());
            ShowSuccess("同步基础配置已保存");
        }
        catch (Exception ex) { ShowError($"保存失败: {ex.Message}"); }
    }

    [RelayCommand]
    private async Task CreateBackupAsync()
    {
        try
        {
            var backupService = App.Services.GetRequiredService<DatabaseBackupService>();
            var result = await backupService.PerformManualBackupAsync("系统设置手动备份");
            await LoadBackupRecordsAsync();
            if (result.Success) ShowSuccess($"手动备份完成：{result.FileName}");
            else ShowError($"备份失败: {result.Message}");
        }
        catch (Exception ex) { ShowError($"备份失败: {ex.Message}"); }
    }

    public class SyncDiffItem
    {
        public string FieldName { get; set; } = "";
        public string DiffType { get; set; } = "";
        public string LocalType { get; set; } = "";
        public string RemoteType { get; set; } = "";
    }
}
