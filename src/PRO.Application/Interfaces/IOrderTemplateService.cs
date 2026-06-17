using PRO.Application.DTOs;

namespace PRO.Application.Interfaces;

/// <summary>
/// 订单模板服务接口 — 支持快速创建重复订单
/// </summary>
public interface IOrderTemplateService
{
    /// <summary>获取用户可见的模板列表（自己的 + 公开的）</summary>
    Task<ApiResponse<List<OrderTemplateDto>>> GetTemplatesAsync(int employeeId, int? customerId = null);

    /// <summary>获取热门模板</summary>
    Task<ApiResponse<List<OrderTemplateDto>>> GetPopularTemplatesAsync(int employeeId, int limit = 10);

    /// <summary>创建模板</summary>
    Task<ApiResponse<int>> CreateTemplateAsync(CreateOrderTemplateRequest request, int employeeId);

    /// <summary>从历史订单创建模板</summary>
    Task<ApiResponse<int>> CreateTemplateFromOrderAsync(CreateTemplateFromOrderRequest request, int employeeId);

    /// <summary>更新模板</summary>
    Task<ApiResponse<bool>> UpdateTemplateAsync(int templateId, CreateOrderTemplateRequest request, int employeeId);

    /// <summary>启用/停用模板（软删除）</summary>
    Task<ApiResponse<bool>> ToggleStatusAsync(int templateId, bool isActive, int employeeId);

    /// <summary>软删除模板（仅创建者可操作）</summary>
    Task<ApiResponse<bool>> DeleteTemplateAsync(int templateId, int employeeId);

    /// <summary>从模板创建订单</summary>
    Task<ApiResponse<int>> CreateOrderFromTemplateAsync(int templateId, int employeeId, DateTime? deliveryTime = null);

    /// <summary>复制已有订单</summary>
    Task<ApiResponse<int>> CopyOrderAsync(int orderId, int employeeId);
}
