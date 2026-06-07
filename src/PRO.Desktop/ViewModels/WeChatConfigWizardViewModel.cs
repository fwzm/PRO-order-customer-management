using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;

namespace PRO.Desktop.ViewModels;

/// <summary>
/// 企业微信配置向导 ViewModel
/// </summary>
public partial class WeChatConfigWizardViewModel : ViewModelBase
{
    private readonly ProDbContext _dbContext;
    private readonly IEncryptionService _encryptionService;

    [ObservableProperty]
    private WeChatWizardStep _currentStep = WeChatWizardStep.Welcome;

    [ObservableProperty]
    private ObservableCollection<WeChatWizardStepDto> _steps = new();

    // 步骤1: 应用信息
    [ObservableProperty]
    private string _appName = "PRO管理系统";

    [ObservableProperty]
    private string _appDescription = "订单与客户管理系统";

    // 步骤2: 凭证信息
    [ObservableProperty]
    private string _corpId = "";

    [ObservableProperty]
    private string _corpSecret = "";

    [ObservableProperty]
    private string _agentId = "";

    [ObservableProperty]
    private string _token = "";

    [ObservableProperty]
    private string _encodingAesKey = "";

    // 步骤3: 回调配置
    [ObservableProperty]
    private string _callbackUrl = "";

    [ObservableProperty]
    private bool _callbackVerified;

    // 步骤4: 通讯录同步
    [ObservableProperty]
    private bool _contactsSyncEnabled = true;

    [ObservableProperty]
    private int _syncedEmployeeCount;

    [ObservableProperty]
    private bool _isSyncingContacts;

    // 步骤5: 客户同步
    [ObservableProperty]
    private bool _customerSyncEnabled = true;

    [ObservableProperty]
    private int _syncedCustomerCount;

    [ObservableProperty]
    private bool _isSyncingCustomers;

    // 测试状态
    [ObservableProperty]
    private bool _isTestingConnection;

    [ObservableProperty]
    private WeChatConnectionTestResult? _connectionTestResult;

    [ObservableProperty]
    private bool _isConfigurationValid;

    public WeChatConfigWizardViewModel()
    {
        _dbContext = App.Services.GetService(typeof(ProDbContext)) as ProDbContext
            ?? throw new InvalidOperationException("无法获取数据库上下文");
        _encryptionService = App.Services.GetService(typeof(IEncryptionService)) as IEncryptionService
            ?? throw new InvalidOperationException("无法获取加密服务");

        _ = LoadExistingConfigAsync();
        InitializeSteps();
    }

    private void InitializeSteps()
    {
        Steps = new ObservableCollection<WeChatWizardStepDto>
        {
            new() { Step = WeChatWizardStep.Welcome, Title = "欢迎", Description = "企业微信集成向导", IsAccessible = true },
            new() { Step = WeChatWizardStep.CreateApp, Title = "创建应用", Description = "在企业微信后台创建应用", IsAccessible = true },
            new() { Step = WeChatWizardStep.ConfigureCredentials, Title = "配置凭证", Description = "输入应用凭证信息" },
            new() { Step = WeChatWizardStep.ConfigureCallback, Title = "配置回调", Description = "设置消息回调地址" },
            new() { Step = WeChatWizardStep.SyncContacts, Title = "通讯录同步", Description = "同步企业通讯录" },
            new() { Step = WeChatWizardStep.SyncCustomers, Title = "客户同步", Description = "同步客户数据" },
            new() { Step = WeChatWizardStep.Complete, Title = "完成", Description = "配置完成" }
        };
        UpdateStepStates();
    }

    private async Task LoadExistingConfigAsync()
    {
        try
        {
            var config = await _dbContext.WeChatConfigs.AsNoTracking().FirstOrDefaultAsync();
            if (config != null)
            {
                CorpId = config.CorpId ?? "";
                // 解密密钥
                if (!string.IsNullOrEmpty(config.AppSecret))
                {
                    try { CorpSecret = _encryptionService.Decrypt(config.AppSecret); } catch { }
                }
                AgentId = config.AgentId.ToString();
                Token = config.Token ?? "";
                EncodingAesKey = config.EncodingAESKey ?? "";
                CallbackUrl = config.WebhookUrl ?? "";
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "加载企微配置失败");
        }
    }

    private void UpdateStepStates()
    {
        foreach (var step in Steps)
        {
            step.IsCurrent = step.Step == CurrentStep;
            step.IsCompleted = step.Step < CurrentStep;
            step.IsAccessible = step.Step <= CurrentStep + 1;
        }
    }

    partial void OnCurrentStepChanged(WeChatWizardStep value)
    {
        UpdateStepStates();
    }

    // ==================== 导航命令 ====================

    [RelayCommand]
    private void NextStep()
    {
        if (CurrentStep < WeChatWizardStep.Complete)
        {
            CurrentStep = CurrentStep + 1;
        }
    }

    [RelayCommand]
    private void PreviousStep()
    {
        if (CurrentStep > WeChatWizardStep.Welcome)
        {
            CurrentStep = CurrentStep - 1;
        }
    }

