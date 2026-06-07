using Microsoft.EntityFrameworkCore;
using PRO.Application.DTOs;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;
using PRO.Application.Interfaces;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 认证服务实现
/// </summary>
public class AuthService : IAuthService
{
    private readonly ProDbContext _dbContext;
    private readonly IEncryptionService _encryptionService;

    public AuthService(ProDbContext dbContext, IEncryptionService encryptionService)
    {
        _dbContext = dbContext;
        _encryptionService = encryptionService;
    }

    public async Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request)
    {
        try
        {
            var employee = await _dbContext.Employees
                .AsNoTracking()
                .Include(e => e.Role)
                .Include(e => e.Branch)
                .Include(e => e.Department)
                .FirstOrDefaultAsync(e => e.EmployeeNo == request.EmployeeNo && e.Status == EmployeeStatus.Active);

            if (employee == null)
                return ApiResponse<LoginResponse>.Fail("工号不存在或已停用");

            if (!_encryptionService.VerifyPassword(request.Password, employee.PasswordHash))
                return ApiResponse<LoginResponse>.Fail("密码错误");

            var permissions = await _dbContext.RolePermissions
                .AsNoTracking()
                .Include(rp => rp.Permission)
                .Where(rp => rp.RoleId == employee.RoleId && rp.IsAllowed && rp.Permission != null)
                .Select(rp => rp.Permission!.Code)
                .ToListAsync();

            var response = new LoginResponse
            {
                EmployeeId = employee.Id,
                EmployeeNo = employee.EmployeeNo,
                Name = employee.Name,
                BranchId = employee.BranchId,
                BranchName = employee.Branch?.Name ?? "",
                RoleId = employee.RoleId,
                RoleName = employee.Role?.Name ?? "",
                RoleType = employee.Role?.RoleType ?? RoleType.Employee,
                Token = _encryptionService.GenerateToken(),
                Permissions = permissions
            };

            // 记录登录日志
            _dbContext.OperationLogs.Add(new OperationLog
            {
                OperatorId = employee.Id,
                OperatorNo = employee.EmployeeNo,
                Module = "系统",
                OperationType = "登录",
                Content = $"员工 {employee.Name} 登录系统",
                Result = "Success"
            });
            await _dbContext.SaveChangesAsync();

            return ApiResponse<LoginResponse>.Ok(response);
        }
        catch (Exception ex)
        {
            return ApiResponse<LoginResponse>.Fail($"登录失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> LogoutAsync(int employeeId)
    {
        try
        {
            var employee = await _dbContext.Employees.FindAsync(employeeId);
            if (employee != null)
            {
                _dbContext.OperationLogs.Add(new OperationLog
                {
                    OperatorId = employeeId,
                    OperatorNo = employee.EmployeeNo,
                    Module = "系统",
                    OperationType = "登出",
                    Content = $"员工 {employee.Name} 登出系统",
                    Result = "Success"
                });
                await _dbContext.SaveChangesAsync();
            }

            return ApiResponse<bool>.Ok(true, "登出成功");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"登出失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> ChangePasswordAsync(ChangePasswordRequest request)
    {
        try
        {
            var employee = await _dbContext.Employees.FindAsync(request.EmployeeId);
            if (employee == null)
                return ApiResponse<bool>.Fail("员工不存在");

            if (!_encryptionService.VerifyPassword(request.OldPassword, employee.PasswordHash))
                return ApiResponse<bool>.Fail("原密码错误");

            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
                return ApiResponse<bool>.Fail("新密码长度不能少于6位");

            employee.PasswordHash = _encryptionService.HashPassword(request.NewPassword);
            employee.UpdatedAt = DateTime.Now;
            await _dbContext.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "密码修改成功");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"修改密码失败: {ex.Message}");
        }
    }

    public Task<ApiResponse<LoginResponse>> RefreshTokenAsync(string token)
    {
        // Token 刷新逻辑（当前版本使用会话模式，暂不实现）
        return Task.FromResult(ApiResponse<LoginResponse>.Fail("Token 刷新功能暂未实现"));
    }

    public async Task<ApiResponse<bool>> ValidateSessionAsync(int employeeId)
    {
        var employee = await _dbContext.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == employeeId && e.Status == EmployeeStatus.Active);
        return ApiResponse<bool>.Ok(employee != null);
    }
}

/// <summary>
/// 操作日志服务实现
/// </summary>
public class OperationLogService : IOperationLogService
{
    private readonly ProDbContext _dbContext;

    public OperationLogService(ProDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ApiResponse<PagedResult<OperationLogDto>>> GetListAsync(PagedRequest request, int? moduleId = null)
    {
        try
        {
            var query = _dbContext.OperationLogs
                .AsNoTracking()
                .Include(l => l.Operator)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.Keyword))
                query = query.Where(l => l.Content.Contains(request.Keyword) || l.OperatorNo.Contains(request.Keyword));

            var totalCount = await query.CountAsync();
            var items = await query.OrderByDescending(l => l.OperatedAt)
                .Skip((request.PageIndex - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(l => new OperationLogDto
                {
                    Id = l.Id,
                    OperatorId = l.OperatorId,
                    OperatorName = l.Operator != null ? l.Operator.Name : "",
                    Module = l.Module,
                    OperationType = l.OperationType,
                    Content = l.Content,
                    Result = l.Result,
                    OperatedAt = l.OperatedAt
                })
                .ToListAsync();

            return ApiResponse<PagedResult<OperationLogDto>>.Ok(new PagedResult<OperationLogDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageIndex = request.PageIndex,
                PageSize = request.PageSize
            });
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<OperationLogDto>>.Fail($"查询日志失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> CreateAsync(int operatorId, string module, string operationType,
        string content, string? entityType = null, int? entityId = null, string? result = null, string? errorMessage = null)
    {
        try
        {
            var employee = await _dbContext.Employees.FindAsync(operatorId);
            _dbContext.OperationLogs.Add(new OperationLog
            {
                OperatorId = operatorId,
                OperatorNo = employee?.EmployeeNo ?? "",
                Module = module,
                OperationType = operationType,
                Content = content,
                EntityType = entityType,
                EntityId = entityId,
                Result = result ?? "Success",
                ErrorMessage = errorMessage
            });
            await _dbContext.SaveChangesAsync();
            return ApiResponse<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"记录日志失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<PagedResult<OperationLogDetailDto>>> GetDetailListAsync(OperationLogQueryRequest request)
    {
        try
        {
            var query = _dbContext.OperationLogs
                .AsNoTracking()
                .Include(l => l.Operator)
                .AsQueryable();

            // 应用筛选条件
            if (!string.IsNullOrWhiteSpace(request.Keyword))
                query = query.Where(l => l.Content.Contains(request.Keyword) || l.OperatorNo.Contains(request.Keyword));
            if (!string.IsNullOrWhiteSpace(request.Module))
                query = query.Where(l => l.Module == request.Module);
            if (!string.IsNullOrWhiteSpace(request.OperationType))
                query = query.Where(l => l.OperationType == request.OperationType);
            if (request.OperatorId.HasValue)
                query = query.Where(l => l.OperatorId == request.OperatorId.Value);
            if (!string.IsNullOrWhiteSpace(request.OperatorNo))
                query = query.Where(l => l.OperatorNo == request.OperatorNo);
            if (request.StartDate.HasValue)
                query = query.Where(l => l.OperatedAt >= request.StartDate.Value);
            if (request.EndDate.HasValue)
                query = query.Where(l => l.OperatedAt < request.EndDate.Value.AddDays(1));
            if (!string.IsNullOrWhiteSpace(request.Result))
                query = query.Where(l => l.Result == request.Result);
            if (!string.IsNullOrWhiteSpace(request.EntityType))
                query = query.Where(l => l.EntityType == request.EntityType);
            if (request.EntityId.HasValue)
                query = query.Where(l => l.EntityId == request.EntityId.Value);

            var totalCount = await query.CountAsync();
            var items = await query.OrderByDescending(l => l.OperatedAt)
                .Skip((request.PageIndex - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(l => new OperationLogDetailDto
                {
                    Id = l.Id,
                    OperatorId = l.OperatorId,
                    OperatorName = l.Operator != null ? l.Operator.Name : "",
                    OperatorNo = l.OperatorNo,
                    Module = l.Module,
                    OperationType = l.OperationType,
                    Content = l.Content,
                    EntityType = l.EntityType,
                    EntityId = l.EntityId,
                    Result = l.Result,
                    ErrorMessage = l.ErrorMessage,
                    OperatedAt = l.OperatedAt,
                    SyncStatus = l.SyncStatus
                })
                .ToListAsync();

            return ApiResponse<PagedResult<OperationLogDetailDto>>.Ok(new PagedResult<OperationLogDetailDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageIndex = request.PageIndex,
                PageSize = request.PageSize
            });
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<OperationLogDetailDto>>.Fail($"查询日志失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<OperationLogDetailDto>> GetByIdAsync(int id)
    {
        try
        {
            var log = await _dbContext.OperationLogs
                .AsNoTracking()
                .Include(l => l.Operator)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (log == null)
                return ApiResponse<OperationLogDetailDto>.Fail("日志不存在");

            return ApiResponse<OperationLogDetailDto>.Ok(new OperationLogDetailDto
            {
                Id = log.Id,
                OperatorId = log.OperatorId,
                OperatorName = log.Operator?.Name ?? "",
                OperatorNo = log.OperatorNo,
                Module = log.Module,
                OperationType = log.OperationType,
                Content = log.Content,
                EntityType = log.EntityType,
                EntityId = log.EntityId,
                Result = log.Result,
                ErrorMessage = log.ErrorMessage,
                OperatedAt = log.OperatedAt,
                SyncStatus = log.SyncStatus
            });
        }
        catch (Exception ex)
        {
            return ApiResponse<OperationLogDetailDto>.Fail($"查询日志详情失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<OperationLogStatsDto>> GetStatsAsync(int? branchId = null, int days = 30)
    {
        try
        {
            var cutoffDate = DateTime.Now.AddDays(-days);
            var query = _dbContext.OperationLogs.AsNoTracking().Where(l => l.OperatedAt >= cutoffDate);

            var totalCount = await query.CountAsync();
            var todayCount = await query.CountAsync(l => l.OperatedAt >= DateTime.Today);
            var weekStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek + 1);
            var weekCount = await query.CountAsync(l => l.OperatedAt >= weekStart);
            var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var monthCount = await query.CountAsync(l => l.OperatedAt >= monthStart);
            var failedCount = await query.CountAsync(l => l.Result == "Failed");

            var moduleStats = await query
                .GroupBy(l => l.Module)
                .Select(g => new ModuleStats { Module = g.Key, Count = g.Count() })
                .OrderByDescending(m => m.Count)
                .ToListAsync();

            var total = moduleStats.Sum(m => m.Count);
            foreach (var ms in moduleStats)
                ms.Percentage = total > 0 ? Math.Round(ms.Count * 100.0 / total, 1) : 0;

            var dailyStats = await query
                .GroupBy(l => l.OperatedAt.Date)
                .Select(g => new DailyStats { Date = g.Key, Count = g.Count() })
                .OrderBy(d => d.Date)
                .ToListAsync();

            return ApiResponse<OperationLogStatsDto>.Ok(new OperationLogStatsDto
            {
                TotalCount = totalCount,
                TodayCount = todayCount,
                WeekCount = weekCount,
                MonthCount = monthCount,
                FailedCount = failedCount,
                ModuleStats = moduleStats,
                DailyStats = dailyStats
            });
        }
        catch (Exception ex)
        {
            return ApiResponse<OperationLogStatsDto>.Fail($"查询统计失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<List<string>>> GetModulesAsync()
    {
        try
        {
            var modules = await _dbContext.OperationLogs
                .AsNoTracking()
                .Select(l => l.Module)
                .Distinct()
                .OrderBy(m => m)
                .ToListAsync();

            return ApiResponse<List<string>>.Ok(modules);
        }
        catch (Exception ex)
        {
            return ApiResponse<List<string>>.Fail($"查询模块列表失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<List<string>>> GetOperationTypesAsync(string? module = null)
    {
        try
        {
            var query = _dbContext.OperationLogs.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(module))
                query = query.Where(l => l.Module == module);

            var types = await query
                .Select(l => l.OperationType)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync();

            return ApiResponse<List<string>>.Ok(types);
        }
        catch (Exception ex)
        {
            return ApiResponse<List<string>>.Fail($"查询操作类型失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<string>> ExportAsync(ExportOperationLogRequest request)
    {
        try
        {
            var query = _dbContext.OperationLogs.AsNoTracking().Include(l => l.Operator).AsQueryable();

            if (request.StartDate.HasValue)
                query = query.Where(l => l.OperatedAt >= request.StartDate.Value);
            if (request.EndDate.HasValue)
                query = query.Where(l => l.OperatedAt < request.EndDate.Value.AddDays(1));
            if (!string.IsNullOrWhiteSpace(request.Module))
                query = query.Where(l => l.Module == request.Module);

            var logs = await query.OrderByDescending(l => l.OperatedAt).Take(10000).ToListAsync();

            // 返回CSV格式的内存数据
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("时间,操作人,工号,模块,操作类型,内容,结果");
            foreach (var log in logs)
            {
                sb.AppendLine($"{log.OperatedAt:yyyy-MM-dd HH:mm:ss},{log.Operator?.Name},{log.OperatorNo},{log.Module},{log.OperationType},{log.Content},{log.Result}");
            }

            return ApiResponse<string>.Ok(sb.ToString());
        }
        catch (Exception ex)
        {
            return ApiResponse<string>.Fail($"导出日志失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> CleanupOldLogsAsync(int retentionDays = 90)
    {
        try
        {
            var cutoffDate = DateTime.Now.AddDays(-retentionDays);
            var oldLogs = await _dbContext.OperationLogs
                .Where(l => l.OperatedAt < cutoffDate)
                .ToListAsync();

            if (oldLogs.Any())
            {
                _dbContext.OperationLogs.RemoveRange(oldLogs);
                await _dbContext.SaveChangesAsync();
            }

            return ApiResponse<bool>.Ok(true, $"已清理 {oldLogs.Count} 条过期日志");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"清理日志失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<int>> SyncPendingLogsAsync()
    {
        try
        {
            var pendingLogs = await _dbContext.OperationLogs
                .Where(l => l.SyncStatus == SyncStatus.Pending)
                .ToListAsync();

            foreach (var log in pendingLogs)
            {
                log.SyncStatus = SyncStatus.Synced;
            }

            var count = await _dbContext.SaveChangesAsync();
            return ApiResponse<int>.Ok(count, $"已同步 {count} 条日志");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"同步日志失败: {ex.Message}");
        }
    }
}
