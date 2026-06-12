using Microsoft.EntityFrameworkCore;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 工作计划服务实现
/// </summary>
public class WorkPlanService : IWorkPlanService
{
    private readonly ProDbContext _dbContext;

    public WorkPlanService(ProDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ApiResponse<PagedResult<WorkPlanListItem>>> GetListAsync(PagedRequest request, int? employeeId = null)
    {
        try
        {
            var query = _dbContext.WorkPlans.AsNoTracking()
                .Include(w => w.Employee)
                .Include(w => w.Customer)
                .AsQueryable();

            if (employeeId.HasValue)
                query = query.Where(w => w.EmployeeId == employeeId.Value);

            if (!string.IsNullOrWhiteSpace(request.Keyword))
                query = query.Where(w => w.Content.Contains(request.Keyword));

            var totalCount = await query.CountAsync();
            var items = await query.OrderByDescending(w => w.PlanDate)
                .ThenBy(w => w.StartTime)
                .Skip((request.PageIndex - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(w => new WorkPlanListItem
                {
                    Id = w.Id,
                    EmployeeId = w.EmployeeId,
                    EmployeeName = w.Employee != null ? w.Employee.Name : "",
                    PlanDate = w.PlanDate,
                    StartTime = w.StartTime,
                    EndTime = w.EndTime,
                    DurationMinutes = w.DurationMinutes,
                    Content = w.Content,
                    CustomerName = w.Customer != null ? w.Customer.Name : null,
                    PlanType = w.PlanType,
                    ExecutionStatus = w.ExecutionStatus,
                    ExecutionRemark = w.ExecutionRemark
                })
                .ToListAsync();

            return ApiResponse<PagedResult<WorkPlanListItem>>.Ok(new PagedResult<WorkPlanListItem>
            {
                Items = items,
                TotalCount = totalCount,
                PageIndex = request.PageIndex,
                PageSize = request.PageSize
            });
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<WorkPlanListItem>>.Fail($"查询工作计划列表失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<DailyPlanView>> GetDailyPlanAsync(int employeeId, DateTime date)
    {
        try
        {
            var day = date.Date;
            var employee = await _dbContext.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId);
            var schedule = await _dbContext.WorkSchedules.AsNoTracking()
                .FirstOrDefaultAsync(w => w.EmployeeId == employeeId && w.ScheduleDate == day);
            var plans = await _dbContext.WorkPlans.AsNoTracking()
                .Include(w => w.Customer)
                .Where(w => w.EmployeeId == employeeId && w.PlanDate == day)
                .OrderBy(w => w.StartTime)
                .ToListAsync();

            var view = new DailyPlanView
            {
                EmployeeId = employeeId,
                EmployeeName = employee?.Name ?? "",
                Date = day,
                Schedule = schedule == null ? null : new WorkScheduleListItem
                {
                    Id = schedule.Id,
                    EmployeeId = schedule.EmployeeId,
                    EmployeeName = employee?.Name ?? "",
                    EmployeeNo = employee?.EmployeeNo ?? "",
                    ScheduleDate = schedule.ScheduleDate,
                    WorkStartTime = schedule.WorkStartTime,
                    WorkEndTime = schedule.WorkEndTime,
                    BreakStartTime = schedule.BreakStartTime,
                    BreakEndTime = schedule.BreakEndTime,
                    TotalWorkMinutes = schedule.TotalWorkMinutes,
                    ScheduleType = schedule.ScheduleType,
                    HasChanged = schedule.HasChanged,
                    Remark = schedule.Remark
                },
                Plans = plans.Select(ToDto).ToList(),
                TotalWorkMinutes = schedule?.TotalWorkMinutes ?? 0,
                TotalPlanMinutes = plans.Sum(p => p.DurationMinutes)
            };

            return ApiResponse<DailyPlanView>.Ok(view);
        }
        catch (Exception ex)
        {
            return ApiResponse<DailyPlanView>.Fail($"查询日计划失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<List<WorkPlanDto>>> GetByDateRangeAsync(int employeeId, DateTime startDate, DateTime endDate)
    {
        try
        {
            var plans = await _dbContext.WorkPlans.AsNoTracking()
                .Include(w => w.Customer)
                .Where(w => w.EmployeeId == employeeId && w.PlanDate >= startDate.Date && w.PlanDate <= endDate.Date)
                .OrderBy(w => w.PlanDate)
                .ThenBy(w => w.StartTime)
                .ToListAsync();

            return ApiResponse<List<WorkPlanDto>>.Ok(plans.Select(ToDto).ToList());
        }
        catch (Exception ex)
        {
            return ApiResponse<List<WorkPlanDto>>.Fail($"查询工作计划失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<int>> CreateAsync(CreateWorkPlanRequest request)
    {
        try
        {
            var now = DateTime.Now;
            var plan = new WorkPlan
            {
                EmployeeId = request.EmployeeId,
                PlanDate = request.PlanDate.Date,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                DurationMinutes = Math.Max(0, (int)(request.EndTime - request.StartTime).TotalMinutes),
                Content = request.Content,
                CustomerId = request.CustomerId,
                OrderId = request.OrderId,
                PlanType = request.PlanType,
                ExecutionStatus = "Pending",
                Remark = request.Remark,
                CreatedAt = now,
                UpdatedAt = now,
                LocalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                SyncStatus = SyncStatus.Pending
            };

            _dbContext.WorkPlans.Add(plan);
            await _dbContext.SaveChangesAsync();
            return ApiResponse<int>.Ok(plan.Id, "工作计划创建成功");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"创建工作计划失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> UpdateAsync(UpdateWorkPlanRequest request)
    {
        try
        {
            var plan = await _dbContext.WorkPlans.FindAsync(request.Id);
            if (plan == null)
                return ApiResponse<bool>.Fail("工作计划不存在");

            plan.StartTime = request.StartTime;
            plan.EndTime = request.EndTime;
            plan.DurationMinutes = Math.Max(0, (int)(request.EndTime - request.StartTime).TotalMinutes);
            plan.Content = request.Content;
            plan.CustomerId = request.CustomerId;
            plan.OrderId = request.OrderId;
            plan.PlanType = request.PlanType;
            plan.ExecutionStatus = request.ExecutionStatus;
            plan.ExecutionRemark = request.ExecutionRemark;
            plan.Remark = request.Remark;
            plan.UpdatedAt = DateTime.Now;
            plan.LocalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            plan.SyncStatus = SyncStatus.Pending;

            await _dbContext.SaveChangesAsync();
            return ApiResponse<bool>.Ok(true, "工作计划更新成功");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"更新工作计划失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id)
    {
        try
        {
            var plan = await _dbContext.WorkPlans.FindAsync(id);
            if (plan == null)
                return ApiResponse<bool>.Fail("工作计划不存在");

            _dbContext.WorkPlans.Remove(plan);
            await _dbContext.SaveChangesAsync();
            return ApiResponse<bool>.Ok(true, "工作计划已删除");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"删除工作计划失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> UpdateExecutionStatusAsync(int id, string status, string? remark)
    {
        try
        {
            var plan = await _dbContext.WorkPlans.FindAsync(id);
            if (plan == null)
                return ApiResponse<bool>.Fail("工作计划不存在");

            plan.ExecutionStatus = status;
            plan.ExecutionRemark = remark;
            plan.UpdatedAt = DateTime.Now;
            plan.LocalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            plan.SyncStatus = SyncStatus.Pending;

            await _dbContext.SaveChangesAsync();
            return ApiResponse<bool>.Ok(true, "执行状态更新成功");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"更新执行状态失败: {ex.Message}");
        }
    }

    private static WorkPlanDto ToDto(WorkPlan plan)
    {
        return new WorkPlanDto
        {
            Id = plan.Id,
            EmployeeId = plan.EmployeeId,
            EmployeeName = plan.Employee?.Name ?? "",
            PlanDate = plan.PlanDate,
            StartTime = plan.StartTime,
            EndTime = plan.EndTime,
            DurationMinutes = plan.DurationMinutes,
            Content = plan.Content,
            CustomerId = plan.CustomerId,
            CustomerName = plan.Customer?.Name,
            OrderId = plan.OrderId,
            PlanType = plan.PlanType,
            ExecutionStatus = plan.ExecutionStatus,
            ExecutionRemark = plan.ExecutionRemark,
            Remark = plan.Remark,
            HasChanged = plan.SyncStatus == SyncStatus.Pending
        };
    }
}
