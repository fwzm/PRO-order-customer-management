using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using PRO.Application.DTOs;
using Serilog;

namespace PRO.Desktop.Services;

/// <summary>
/// API 客户端基类 - 封装 HTTP 调用
/// </summary>
public abstract class ApiClientBase
{
    protected readonly HttpClient _httpClient;
    protected readonly JsonSerializerOptions _jsonOptions;

    protected ApiClientBase(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    /// <summary>
    /// 设置认证 Token
    /// </summary>
    public void SetAuthToken(string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// 清除认证 Token
    /// </summary>
    public void ClearAuthToken()
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }

    protected async Task<ApiResponse<T>> GetAsync<T>(string url, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ApiResponse<T>>(_jsonOptions, ct)
                ?? ApiResponse<T>.Fail("响应解析失败");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            Log.Error(ex, "API GET 请求失败: {Url}", url);
            return ApiResponse<T>.Fail($"请求失败: {ex.Message}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "API GET 请求异常: {Url}", url);
            return ApiResponse<T>.Fail($"请求异常: {ex.Message}");
        }
    }

    protected async Task<ApiResponse<T>> PostAsync<T>(string url, object? data = null, CancellationToken ct = default)
    {
        try
        {
            var content = data != null
                ? JsonContent.Create(data, options: _jsonOptions)
                : null;

            var response = await _httpClient.PostAsync(url, content, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ApiResponse<T>>(_jsonOptions, ct)
                ?? ApiResponse<T>.Fail("响应解析失败");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            Log.Error(ex, "API POST 请求失败: {Url}", url);
            return ApiResponse<T>.Fail($"请求失败: {ex.Message}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "API POST 请求异常: {Url}", url);
            return ApiResponse<T>.Fail($"请求异常: {ex.Message}");
        }
    }

    protected async Task<ApiResponse<T>> PutAsync<T>(string url, object? data = null, CancellationToken ct = default)
    {
        try
        {
            var content = data != null
                ? JsonContent.Create(data, options: _jsonOptions)
                : null;

            var response = await _httpClient.PutAsync(url, content, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ApiResponse<T>>(_jsonOptions, ct)
                ?? ApiResponse<T>.Fail("响应解析失败");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            Log.Error(ex, "API PUT 请求失败: {Url}", url);
            return ApiResponse<T>.Fail($"请求失败: {ex.Message}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "API PUT 请求异常: {Url}", url);
            return ApiResponse<T>.Fail($"请求异常: {ex.Message}");
        }
    }

    protected async Task<ApiResponse<bool>> DeleteAsync(string url, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync(url, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ApiResponse<bool>>(_jsonOptions, ct)
                ?? ApiResponse<bool>.Fail("响应解析失败");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            Log.Error(ex, "API DELETE 请求失败: {Url}", url);
            return ApiResponse<bool>.Fail($"请求失败: {ex.Message}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "API DELETE 请求异常: {Url}", url);
            return ApiResponse<bool>.Fail($"请求异常: {ex.Message}");
        }
    }
}
