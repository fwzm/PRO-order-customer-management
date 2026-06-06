using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PRO.Application.Interfaces;
using PRO.Application.DTOs;

namespace PRO.Infrastructure.WeChat;

public partial class WeChatService
{
    private HttpClient GetClient() => _httpClientFactory.CreateClient();

    public async Task<ApiResponse<List<WeChatTagDto>>> GetTagsAsync()
    {
        var token = await GetAccessTokenAsync();
        if (token == null) return ApiResponse<List<WeChatTagDto>>.Fail("获取Token失败");
        try
        {
            var client = GetClient();
            var response = await client.GetStringAsync($"{ApiBaseUrl}/externalcontact/get_corp_tag_list?access_token={token}");
            var json = JObject.Parse(response);
            if (json["errcode"]?.Value<int>() != 0) return ApiResponse<List<WeChatTagDto>>.Fail(json["errmsg"]?.Value<string>() ?? "未知错误");
            var tags = new List<WeChatTagDto>();
            var groups = json["tag_group"] as JArray;
            if (groups != null)
                foreach (var g in groups)
                    foreach (var t in g["tag"] as JArray ?? new JArray())
                        tags.Add(new WeChatTagDto { Id = t["id"]?.Value<string>() ?? "", Name = t["name"]?.Value<string>() ?? "", GroupName = g["group_name"]?.Value<string>() ?? "" });
            return ApiResponse<List<WeChatTagDto>>.Ok(tags);
        }
        catch (Exception ex) { return ApiResponse<List<WeChatTagDto>>.Fail($"获取标签失败: {ex.Message}"); }
    }

    public async Task<ApiResponse<bool>> CreateTagAsync(string groupName, string tagName)
    {
        var token = await GetAccessTokenAsync();
        if (token == null) return ApiResponse<bool>.Fail("获取Token失败");
        try
        {
            var client = GetClient();
            var body = new { group_name = groupName, tag = new[] { new { name = tagName } } };
            var content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{ApiBaseUrl}/externalcontact/add_corp_tag?access_token={token}", content);
            var json = JObject.Parse(await response.Content.ReadAsStringAsync());
            return json["errcode"]?.Value<int>() == 0 ? ApiResponse<bool>.Ok(true) : ApiResponse<bool>.Fail(json["errmsg"]?.Value<string>() ?? "");
        }
        catch (Exception ex) { return ApiResponse<bool>.Fail($"创建标签失败: {ex.Message}"); }
    }

    public async Task<ApiResponse<bool>> TagCustomerAsync(string externalUserId, List<string> tagIds)
    {
        var token = await GetAccessTokenAsync();
        if (token == null) return ApiResponse<bool>.Fail("获取Token失败");
        try
        {
            var client = GetClient();
            var body = new { userid = "", external_userid = new[] { externalUserId }, add_tag = tagIds.ToArray() };
            var content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{ApiBaseUrl}/externalcontact/mark_tag?access_token={token}", content);
            var json = JObject.Parse(await response.Content.ReadAsStringAsync());
            return json["errcode"]?.Value<int>() == 0 ? ApiResponse<bool>.Ok(true) : ApiResponse<bool>.Fail(json["errmsg"]?.Value<string>() ?? "");
        }
        catch (Exception ex) { return ApiResponse<bool>.Fail($"打标签失败: {ex.Message}"); }
    }

    public async Task<ApiResponse<bool>> UntagCustomerAsync(string externalUserId, List<string> tagIds)
    {
        var token = await GetAccessTokenAsync();
        if (token == null) return ApiResponse<bool>.Fail("获取Token失败");
        try
        {
            var client = GetClient();
            var body = new { userid = "", external_userid = new[] { externalUserId }, remove_tag = tagIds.ToArray() };
            var content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{ApiBaseUrl}/externalcontact/mark_tag?access_token={token}", content);
            var json = JObject.Parse(await response.Content.ReadAsStringAsync());
            return json["errcode"]?.Value<int>() == 0 ? ApiResponse<bool>.Ok(true) : ApiResponse<bool>.Fail(json["errmsg"]?.Value<string>() ?? "");
        }
        catch (Exception ex) { return ApiResponse<bool>.Fail($"取消标签失败: {ex.Message}"); }
    }

