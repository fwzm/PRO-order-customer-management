using Microsoft.EntityFrameworkCore;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 员工服务实现
/// </summary>
public class EmployeeService : IEmployeeService
{
    private readonly ProDbContext _dbContext;
    private readonly IEncryptionService _encryptionService;

    public EmployeeService(ProDbContext dbContext, IEncryptionService encryptionService)
    {
        _dbContext = dbContext;
        _encryptionService = encryptionService;
    }

    public async Task<ApiResponse<PagedResult<EmployeeListItem>>> GetListAsync(PagedRequest request)
    {
        try
        {
            var query = _dbContext.Employees.AsNoTracking()
                .Include(e => e.Branch)
                .Include(e => e.Department)
                .Include(e => e.Role)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.Keyword))
                query = query.Where(e => e.Name.Contains(request.Keyword) || e.EmployeeNo.Contains(request.Keyword));

            var totalCount = await query.CountAsync();
            var items = await query.OrderBy(e => e.EmployeeNo)
                .Skip((request.PageIndex - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(e => new EmployeeListItem
                {
                    Id = e.Id,
                    Name = e.Name,
                    EmployeeNo = e.EmployeeNo,
                    DepartmentName = e.Department != null ? e.Department.Name : "",
                    DepartmentId = e.DepartmentId,
                    BranchName = e.Branch != null ? e.Branch.Name : "",
                    BranchId = e.BranchId,
                    RoleName = e.Role != null ? e.Role.Name : "",
                    Phone = e.Phone,
                    Status = e.Status,
                    CreatedAt = e.CreatedAt
                })
                .ToListAsync();

            return ApiResponse<PagedResult<EmployeeListItem>>.Ok(new PagedResult<EmployeeListItem>
            {
                Items = items,
                TotalCount = totalCount,
                PageIndex = request.PageIndex,
                PageSize = request.PageSize
            });
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<EmployeeListItem>>.Fail($"查询员工列表失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<EmployeeListItem>> GetByIdAsync(int id)
    {
        try
        {
            var employee = await _dbContext.Employees.AsNoTracking()
                .Include(e => e.Branch)
                .Include(e => e.Department)
                .Include(e => e.Role)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee == null)
                return ApiResponse<EmployeeListItem>.Fail("员工不存在");

            return ApiResponse<EmployeeListItem>.Ok(new EmployeeListItem
            {
                Id = employee.Id,
                Name = employee.Name,
                EmployeeNo = employee.EmployeeNo,
                DepartmentName = employee.Department?.Name ?? "",
                DepartmentId = employee.DepartmentId,
                BranchName = employee.Branch?.Name ?? "",
                BranchId = employee.BranchId,
                RoleName = employee.Role?.Name ?? "",
                Phone = employee.Phone,
                Status = employee.Status,
                CreatedAt = employee.CreatedAt
            });
        }
        catch (Exception ex)
        {
            return ApiResponse<EmployeeListItem>.Fail($"查询员工详情失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<int>> CreateAsync(CreateEmployeeRequest request)
    {
        try
        {
            if (await _dbContext.Employees.AnyAsync(e => e.EmployeeNo == request.EmployeeNo))
                return ApiResponse<int>.Fail("工号已存在");

            var employee = new Employee
            {
                Name = request.Name,
                EmployeeNo = request.EmployeeNo,
                PasswordHash = _encryptionService.HashPassword(request.Password),
                DepartmentId = request.DepartmentId,
                BranchId = request.BranchId,
                RoleId = request.RoleId,
                Phone = request.Phone,
                Email = request.Email,
                Gender = request.Gender,
                Status = EmployeeStatus.Active,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _dbContext.Employees.Add(employee);
            await _dbContext.SaveChangesAsync();

            return ApiResponse<int>.Ok(employee.Id, "员工创建成功");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"创建员工失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> UpdateAsync(UpdateEmployeeRequest request)
    {
        try
        {
            var employee = await _dbContext.Employees.FindAsync(request.Id);
            if (employee == null)
                return ApiResponse<bool>.Fail("员工不存在");

            employee.Name = request.Name;
            employee.DepartmentId = request.DepartmentId;
            employee.RoleId = request.RoleId;
            employee.Phone = request.Phone;
            employee.Email = request.Email;
            employee.Gender = request.Gender;
            employee.Status = request.Status;
            employee.UpdatedAt = DateTime.Now;

            await _dbContext.SaveChangesAsync();
            return ApiResponse<bool>.Ok(true, "员工更新成功");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"更新员工失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> UpdatePasswordAsync(int id, string newPassword)
    {
        try
        {
            var employee = await _dbContext.Employees.FindAsync(id);
            if (employee == null)
                return ApiResponse<bool>.Fail("员工不存在");

            employee.PasswordHash = _encryptionService.HashPassword(newPassword);
            employee.UpdatedAt = DateTime.Now;

            await _dbContext.SaveChangesAsync();
            return ApiResponse<bool>.Ok(true, "密码修改成功");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"修改密码失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> UpdateStatusAsync(int id, EmployeeStatus status)
    {
        try
        {
            var employee = await _dbContext.Employees.FindAsync(id);
            if (employee == null)
                return ApiResponse<bool>.Fail("员工不存在");

            employee.Status = status;
            employee.UpdatedAt = DateTime.Now;

            await _dbContext.SaveChangesAsync();
            return ApiResponse<bool>.Ok(true, "状态更新成功");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"更新状态失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> ResetPasswordAsync(int id, string newPassword)
    {
        try
        {
            var employee = await _dbContext.Employees.FindAsync(id);
            if (employee == null)
                return ApiResponse<bool>.Fail("员工不存在");

            employee.PasswordHash = _encryptionService.HashPassword(newPassword);
            employee.LoginFailCount = 0;
            employee.LockedUntil = null;
            employee.UpdatedAt = DateTime.Now;

            await _dbContext.SaveChangesAsync();
            return ApiResponse<bool>.Ok(true, "密码重置成功");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"重置密码失败: {ex.Message}");
        }
    }
}
