using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;

namespace PRO.WebApi.Authorization;

/// <summary>
/// 权限需求
/// </summary>
public class PermissionRequirement(string permission, params string[] compatiblePermissions) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
    public IReadOnlyList<string> AcceptedPermissions { get; } = [.. new[] { permission }
            .Concat(compatiblePermissions)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)];
}

/// <summary>
/// 权限处理器 - 优先使用 JWT 权限声明，并回查数据库兼容权限变更
/// </summary>
public class PermissionHandler(IServiceScopeFactory scopeFactory, ILogger<PermissionHandler> logger) : AuthorizationHandler<PermissionRequirement>
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<PermissionHandler> _logger = logger;

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var employeeIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirst("EmployeeId");
        if (employeeIdClaim == null || !int.TryParse(employeeIdClaim.Value, out var employeeId))
        {
            context.Fail();
            return;
        }

        var roleTypeClaim = context.User.FindFirst("RoleType");
        if (string.Equals(roleTypeClaim?.Value, RoleType.HeadquartersAdmin.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
            return;
        }

        try
        {
            var claimPermissions = context.User.FindAll("Permission")
                .Select(c => c.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToList();

            if (HasRequiredPermission(claimPermissions, requirement.AcceptedPermissions))
            {
                context.Succeed(requirement);
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ProDbContext>();

            var employee = await dbContext.Employees
                .AsNoTracking()
                .Include(e => e.Role)
                .FirstOrDefaultAsync(e => e.Id == employeeId && e.Status == EmployeeStatus.Active);

            if (employee?.Role?.RoleType == RoleType.HeadquartersAdmin)
            {
                context.Succeed(requirement);
                return;
            }

            if (employee == null)
            {
                context.Fail();
                return;
            }

            var rolePermissions = await dbContext.RolePermissions
                .AsNoTracking()
                .Include(rp => rp.Permission)
                .Where(rp => rp.RoleId == employee.RoleId
                    && rp.Permission != null
                    && rp.IsAllowed)
                .Select(rp => rp.Permission!.Code)
                .ToListAsync();

            if (HasRequiredPermission(rolePermissions, requirement.AcceptedPermissions))
            {
                context.Succeed(requirement);
            }
            else
            {
                _logger.LogWarning("员工 {EmployeeId} 缺少权限 {Permission}", employeeId, requirement.Permission);
                context.Fail();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "权限检查失败: {Permission}", requirement.Permission);
            context.Fail();
        }
    }

    private static bool HasRequiredPermission(IEnumerable<string> grantedPermissions, IEnumerable<string> acceptedPermissions)
    {
        return grantedPermissions.Any(granted =>
            acceptedPermissions.Any(accepted => IsPermissionSatisfiedBy(granted, accepted)));
    }

    private static bool IsPermissionSatisfiedBy(string grantedPermission, string acceptedPermission)
    {
        if (string.Equals(grantedPermission, acceptedPermission, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!grantedPermission.EndsWith(".*", StringComparison.Ordinal))
            return false;

        var modulePrefix = grantedPermission[..^1];
        return acceptedPermission.StartsWith(modulePrefix, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// 权限授权属性
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permission)
    {
        Policy = permission;
    }
}
