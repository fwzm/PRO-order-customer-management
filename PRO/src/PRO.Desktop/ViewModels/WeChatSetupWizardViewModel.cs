using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRO.Domain.Entities;
using PRO.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace PRO.Desktop.ViewModels;

/// <summary>
/// 企业微信配置向导 ViewModel — 4步完成企微集成
/// Step 1: 应用创建 → Step 2: 回调配置 → Step 3: 通讯录同步 → Step 4: 完成验证
/// </summary>
public partial class WeChatSetupWizardViewModel : ViewModelBase
{
    private readonly ProDbContext _dbContext;
    private readonly HttpClient _httpClient;

    // ─── 向导步骤 ─────────────────────────────────────────────

    [ObservableProperty]
    private int _currentStep = 1;

    [ObservableProperty]
    private bool _step1Completed;

    [ObservableProperty]
    private bool _step2Completed;

    [ObservableProperty]
    private bool _step3Completed;

    [ObservableProperty]
    private string _wizardStatus = "就绪";

    public bool IsStep1 => CurrentStep == 1;
    public bool IsStep2 => CurrentStep == 2;
    public bool IsStep3 => CurrentStep == 3;
    public bool IsStep4 => CurrentStep == 4;
    public bool IsLastStep => CurrentStep == 4;
    public bool IsFirstStep => CurrentStep == 1;
    public bool CanNext => (CurrentStep == 1 && Step1Completed)
                        || (CurrentStep == 2 && Step2Completed)
                        || (CurrentStep == 3 && Step3Completed);

    // ─── Step 1: 应用创建 ─────────────────────────────────────

    [ObservableProperty]
    private string _corpId = string.Empty;

    [ObservableProperty]
    private string _appSecret = string.Empty;

    [ObservableProperty]
    private int _agentId;

    [ObservableProperty]
    private string _appName = "PRO订单管理系统";

    [ObservableProperty]
    private bool _isTestingConnection;

    [ObservableProperty]
    private string _connectionTestResult = string.Empty;

    // ─── Step 2: 回调配置 ─────────────────────────────────────

    [ObservableProperty]
    private string? _callbackUrl = string.Empty;

    [ObservableProperty]
    private string? _token = string.Empty;

    [ObservableProperty]
    private string? _encodingAESKey = string.Empty;

    [ObservableProperty]
    private string? _webhookUrl = string.Empty;

    [ObservableProperty]
    private bool _isVerifyingCallback;

    [ObservableProperty]
    private string _callbackVerifyResult = string.Empty;

    // ─── Step 3: 通讯录同步 ────────────────────────────────────

    [ObservableProperty]
    private bool _isSyncingOrg;

    [ObservableProperty]
    private int _syncedDepartmentCount;

    [ObservableProperty]
    private int _syncedEmployeeCount;

    [ObservableProperty]
    private int _syncedCustomerCount;

    [ObservableProperty]
    private string _syncProgress = string.Empty;

    [ObservableProperty]
    private bool _isPullingCustomers;

    // ─── Step 4: 完成 & 测试 ──────────────────────────────────

    [ObservableProperty]
    private bool _isSendingTest;

    [ObservableProperty]
    private string _testSendResult = string.Empty;

    [ObservableProperty]
    private string _setupSummary = string.Empty;

    // ─── 命令 ────────────────────────────────────────────────

    public IAsyncRelayCommand NextStepCommand { get; }
    public IRelayCommand PreviousStepCommand { get; }
    public IAsyncRelayCommand TestConnectionCommand { get; }
    public IAsyncRelayCommand VerifyCallbackCommand { get; }
    public IAsyncRelayCommand SyncOrganizationCommand { get; }
    public IAsyncRelayCommand PullCustomersCommand { get; }
    public IAsyncRelayCommand SendTestMessageCommand { get; }
    public IAsyncRelayCommand FinishSetupCommand { get; }
    public IRelayCommand GenerateTokenCommand { get; }
    public IRelayCommand GenerateAesKeyCommand { get; }

