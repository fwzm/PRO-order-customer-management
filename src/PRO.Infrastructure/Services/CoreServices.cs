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
