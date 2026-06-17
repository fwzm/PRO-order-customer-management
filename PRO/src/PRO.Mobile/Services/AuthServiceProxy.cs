using PRO.Application.DTOs;
using PRO.Mobile.Stores;
using System.Text.Json;

namespace PRO.Mobile.Services;

/// <summary>
/// 移动端认证服务 — 通过 PRO.WebApi 登录/登出/修改密码/刷新 Token
/// </summary>
public class AuthServiceProxy : IAuthService
{
    private readonly IApiClient _apiClient;
    private readonly TokenStore _tokenStore;
    private readonly UserStore _userStore;

    public AuthServiceProxy(IApiClient apiClient, TokenStore tokenStore, UserStore userStore)
    {
        _apiClient = apiClient;
        _tokenStore = tokenStore;
        _userStore = userStore;
    }

    public async Task<ApiResponse<LoginResponse>> LoginAsync(string employeeNo, string password)
    {
        var result = await _apiClient.PostAsync<LoginResponse>("api/auth/login", new
        {
            employeeNo,
            password
        });

        if (result.Success && result.Data != null)
        {
            await _apiClient.SetTokenAsync(result.Data.Token);
            _userStore.SetUser(result.Data);
        }

        return result;
    }

    public async Task LogoutAsync()
    {
        try
        {
            // 通知服务端登出（忽略失败，确保本地清理一定执行）
            await _apiClient.PostAsync<object>("api/auth/logout");
        }
        catch
        {
            // 服务端登出失败不影响本地清理
        }
        finally
        {
            await _apiClient.ClearTokenAsync();
            _tokenStore.ClearToken();
            _userStore.ClearUser();
        }
    }

    public async Task<ApiResponse<LoginResponse>> RefreshTokenAsync()
    {
        var token = await _apiClient.GetTokenAsync();
        if (string.IsNullOrEmpty(token))
            return ApiResponse<LoginResponse>.Fail("无 Token 可刷新");

        // 以对象格式发送 refresh token（匹配后端期望）
        var result = await _apiClient.PostAsync<LoginResponse>("api/auth/refresh-token", new { token });

        if (result.Success && result.Data != null)
        {
            await _apiClient.SetTokenAsync(result.Data.Token);
            _userStore.SetUser(result.Data);
        }

        return result;
    }

    public async Task<ApiResponse<bool>> ChangePasswordAsync(string oldPassword, string newPassword)
    {
        return await _apiClient.PostAsync<bool>("api/auth/change-password", new
        {
            oldPassword,
            newPassword
        });
    }

    public Task<bool> IsLoggedInAsync()
    {
        var loggedIn = !string.IsNullOrEmpty(_tokenStore.CurrentToken) && _userStore.CurrentUser != null;
        return Task.FromResult(loggedIn);
    }
}
