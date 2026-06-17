using PRO.Application.DTOs;

namespace PRO.Mobile.Services;

/// <summary>
/// 移动端工作台服务实现
/// </summary>
public class DashboardMobileService : IDashboardMobileService
{
    private readonly IApiClient _apiClient;
    public DashboardMobileService(IApiClient apiClient) => _apiClient = apiClient;

    public async Task<ApiResponse<BusinessDashboardDto>> GetWorkbenchDataAsync(bool forceRefresh = false)
    {
        var queryParams = new Dictionary<string, string>();
        if (forceRefresh) queryParams["forceRefresh"] = "true";
        return await _apiClient.GetAsync<BusinessDashboardDto>("api/dashboard/workbench", queryParams);
    }
}

/// <summary>
/// 移动端健康检查服务实现
/// </summary>
public class HealthCheckService : IHealthCheckService
{
    private readonly IApiClient _apiClient;
    public HealthCheckService(IApiClient apiClient) => _apiClient = apiClient;

    public async Task<ApiResponse<HealthCheckResult>> CheckAsync()
    {
        try
        {
            return await _apiClient.GetAsync<HealthCheckResult>("health");
        }
        catch
        {
            return ApiResponse<HealthCheckResult>.Fail("健康检查请求失败");
        }
    }
}

/// <summary>
/// 移动端客户服务实现
/// </summary>
public class CustomerMobileService : ICustomerMobileService
{
    private readonly IApiClient _apiClient;
    public CustomerMobileService(IApiClient apiClient) => _apiClient = apiClient;

    public async Task<ApiResponse<PagedResult<CustomerListItem>>> GetListAsync(
        int pageIndex = 1, int pageSize = 20, string? keyword = null, int? customerType = null)
    {
        var queryParams = new Dictionary<string, string>
        {
            ["pageIndex"] = pageIndex.ToString(),
            ["pageSize"] = pageSize.ToString(),
        };
        if (!string.IsNullOrWhiteSpace(keyword)) queryParams["keyword"] = keyword;
        if (customerType.HasValue) queryParams["customerType"] = customerType.Value.ToString();
        return await _apiClient.GetAsync<PagedResult<CustomerListItem>>("api/customers", queryParams);
    }

    public async Task<ApiResponse<CustomerDetailDto>> GetDetailAsync(int id)
        => await _apiClient.GetAsync<CustomerDetailDto>($"api/customers/{id}");

    public async Task<ApiResponse<int>> CreateAsync(CreateCustomerRequest request)
        => await _apiClient.PostAsync<int>("api/customers", request);

    public async Task<ApiResponse<bool>> UpdateAsync(int id, UpdateCustomerRequest request)
        => await _apiClient.PutAsync<bool>($"api/customers/{id}", request);

    public async Task<ApiResponse<bool>> DeleteAsync(int id)
        => await _apiClient.DeleteAsync<bool>($"api/customers/{id}");
}

/// <summary>
/// 移动端订单服务实现
/// </summary>
public class OrderMobileService : IOrderMobileService
{
    private readonly IApiClient _apiClient;
    public OrderMobileService(IApiClient apiClient) => _apiClient = apiClient;

    public async Task<ApiResponse<PagedResult<OrderListItem>>> GetListAsync(
        int pageIndex = 1, int pageSize = 20, string? keyword = null, int? status = null, int? paymentStatus = null)
    {
        var queryParams = new Dictionary<string, string>
        {
            ["pageIndex"] = pageIndex.ToString(),
            ["pageSize"] = pageSize.ToString(),
        };
        if (!string.IsNullOrWhiteSpace(keyword)) queryParams["keyword"] = keyword;
        if (status.HasValue) queryParams["status"] = status.Value.ToString();
        if (paymentStatus.HasValue) queryParams["paymentStatus"] = paymentStatus.Value.ToString();
        return await _apiClient.GetAsync<PagedResult<OrderListItem>>("api/orders", queryParams);
    }

    public async Task<ApiResponse<OrderDetailDto>> GetDetailAsync(int id)
        => await _apiClient.GetAsync<OrderDetailDto>($"api/orders/{id}");

    public async Task<ApiResponse<int>> CreateAsync(CreateOrderRequest request)
        => await _apiClient.PostAsync<int>("api/orders", request);

    public async Task<ApiResponse<bool>> UpdateAsync(int id, UpdateOrderRequest request)
        => await _apiClient.PutAsync<bool>($"api/orders/{id}", request);

    public async Task<ApiResponse<bool>> DeleteAsync(int id)
        => await _apiClient.DeleteAsync<bool>($"api/orders/{id}");

    public async Task<ApiResponse<bool>> UpdateStatusAsync(int id, UpdateOrderStatusRequest request)
        => await _apiClient.PutAsync<bool>($"api/orders/{id}/status", request);
}

/// <summary>
/// 移动端产品服务实现
/// </summary>
public class ProductMobileService : IProductMobileService
{
    private readonly IApiClient _apiClient;
    public ProductMobileService(IApiClient apiClient) => _apiClient = apiClient;

    public async Task<ApiResponse<PagedResult<ProductListItem>>> GetListAsync(
        int pageIndex = 1, int pageSize = 20, string? keyword = null)
    {
        var queryParams = new Dictionary<string, string>
        {
            ["pageIndex"] = pageIndex.ToString(),
            ["pageSize"] = pageSize.ToString(),
        };
        if (!string.IsNullOrWhiteSpace(keyword)) queryParams["keyword"] = keyword;
        return await _apiClient.GetAsync<PagedResult<ProductListItem>>("api/products", queryParams);
    }

    public async Task<ApiResponse<ProductListItem>> GetDetailAsync(int id)
        => await _apiClient.GetAsync<ProductListItem>($"api/products/{id}");
}

/// <summary>
/// 移动端分公司服务实现
/// </summary>
public class BranchMobileService : IBranchMobileService
{
    private readonly IApiClient _apiClient;
    public BranchMobileService(IApiClient apiClient) => _apiClient = apiClient;

    public async Task<ApiResponse<PagedResult<BranchListItem>>> GetListAsync(int pageIndex = 1, int pageSize = 50)
    {
        var queryParams = new Dictionary<string, string>
        {
            ["pageIndex"] = pageIndex.ToString(),
            ["pageSize"] = pageSize.ToString(),
        };
        return await _apiClient.GetAsync<PagedResult<BranchListItem>>("api/branches", queryParams);
    }
}
