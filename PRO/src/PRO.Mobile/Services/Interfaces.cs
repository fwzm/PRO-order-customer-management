using PRO.Application.DTOs;

namespace PRO.Mobile.Services;

/// <summary>
/// API 客户端 — 统一 HTTP 请求封装，自动注入 Auth Header，处理 401/网络错误
/// </summary>
public interface IApiClient
{
    Task<ApiResponse<T>> GetAsync<T>(string endpoint, Dictionary<string, string>? queryParams = null);
    Task<ApiResponse<T>> PostAsync<T>(string endpoint, object? body = null);
    Task<ApiResponse<T>> PutAsync<T>(string endpoint, object? body = null);
    Task<ApiResponse<T>> DeleteAsync<T>(string endpoint);
    Task<ApiResponse<T>> SendAsync<T>(HttpMethod method, string endpoint, object? body = null, Dictionary<string, string>? queryParams = null);

    /// <summary>设置 Bearer Token 并持久化</summary>
    Task SetTokenAsync(string token);

    /// <summary>清除 Token</summary>
    Task ClearTokenAsync();

    /// <summary>获取当前 Token</summary>
    Task<string?> GetTokenAsync();
}

/// <summary>
/// 移动端认证服务 — 登录、登出、Token 刷新
/// </summary>
public interface IAuthService
{
    Task<ApiResponse<LoginResponse>> LoginAsync(string employeeNo, string password);
    Task LogoutAsync();
    Task<ApiResponse<LoginResponse>> RefreshTokenAsync();
    Task<bool> IsLoggedInAsync();
}

/// <summary>
/// 移动端客户服务
/// </summary>
public interface ICustomerMobileService
{
    Task<ApiResponse<PagedResult<CustomerListItem>>> GetListAsync(int pageIndex = 1, int pageSize = 20, string? keyword = null, int? customerType = null);
    Task<ApiResponse<CustomerDetailDto>> GetDetailAsync(int id);
}

/// <summary>
/// 移动端订单服务
/// </summary>
public interface IOrderMobileService
{
    Task<ApiResponse<PagedResult<OrderListItem>>> GetListAsync(int pageIndex = 1, int pageSize = 20, string? keyword = null, int? status = null, int? paymentStatus = null);
    Task<ApiResponse<OrderDetailDto>> GetDetailAsync(int id);
    Task<ApiResponse<int>> CreateAsync(CreateOrderRequest request);
}

/// <summary>
/// 网络连接检测
/// </summary>
public interface IConnectivityService
{
    bool IsConnected { get; }
    event EventHandler<bool>? ConnectivityChanged;
}

/// <summary>
/// 安全 Token 存储 — iOS Keychain / Android KeyStore
/// </summary>
public interface ISecureTokenStore
{
    Task SaveAsync(string key, string value);
    Task<string?> GetAsync(string key);
    Task RemoveAsync(string key);
}

/// <summary>
/// 移动端 Toast 提示
/// </summary>
public interface IMobileToastService
{
    Task ShowAsync(string message, ToastSeverity severity = ToastSeverity.Info);
    Task ShowErrorAsync(string message);
    Task ShowSuccessAsync(string message);
}

public enum ToastSeverity { Info, Success, Warning, Error }
