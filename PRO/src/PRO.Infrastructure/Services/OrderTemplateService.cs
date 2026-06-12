using Microsoft.EntityFrameworkCore;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Entities;
using PRO.Infrastructure.Persistence;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 订单模板服务 - 支持快速创建重复订单
/// </summary>
public class OrderTemplateService : IOrderTemplateService
{
    private readonly ProDbContext _dbContext;
    private readonly IOrderService _orderService;

    public OrderTemplateService(ProDbContext dbContext, IOrderService orderService)
    {
        _dbContext = dbContext;
        _orderService = orderService;
    }

    /// <summary>
    /// 获取用户的订单模板列表
    /// </summary>
    public async Task<ApiResponse<List<OrderTemplateDto>>> GetTemplatesAsync(int employeeId, int? customerId = null)
    {
        try
        {
            var query = _dbContext.OrderTemplates
                .AsNoTracking()
                .Include(t => t.Customer)
                .Include(t => t.Items)
                .ThenInclude(i => i.Product)
                .Where(t => t.IsActive && (t.CreatedById == employeeId || t.IsPublic));

            if (customerId.HasValue)
                query = query.Where(t => t.CustomerId == customerId.Value);

            var templates = await query
                .OrderByDescending(t => t.LastUsedAt)
                .Take(50)
                .Select(t => new OrderTemplateDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    CustomerId = t.CustomerId,
                    CustomerName = t.Customer != null ? t.Customer.Name : "",
                    DeliveryAddress = t.DeliveryAddress,
                    Items = t.Items.Select(i => new OrderTemplateItemDto
                    {
                        ProductId = i.ProductId,
                        ProductName = i.Product != null ? i.Product.Name : "",
                        ProductSku = i.Product != null ? i.Product.SKU : null,
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice
                    }).ToList(),
                    Remark = t.Remark,
                    UseCount = t.UseCount,
                    LastUsedAt = t.LastUsedAt ?? DateTime.MinValue,
                    CreatedAt = t.CreatedAt
                })
                .ToListAsync();

            return ApiResponse<List<OrderTemplateDto>>.Ok(templates);
        }
        catch (Exception ex)
        {
            return ApiResponse<List<OrderTemplateDto>>.Fail($"获取模板失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 从历史订单创建模板
    /// </summary>
    public async Task<ApiResponse<int>> CreateTemplateFromOrderAsync(CreateTemplateFromOrderRequest request, int employeeId)
    {
        try
        {
            var order = await _dbContext.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == request.OrderId);

            if (order == null)
                return ApiResponse<int>.Fail("订单不存在");

            var template = new OrderTemplate
            {
                Name = request.TemplateName,
                CustomerId = order.CustomerId,
                DeliveryAddress = order.DeliveryAddress,
                Remark = order.Remark,
                CreatedById = employeeId,
                IsPublic = false,
                UseCount = 0,
                LastUsedAt = DateTime.Now,
                CreatedAt = DateTime.Now
            };

            foreach (var item in order.Items)
            {
                template.Items.Add(new OrderTemplateItem
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice
                });
            }

            _dbContext.OrderTemplates.Add(template);
            await _dbContext.SaveChangesAsync();

            return ApiResponse<int>.Ok(template.Id, "模板创建成功");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"创建模板失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 创建新模板
    /// </summary>
    public async Task<ApiResponse<int>> CreateTemplateAsync(CreateOrderTemplateRequest request, int employeeId)
    {
        try
        {
            var template = new OrderTemplate
            {
                Name = request.Name,
                CustomerId = request.CustomerId,
                DeliveryAddress = request.DeliveryAddress,
                Remark = request.Remark,
                CreatedById = employeeId,
                IsPublic = false,
                UseCount = 0,
                LastUsedAt = DateTime.Now,
                CreatedAt = DateTime.Now
            };

            foreach (var item in request.Items)
            {
                template.Items.Add(new OrderTemplateItem
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice
                });
            }

            _dbContext.OrderTemplates.Add(template);
            await _dbContext.SaveChangesAsync();

            return ApiResponse<int>.Ok(template.Id, "模板创建成功");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"创建模板失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 使用模板创建订单
    /// </summary>
    public async Task<ApiResponse<int>> CreateOrderFromTemplateAsync(int templateId, int employeeId, DateTime? deliveryTime = null)
    {
        try
        {
            var template = await _dbContext.OrderTemplates
                .Include(t => t.Items)
                .FirstOrDefaultAsync(t => t.Id == templateId);

            if (template == null)
                return ApiResponse<int>.Fail("模板不存在");

            var request = new CreateOrderRequest
            {
                CustomerId = template.CustomerId,
                DeliveryAddress = template.DeliveryAddress,
                Remark = template.Remark,
                IsDraft = false,
                Items = template.Items.Select(i => new CreateOrderItemRequest
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice
                }).ToList()
            };

            var result = await _orderService.CreateAsync(request, employeeId);

            if (result.Success)
            {
                // 更新模板使用次数
                template.UseCount++;
                template.LastUsedAt = DateTime.Now;
                await _dbContext.SaveChangesAsync();
            }

            return result;
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"从模板创建订单失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 复制订单
    /// </summary>
    public async Task<ApiResponse<int>> CopyOrderAsync(int orderId, int employeeId)
    {
        try
        {
            var order = await _dbContext.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
                return ApiResponse<int>.Fail("订单不存在");

            var request = new CreateOrderRequest
            {
                CustomerId = order.CustomerId,
                DeliveryAddress = order.DeliveryAddress,
                Remark = order.Remark,
                IsDraft = true, // 复制的订单默认为草稿
                Items = order.Items.Select(i => new CreateOrderItemRequest
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice
                }).ToList()
            };

            return await _orderService.CreateAsync(request, employeeId);
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"复制订单失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 更新模板
    /// </summary>
    public async Task<ApiResponse<bool>> UpdateTemplateAsync(int templateId, CreateOrderTemplateRequest request, int employeeId)
    {
        try
        {
            var template = await _dbContext.OrderTemplates
                .Include(t => t.Items)
                .FirstOrDefaultAsync(t => t.Id == templateId && t.CreatedById == employeeId && t.IsActive);

            if (template == null)
                return ApiResponse<bool>.Fail("模板不存在或无权修改");

            template.Name = request.Name;
            template.CustomerId = request.CustomerId;
            template.DeliveryAddress = request.DeliveryAddress;
            template.Remark = request.Remark;

            // 替换明细
            _dbContext.OrderTemplateItems.RemoveRange(template.Items);
            foreach (var item in request.Items)
            {
                template.Items.Add(new OrderTemplateItem
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice
                });
            }

            await _dbContext.SaveChangesAsync();
            return ApiResponse<bool>.Ok(true, "模板更新成功");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"更新模板失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 启用/停用模板（软删除）
    /// </summary>
    public async Task<ApiResponse<bool>> ToggleStatusAsync(int templateId, bool isActive, int employeeId)
    {
        try
        {
            var template = await _dbContext.OrderTemplates
                .FirstOrDefaultAsync(t => t.Id == templateId && t.CreatedById == employeeId);

            if (template == null)
                return ApiResponse<bool>.Fail("模板不存在或无权操作");

            template.IsActive = isActive;
            await _dbContext.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, isActive ? "模板已启用" : "模板已停用");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"操作失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 删除模板（软删除，仅创建者可操作）
    /// </summary>
    public async Task<ApiResponse<bool>> DeleteTemplateAsync(int templateId, int employeeId)
    {
        try
        {
            var template = await _dbContext.OrderTemplates
                .FirstOrDefaultAsync(t => t.Id == templateId && t.CreatedById == employeeId);

            if (template == null)
                return ApiResponse<bool>.Fail("模板不存在或无权删除");

            template.IsActive = false;
            await _dbContext.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "模板已删除（可联系管理员恢复）");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"删除模板失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取热门模板（按使用次数）
    /// </summary>
    public async Task<ApiResponse<List<OrderTemplateDto>>> GetPopularTemplatesAsync(int employeeId, int limit = 10)
    {
        try
        {
            var templates = await _dbContext.OrderTemplates
                .AsNoTracking()
                .Include(t => t.Customer)
                .Include(t => t.Items)
                .ThenInclude(i => i.Product)
                .Where(t => t.IsActive && t.CreatedById == employeeId)
                .OrderByDescending(t => t.UseCount)
                .Take(limit)
                .Select(t => new OrderTemplateDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    CustomerId = t.CustomerId,
                    CustomerName = t.Customer != null ? t.Customer.Name : "",
                    UseCount = t.UseCount,
                    LastUsedAt = t.LastUsedAt ?? DateTime.MinValue
                })
                .ToListAsync();

            return ApiResponse<List<OrderTemplateDto>>.Ok(templates);
        }
        catch (Exception ex)
        {
            return ApiResponse<List<OrderTemplateDto>>.Fail($"获取热门模板失败: {ex.Message}");
        }
    }
}
