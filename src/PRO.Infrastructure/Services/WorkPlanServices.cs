using Microsoft.EntityFrameworkCore;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 排班服务实现
/// </summary>
public class WorkScheduleService : IWorkScheduleService
{
    private readonly ProDbContext _dbContext;

    public WorkScheduleService(ProDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ApiResponse<PagedResult<WorkScheduleListItem>>> GetListAsync(PagedRequest request, int? employeeId = null)
    {
        try
        {
            var query = _dbContext.WorkSchedules.AsNoTracking()
                .Include(w => w.Employee)
                .AsQueryable();

            if (employeeId.HasValue)
                query = query.Where(w => w.EmployeeId == employeeId.Value);

            if (!string.IsNullOrWhiteSpace(request.Keyword))
                query = query.Where(w => w.Employee != null && w.Employee.Name.Contains(request.Keyword));

            var totalCount = await query.CountAsync();
            var items = await query.OrderByDescending(w => w.ScheduleDate)
                .Skip((request.PageIndex - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(w => new WorkScheduleListItem
                {
                    Id = w.Id,
                    EmployeeId = w.EmployeeId,
                    EmployeeName = w.Employee != null ? w.Employee.Name : "",
                    EmployeeNo = w.Employee != null ? w.Employee.EmployeeNo : "",
                    ScheduleDate = w.ScheduleDate,
                    WorkStartTime = w.WorkStartTime,
                    WorkEndTime = w.WorkEndTime,
                    BreakStartTime = w.BreakStartTime,
                    BreakEndTime = w.BreakEndTime,
                    TotalWorkMinutes = w.TotalWorkMinutes,
                    ScheduleType = w.ScheduleType,
                    HasChanged = w.HasChanged,
                    Remark = w.Remark
                })
                .ToListAsync();

            return ApiResponse<PagedResult<WorkScheduleListItem>>.Ok(new PagedResult<WorkScheduleListItem>
            {
                Items = items,
                TotalCount = totalCount,
                PageIndex = request.PageIndex,
                PageSize = request.PageSize
            });
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<WorkScheduleListItem>>.Fail($"查询排班列表失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<List<CalendarScheduleItem>>> GetCalendarAsync(EmployeeScheduleCalendarRequest request)
    {
        try
        {
            var start = request.StartDate.Date;
            var end = request.EndDate.Date;
            if (end < start)
                return ApiResponse<List<CalendarScheduleItem>>.Fail("结束日期不能早于开始日期");

            var schedules = await _dbContext.WorkSchedules.AsNoTracking()
                .Where(w => w.EmployeeId == request.EmployeeId && w.ScheduleDate >= start && w.ScheduleDate <= end)
                .ToListAsync();

            var items = new List<CalendarScheduleItem>();
            for (var date = start; date <= end; date = date.AddDays(1))
            {
                var schedule = schedules.FirstOrDefault(s => s.ScheduleDate.Date == date.Date);
                items.Add(new CalendarScheduleItem
                {
                    Date = date,
                    IsWorkday = schedule != null,
                    WorkStartTime = schedule?.WorkStartTime.ToString(@"hh\:mm"),
                    WorkEndTime = schedule?.WorkEndTime.ToString(@"hh\:mm"),
                    TotalWorkMinutes = schedule?.TotalWorkMinutes ?? 0,
                    ScheduleType = schedule?.ScheduleType ?? "Rest",
                    HasChanged = schedule?.HasChanged ?? false,
                    Remark = schedule?.Remark
                });
            }

            return ApiResponse<List<CalendarScheduleItem>>.Ok(items);
        }
        catch (Exception ex)
        {
            return ApiResponse<List<CalendarScheduleItem>>.Fail($"查询日历排班失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<int>> CreateAsync(CreateWorkScheduleRequest request, int createdById)
    {
        try
        {
            var employee = await _dbContext.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == request.EmployeeId);
            if (employee == null)
                return ApiResponse<int>.Fail("员工不存在");

            var scheduleDate = request.ScheduleDate.Date;
            var exists = await _dbContext.WorkSchedules.AnyAsync(w => w.EmployeeId == request.EmployeeId && w.ScheduleDate == scheduleDate);
            if (exists)
                return ApiResponse<int>.Fail("该日期已有排班");

            var now = DateTime.Now;
            var schedule = new WorkSchedule
            {
                EmployeeId = request.EmployeeId,
                BranchId = employee.BranchId,
                ScheduleDate = scheduleDate,
                WorkStartTime = request.WorkStartTime,
                WorkEndTime = request.WorkEndTime,
                BreakStartTime = request.BreakStartTime,
                BreakEndTime = request.BreakEndTime,
                TotalWorkMinutes = CalculateWorkMinutes(request.WorkStartTime, request.WorkEndTime, request.BreakStartTime, request.BreakEndTime),
                ScheduleType = request.ScheduleType,
                Remark = request.Remark,
                HasChanged = true,
                CreatedById = createdById,
                CreatedAt = now,
                UpdatedAt = now,
                LocalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                SyncStatus = SyncStatus.Pending
            };

            _dbContext.WorkSchedules.Add(schedule);
            await _dbContext.SaveChangesAsync();
            return ApiResponse<int>.Ok(schedule.Id, "排班创建成功");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"创建排班失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> BatchCreateAsync(BatchCreateWorkScheduleRequest request, int createdById)
    {
        try
        {
            var employee = await _dbContext.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == request.EmployeeId);
            if (employee == null)
                return ApiResponse<bool>.Fail("员工不存在");

            var start = request.StartDate.Date;
            var end = request.EndDate.Date;
            if (end < start)
                return ApiResponse<bool>.Fail("结束日期不能早于开始日期");

            var existingDates = await _dbContext.WorkSchedules.AsNoTracking()
                .Where(w => w.EmployeeId == request.EmployeeId && w.ScheduleDate >= start && w.ScheduleDate <= end)
                .Select(w => w.ScheduleDate)
                .ToListAsync();

            var existingSet = existingDates.Select(d => d.Date).ToHashSet();
            var now = DateTime.Now;
            for (var date = start; date <= end; date = date.AddDays(1))
            {
                if (existingSet.Contains(date))
                    continue;

                _dbContext.WorkSchedules.Add(new WorkSchedule
                {
                    EmployeeId = request.EmployeeId,
                    BranchId = employee.BranchId,
                    ScheduleDate = date,
                    WorkStartTime = request.WorkStartTime,
                    WorkEndTime = request.WorkEndTime,
                    BreakStartTime = request.BreakStartTime,
                    BreakEndTime = request.BreakEndTime,
                    TotalWorkMinutes = CalculateWorkMinutes(request.WorkStartTime, request.WorkEndTime, request.BreakStartTime, request.BreakEndTime),
                    ScheduleType = request.ScheduleType,
                    Remark = request.Remark,
                    HasChanged = true,
                    CreatedById = createdById,
                    CreatedAt = now,
                    UpdatedAt = now,
                    LocalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    SyncStatus = SyncStatus.Pending
                });
            }

            await _dbContext.SaveChangesAsync();
            return ApiResponse<bool>.Ok(true, "批量排班成功");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"批量排班失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> UpdateAsync(int id, CreateWorkScheduleRequest request, int modifiedById)
    {
        try
        {
            var schedule = await _dbContext.WorkSchedules.FindAsync(id);
            if (schedule == null)
                return ApiResponse<bool>.Fail("排班记录不存在");

            schedule.ScheduleDate = request.ScheduleDate.Date;
            schedule.WorkStartTime = request.WorkStartTime;
            schedule.WorkEndTime = request.WorkEndTime;
            schedule.BreakStartTime = request.BreakStartTime;
            schedule.BreakEndTime = request.BreakEndTime;
            schedule.TotalWorkMinutes = CalculateWorkMinutes(request.WorkStartTime, request.WorkEndTime, request.BreakStartTime, request.BreakEndTime);
            schedule.ScheduleType = request.ScheduleType;
            schedule.Remark = request.Remark;
            schedule.HasChanged = true;
            schedule.UpdatedAt = DateTime.Now;
            schedule.LocalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            schedule.SyncStatus = SyncStatus.Pending;

            await _dbContext.SaveChangesAsync();
            return ApiResponse<bool>.Ok(true, "排班更新成功");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"更新排班失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id)
    {
        try
        {
            var schedule = await _dbContext.WorkSchedules.FindAsync(id);
            if (schedule == null)
                return ApiResponse<bool>.Fail("排班记录不存在");

            _dbContext.WorkSchedules.Remove(schedule);
            await _dbContext.SaveChangesAsync();
            return ApiResponse<bool>.Ok(true, "排班已删除");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"删除排班失败: {ex.Message}");
        }
    }

    public Task<ApiResponse<bool>> NotifyEmployeeAsync(int scheduleId)
    {
        return Task.FromResult(ApiResponse<bool>.Fail("排班通知需要企业微信配置并联调后启用"));
    }

    private static int CalculateWorkMinutes(TimeSpan start, TimeSpan end, TimeSpan? breakStart, TimeSpan? breakEnd)
    {
        var minutes = (int)(end - start).TotalMinutes;
        if (breakStart.HasValue && breakEnd.HasValue)
            minutes -= (int)(breakEnd.Value - breakStart.Value).TotalMinutes;
        return Math.Max(0, minutes);
    }
}

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
