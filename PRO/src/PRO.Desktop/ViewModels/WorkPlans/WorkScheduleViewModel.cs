using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Desktop.Controls;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;
using PRO.Infrastructure.WeChat;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Net.Http;

namespace PRO.Desktop.ViewModels;

public partial class WorkScheduleViewModel : ViewModelBase
{
    // 空状态支持
    [ObservableProperty] private EmptyStateViewModel? _emptyState;
    [ObservableProperty] private bool _showEmptyState;
    private readonly ProDbContext _dbContext;
    private readonly IWorkScheduleService _workScheduleService;
    private readonly IWorkPlanService _workPlanService;
    private readonly IPlanDraftService _planDraftService;

    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Now;

    [ObservableProperty]
    private int _selectedMonth;

    [ObservableProperty]
    private int _selectedYear;

    [ObservableProperty]
    private ObservableCollection<CalendarScheduleItem> _calendarItems = [];

    [ObservableProperty]
    private DailyPlanView? _currentDailyPlan;

    [ObservableProperty]
    private ObservableCollection<EmployeeListItem> _employees = [];

    [ObservableProperty]
    private EmployeeListItem? _selectedEmployee;

    [ObservableProperty]
    private bool _isEditMode;

    [ObservableProperty]
    private ObservableCollection<PlanDraftItem> _drafts = [];

    [ObservableProperty]
    private ObservableCollection<WeeklyScheduleRow> _weeklyScheduleRows = [];

    [ObservableProperty]
    private DateTime _weekStartDate = DateTime.Now.AddDays(-(int)DateTime.Now.DayOfWeek + 1);

    public WorkScheduleViewModel()
    {
        _dbContext = App.Services.GetService(typeof(ProDbContext)) as ProDbContext
            ?? throw new InvalidOperationException("无法获取数据库上下文");
        _workScheduleService = App.Services.GetRequiredService<IWorkScheduleService>();
        _workPlanService = App.Services.GetRequiredService<IWorkPlanService>();
        _planDraftService = App.Services.GetRequiredService<IPlanDraftService>();

        _selectedMonth = DateTime.Now.Month;
        _selectedYear = DateTime.Now.Year;
        SelectedEmployee = Employees.FirstOrDefault(e => e.Id == CurrentSession.CurrentEmployeeId);

        RunInBackground(LoadScheduleInitAsync(), "加载排班数据失败");
    }

    private async Task LoadScheduleInitAsync()
    {
        try
        {
            ShowEmptyState = false;
            await LoadEmployeesAsync();
            await LoadCalendarAsync();
            await LoadDraftsAsync();
            await LoadWeeklyScheduleAsync();
        }
        catch (Exception ex)
        {
            ShowError($"工作计划加载失败: {ex.Message}");
            ShowEmptyState = true;
            EmptyState = EmptyStateViewModel.CreateForLoadFailed(LoadCalendarCommand);
        }
    }

    partial void OnSelectedEmployeeChanged(EmployeeListItem? value)
    {
        RunInBackground(ReloadScheduleContextAsync(), "重新加载排班失败");
    }

    private async Task ReloadScheduleContextAsync()
    {
        await LoadCalendarAsync();
        await LoadDailyPlanAsync(SelectedDate);
    }

    private async Task LoadEmployeesAsync()
    {
        var branchId = CurrentSession.CurrentBranchId;
        var query = _dbContext.Employees
            .AsNoTracking()
            .Include(e => e.Department)
            .Include(e => e.Role)
            .Where(e => e.BranchId == branchId && e.Status == EmployeeStatus.Active);

        if (!CurrentSession.IsAdmin)
        {
            query = query.Where(e => e.Id == CurrentSession.CurrentEmployeeId);
        }

        var employees = await query.ToListAsync();

        Employees = new ObservableCollection<EmployeeListItem>(employees.Select(e => new EmployeeListItem
        {
            Id = e.Id,
            Name = e.Name,
            EmployeeNo = e.EmployeeNo,
            DepartmentName = e.Department?.Name ?? "",
            RoleName = e.Role?.Name ?? "",
            Status = e.Status
        }));

        if (SelectedEmployee == null && Employees.Any())
        {
            SelectedEmployee = Employees.First();
        }
    }

