using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using PRO.Domain.Entities;
using PRO.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace PRO.Infrastructure.WeChat;

/// <summary>
/// 企业微信智能表格同步服务
/// 官方文档: https://developer.work.weixin.qq.com/document/path/99910
/// </summary>
public class SmartTableSyncService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ProDbContext _db;
    private const string ApiBase = "https://qyapi.weixin.qq.com/cgi-bin/wedoc/smartsheet";

    public SmartTableSyncService(IHttpClientFactory httpClientFactory, ProDbContext db)
    {
        _httpClientFactory = httpClientFactory;
        _db = db;
    }

    /// <summary>
    /// 获取AccessToken
    /// </summary>
    private async Task<string?> GetTokenAsync()
    {
        var config = await _db.WeChatConfigs.AsNoTracking().FirstOrDefaultAsync();
        if (config == null || string.IsNullOrEmpty(config.CorpId) || string.IsNullOrEmpty(config.AppSecret))
            return null;

        // 获取Token
        var httpClient = _httpClientFactory.CreateClient();
        var secret = config.AppSecret;
        var url = $"https://qyapi.weixin.qq.com/cgi-bin/gettoken?corpid={config.CorpId}&corpsecret={secret}";
        var resp = await httpClient.GetStringAsync(url);
        var tokenResp = JsonConvert.DeserializeObject<GetTokenResponse>(resp);
        return tokenResp?.AccessToken;
    }

    /// <summary>
    /// 获取智能表格的 docid 和 sheet_id 配置
    /// </summary>
    private async Task<(string docId, string sheetId)?> GetConfigAsync()
    {
        var settings = await _db.LocalSettings.AsNoTracking().ToListAsync();
        var docId = settings.FirstOrDefault(s => s.SettingKey == "WeChatSmartTableDocId")?.SettingValue;
        var sheetId = settings.FirstOrDefault(s => s.SettingKey == "WeChatSmartTableSheetId")?.SettingValue;
        if (string.IsNullOrEmpty(docId) || string.IsNullOrEmpty(sheetId)) return null;
        return (docId, sheetId);
    }

    /// <summary>
    /// 从智能表格读取记录
    /// </summary>
    public async Task<List<SmartTableRecord>> GetRecordsAsync()
    {
        var token = await GetTokenAsync();
        var config = await GetConfigAsync();
        if (token == null || config == null) return new();

        var httpClient = _httpClientFactory.CreateClient();
        var requestBody = new
        {
            docid = config.Value.docId,
            sheet_id = config.Value.sheetId
        };

        var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync($"{ApiBase}/get_records?access_token={token}", content);
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<SmartTableResponse>(json);

        return result?.Records ?? new();
    }

    /// <summary>
    /// 删除智能表格中的记录（客户关联后删除）
    /// </summary>
    public async Task<bool> DeleteRecordsAsync(List<string> recordIds)
    {
        if (!recordIds.Any()) return true;

        var token = await GetTokenAsync();
        var config = await GetConfigAsync();
        if (token == null || config == null) return false;

        var httpClient = _httpClientFactory.CreateClient();
        var requestBody = new
        {
            docid = config.Value.docId,
            sheet_id = config.Value.sheetId,
            record_ids = recordIds
        };

        var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync($"{ApiBase}/delete_records?access_token={token}", content);
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<WeChatApiResponse>(json);
        return result?.IsSuccess == true;
    }

    /// <summary>
    /// 同步拜访记录：从智能表格读取并保存为WeChatVisitRecord
    /// </summary>
    public async Task<int> SyncVisitRecordsAsync()
    {
        var records = await GetRecordsAsync();
        var count = 0;

        foreach (var record in records)
        {
            // 检查是否已同步过
            var exists = await _db.WeChatVisitRecords.AnyAsync(v => v.SourceRecordId == record.RecordId);
            if (exists) continue;

            _db.WeChatVisitRecords.Add(new WeChatVisitRecord
            {
                SourceRecordId = record.RecordId,
                CustomerName = GetFieldValue(record, "客户名称"),
                VisitorName = GetFieldValue(record, "拜访人"),
                VisitDate = ParseDateTime(GetFieldValue(record, "拜访日期")) ?? DateTime.Now,
                VisitType = GetFieldValue(record, "拜访方式") ?? "电话",
                Content = GetFieldValue(record, "拜访内容"),
                Purpose = GetFieldValue(record, "拜访目的"),
                CustomerDemand = GetFieldValue(record, "客户需求"),
                NextAction = GetFieldValue(record, "后续计划"),
                RawData = JsonConvert.SerializeObject(record.Values),
                Status = "Unlinked",
                SyncedAt = DateTime.Now
            });
            count++;
        }

        if (count > 0) await _db.SaveChangesAsync();
        return count;
    }

    private static string? GetFieldValue(SmartTableRecord record, string fieldName)
    {
        if (record.Values.TryGetValue(fieldName, out var value))
        {
            if (value is List<SmartTableValue> values && values.Any())
            {
                return values[0].Text;
            }
        }
        return null;
    }

    private static DateTime? ParseDateTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateTime.TryParse(value, out var dt) ? dt : null;
    }
}

/// <summary>
/// 智能表格记录
/// </summary>
public class SmartTableRecord
{
    [JsonProperty("record_id")] public string RecordId { get; set; } = string.Empty;
    [JsonProperty("values")] public Dictionary<string, object> Values { get; set; } = new();
    [JsonProperty("creator_name")] public string? CreatorName { get; set; }
    [JsonProperty("create_time")] public string? CreateTime { get; set; }
}

public class SmartTableValue
{
    [JsonProperty("text")] public string? Text { get; set; }
    [JsonProperty("number")] public double? Number { get; set; }
}

public class SmartTableResponse : WeChatApiResponse
{
    [JsonProperty("records")] public List<SmartTableRecord>? Records { get; set; }
}