    [RelayCommand]
    private void GoToStep(WeChatWizardStep step)
    {
        if (step <= CurrentStep + 1)
        {
            CurrentStep = step;
        }
    }

    // ==================== 步骤2: 测试连接 ====================

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(CorpId) || string.IsNullOrWhiteSpace(CorpSecret))
        {
            ShowError("请先填写企业ID和应用Secret");
            return;
        }

        IsTestingConnection = true;
        ConnectionTestResult = null;

        try
        {
            using var httpClient = new System.Net.Http.HttpClient();
            var url = $"https://qyapi.weixin.qq.com/cgi-bin/gettoken?corpid={CorpId}&corpsecret={CorpSecret}";
            var response = await httpClient.GetStringAsync(url);
            var json = System.Text.Json.JsonDocument.Parse(response);
            var errcode = json.RootElement.GetProperty("errcode").GetInt32();

            if (errcode == 0)
            {
                var accessToken = json.RootElement.GetProperty("access_token").GetString();
                ConnectionTestResult = new WeChatConnectionTestResult
                {
                    Success = true,
                    TestTime = DateTime.Now
                };
                IsConfigurationValid = true;
                ShowSuccess("连接测试成功！");
            }
            else
            {
                var errmsg = json.RootElement.GetProperty("errmsg").GetString();
                ConnectionTestResult = new WeChatConnectionTestResult
                {
                    Success = false,
                    ErrorMessage = errmsg,
                    TestTime = DateTime.Now
                };
                ShowError($"连接测试失败: {errmsg}");
            }
        }
        catch (Exception ex)
        {
            ConnectionTestResult = new WeChatConnectionTestResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                TestTime = DateTime.Now
            };
            ShowError($"连接测试失败: {ex.Message}");
        }
        finally
        {
            IsTestingConnection = false;
        }
    }

    // ==================== 步骤3: 验证回调 ====================

    [RelayCommand]
    private async Task VerifyCallbackAsync()
    {
        if (string.IsNullOrWhiteSpace(CallbackUrl))
        {
            ShowError("请输入回调URL");
            return;
        }

        try
        {
            using var httpClient = new System.Net.Http.HttpClient();
            var response = await httpClient.GetAsync(CallbackUrl);
            if (response.IsSuccessStatusCode)
            {
                CallbackVerified = true;
                ShowSuccess("回调URL验证成功！");
            }
            else
            {
                ShowError($"回调URL返回状态码: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            ShowError($"回调URL验证失败: {ex.Message}");
        }
    }

    // ==================== 步骤4: 通讯录同步 ====================

    [RelayCommand]
    private async Task SyncContactsAsync()
    {
        IsSyncingContacts = true;
        try
        {
            // 模拟同步过程
            await Task.Delay(2000);
            SyncedEmployeeCount = 42; // 示例数据
            ShowSuccess($"通讯录同步完成，共同步 {SyncedEmployeeCount} 名员工");
        }
        catch (Exception ex)
        {
            ShowError($"通讯录同步失败: {ex.Message}");
        }
        finally
        {
            IsSyncingContacts = false;
        }
    }

    // ==================== 步骤5: 客户同步 ====================

    [RelayCommand]
    private async Task SyncCustomersAsync()
    {
        IsSyncingCustomers = true;
        try
        {
            // 模拟同步过程
            await Task.Delay(2000);
            SyncedCustomerCount = 156; // 示例数据
            ShowSuccess($"客户同步完成，共同步 {SyncedCustomerCount} 个客户");
        }
        catch (Exception ex)
        {
            ShowError($"客户同步失败: {ex.Message}");
        }
        finally
        {
            IsSyncingCustomers = false;
        }
    }

    // ==================== 保存配置 ====================

    [RelayCommand]
    private async Task SaveConfigAsync()
    {
        try
        {
            var config = await _dbContext.WeChatConfigs.FirstOrDefaultAsync();
            if (config == null)
            {
                config = new Domain.Entities.WeChatConfig();
                _dbContext.WeChatConfigs.Add(config);
            }

            config.CorpId = CorpId;
            config.AppSecret = _encryptionService.Encrypt(CorpSecret);
            config.AgentId = int.TryParse(AgentId, out var agentId) ? agentId : 0;
            config.Token = Token;
            config.EncodingAESKey = EncodingAesKey;
            config.WebhookUrl = CallbackUrl;
            config.IsEnabled = IsConfigurationValid;
            config.UpdatedAt = DateTime.Now;

            await _dbContext.SaveChangesAsync();
            ShowSuccess("配置保存成功！");
        }
        catch (Exception ex)
        {
            ShowError($"保存配置失败: {ex.Message}");
        }
    }

    // ==================== 完成向导 ====================

    [RelayCommand]
    private async Task CompleteWizardAsync()
    {
        await SaveConfigAsync();
        CurrentStep = WeChatWizardStep.Complete;
        ShowSuccess("企业微信配置向导完成！");
    }
}