    public WeChatSetupWizardViewModel(ProDbContext dbContext, IHttpClientFactory httpClientFactory)
    {
        _dbContext = dbContext;
        _httpClient = httpClientFactory.CreateClient("WeChatWork");

        NextStepCommand = new AsyncRelayCommand(GoToNextStepAsync, () => CanNext);
        PreviousStepCommand = new RelayCommand(GoToPreviousStep, () => !IsFirstStep);
        TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync);
        VerifyCallbackCommand = new AsyncRelayCommand(VerifyCallbackAsync);
        SyncOrganizationCommand = new AsyncRelayCommand(SyncOrganizationAsync);
        PullCustomersCommand = new AsyncRelayCommand(PullCustomersAsync);
        SendTestMessageCommand = new AsyncRelayCommand(SendTestMessageAsync);
        FinishSetupCommand = new AsyncRelayCommand(FinishSetupAsync);
        GenerateTokenCommand = new RelayCommand(() => Token = GenerateRandomString(32));
        GenerateAesKeyCommand = new RelayCommand(() => EncodingAESKey = GenerateRandomString(43));
    }

    // ─── 初始化 — 加载已有配置 ─────────────────────────────────

    [RelayCommand]
    public async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            var config = await _dbContext.WeChatConfigs
                .FirstOrDefaultAsync(c => c.IsEnabled);

            if (config != null)
            {
                CorpId = config.CorpId;
                AppSecret = config.AppSecret;
                AgentId = config.AgentId;
                AppName = config.Name;
                Token = config.Token;
                EncodingAESKey = config.EncodingAESKey;
                WebhookUrl = config.WebhookUrl;
                Step1Completed = true;
                Step2Completed = !string.IsNullOrWhiteSpace(config.Token);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载企微配置失败");
            ShowError($"加载配置失败: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ─── Step 1: 测试连接 ─────────────────────────────────────

    private async Task TestConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(CorpId) || string.IsNullOrWhiteSpace(AppSecret))
        {
            ShowError("请填写企业ID和应用Secret");
            return;
        }

        IsTestingConnection = true;
        ConnectionTestResult = string.Empty;
        try
        {
            // 调用企微 /cgi-bin/gettoken 验证
            var url = $"https://qyapi.weixin.qq.com/cgi-bin/gettoken?corpid={CorpId}&corpsecret={AppSecret}";
            var response = await _httpClient.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("errcode", out var errCode) && errCode.GetInt32() == 0)
            {
                var accessToken = doc.RootElement.GetProperty("access_token").GetString();
                ConnectionTestResult = "✓ 连接成功！应用凭证验证通过";
                Step1Completed = true;
                ShowSuccess("企业微信连接验证成功");

                Log.Information("企微连接测试成功 CorpId={CorpId}", CorpId[..Math.Min(8, CorpId.Length)]);
            }
            else
            {
                var errMsg = doc.RootElement.GetProperty("errmsg").GetString();
                ConnectionTestResult = $"✗ 连接失败: {errMsg} (errcode={errCode.GetInt32()})";
                Step1Completed = false;
                ShowError($"连接失败: {errMsg}");
            }
        }
        catch (HttpRequestException ex)
        {
            ConnectionTestResult = $"✗ 网络请求失败: {ex.Message}";
            Step1Completed = false;
            ShowError("无法连接企业微信服务器，请检查网络");
        }
        catch (Exception ex)
        {
            ConnectionTestResult = $"✗ 异常: {ex.Message}";
            Step1Completed = false;
            Log.Error(ex, "企微连接测试异常");
        }
        finally
        {
            IsTestingConnection = false;
        }
    }

    // ─── Step 2: 验证回调配置 ──────────────────────────────────

    private async Task VerifyCallbackAsync()
    {
        if (string.IsNullOrWhiteSpace(CallbackUrl) || string.IsNullOrWhiteSpace(Token))
        {
            ShowError("请填写回调URL和Token");
            return;
        }

        IsVerifyingCallback = true;
        CallbackVerifyResult = "验证中...";
        try
        {
            // 保存回调地址到本地数据库
            var config = await _dbContext.WeChatConfigs
                .FirstOrDefaultAsync(c => c.IsEnabled);
            if (config != null)
            {
                config.Token = Token;
                config.EncodingAESKey = EncodingAESKey;
                config.WebhookUrl = WebhookUrl;
                config.UpdatedAt = DateTime.Now;
                await _dbContext.SaveChangesAsync();
            }

            // 企微回调验证需要服务端在公网运行，此处做本地校验
            if (CallbackUrl.StartsWith("http"))
            {
                CallbackVerifyResult = "✓ 回调URL格式正确。回调地址验证需要服务端上线后由企微服务器发起，请部署后重试。";
            }
            else
            {
                CallbackVerifyResult = "⚠ 请使用完整的 http/https URL（需要企业微信服务器可访问到的公网地址）";
                ShowError("回调地址必须是公网URL");
                IsVerifyingCallback = false;
                return;
            }

            Step2Completed = true;
            ShowSuccess("回调配置已保存");
        }
        catch (Exception ex)
        {
            CallbackVerifyResult = $"✗ 保存失败: {ex.Message}";
            Log.Error(ex, "企微回调配置保存失败");
            ShowError($"保存失败: {ex.Message}");
        }
        finally
        {
            IsVerifyingCallback = false;
        }
    }

    // ─── Step 3: 通讯录同步 ────────────────────────────────────

    private async Task SyncOrganizationAsync()
    {
        if (!Step1Completed)
        {
            ShowError("请先完成第一步：应用创建和连接验证");
            return;
        }

        IsSyncingOrg = true;
        SyncProgress = "正在同步部门...";
        try
        {
            // 1. 同步部门
            var deptUrl = $"https://qyapi.weixin.qq.com/cgi-bin/department/list?access_token={await GetAccessTokenAsync()}";
            var deptResponse = await _httpClient.GetAsync(deptUrl);
            var deptJson = await deptResponse.Content.ReadAsStringAsync();
            using var deptDoc = JsonDocument.Parse(deptJson);

            if (deptDoc.RootElement.TryGetProperty("department", out var departments))
            {
                SyncedDepartmentCount = departments.GetArrayLength();
                SyncProgress = $"已同步 {SyncedDepartmentCount} 个部门，正在同步成员...";

                // 存入日志
                _dbContext.WeChatSyncLogs.Add(new WeChatSyncLog
                {
                    SyncType = "组织架构",
                    SyncDirection = "下载",
                    RecordCount = SyncedDepartmentCount,
                    Status = "Success",
                    SyncTime = DateTime.Now,
                    Details = deptJson
                });
            }

            // 2. 同步成员
            var userUrl = $"https://qyapi.weixin.qq.com/cgi-bin/user/list?access_token={await GetAccessTokenAsync()}&department_id=1&fetch_child=1";
            var userResponse = await _httpClient.GetAsync(userUrl);
            var userJson = await userResponse.Content.ReadAsStringAsync();
            using var userDoc = JsonDocument.Parse(userJson);

            if (userDoc.RootElement.TryGetProperty("userlist", out var users))
            {
                SyncedEmployeeCount = users.GetArrayLength();
                SyncProgress = $"已同步 {SyncedEmployeeCount} 个成员";
            }

            Step3Completed = true;
            await _dbContext.SaveChangesAsync();
            ShowSuccess($"同步完成: {SyncedDepartmentCount} 个部门, {SyncedEmployeeCount} 个成员");

            Log.Information("企微组织架构同步完成 Departments={Dept}, Employees={Emp}",
                SyncedDepartmentCount, SyncedEmployeeCount);
        }
        catch (HttpRequestException ex)
        {
            SyncProgress = $"同步失败: 网络错误 - {ex.Message}";
            ShowError("网络请求失败，请检查连接");
        }
        catch (Exception ex)
        {
            SyncProgress = $"同步失败: {ex.Message}";
            Log.Error(ex, "企微组织架构同步失败");
            ShowError($"同步失败: {ex.Message}");
        }
        finally
        {
            IsSyncingOrg = false;
        }
    }

    private async Task PullCustomersAsync()
    {
        IsPullingCustomers = true;
        SyncProgress = "正在拉取客户列表...";
        try
        {
            var url = $"https://qyapi.weixin.qq.com/cgi-bin/externalcontact/list?access_token={await GetAccessTokenAsync()}&userid=";
            var response = await _httpClient.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("external_userid", out var users))
            {
                SyncedCustomerCount = users.GetArrayLength();
                SyncProgress = $"已拉取 {SyncedCustomerCount} 个客户";
            }

            _dbContext.WeChatSyncLogs.Add(new WeChatSyncLog
            {
                SyncType = "客户",
                SyncDirection = "下载",
                RecordCount = SyncedCustomerCount,
                Status = "Success",
                SyncTime = DateTime.Now,
                Details = json
            });
            await _dbContext.SaveChangesAsync();

            ShowSuccess($"客户拉取完成: {SyncedCustomerCount} 条");
        }
        catch (Exception ex)
        {
            SyncProgress = $"拉取失败: {ex.Message}";
            Log.Error(ex, "企微客户拉取失败");
            ShowError($"拉取失败: {ex.Message}");
        }
        finally
        {
            IsPullingCustomers = false;
        }
    }

    // ─── Step 4: 发送测试消息 ──────────────────────────────────

    private async Task SendTestMessageAsync()
    {
        IsSendingTest = true;
        TestSendResult = "发送中...";
        try
        {
            var token = await GetAccessTokenAsync();
            var url = $"https://qyapi.weixin.qq.com/cgi-bin/message/send?access_token={token}";

            var payload = new
            {
                touser = "@all",
                msgtype = "text",
                agentid = AgentId,
                text = new { content = $"PRO系统企微集成测试 — {DateTime.Now:yyyy-MM-dd HH:mm:ss}" }
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content);
            var json = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("errcode", out var errCode) && errCode.GetInt32() == 0)
            {
                TestSendResult = "✓ 测试消息发送成功！";
                ShowSuccess("测试消息已发送");
            }
            else
            {
                var errMsg = doc.RootElement.GetProperty("errmsg").GetString();
                TestSendResult = $"✗ 发送失败: {errMsg}";
                ShowError($"发送失败: {errMsg}");
            }
        }
        catch (Exception ex)
        {
            TestSendResult = $"✗ 异常: {ex.Message}";
            Log.Error(ex, "企微测试消息发送失败");
        }
        finally
        {
            IsSendingTest = false;
        }
    }

    // ─── 完成 ─────────────────────────────────────────────────

    private async Task FinishSetupAsync()
    {
        try
        {
            // 保存所有配置到 WeChatConfig
            var config = await _dbContext.WeChatConfigs
                .FirstOrDefaultAsync(c => c.CorpId == CorpId);

            if (config == null)
            {
                config = new WeChatConfig
                {
                    Name = AppName,
                    CorpId = CorpId,
                    AppSecret = AppSecret,
                    AgentId = AgentId,
                    Token = Token,
                    EncodingAESKey = EncodingAESKey,
                    WebhookUrl = WebhookUrl,
                    IsEnabled = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                _dbContext.WeChatConfigs.Add(config);
            }
            else
            {
                config.Name = AppName;
                config.AppSecret = AppSecret;
                config.AgentId = AgentId;
                config.Token = Token;
                config.EncodingAESKey = EncodingAESKey;
                config.WebhookUrl = WebhookUrl;
                config.IsEnabled = true;
                config.UpdatedAt = DateTime.Now;
            }

            await _dbContext.SaveChangesAsync();

            SetupSummary = $"""
                ✓ 企业微信集成配置完成
                
                应用名称: {AppName}
                企业ID: {CorpId}
                AgentId: {AgentId}
                同步部门数: {SyncedDepartmentCount}
                同步成员数: {SyncedEmployeeCount}
                拉取客户数: {SyncedCustomerCount}
                回调URL: {CallbackUrl}
                Webhook: {WebhookUrl}
                """;

            ShowSuccess("企业微信集成配置已完成！");
            Log.Information("企微集成配置完成 CorpId={CorpId}", CorpId[..Math.Min(8, CorpId.Length)]);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "保存企微配置失败");
            ShowError($"保存失败: {ex.Message}");
        }
    }

    // ─── 导航 ─────────────────────────────────────────────────

    private async Task GoToNextStepAsync()
    {
        CurrentStep = Math.Min(4, CurrentStep + 1);
        UpdateStepStates();
        if (CurrentStep == 4)
            await RefreshSummaryAsync();
    }

    private void GoToPreviousStep()
    {
        CurrentStep = Math.Max(1, CurrentStep - 1);
        UpdateStepStates();
    }

    private void UpdateStepStates()
    {
        OnPropertyChanged(nameof(IsStep1));
        OnPropertyChanged(nameof(IsStep2));
        OnPropertyChanged(nameof(IsStep3));
        OnPropertyChanged(nameof(IsStep4));
        OnPropertyChanged(nameof(IsLastStep));
        OnPropertyChanged(nameof(IsFirstStep));
        OnPropertyChanged(nameof(CanNext));
        WizardStatus = CurrentStep switch
        {
            1 => "Step 1/4: 填写企业微信应用凭证",
            2 => "Step 2/4: 配置回调地址与Token",
            3 => "Step 3/4: 同步通讯录与客户",
            4 => "Step 4/4: 验证配置并完成",
            _ => ""
        };
    }

    private async Task RefreshSummaryAsync()
    {
        // 统计同步状态
        var syncLog = await _dbContext.WeChatSyncLogs
            .OrderByDescending(l => l.SyncTime)
            .FirstOrDefaultAsync();
        var config = await _dbContext.WeChatConfigs
            .FirstOrDefaultAsync(c => c.IsEnabled);

        SetupSummary = $"""
            应用: {config?.Name ?? AppName}
            已同步部门: {SyncedDepartmentCount}
            已同步成员: {SyncedEmployeeCount}
            已拉取客户: {SyncedCustomerCount}
            最近同步: {syncLog?.SyncTime:yyyy-MM-dd HH:mm:ss}
            状态: {config?.IsEnabled == true}
            """;
    }

    // ─── 辅助 ─────────────────────────────────────────────────

    private async Task<string> GetAccessTokenAsync()
    {
        // 缓存：先查数据库最近token
        var url = $"https://qyapi.weixin.qq.com/cgi-bin/gettoken?corpid={CorpId}&corpsecret={AppSecret}";
        var response = await _httpClient.GetAsync(url);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("errcode", out var errCode) && errCode.GetInt32() == 0)
            return doc.RootElement.GetProperty("access_token").GetString()!;

        throw new InvalidOperationException($"获取AccessToken失败: {json}");
    }

    private static string GenerateRandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}
