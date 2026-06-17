using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;

namespace PRO.Desktop.Services;

/// <summary>
/// Desktop API 兼容层基类 — 通过 HTTP API 调用后端，替代直接 DB 访问
/// 封装：认证、序列化、错误处理、重试
/// </summary>
public abstract class ApiServiceBase
{
    protected readonly HttpClient _httpClient;
    protected readonly JsonSerializerOptions _jsonOptions;

    protected ApiServiceBase(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    public void SetAuthToken(string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
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
        catch (Exception ex)
        {
            return ApiResponse<T>.Fail($"API调用失败: {ex.Message}");
        }
    }

    protected async Task<ApiResponse<T>> PostAsync<T>(string url, object? data = null, CancellationToken ct = default)
    {
        try
        {
            var content = data != null
                ? new StringContent(JsonSerializer.Serialize(data, _jsonOptions), Encoding.UTF8, "application/json")
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
        catch (Exception ex)
        {
            return ApiResponse<T>.Fail($"API调用失败: {ex.Message}");
        }
    }

    protected async Task<ApiResponse<T>> PutAsync<T>(string url, object? data = null, CancellationToken ct = default)
    {
        try
        {
            var content = data != null
                ? new StringContent(JsonSerializer.Serialize(data, _jsonOptions), Encoding.UTF8, "application/json")
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
        catch (Exception ex)
        {
            return ApiResponse<T>.Fail($"API调用失败: {ex.Message}");
        }
    }

    protected async Task<ApiResponse<T>> DeleteAsync<T>(string url, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync(url, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ApiResponse<T>>(_jsonOptions, ct)
                ?? ApiResponse<T>.Fail("响应解析失败");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ApiResponse<T>.Fail($"API调用失败: {ex.Message}");
        }
    }
}
