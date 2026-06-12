using Microsoft.EntityFrameworkCore;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 计划草稿服务实现
/// </summary>
public class PlanDraftService : IPlanDraftService
{
    private readonly ProDbContext _dbContext;

    public PlanDraftService(ProDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ApiResponse<List<PlanDraftDto>>> GetListAsync(int createdById)
    {
        try
        {
            var drafts = await _dbContext.PlanDrafts.AsNoTracking()
                .Where(d => d.CreatedById == createdById)
                .OrderByDescending(d => d.UpdatedAt)
                .Select(d => new PlanDraftDto
                {
                    Id = d.Id,
                    CreatedById = d.CreatedById,
                    DraftType = d.DraftType,
                    EmployeeId = d.EmployeeId,
                    PlanDate = d.PlanDate,
                    Content = d.Content,
                    CreatedAt = d.CreatedAt
                })
                .ToListAsync();

            return ApiResponse<List<PlanDraftDto>>.Ok(drafts);
        }
        catch (Exception ex)
        {
            return ApiResponse<List<PlanDraftDto>>.Fail($"查询草稿失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<int>> SaveAsync(SavePlanDraftRequest request, int createdById)
    {
        try
        {
            var now = DateTime.Now;
            PlanDraft draft;
            if (request.Id.HasValue)
            {
                draft = await _dbContext.PlanDrafts.FindAsync(request.Id.Value)
                    ?? throw new InvalidOperationException("草稿不存在");
                draft.DraftType = request.DraftType;
                draft.EmployeeId = request.EmployeeId;
                draft.PlanDate = request.PlanDate?.Date;
                draft.Content = request.Content;
                draft.UpdatedAt = now;
            }
            else
            {
                draft = new PlanDraft
                {
                    CreatedById = createdById,
                    DraftType = request.DraftType,
                    EmployeeId = request.EmployeeId,
                    PlanDate = request.PlanDate?.Date,
                    Content = request.Content,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _dbContext.PlanDrafts.Add(draft);
            }

            await _dbContext.SaveChangesAsync();
            return ApiResponse<int>.Ok(draft.Id, "草稿保存成功");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"保存草稿失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id)
    {
        try
        {
            var draft = await _dbContext.PlanDrafts.FindAsync(id);
            if (draft == null)
                return ApiResponse<bool>.Fail("草稿不存在");

            _dbContext.PlanDrafts.Remove(draft);
            await _dbContext.SaveChangesAsync();
            return ApiResponse<bool>.Ok(true, "草稿已删除");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"删除草稿失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> PublishAsync(int draftId, int publishedById)
    {
        return await DeleteAsync(draftId);
    }
}