    [RelayCommand]
    private async Task LoadCalendarAsync()
    {
        IsLoading = true;
        try
        {
            var employeeId = SelectedEmployee?.Id ?? CurrentSession.CurrentEmployeeId;
            var startDate = new DateTime(SelectedYear, SelectedMonth, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            var result = await _workScheduleService.GetCalendarAsync(new EmployeeScheduleCalendarRequest
            {
                EmployeeId = employeeId,
                StartDate = startDate,
                EndDate = endDate
            });

            if (!result.Success || result.Data == null)
            {
                ShowError(result.Message);
                return;
            }

            CalendarItems.Clear();
            foreach (var item in result.Data)
            {
                CalendarItems.Add(new CalendarScheduleItem
                {
                    Date = item.Date,
                    IsWorkday = item.IsWorkday,
                    WorkStartTime = item.WorkStartTime,
                    WorkEndTime = item.WorkEndTime,
                    TotalWorkMinutes = item.TotalWorkMinutes,
                    ScheduleType = item.ScheduleType,
                    HasChanged = item.HasChanged
                });
            }
        }
        catch (Exception ex)
        {
            ShowError($"加载数据失败: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadWeeklyScheduleAsync()
    {
        try
        {
            var branchId = CurrentSession.CurrentBranchId;
            var employees = await _dbContext.Employees
                .AsNoTracking()
                .Where(e => e.BranchId == branchId && e.Status == EmployeeStatus.Active)
                .OrderBy(e => e.Name)
                .ToListAsync();

            var weekEnd = WeekStartDate.AddDays(7);
            var schedules = await _dbContext.WorkSchedules
                .AsNoTracking()
                .Where(s => employees.Select(e => e.Id).Contains(s.EmployeeId)
                    && s.ScheduleDate >= WeekStartDate && s.ScheduleDate < weekEnd)
                .ToListAsync();

            var rows = new ObservableCollection<WeeklyScheduleRow>();
            foreach (var emp in employees)
            {
                var row = new WeeklyScheduleRow { EmployeeName = emp.Name };
                for (int i = 0; i < 7; i++)
                {
                    var day = WeekStartDate.AddDays(i);
                    var sch = schedules.FirstOrDefault(s => s.EmployeeId == emp.Id && s.ScheduleDate.Date == day.Date);
                    row.Days[i] = sch != null
                        ? $"{sch.WorkStartTime:hh\\:mm}-{sch.WorkEndTime:hh\\:mm}"
                        : "休息";
                }
                rows.Add(row);
            }
            WeeklyScheduleRows = rows;
        }
        catch (Exception ex) { ShowError($"加载周视图失败: {ex.Message}"); }
    }

    [RelayCommand]
    private async Task PreviousMonthAsync()
    {
        if (SelectedMonth == 1)
        {
            SelectedMonth = 12;
            SelectedYear--;
        }
        else
        {
            SelectedMonth--;
        }

        var day = Math.Min(SelectedDate.Day, DateTime.DaysInMonth(SelectedYear, SelectedMonth));
        await LoadDailyPlanAsync(new DateTime(SelectedYear, SelectedMonth, day));
        await LoadCalendarAsync();
    }

    [RelayCommand]
    private async Task NextMonthAsync()
    {
        if (SelectedMonth == 12)
        {
            SelectedMonth = 1;
            SelectedYear++;
        }
        else
        {
            SelectedMonth++;
        }

        var day = Math.Min(SelectedDate.Day, DateTime.DaysInMonth(SelectedYear, SelectedMonth));
        await LoadDailyPlanAsync(new DateTime(SelectedYear, SelectedMonth, day));
        await LoadCalendarAsync();
    }

    [RelayCommand]
    private async Task LoadDailyPlanAsync(DateTime? date)
    {
        if (!date.HasValue) return;

        SelectedDate = date.Value;
        var employeeId = SelectedEmployee?.Id ?? CurrentSession.CurrentEmployeeId;

        var result = await _workPlanService.GetDailyPlanAsync(employeeId, date.Value.Date);
        if (!result.Success || result.Data == null)
        {
            ShowError(result.Message);
            return;
        }

        var daily = result.Data;
        var schedule = daily.Schedule;
        CurrentDailyPlan = new DailyPlanView
        {
            Date = date.Value,
            EmployeeId = employeeId,
            EmployeeName = daily.EmployeeName,
            Schedule = schedule != null ? new WorkScheduleListItem
            {
                Id = schedule.Id,
                EmployeeId = schedule.EmployeeId,
                ScheduleDate = schedule.ScheduleDate,
                WorkStartTime = schedule.WorkStartTime,
                WorkEndTime = schedule.WorkEndTime,
                BreakStartTime = schedule.BreakStartTime,
                BreakEndTime = schedule.BreakEndTime,
                TotalWorkMinutes = schedule.TotalWorkMinutes,
                ScheduleType = schedule.ScheduleType,
                HasChanged = schedule.HasChanged
            } : null,
            Plans = daily.Plans,
            TotalWorkMinutes = daily.TotalWorkMinutes,
            TotalPlanMinutes = daily.TotalPlanMinutes
        };
    }

    private async Task LoadDraftsAsync()
    {
        var employeeId = CurrentSession.CurrentEmployeeId;
        var result = await _planDraftService.GetListAsync(employeeId);
        if (!result.Success || result.Data == null)
        {
            ShowError(result.Message);
            return;
        }

        Drafts = new ObservableCollection<PlanDraftItem>(result.Data.Select(d => new PlanDraftItem
        {
            Id = d.Id,
            Title = d.Content.Length > 50 ? d.Content.Substring(0, 50) + "..." : d.Content,
            Content = d.Content,
            DraftType = d.DraftType,
            CreatedAt = d.CreatedAt
        }));
    }

    [RelayCommand]
    private async Task CreateScheduleAsync()
    {
        var employeeId = SelectedEmployee?.Id ?? CurrentSession.CurrentEmployeeId;
        if (employeeId == 0) { ShowError("请先选择员工"); return; }

        try
        {
            var result = await _workScheduleService.CreateAsync(new CreateWorkScheduleRequest
            {
                EmployeeId = employeeId,
                ScheduleDate = SelectedDate.Date,
                WorkStartTime = new TimeSpan(9, 0, 0),
                WorkEndTime = new TimeSpan(18, 0, 0),
                ScheduleType = "Normal"
            }, CurrentSession.CurrentEmployeeId);

            if (!result.Success)
            {
                ShowError(result.Message);
                return;
            }

            ShowSuccess("排班已创建");
            await LoadCalendarAsync();
            await LoadDailyPlanAsync(SelectedDate);
        }
        catch (Exception ex) { ShowError($"创建排班失败: {ex.Message}"); }
    }

    [RelayCommand]
    private async Task SaveScheduleAsync()
    {
        if (CurrentDailyPlan?.Schedule == null) return;

        try
        {
            var schedule = CurrentDailyPlan.Schedule;
            var request = new CreateWorkScheduleRequest
            {
                EmployeeId = schedule.EmployeeId,
                ScheduleDate = schedule.ScheduleDate,
                WorkStartTime = schedule.WorkStartTime,
                WorkEndTime = schedule.WorkEndTime,
                BreakStartTime = schedule.BreakStartTime,
                BreakEndTime = schedule.BreakEndTime,
                ScheduleType = schedule.ScheduleType
            };

            bool success;
            string message;
            if (schedule.Id > 0)
            {
                var updateResult = await _workScheduleService.UpdateAsync(schedule.Id, request, CurrentSession.CurrentEmployeeId);
                success = updateResult.Success; message = updateResult.Message;
            }
            else
            {
                var createResult = await _workScheduleService.CreateAsync(request, CurrentSession.CurrentEmployeeId);
                success = createResult.Success; message = createResult.Message;
            }
            if (!success)
            {
                ShowError(message);
                return;
            }

            if (schedule.Id > 0 && schedule.EmployeeId != CurrentSession.CurrentEmployeeId)
            {
                await NotifyEmployeeScheduleChangeAsync(schedule.EmployeeId);
            }

            ShowSuccess("保存成功");
            IsEditMode = false;
            await LoadCalendarAsync();
        }
        catch (Exception ex)
        {
            ShowError($"保存失败: {ex.Message}");
        }
    }

    private async Task NotifyEmployeeScheduleChangeAsync(int employeeId)
    {
        try
        {
            var encryptionService = App.Services.GetService(typeof(IEncryptionService)) as IEncryptionService;
            var httpClientFactory = App.Services.GetService(typeof(IHttpClientFactory)) as IHttpClientFactory;
            if (encryptionService == null || httpClientFactory == null) return;

            var weChatService = new WeChatService(httpClientFactory, encryptionService, _dbContext);
            var schedule = CurrentDailyPlan?.Schedule;
            if (schedule == null) return;

            var scheduleInfo = $"{schedule.ScheduleDate:yyyy-MM-dd}\n工作时间: {schedule.WorkStartTime} - {schedule.WorkEndTime}";
            await weChatService.NotifyScheduleChangeAsync(employeeId, scheduleInfo);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "通知排班变动失败");
        }
    }

    [RelayCommand]
    private async Task CreatePlanAsync()
    {
        var employeeId = SelectedEmployee?.Id ?? CurrentSession.CurrentEmployeeId;
        if (employeeId == 0) { ShowError("请先选择员工"); return; }

        try
        {
            var result = await _workPlanService.CreateAsync(new CreateWorkPlanRequest
            {
                EmployeeId = employeeId,
                PlanDate = SelectedDate.Date,
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(10, 0, 0),
                Content = "新工作计划",
                PlanType = "Other"
            });
            if (!result.Success)
            {
                ShowError(result.Message);
                return;
            }

            ShowSuccess("工作计划已创建");
            await LoadDailyPlanAsync(SelectedDate);
        }
        catch (Exception ex) { ShowError($"创建计划失败: {ex.Message}"); }
    }

    private async Task SaveAsDraftAsync(string title, string content)
    {
        try
        {
            var result = await _planDraftService.SaveAsync(new SavePlanDraftRequest
            {
                EmployeeId = CurrentSession.CurrentEmployeeId,
                PlanDate = SelectedDate.Date,
                Content = $"标题:{title}\n{content}",
                DraftType = "Personal"
            }, CurrentSession.CurrentEmployeeId);

            if (!result.Success)
            {
                ShowError(result.Message);
                return;
            }

            ShowSuccess("草稿保存成功");
            await LoadDraftsAsync();
        }
        catch (Exception ex)
        {
            ShowError($"保存失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task PublishDraftAsync(PlanDraftItem? draft)
    {
        if (draft == null) return;

        try
        {
            var result = await _planDraftService.PublishAsync(draft.Id, CurrentSession.CurrentEmployeeId);
            if (!result.Success)
            {
                ShowError(result.Message);
                return;
            }

            ShowSuccess("草稿已发布");
            await LoadDraftsAsync();
        }
        catch (Exception ex)
        {
            ShowError($"发布失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DeleteDraftAsync(PlanDraftItem? draft)
    {
        if (draft == null) return;

        try
        {
            var result = await _planDraftService.DeleteAsync(draft.Id);
            if (!result.Success)
            {
                ShowError(result.Message);
                return;
            }

            ShowSuccess("草稿已删除");
            await LoadDraftsAsync();
        }
        catch (Exception ex)
        {
            ShowError($"删除失败: {ex.Message}");
        }
    }
}

public class PlanDraftItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string DraftType { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class DailyPlanView
{
    public DateTime Date { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = "";
    public WorkScheduleListItem? Schedule { get; set; }
    public List<WorkPlanDto> Plans { get; set; } = [];
    public int TotalWorkMinutes { get; set; }
    public int TotalPlanMinutes { get; set; }
}

public class WorkScheduleListItem
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public DateTime ScheduleDate { get; set; }
    public TimeSpan WorkStartTime { get; set; }
    public TimeSpan WorkEndTime { get; set; }
    public TimeSpan? BreakStartTime { get; set; }
    public TimeSpan? BreakEndTime { get; set; }
    public int TotalWorkMinutes { get; set; }
    public string ScheduleType { get; set; } = "";
    public bool HasChanged { get; set; }
}

public class CalendarScheduleItem
{
    public DateTime Date { get; set; }
    public bool IsWorkday { get; set; }
    public string? WorkStartTime { get; set; }
    public string? WorkEndTime { get; set; }
    public int TotalWorkMinutes { get; set; }
    public string ScheduleType { get; set; } = "";
    public bool HasChanged { get; set; }
}

public class WeeklyScheduleRow
{
    public string EmployeeName { get; set; } = "";
    public string DayMon { get => Days[0]; set => Days[0] = value; }
    public string DayTue { get => Days[1]; set => Days[1] = value; }
    public string DayWed { get => Days[2]; set => Days[2] = value; }
    public string DayThu { get => Days[3]; set => Days[3] = value; }
    public string DayFri { get => Days[4]; set => Days[4] = value; }
    public string DaySat { get => Days[5]; set => Days[5] = value; }
    public string DaySun { get => Days[6]; set => Days[6] = value; }
    public string[] Days { get; set; } = new string[7];
}
