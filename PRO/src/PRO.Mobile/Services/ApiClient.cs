using System.Net.Http.Json;
using System.Text.Json;
using PRO.Application.DTOs;
using PRO.Mobile.Stores;

namespace PRO.Mobile.Services;

/// <summary>
/// API 客户端实现 — 自动注入 Auth Header，401 自动退出
/// </summary>
public class ApiClient : IApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TokenStore _tokenStore;
    private readonly IMobileToastService _toast;
    private readonly ISecureTokenStore _secureStore;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public ApiClient(IHttpClientFactory httpClientFactory, TokenStore tokenStore,
        IMobileToastService toast, ISecureTokenStore secureStore)
    {
        _httpClientFactory = httpClientFactory;
        _tokenStore = tokenStore;
        _toast = toast;
        _secureStore = secureStore;
    }

    public Task SetTokenAsync(string token)
    {
        _tokenStore.SetToken(token);
        return _secureStore.SaveAsync("access_token", token);
    }

    public Task ClearTokenAsync()
    {
        _tokenStore.ClearToken();
        return _secureStore.RemoveAsync("access_token");
    }

    public async Task<string?> GetTokenAsync()
    {
        if (!string.IsNullOrEmpty(_tokenStore.CurrentToken))
            return _tokenStore.CurrentToken;

        // 从安全存储恢复
        var token = await _secureStore.GetAsync("access_token");
        if (!string.IsNullOrEmpty(token))
            _tokenStore.SetToken(token);
        return token;
    }

    public async Task<ApiResponse<T>> SendAsync<T>(HttpMethod method, string endpoint, object? body = null,
        Dictionary<string, string>? queryParams = null)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("PRO.WebApi");
            var token = await GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // 构建 query string
            var url = endpoint;
            if (queryParams?.Count > 0)
            {
                var qs = string.Join("&", queryParams.Select(kv =>
                    $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
                url = $"{endpoint}?{qs}";
            }

            var request = new HttpRequestMessage(method, url);
            if (body != null)
                request.Content = JsonContent.Create(body, options: JsonOptions);

            var response = await client.SendAsync(request);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await ClearTokenAsync();
                _tokenStore.NotifyUnauthorized();
                return ApiResponse<T>.Fail("登录已过期，请重新登录");
            }

            var json = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<T>>(json, JsonOptions);
            return apiResponse ?? ApiResponse<T>.Fail("解析响应失败");
        }
        catch (HttpRequestException)
        {
            await _toast.ShowErrorAsync("网络连接失败，请检查网络");
            return ApiResponse<T>.Fail("网络连接失败");
        }
        catch (TaskCanceledException)
        {
            await _toast.ShowErrorAsync("请求超时，请重试");
            return ApiResponse<T>.Fail("请求超时");
        }
        catch (Exception ex)
        {
            await _toast.ShowErrorAsync($"请求失败: {ex.Message}");
            return ApiResponse<T>.Fail(ex.Message);
        }
    }

    public Task<ApiResponse<T>> GetAsync<T>(string endpoint, Dictionary<string, string>? queryParams = null)
        => SendAsync<T>(HttpMethod.Get, endpoint, null, queryParams);

    public Task<ApiResponse<T>> PostAsync<T>(string endpoint, object? body = null)
        => SendAsync<T>(HttpMethod.Post, endpoint, body);

    public Task<ApiResponse<T>> PutAsync<T>(string endpoint, object? body = null)
        => SendAsync<T>(HttpMethod.Put, endpoint, body);

    public Task<ApiResponse<T>> DeleteAsync<T>(string endpoint)
        => SendAsync<T>(HttpMethod.Delete, endpoint);
}
