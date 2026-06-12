using System.Net.Http;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Enums;

namespace PRO.Desktop.Services;

/// <summary>
/// 订单 API 客户端
/// </summary>
public class OrderApiClient : ApiClientBase, IOrderService
{
    public OrderApiClient(HttpClient httpClient) : base(httpClient) { }

    public async Task<ApiResponse<PagedResult<OrderListItem>>> GetListAsync(PagedRequest request, int? branchId = null, OrderStatus? status = null, PaymentStatus? paymentStatus = null)
    {
        var url = $"api/orders?pageIndex={request.PageIndex}&pageSize={request.PageSize}";
        if (!string.IsNullOrEmpty(request.Keyword)) url += $"&keyword={request.Keyword}";
        if (branchId.HasValue) url += $"&branchId={branchId}";
        if (status.HasValue) url += $"&status={status}";
        if (paymentStatus.HasValue) url += $"&paymentStatus={paymentStatus}";
        return await GetAsync<PagedResult<OrderListItem>>(url);
    }

    public async Task<ApiResponse<OrderDetailDto>> GetByIdAsync(int id)
    {
        return await GetAsync<OrderDetailDto>($"api/orders/{id}");
    }

    public async Task<ApiResponse<int>> CreateAsync(CreateOrderRequest request, int createdById)
    {
        return await PostAsync<int>("api/orders", request);
    }

    public async Task<ApiResponse<bool>> UpdateAsync(UpdateOrderRequest request, int modifiedById)
    {
        return await PutAsync<bool>($"api/orders/{request.Id}", request);
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id)
    {
        return await DeleteAsync($"api/orders/{id}");
    }

    public async Task<ApiResponse<bool>> AssignAsync(AssignOrderRequest request, int assignedById)
    {
        return await PostAsync<bool>($"api/orders/{request.OrderId}/assign", request);
    }

    public async Task<ApiResponse<bool>> UpdateStatusAsync(UpdateOrderStatusRequest request, int modifiedById)
    {
        return await PostAsync<bool>($"api/orders/{request.OrderId}/status", request);
    }

    public async Task<ApiResponse<bool>> ConfirmDraftAsync(int orderId, int modifiedById)
    {
        return await PostAsync<bool>($"api/orders/{orderId}/confirm-draft");
    }

    public Task<ApiResponse<BatchOperationResult>> BatchAssignAsync(IEnumerable<int> orderIds, int deliveryPersonId, int assignedById)
    {
        return PostAsync<BatchOperationResult>("api/orders/batch-assign", new { orderIds, deliveryPersonId, assignedById });
    }

    public Task<ApiResponse<BatchOperationResult>> BatchUpdateStatusAsync(IEnumerable<int> orderIds, OrderStatus newStatus, int modifiedById, string? reason = null)
    {
        return PostAsync<BatchOperationResult>("api/orders/batch-status", new { orderIds, newStatus, modifiedById, reason });
    }

    public Task<ApiResponse<BatchOperationResult>> BatchConfirmDraftsAsync(IEnumerable<int> orderIds, int modifiedById)
    {
        return PostAsync<BatchOperationResult>("api/orders/batch-confirm-drafts", new { orderIds, modifiedById });
    }

    public Task<ApiResponse<BatchOperationResult>> BatchAssignWithProgressAsync(IEnumerable<int> orderIds, int deliveryPersonId, int assignedById, BatchOperationContext? context = null, IProgress<BatchOperationProgress>? progress = null)
    {
        return Task.FromResult(ApiResponse<BatchOperationResult>.Fail("请使用 Service 层批量操作"));
    }

    public Task<ApiResponse<BatchOperationResult>> BatchUpdateStatusWithProgressAsync(IEnumerable<int> orderIds, OrderStatus newStatus, int modifiedById, string? reason = null, BatchOperationContext? context = null, IProgress<BatchOperationProgress>? progress = null)
    {
        return Task.FromResult(ApiResponse<BatchOperationResult>.Fail("请使用 Service 层批量操作"));
    }

    public Task<ApiResponse<BatchOperationResult>> BatchConfirmDraftsWithProgressAsync(IEnumerable<int> orderIds, int modifiedById, BatchOperationContext? context = null, IProgress<BatchOperationProgress>? progress = null)
    {
        return Task.FromResult(ApiResponse<BatchOperationResult>.Fail("请使用 Service 层批量操作"));
    }

    public Task<ApiResponse<string>> ExportToExcelAsync(PagedRequest request, int? branchId = null, bool forGaode = false)
    {
        return Task.FromResult(ApiResponse<string>.Fail("请在界面层实现导出"));
    }

    public Task<ApiResponse<string>> ExportDeliveryPlanAsync(List<int> orderIds, string groupName)
    {
        return Task.FromResult(ApiResponse<string>.Fail("请在界面层实现导出"));
    }

    public Task<List<CustomerListItem>> GetCustomersForSelectionAsync(int branchId)
    {
        return Task.FromResult(new List<CustomerListItem>());
    }

    public Task<List<ProductListItem>> GetProductsForSelectionAsync()
    {
        return Task.FromResult(new List<ProductListItem>());
    }

    public Task<List<DeliveryPersonListItem>> GetDeliveryPersonsForSelectionAsync(int branchId)
    {
        return Task.FromResult(new List<DeliveryPersonListItem>());
    }

    public Task<Domain.Entities.Order?> GetEntityByIdAsync(int id)
    {
        return Task.FromResult<Domain.Entities.Order?>(null);
    }
}
