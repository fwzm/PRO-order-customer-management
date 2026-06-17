using PRO.Application.DTOs;

namespace PRO.Mobile.Services;

/// <summary>
/// 移动端客户服务实现
/// </summary>
public class CustomerMobileService : ICustomerMobileService
{
    private readonly IApiClient _apiClient;

    public CustomerMobileService(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<ApiResponse<PagedResult<CustomerListItem>>> GetListAsync(
        int pageIndex = 1, int pageSize = 20, string? keyword = null, int? customerType = null)
    {
        var queryParams = new Dictionary<string, string>
        {
            ["pageIndex"] = pageIndex.ToString(),
            ["pageSize"] = pageSize.ToString(),
        };

        if (!string.IsNullOrWhiteSpace(keyword))
            queryParams["keyword"] = keyword;
        if (customerType.HasValue)
            queryParams["customerType"] = customerType.Value.ToString();

        return await _apiClient.GetAsync<PagedResult<CustomerListItem>>("api/customers", queryParams);
    }

    public async Task<ApiResponse<CustomerDetailDto>> GetDetailAsync(int id)
    {
        return await _apiClient.GetAsync<CustomerDetailDto>($"api/customers/{id}");
    }
}

/// <summary>
/// 移动端订单服务实现
/// </summary>
public class OrderMobileService : IOrderMobileService
{
    private readonly IApiClient _apiClient;

    public OrderMobileService(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<ApiResponse<PagedResult<OrderListItem>>> GetListAsync(
        int pageIndex = 1, int pageSize = 20, string? keyword = null, int? status = null, int? paymentStatus = null)
    {
        var queryParams = new Dictionary<string, string>
        {
            ["pageIndex"] = pageIndex.ToString(),
            ["pageSize"] = pageSize.ToString(),
        };

        if (!string.IsNullOrWhiteSpace(keyword))
            queryParams["keyword"] = keyword;
        if (status.HasValue)
            queryParams["status"] = status.Value.ToString();
        if (paymentStatus.HasValue)
            queryParams["paymentStatus"] = paymentStatus.Value.ToString();

        return await _apiClient.GetAsync<PagedResult<OrderListItem>>("api/orders", queryParams);
    }

    public async Task<ApiResponse<OrderDetailDto>> GetDetailAsync(int id)
    {
        return await _apiClient.GetAsync<OrderDetailDto>($"api/orders/{id}");
    }

    public async Task<ApiResponse<int>> CreateAsync(CreateOrderRequest request)
    {
        return await _apiClient.PostAsync<int>("api/orders", request);
    }
}
