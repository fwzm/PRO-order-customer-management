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
