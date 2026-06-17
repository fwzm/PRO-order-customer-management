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

    Task SetTokenAsync(string token);
    Task ClearTokenAsync();
    Task<string?> GetTokenAsync();
}

/// <summary>
/// 移动端认证服务
/// </summary>
public interface IAuthService
{
    Task<ApiResponse<LoginResponse>> LoginAsync(string employeeNo, string password);
    Task LogoutAsync();
    Task<ApiResponse<LoginResponse>> RefreshTokenAsync();
    Task<ApiResponse<bool>> ChangePasswordAsync(string oldPassword, string newPassword);
    Task<bool> IsLoggedInAsync();
}

/// <summary>
/// 移动端工作台服务
/// </summary>
public interface IDashboardMobileService
{
    Task<ApiResponse<BusinessDashboardDto>> GetWorkbenchDataAsync(bool forceRefresh = false);
}

/// <summary>
/// 移动端健康检查服务
/// </summary>
public interface IHealthCheckService
{
    Task<ApiResponse<HealthCheckResult>> CheckAsync();
}

public class HealthCheckResult
{
    public string Status { get; set; } = "unknown";
    public string Database { get; set; } = "unknown";
    public string Timestamp { get; set; } = "";
}

/// <summary>
/// 移动端客户服务
/// </summary>
public interface ICustomerMobileService
{
    Task<ApiResponse<PagedResult<CustomerListItem>>> GetListAsync(int pageIndex = 1, int pageSize = 20, string? keyword = null, int? customerType = null);
    Task<ApiResponse<CustomerDetailDto>> GetDetailAsync(int id);
    Task<ApiResponse<int>> CreateAsync(CreateCustomerRequest request);
    Task<ApiResponse<bool>> UpdateAsync(int id, UpdateCustomerRequest request);
    Task<ApiResponse<bool>> DeleteAsync(int id);
}

/// <summary>
/// 移动端订单服务
/// </summary>
public interface IOrderMobileService
{
    Task<ApiResponse<PagedResult<OrderListItem>>> GetListAsync(int pageIndex = 1, int pageSize = 20, string? keyword = null, int? status = null, int? paymentStatus = null);
    Task<ApiResponse<OrderDetailDto>> GetDetailAsync(int id);
    Task<ApiResponse<int>> CreateAsync(CreateOrderRequest request);
    Task<ApiResponse<bool>> UpdateAsync(int id, UpdateOrderRequest request);
    Task<ApiResponse<bool>> DeleteAsync(int id);
    Task<ApiResponse<bool>> UpdateStatusAsync(int id, UpdateOrderStatusRequest request);
}

/// <summary>
/// 移动端产品服务
/// </summary>
public interface IProductMobileService
{
    Task<ApiResponse<PagedResult<ProductListItem>>> GetListAsync(int pageIndex = 1, int pageSize = 20, string? keyword = null);
    Task<ApiResponse<ProductListItem>> GetDetailAsync(int id);
}

/// <summary>
/// 移动端分公司服务
/// </summary>
public interface IBranchMobileService
{
    Task<ApiResponse<PagedResult<BranchListItem>>> GetListAsync(int pageIndex = 1, int pageSize = 50);
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