    public async Task<ApiResponse<List<WeChatGroupChatDto>>> GetGroupChatsAsync(int offset = 0, int limit = 100)
    {
        var token = await GetAccessTokenAsync();
        if (token == null) return ApiResponse<List<WeChatGroupChatDto>>.Fail("获取Token失败");
        try
        {
            var client = GetClient();
            var body = new { offset, limit, status_filter = 0 };
            var response = await client.PostAsync($"{ApiBaseUrl}/externalcontact/groupchat/list?access_token={token}",
                new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json"));
            var json = JObject.Parse(await response.Content.ReadAsStringAsync());
            if (json["errcode"]?.Value<int>() != 0) return ApiResponse<List<WeChatGroupChatDto>>.Fail(json["errmsg"]?.Value<string>() ?? "");
            var chats = new List<WeChatGroupChatDto>();
            var list = json["group_chat_list"] as JArray;
            if (list != null)
                foreach (var c in list)
                    chats.Add(new WeChatGroupChatDto { ChatId = c["chat_id"]?.Value<string>() ?? "", Name = c["name"]?.Value<string>() ?? "", MemberCount = c["member_count"]?.Value<int>() ?? 0, CreateTime = DateTimeOffset.FromUnixTimeSeconds(c["create_time"]?.Value<long>() ?? 0).LocalDateTime });
            return ApiResponse<List<WeChatGroupChatDto>>.Ok(chats);
        }
        catch (Exception ex) { return ApiResponse<List<WeChatGroupChatDto>>.Fail($"获取群聊失败: {ex.Message}"); }
    }

    public async Task<ApiResponse<WeChatGroupChatDetail>> GetGroupChatDetailAsync(string chatId)
    {
        var token = await GetAccessTokenAsync();
        if (token == null) return ApiResponse<WeChatGroupChatDetail>.Fail("获取Token失败");
        try
        {
            var client = GetClient();
            var body = new { chat_id = chatId, need_name = 1 };
            var response = await client.PostAsync($"{ApiBaseUrl}/externalcontact/groupchat/get?access_token={token}",
                new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json"));
            var json = JObject.Parse(await response.Content.ReadAsStringAsync());
            if (json["errcode"]?.Value<int>() != 0) return ApiResponse<WeChatGroupChatDetail>.Fail(json["errmsg"]?.Value<string>() ?? "");
            var groupChat = json["group_chat"];
            var detail = new WeChatGroupChatDetail { ChatId = groupChat?["chat_id"]?.Value<string>() ?? "", Name = groupChat?["name"]?.Value<string>() ?? "", MemberCount = groupChat?["member_count"]?.Value<int>() ?? 0 };
            var members = groupChat?["member_list"] as JArray;
            if (members != null)
                foreach (var m in members)
                    detail.Members.Add(new GroupChatMember { UserId = m["userid"]?.Value<string>() ?? "", Name = m["name"]?.Value<string>(), Type = m["type"]?.Value<int>() == 1 ? "user" : "external", JoinTime = m["join_time"]?.Value<long>() ?? 0 });
            return ApiResponse<WeChatGroupChatDetail>.Ok(detail);
        }
        catch (Exception ex) { return ApiResponse<WeChatGroupChatDetail>.Fail($"获取群详情失败: {ex.Message}"); }
    }

    public async Task<ApiResponse<string>> CreateContactWayAsync(int type, string userId, string? remark = null)
    {
        var token = await GetAccessTokenAsync();
        if (token == null) return ApiResponse<string>.Fail("获取Token失败");
        try
        {
            var client = GetClient();
            var body = new { type, scene = 1, user = new[] { userId }, remark = remark ?? "" };
            var response = await client.PostAsync($"{ApiBaseUrl}/externalcontact/add_contact_way?access_token={token}",
                new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json"));
            var json = JObject.Parse(await response.Content.ReadAsStringAsync());
            if (json["errcode"]?.Value<int>() != 0) return ApiResponse<string>.Fail(json["errmsg"]?.Value<string>() ?? "");
            return ApiResponse<string>.Ok(json["qr_code"]?.Value<string>() ?? "");
        }
        catch (Exception ex) { return ApiResponse<string>.Fail($"创建联系我失败: {ex.Message}"); }
    }

    public async Task<ApiResponse<bool>> UpdateContactWayAsync(string configId, string? remark = null) { await Task.CompletedTask; return ApiResponse<bool>.Ok(true); }
    public async Task<ApiResponse<bool>> DeleteContactWayAsync(string configId) { await Task.CompletedTask; return ApiResponse<bool>.Ok(true); }

    public async Task<ApiResponse<string>> CreateMassMessageAsync(string content, string? tagIds = null, bool sendToAll = false)
    {
        var token = await GetAccessTokenAsync();
        if (token == null) return ApiResponse<string>.Fail("获取Token失败");
        try
        {
            var client = GetClient();
            var body = new { chat_type = "single", external_userid = sendToAll ? new[] { "@all" } : null, text = new { content } };
            var jsonBody = JsonConvert.SerializeObject(body, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            var response = await client.PostAsync($"{ApiBaseUrl}/externalcontact/add_msg_template?access_token={token}",
                new StringContent(jsonBody, Encoding.UTF8, "application/json"));
            var result = JObject.Parse(await response.Content.ReadAsStringAsync());
            return result["errcode"]?.Value<int>() == 0 ? ApiResponse<string>.Ok(result["msgid"]?.Value<string>() ?? "") : ApiResponse<string>.Fail(result["errmsg"]?.Value<string>() ?? "");
        }
        catch (Exception ex) { return ApiResponse<string>.Fail($"创建群发失败: {ex.Message}"); }
    }

    public async Task<ApiResponse<MassMessageResult>> GetMassMessageResultAsync(string msgId) { await Task.CompletedTask; return ApiResponse<MassMessageResult>.Ok(new MassMessageResult { MsgId = msgId }); }

    public async Task<ApiResponse<List<string>>> GetUnassignedCustomersAsync(int offset = 0, int limit = 100)
    {
        var token = await GetAccessTokenAsync();
        if (token == null) return ApiResponse<List<string>>.Fail("获取Token失败");
        try
        {
            var client = GetClient();
            var body = new { page_id = offset / limit + 1, page_size = limit };
            var response = await client.PostAsync($"{ApiBaseUrl}/externalcontact/get_unassigned_list?access_token={token}",
                new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json"));
            var json = JObject.Parse(await response.Content.ReadAsStringAsync());
            if (json["errcode"]?.Value<int>() != 0) return ApiResponse<List<string>>.Fail(json["errmsg"]?.Value<string>() ?? "");
            var ids = (json["info"] as JArray)?.Select(i => i["external_userid"]?.Value<string>() ?? "").Where(id => !string.IsNullOrEmpty(id)).ToList() ?? new();
            return ApiResponse<List<string>>.Ok(ids);
        }
        catch (Exception ex) { return ApiResponse<List<string>>.Fail($"获取离职客户失败: {ex.Message}"); }
    }

    public async Task<ApiResponse<bool>> TransferCustomerAsync(string externalUserId, string handoverUserId, string takeoverUserId)
    {
        var token = await GetAccessTokenAsync();
        if (token == null) return ApiResponse<bool>.Fail("获取Token失败");
        try
        {
            var client = GetClient();
            var body = new { handover_userid = handoverUserId, external_userid = new[] { externalUserId }, takeover_userid = takeoverUserId, transfer_success_msg = 0 };
            var response = await client.PostAsync($"{ApiBaseUrl}/externalcontact/transfer?access_token={token}",
                new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json"));
            var json = JObject.Parse(await response.Content.ReadAsStringAsync());
            return json["errcode"]?.Value<int>() == 0 ? ApiResponse<bool>.Ok(true) : ApiResponse<bool>.Fail(json["errmsg"]?.Value<string>() ?? "");
        }
        catch (Exception ex) { return ApiResponse<bool>.Fail($"分配失败: {ex.Message}"); }
    }
}
