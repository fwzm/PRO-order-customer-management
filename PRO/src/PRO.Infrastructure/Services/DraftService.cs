using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PRO.Domain.Entities;
using PRO.Infrastructure.Persistence;
using Serilog;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 表单草稿自动保存服务
/// </summary>
public class DraftService
{
    private readonly ProDbContext _dbContext;

    public DraftService(ProDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// 保存草稿
    /// </summary>
    public async Task SaveDraftAsync(string draftKey, object data, int employeeId)
    {
        try
        {
            var json = JsonSerializer.Serialize(data);
            var existing = await _dbContext.PlanDrafts
                .FirstOrDefaultAsync(d => d.DraftType == draftKey && d.EmployeeId == employeeId);

            if (existing != null)
            {
                existing.Content = json;
                existing.UpdatedAt = DateTime.Now;
            }
            else
            {
                _dbContext.PlanDrafts.Add(new PlanDraft
                {
                    DraftType = draftKey,
                    EmployeeId = employeeId,
                    Content = json,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                });
            }

            await _dbContext.SaveChangesAsync();
            Log.Debug("草稿已保存: {Key}", draftKey);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "保存草稿失败: {Key}", draftKey);
        }
    }

    /// <summary>
    /// 加载草稿
    /// </summary>
    public async Task<T?> LoadDraftAsync<T>(string draftKey, int employeeId) where T : class
    {
        try
        {
            var draft = await _dbContext.PlanDrafts
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.DraftType == draftKey && d.EmployeeId == employeeId);

            if (draft == null || string.IsNullOrEmpty(draft.Content))
                return null;

            return JsonSerializer.Deserialize<T>(draft.Content);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "加载草稿失败: {Key}", draftKey);
            return null;
        }
    }

    /// <summary>
    /// 删除草稿
    /// </summary>
    public async Task DeleteDraftAsync(string draftKey, int employeeId)
    {
        try
        {
            var draft = await _dbContext.PlanDrafts
                .FirstOrDefaultAsync(d => d.DraftType == draftKey && d.EmployeeId == employeeId);

            if (draft != null)
            {
                _dbContext.PlanDrafts.Remove(draft);
                await _dbContext.SaveChangesAsync();
                Log.Debug("草稿已删除: {Key}", draftKey);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "删除草稿失败: {Key}", draftKey);
        }
    }

    /// <summary>
    /// 检查是否存在草稿
    /// </summary>
    public async Task<bool> HasDraftAsync(string draftKey, int employeeId)
    {
        return await _dbContext.PlanDrafts
            .AnyAsync(d => d.DraftType == draftKey && d.EmployeeId == employeeId);
    }

    /// <summary>
    /// 清理过期草稿（超过7天）
    /// </summary>
    public async Task CleanupExpiredDraftsAsync()
    {
        try
        {
            var expiredDate = DateTime.Now.AddDays(-7);
            var expiredDrafts = await _dbContext.PlanDrafts
                .Where(d => d.UpdatedAt < expiredDate)
                .ToListAsync();

            if (expiredDrafts.Any())
            {
                _dbContext.PlanDrafts.RemoveRange(expiredDrafts);
                await _dbContext.SaveChangesAsync();
                Log.Information("清理了 {Count} 条过期草稿", expiredDrafts.Count);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "清理过期草稿失败");
        }
    }
}

/// <summary>
/// 客户编辑草稿数据
/// </summary>
public class CustomerDraftData
{
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public string? Province { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }
    public string? Address { get; set; }
    public string? LegalPerson { get; set; }
    public string? Remark { get; set; }
    public int? BusinessDistrictId { get; set; }
    public int? ParentCustomerId { get; set; }
    public double? Longitude { get; set; }
    public double? Latitude { get; set; }
}

/// <summary>
/// 订单编辑草稿数据
/// </summary>
public class OrderDraftData
{
    public int CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? DeliveryAddress { get; set; }
    public double? DeliveryLongitude { get; set; }
    public double? DeliveryLatitude { get; set; }
    public DateTime? DeliveryTime { get; set; }
    public string? Remark { get; set; }
    public decimal DiscountAmount { get; set; }
    public List<OrderItemDraftData> Items { get; set; } = new();
}

public class OrderItemDraftData
{
    public int ProductId { get; set; }
    public string? ProductName { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
}
