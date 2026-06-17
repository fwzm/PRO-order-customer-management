using System.Net.Http;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Entities;
using PRO.Domain.Enums;

namespace PRO.Desktop.Services;

/// <summary>
/// 客户 API 客户端
/// </summary>
public class CustomerApiClient : ApiClientBase, ICustomerService
{
    public CustomerApiClient(HttpClient httpClient) : base(httpClient) { }

    public async Task<ApiResponse<PagedResult<CustomerListItem>>> GetListAsync(PagedRequest request, int? branchId = null, CustomerType? customerType = null, bool showMajorOnly = true)
    {
        var url = $"api/customers?pageIndex={request.PageIndex}&pageSize={request.PageSize}";
        if (!string.IsNullOrEmpty(request.Keyword)) url += $"&keyword={request.Keyword}";
        if (branchId.HasValue) url += $"&branchId={branchId}";
        if (customerType.HasValue) url += $"&customerType={customerType}";
        return await GetAsync<PagedResult<CustomerListItem>>(url);
    }

    public async Task<ApiResponse<CustomerDetailDto>> GetByIdAsync(int id)
    {
        return await GetAsync<CustomerDetailDto>($"api/customers/{id}");
    }

    public async Task<ApiResponse<CustomerListItem>> GetSubCustomersAsync(int parentCustomerId)
    {
        return await GetAsync<CustomerListItem>($"api/customers/{parentCustomerId}/sub");
    }

    public async Task<ApiResponse<int>> CreateAsync(CreateCustomerRequest request)
    {
        return await PostAsync<int>("api/customers", request);
    }

    public async Task<ApiResponse<bool>> UpdateAsync(UpdateCustomerRequest request)
    {
        return await PutAsync<bool>($"api/customers/{request.Id}", request);
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id)
    {
        return await DeleteAsync($"api/customers/{id}");
    }

    public async Task<ApiResponse<CustomerDuplicateCheckResult>> CheckDuplicatesAsync(string? phone, string? name, string? address, string? legalPerson)
    {
        return await PostAsync<CustomerDuplicateCheckResult>("api/customers/check-duplicates", new { phone, name, address, legalPerson });
    }

    public async Task<ApiResponse<bool>> MergeCustomersAsync(MergeCustomerRequest request)
    {
        return await PostAsync<bool>("api/customers/merge", request);
    }

    public Task<ApiResponse<string>> ExportToExcelAsync(PagedRequest request, int? branchId = null)
    {
        return Task.FromResult(ApiResponse<string>.Fail("请在界面层实现导出"));
    }

    public async Task<ApiResponse<bool>> BulkAssignCustomersAsync(BulkAssignRequest request)
    {
        return await PostAsync<bool>("api/customers/bulk-assign", request);
    }

    public Task<ApiResponse<BatchOperationResult>> BulkAssignWithProgressAsync(BulkAssignRequest request, BatchOperationContext? context = null, IProgress<BatchOperationProgress>? progress = null)
    {
        return Task.FromResult(ApiResponse<BatchOperationResult>.Fail("请使用 Service 层批量操作"));
    }

    public Task<string> GenerateCustomerNoAsync(int branchId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(string.Empty);
    }

    public Task<List<BusinessDistrict>> GetBusinessDistrictsAsync(int? branchId = null)
    {
        return Task.FromResult(new List<BusinessDistrict>());
    }

    public Task<List<CustomerListItem>> GetMajorCustomersAsync(int branchId)
    {
        return Task.FromResult(new List<CustomerListItem>());
    }

    public Task<Domain.Entities.Customer?> GetEntityByIdAsync(int id)
    {
        return Task.FromResult<Domain.Entities.Customer?>(null);
    }
}
