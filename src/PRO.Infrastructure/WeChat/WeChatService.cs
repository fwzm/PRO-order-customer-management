using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;
using PRO.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace PRO.Infrastructure.WeChat;

/// <summary>
/// 企业微信服务实现 - 完整API支持
/// 官方文档: https://developer.work.weixin.qq.com/document/path/90664
/// </summary>
public partial class WeChatService : IWeChatService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IEncryptionService _encryptionService;
    private readonly ProDbContext _dbContext;

    private const string ApiBaseUrl = "https://qyapi.weixin.qq.com/cgi-bin";
    private WeChatConfig? _cachedConfig;
    private DateTime _configCacheTime;

    public WeChatService(
        IHttpClientFactory httpClientFactory,
        IEncryptionService encryptionService,
        ProDbContext dbContext)
    {
        _httpClientFactory = httpClientFactory;
        _encryptionService = encryptionService;
        _dbContext = dbContext;
    }

    #region 访问令牌管理

    private async Task<string?> GetAccessTokenAsync(bool forceRefresh = false)
    {
        var config = await GetWeChatConfigAsync();
        if (config == null || string.IsNullOrEmpty(config.CorpId) || string.IsNullOrEmpty(config.CorpSecret))
            return null;

        // 令牌有效直接返回
        if (!forceRefresh && !config.NeedsTokenRefresh)
            return config.AccessToken;

        // 获取新令牌
        var client = _httpClientFactory.CreateClient();
        var url = $"{ApiBaseUrl}/gettoken?corpid={config.CorpId}&corpsecret={config.CorpSecret}";

        try
        {
            var response = await client.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<JObject>(json);
            if (result == null)
                return null;

            var errcode = result["errcode"]?.Value<int>() ?? -1;
            if (errcode == 0)
            {
                config.AccessToken = result["access_token"]?.ToString();
                config.TokenExpiresAt = DateTime.UtcNow.AddSeconds(result["expires_in"]?.Value<int>() ?? 7200);
                await SaveWeChatConfigAsync(config);
                return config.AccessToken;
            }
        }
        catch { }

        return null;
    }

    #endregion

    #region 带自动重试的API调用

    /// <summary>
    /// 带Token自动刷新和重试机制的API请求
    /// </summary>
    private async Task<JObject?> CallApiWithRetryAsync(string path, string method = "GET", object? body = null)
    {
        var token = await GetAccessTokenAsync();
        if (string.IsNullOrEmpty(token)) return null;

        var client = _httpClientFactory.CreateClient();
        var url = $"{ApiBaseUrl}{path}?access_token={token}";

        try
        {
            HttpResponseMessage response;
            if (method == "POST")
            {
                var json = JsonConvert.SerializeObject(body);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                response = await client.PostAsync(url, content);
            }
            else
            {
                response = await client.GetAsync(url);
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JObject.Parse(responseJson);
            var errcode = result["errcode"]?.Value<int>() ?? -1;

            // Token过期，刷新后重试一次
            if (errcode == 42001)
            {
                token = await GetAccessTokenAsync(forceRefresh: true);
                if (string.IsNullOrEmpty(token)) return null;

                url = $"{ApiBaseUrl}{path}?access_token={token}";
                if (method == "POST")
                {
                    var json = JsonConvert.SerializeObject(body);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    response = await client.PostAsync(url, content);
                }
                else
                {
                    response = await client.GetAsync(url);
                }
                responseJson = await response.Content.ReadAsStringAsync();
                result = JObject.Parse(responseJson);
            }

            return result;
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region 配置管理

    public async Task<WeChatConfig?> GetWeChatConfigAsync()
    {
        if (_cachedConfig != null && (DateTime.Now - _configCacheTime).TotalMinutes < 1)
            return _cachedConfig;

        try
        {
            var entity = await _dbContext.WeChatConfigs
                .OrderByDescending(c => c.UpdatedAt)
                .FirstOrDefaultAsync();

            var result = new WeChatConfig();
            if (entity != null)
            {
                result.CorpId = entity.CorpId;
                result.CorpSecret = !string.IsNullOrEmpty(entity.AppSecret)
                    ? _encryptionService.Decrypt(entity.AppSecret) : string.Empty;
                result.AgentId = entity.AgentId.ToString();
                result.DefaultWebhookUrl = entity.WebhookUrl;
                result.IsEnabled = entity.IsEnabled;
                // AccessToken和TokenExpiresAt在Domain实体中没有持久化，运行时临时存储
            }

            _cachedConfig = result;
            _configCacheTime = DateTime.Now;
            return _cachedConfig;
        }
        catch
        {
            return new WeChatConfig();
        }
    }

    public async Task SaveWeChatConfigAsync(WeChatConfig config)
    {
        var entity = await _dbContext.WeChatConfigs.FirstOrDefaultAsync();
        if (entity != null)
        {
            entity.CorpId = config.CorpId;
            entity.AppSecret = !string.IsNullOrEmpty(config.CorpSecret)
                ? _encryptionService.Encrypt(config.CorpSecret) : entity.AppSecret;
            entity.AgentId = int.TryParse(config.AgentId, out var aid) ? aid : entity.AgentId;
            entity.WebhookUrl = config.DefaultWebhookUrl;
            entity.IsEnabled = config.IsEnabled;
            entity.UpdatedAt = DateTime.Now;
        }
        else
        {
            _dbContext.WeChatConfigs.Add(new Domain.Entities.WeChatConfig
            {
                CorpId = config.CorpId,
                AppSecret = !string.IsNullOrEmpty(config.CorpSecret)
                    ? _encryptionService.Encrypt(config.CorpSecret) : string.Empty,
                AgentId = int.TryParse(config.AgentId, out var aid) ? aid : 0,
                WebhookUrl = config.DefaultWebhookUrl,
                IsEnabled = config.IsEnabled,
                UpdatedAt = DateTime.Now
            });
        }

        await _dbContext.SaveChangesAsync();
        _cachedConfig = null;
    }

    public async Task<bool> TestConnectionAsync()
    {
        var config = await GetWeChatConfigAsync();
        if (config == null || string.IsNullOrEmpty(config.CorpId) || string.IsNullOrEmpty(config.CorpSecret))
            return false;

        var client = _httpClientFactory.CreateClient();
        var url = $"{ApiBaseUrl}/gettoken?corpid={config.CorpId}&corpsecret={config.CorpSecret}";

        try
        {
            var response = await client.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();
            var result = JObject.Parse(json);
            return result["errcode"]?.Value<int>() == 0;
        }
        catch { return false; }
    }

    #endregion

    #region 消息发送

    /// <summary>
    /// 发送文本消息
    /// </summary>
    public async Task<bool> SendTextMessageAsync(string toUser, string content, string? agentId = null)
    {
        var config = await GetWeChatConfigAsync();
        var result = await CallApiWithRetryAsync("/message/send", "POST", new
        {
            touser = toUser,
            msgtype = "text",
            agentid = agentId ?? config?.AgentId.ToString() ?? "0",
            text = new { content }
        });

        return result?["errcode"]?.Value<int>() == 0;
    }

    /// <summary>
    /// 发送Markdown消息
    /// </summary>
    public async Task<bool> SendMarkdownMessageAsync(string toUser, string markdown, string? agentId = null)
    {
        var config = await GetWeChatConfigAsync();
        var result = await CallApiWithRetryAsync("/message/send", "POST", new
        {
            touser = toUser,
            msgtype = "markdown",
            agentid = agentId ?? config?.AgentId.ToString() ?? "0",
            markdown = new { content = markdown }
        });

        return result?["errcode"]?.Value<int>() == 0;
    }

    /// <summary>
    /// 发送模板卡片消息（文本通知型）
    /// </summary>
    public async Task<bool> SendTemplateCardAsync(string toUser, string title, string desc,
        string? url = null, string? remark = null, string? agentId = null)
    {
        var config = await GetWeChatConfigAsync();

        var card = new Dictionary<string, object>
        {
            ["card_type"] = "text_notice",
            ["main_title"] = new Dictionary<string, string>
            {
                ["title"] = title,
                ["desc"] = desc ?? ""
            }
        };

        if (!string.IsNullOrEmpty(remark))
        {
            card["sub_title_text"] = remark;
        }

        if (!string.IsNullOrEmpty(url))
        {
            card["card_action"] = new Dictionary<string, object>
            {
                ["type"] = 1,
                ["url"] = url
            };
        }

        var result = await CallApiWithRetryAsync("/message/send", "POST", new
        {
            touser = toUser,
            msgtype = "template_card",
            agentid = agentId ?? config?.AgentId.ToString() ?? "0",
            template_card = card
        });

        return result?["errcode"]?.Value<int>() == 0;
    }

    /// <summary>
    /// 通过Webhook发送群机器人消息
    /// </summary>
    public async Task<bool> SendWebhookMessageAsync(string webhookUrl, WebhookMessage message)
    {
        var client = _httpClientFactory.CreateClient();
        try
        {
            var json = JsonConvert.SerializeObject(message, Formatting.None);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(webhookUrl, content);
            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JObject.Parse(responseJson);
            return result["errcode"]?.Value<int>() == 0;
        }
        catch { return false; }
    }

    /// <summary>
    /// 发送排班变动通知给员工
    /// </summary>
    public async Task<bool> NotifyScheduleChangeAsync(int employeeId, string scheduleInfo)
    {
        var employee = await _dbContext.Employees.FindAsync(employeeId);
        if (employee == null || string.IsNullOrEmpty(employee.WeChatUserId))
            return false;

        var message = $"【排班变动提醒】\n您的排班已更新：\n{scheduleInfo}\n\n请登录系统查看详情。";
        return await SendTextMessageAsync(employee.WeChatUserId, message);
    }

    #endregion

    #region IWeChatService接口实现

    public async Task<ApiResponse<bool>> SendMessageAsync(string toUser, string content, string? agentId = null)
    {
        var result = await SendTextMessageAsync(toUser, content, agentId);
        return ApiResponse<bool>.Ok(result, result ? "发送成功" : "发送失败");
    }

    public async Task<ApiResponse<bool>> SendTemplateMessageAsync(string toUser, string templateId, Dictionary<string, string> data)
    {
        // 使用模板卡片格式发送
        var title = data.GetValueOrDefault("title", "通知");
        var desc = data.GetValueOrDefault("content", "");
        var url = data.GetValueOrDefault("url");
        var result = await SendTemplateCardAsync(toUser, title, desc, url);
        return ApiResponse<bool>.Ok(result, result ? "发送成功" : "发送失败");
    }

    public async Task<ApiResponse<WeChatUserInfo>> GetUserInfoAsync(string code)
    {
        var token = await GetAccessTokenAsync();
        if (string.IsNullOrEmpty(token))
            return ApiResponse<WeChatUserInfo>.Fail("获取令牌失败");

        var client = _httpClientFactory.CreateClient();
        var url = $"{ApiBaseUrl}/user/getuserinfo?access_token={token}&code={code}";

        try
        {
            var response = await client.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();
            var result = JObject.Parse(json);

            if (result["errcode"]?.Value<int>() == 0)
            {
                var userId = result["UserId"]?.ToString();
                if (!string.IsNullOrEmpty(userId))
                {
                    var userDetail = await GetUserDetailAsync(userId);
                    if (userDetail != null)
                    {
                        return ApiResponse<WeChatUserInfo>.Ok(new WeChatUserInfo
                        {
                            UserId = userId,
                            Name = userDetail.Name,
                            Phone = userDetail.Mobile
                        });
                    }
                }
            }
        }
        catch { }

        return ApiResponse<WeChatUserInfo>.Fail("获取用户信息失败");
    }

    private async Task<WeChatUser?> GetUserDetailAsync(string userId)
    {
        var token = await GetAccessTokenAsync();
        if (string.IsNullOrEmpty(token)) return null;

        var client = _httpClientFactory.CreateClient();
        var url = $"{ApiBaseUrl}/user/get?access_token={token}&userid={userId}";

        try
        {
            var response = await client.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();
            var result = JObject.Parse(json);
            if (result["errcode"]?.Value<int>() == 0)
            {
                return JsonConvert.DeserializeObject<WeChatUser>(json);
            }
        }
        catch { }
        return null;
    }

    public async Task<ApiResponse<bool>> SyncOrganizationAsync()
    {
        var startTime = DateTime.Now;
        var syncLog = new WeChatSyncLog
        {
            SyncType = "Full",
            SyncDirection = "Download",
            Status = "Processing",
            SyncTime = startTime
        };

        try
        {
            var token = await GetAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
                return ApiResponse<bool>.Fail("获取企业微信令牌失败");

            var client = _httpClientFactory.CreateClient();

            // 获取部门列表
            var deptResp = await client.GetAsync($"{ApiBaseUrl}/department/list?access_token={token}");
            var deptJson = await deptResp.Content.ReadAsStringAsync();
            var deptResult = JObject.Parse(deptJson);
            var departments = deptResult["department"]?.ToObject<List<WeChatDepartment>>() ?? new List<WeChatDepartment>();

            // 同步部门（使用 wx_ 前缀避免ID冲突）
            foreach (var dept in departments)
            {
                var wxDeptId = $"wx_{dept.Id}";
                var existing = await _dbContext.Departments
                    .FirstOrDefaultAsync(d => d.Code == wxDeptId);

                if (existing != null)
                {
                    existing.Name = dept.Name;
                    existing.ParentId = dept.ParentId > 0
                        ? (_dbContext.Departments.FirstOrDefault(d => d.Code == $"wx_{dept.ParentId}")?.Id)
                        : null;
                }
                else
                {
                    _dbContext.Departments.Add(new Department
                    {
                        Name = dept.Name,
                        Code = wxDeptId,
                        BranchId = 1,
                        ParentId = dept.ParentId > 0
                            ? (_dbContext.Departments.FirstOrDefault(d => d.Code == $"wx_{dept.ParentId}")?.Id)
                            : null,
                        Status = EntityStatus.Active,
                        CreatedAt = DateTime.Now
                    });
                }
            }

            await _dbContext.SaveChangesAsync();

            // 获取用户列表
            var updatedCount = 0;
            var createdCount = 0;

            foreach (var dept in departments)
            {
                var userResp = await client.GetAsync(
                    $"{ApiBaseUrl}/user/list?access_token={token}&department_id={dept.Id}");
                var userJson = await userResp.Content.ReadAsStringAsync();
                var userResult = JObject.Parse(userJson);
                var userList = userResult["userlist"]?.ToObject<List<WeChatUser>>() ?? new List<WeChatUser>();

                foreach (var user in userList)
                {
                    var existingEmployee = await _dbContext.Employees
                        .FirstOrDefaultAsync(e => e.WeChatUserId == user.UserId);

                    if (existingEmployee != null)
                    {
                        existingEmployee.Name = user.Name;
                        existingEmployee.Phone = user.Mobile ?? existingEmployee.Phone;
                        updatedCount++;
                    }
                    else
                    {
                        var defaultBranch = await _dbContext.Branches.FirstOrDefaultAsync();
                        var defaultRole = await _dbContext.Roles.FirstOrDefaultAsync(r => r.Code == "EMPLOYEE");

                        _dbContext.Employees.Add(new Employee
                        {
                            Name = user.Name,
                            EmployeeNo = user.UserId,
                            BranchId = defaultBranch?.Id ?? 1,
                            RoleId = defaultRole?.Id ?? 4,
                            Phone = user.Mobile,
                            WeChatUserId = user.UserId,
                            Status = EmployeeStatus.Active,
                            CreatedAt = DateTime.Now
                        });
                        createdCount++;
                    }
                }
            }

            await _dbContext.SaveChangesAsync();

            syncLog.RecordCount = updatedCount + createdCount;
            syncLog.Status = "Success";
            _dbContext.WeChatSyncLogs.Add(syncLog);
            await _dbContext.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, $"同步完成：更新 {updatedCount} 条，新增 {createdCount} 条");
        }
        catch (Exception ex)
        {
            syncLog.Status = "Failed";
            syncLog.ErrorMessage = ex.Message;
            _dbContext.WeChatSyncLogs.Add(syncLog);
            await _dbContext.SaveChangesAsync();
            return ApiResponse<bool>.Fail($"同步失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> SyncEmployeeAsync(int employeeId)
    {
        try
        {
            var employee = await _dbContext.Employees.FindAsync(employeeId);
            if (employee == null) return ApiResponse<bool>.Fail("员工不存在");
            if (string.IsNullOrEmpty(employee.WeChatUserId))
                return ApiResponse<bool>.Fail("该员工未绑定企业微信账号");

            var token = await GetAccessTokenAsync();
            if (string.IsNullOrEmpty(token)) return ApiResponse<bool>.Fail("获取令牌失败");

            var client = _httpClientFactory.CreateClient();
            var url = $"{ApiBaseUrl}/user/get?access_token={token}&userid={employee.WeChatUserId}";
            var response = await client.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();
            var result = JObject.Parse(json);

            if (result["errcode"]?.Value<int>() == 0)
            {
                var name = result["name"]?.ToString();
                var mobile = result["mobile"]?.ToString();
                if (!string.IsNullOrEmpty(name)) employee.Name = name;
                if (!string.IsNullOrEmpty(mobile)) employee.Phone = mobile;
                await _dbContext.SaveChangesAsync();
                return ApiResponse<bool>.Ok(true, $"员工 {employee.Name} 同步成功");
            }

            return ApiResponse<bool>.Fail("获取企业微信用户信息失败");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"同步失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> SyncCustomerAsync(int customerId)
    {
        try
        {
            var customer = await _dbContext.Customers.FindAsync(customerId);
            if (customer == null)
                return ApiResponse<bool>.Fail("客户不存在");

            // 如果有企业微信外部联系人ID，同步到企业微信
            if (!string.IsNullOrEmpty(customer.WeChatExternalUserId))
            {
                var token = await GetAccessTokenAsync();
                if (string.IsNullOrEmpty(token))
                    return ApiResponse<bool>.Fail("获取企业微信令牌失败");

                // 找一个有企业微信的员工来执行备注更新
                var employee = await _dbContext.Employees
                    .Where(e => !string.IsNullOrEmpty(e.WeChatUserId))
                    .FirstOrDefaultAsync();

                if (employee == null)
                    return ApiResponse<bool>.Fail("没有找到已绑定企业微信的员工");

                var client = _httpClientFactory.CreateClient();
                var url = $"{ApiBaseUrl}/externalcontact/remark?access_token={token}";

                var body = new
                {
                    userid = employee.WeChatUserId,
                    external_userid = customer.WeChatExternalUserId,
                    remark = Truncate(customer.Name, 20),
                    description = Truncate(customer.Remark ?? $"来自PRO系统 - {customer.Name}", 150),
                    remark_mobiles = !string.IsNullOrEmpty(customer.Phone)
                        ? new[] { customer.Phone } : Array.Empty<string>()
                };

                var json = JsonConvert.SerializeObject(body);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(url, content);
                var resultJson = await response.Content.ReadAsStringAsync();
                var result = JObject.Parse(resultJson);

                if (result["errcode"]?.Value<int>() != 0)
                {
                    var errMsg = result["errmsg"]?.ToString() ?? "未知错误";
                    return ApiResponse<bool>.Fail($"同步到企业微信失败: {errMsg}");
                }
            }

            // 记录同步日志
            var syncLog = new WeChatSyncLog
            {
                SyncType = "Single",
                SyncDirection = "Upload",
                RecordCount = 1,
                Status = "Success",
                Details = JsonConvert.SerializeObject(new { CustomerId = customerId, Name = customer.Name }),
                SyncTime = DateTime.Now
            };
            _dbContext.WeChatSyncLogs.Add(syncLog);
            await _dbContext.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, $"客户「{customer.Name}」已同步至企业微信");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"同步失败: {ex.Message}");
        }
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= maxLength ? value : value.Substring(0, maxLength);
    }

    public async Task<ApiResponse<bool>> PullNewCustomersAsync()
    {
        var startTime = DateTime.Now;
        var syncLog = new WeChatSyncLog
        {
            SyncType = "Pull",
            SyncDirection = "Download",
            Status = "Processing",
            SyncTime = startTime
        };

        try
        {
            var employees = await _dbContext.Employees
                .Where(e => !string.IsNullOrEmpty(e.WeChatUserId))
                .ToListAsync();

            var newCount = 0;
            foreach (var employee in employees)
            {
                var token = await GetAccessTokenAsync();
                if (string.IsNullOrEmpty(token)) continue;

                var client = _httpClientFactory.CreateClient();
                var url = $"{ApiBaseUrl}/externalcontact/list?access_token={token}&userid={employee.WeChatUserId}";
                var response = await client.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();
                var result = JObject.Parse(json);

                if (result["errcode"]?.Value<int>() != 0) continue;

                var externalIds = result["external_userid"]?.ToObject<List<string>>() ?? new List<string>();
                foreach (var extId in externalIds)
                {
                    var exists = await _dbContext.Customers
                        .AnyAsync(c => c.WeChatExternalUserId == extId);
                    if (exists) continue;

                    // 获取详情
                    var detailUrl = $"{ApiBaseUrl}/externalcontact/get?access_token={token}&external_userid={extId}";
                    var detailResp = await client.GetAsync(detailUrl);
                    var detailJson = await detailResp.Content.ReadAsStringAsync();
                    var detailResult = JObject.Parse(detailJson);

                    if (detailResult["errcode"]?.Value<int>() == 0)
                    {
                        var contact = detailResult["external_contact"];
                        var branch = await _dbContext.Branches.FindAsync(employee.BranchId);
                        var branchCode = branch?.Code ?? "0000";
                        if (branchCode.Length != 4)
                            branchCode = branchCode.PadLeft(4, '0').Substring(0, 4);
                        var datePart = DateTime.Now.ToString("yyyyMMdd");
                        var custPrefix = $"K{datePart}{branchCode}";
                        var maxCustNo = await _dbContext.Customers
                            .Where(c => c.CustomerNo.StartsWith(custPrefix))
                            .MaxAsync(c => (string?)c.CustomerNo) ?? "";
                        var custSeq = 1;
                        if (maxCustNo.Length >= custPrefix.Length + 4 &&
                            int.TryParse(maxCustNo.Substring(custPrefix.Length, 4), out var parsedSeq))
                            custSeq = parsedSeq + 1;
                        var customerNo = $"{custPrefix}{custSeq:D4}";

                        var contactName = contact?["name"]?.ToString() ?? "未知客户";
                        var contactPhone = contact?["mobile"]?.ToString();
                        var contactCorpName = contact?["corp_name"]?.ToString();

                        _dbContext.Customers.Add(new Customer
                        {
                            CustomerNo = customerNo,
                            Name = contactName,
                            CustomerType = CustomerType.Major,
                            BranchId = employee.BranchId,
                            Phone = contactPhone,
                            WeChatExternalUserId = extId,
                            Remark = string.IsNullOrEmpty(contactCorpName) ? null : contactCorpName,
                            CreatedById = employee.Id,
                            Status = CustomerStatus.Active,
                            CreatedAt = DateTime.Now
                        });
                        newCount++;
                    }
                }
            }

            await _dbContext.SaveChangesAsync();

            syncLog.RecordCount = newCount;
            syncLog.Status = "Success";
            _dbContext.WeChatSyncLogs.Add(syncLog);
            await _dbContext.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, $"成功拉取 {newCount} 个新客户");
        }
        catch (Exception ex)
        {
            syncLog.Status = "Failed";
            syncLog.ErrorMessage = ex.Message;
            _dbContext.WeChatSyncLogs.Add(syncLog);
            await _dbContext.SaveChangesAsync();
            return ApiResponse<bool>.Fail($"拉取失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<List<WeChatSyncLogDto>>> GetSyncLogsAsync(int days = 7)
    {
        try
        {
            var startDate = DateTime.Now.AddDays(-days);
            var logs = await _dbContext.WeChatSyncLogs
                .Where(l => l.SyncTime >= startDate)
                .OrderByDescending(l => l.SyncTime)
                .Take(100)
                .ToListAsync();

            var dtos = logs.Select(l => new WeChatSyncLogDto
            {
                Id = l.Id,
                SyncType = l.SyncType,
                SyncDirection = l.SyncDirection,
                RecordCount = l.RecordCount,
                Status = l.Status,
                ErrorMessage = l.ErrorMessage,
                SyncTime = l.SyncTime
            }).ToList();

            return ApiResponse<List<WeChatSyncLogDto>>.Ok(dtos, $"获取了 {dtos.Count} 条同步日志");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<WeChatSyncLogDto>>.Fail($"获取日志失败: {ex.Message}");
        }
    }

    #endregion

    #region Webhook触发

    public async Task TriggerWebhookAsync(string triggerType, object data)
    {
        try
        {
            var webhooks = await _dbContext.Webhooks
                .Where(w => w.IsEnabled && (string.IsNullOrEmpty(w.TriggerCondition) || w.TriggerCondition == triggerType))
                .ToListAsync();

            foreach (var webhook in webhooks)
            {
                var message = new WebhookMessage
                {
                    MsgType = "markdown",
                    Markdown = $"### {WebhookTriggerTypes.DisplayNames.GetValueOrDefault(triggerType, triggerType)}\n\n```\n{JsonConvert.SerializeObject(data, Formatting.None)}\n```\n\n> 来自 PRO 系统"
                };
                await SendWebhookMessageAsync(webhook.WebhookUrl, message);
            }
        }
        catch { }
    }

    #endregion
}
