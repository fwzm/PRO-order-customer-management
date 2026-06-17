using System.Security.Claims;
using System.Linq.Expressions;

namespace PRO.WebApi.Filters;

/// <summary>
/// 分公司数据范围过滤 - 防止跨分公司数据泄露
/// </summary>
public class BranchDataFilter(IHttpContextAccessor httpContextAccessor)
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    /// <summary>
    /// 获取当前用户的分公司ID
    /// </summary>
    public int GetCurrentBranchId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user == null) return 0;

        var branchClaim = user.FindFirst("BranchId");
        if (branchClaim != null && int.TryParse(branchClaim.Value, out var branchId))
            return branchId;

        return 0;
    }

    /// <summary>
    /// 是否总部管理员
    /// </summary>
    public bool IsHeadquartersAdmin()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user == null) return false;

        var roleClaim = user.FindFirst("RoleType");
        return roleClaim?.Value == "HeadquartersAdmin";
    }

    /// <summary>
    /// 获取分公司过滤条件（用于查询）
    /// 总部管理员返回null表示不过滤
    /// </summary>
    public int? GetBranchFilter()
    {
        if (IsHeadquartersAdmin())
            return null; // 总部管理员可查看所有分公司

        var branchId = GetCurrentBranchId();
        return branchId > 0 ? branchId : null;
    }

    /// <summary>
    /// 应用请求分公司范围：总部保留请求值，分公司用户强制使用当前分公司
    /// </summary>
    public int? ApplyBranchScope(int? requestedBranchId)
    {
        if (IsHeadquartersAdmin())
            return requestedBranchId;

        var branchId = GetCurrentBranchId();
        return branchId > 0 ? branchId : null;
    }

    /// <summary>
    /// 获取当前分公司ID；未分配分公司时抛出未授权异常
    /// </summary>
    public int RequireCurrentBranchId()
    {
        var branchId = GetCurrentBranchId();
        if (branchId <= 0)
            throw new UnauthorizedAccessException("当前用户未分配分公司");

        return branchId;
    }

    /// <summary>
    /// 验证数据是否属于当前用户分公司
    /// </summary>
    public bool IsAccessible(int dataBranchId)
    {
        if (IsHeadquartersAdmin())
            return true; // 总部管理员可访问所有

        var currentBranchId = GetCurrentBranchId();
        return currentBranchId > 0 && currentBranchId == dataBranchId;
    }

    /// <summary>
    /// 验证并抛出异常（如果不属于当前分公司）
    /// </summary>
    public void EnsureAccessible(int dataBranchId, string resourceName = "数据")
    {
        if (!IsAccessible(dataBranchId))
        {
            throw new UnauthorizedAccessException($"无权访问其他分公司的{resourceName}");
        }
    }

    /// <summary>
    /// 获取数据范围描述（用于日志）
    /// </summary>
    public string GetScopeDescription()
    {
        if (IsHeadquartersAdmin())
            return "全部分公司";

        var branchId = GetCurrentBranchId();
        return branchId > 0 ? $"分公司{branchId}" : "未分配分公司";
    }
}

/// <summary>
/// 分公司数据过滤扩展方法
/// </summary>
public static class BranchDataFilterExtensions
{
    /// <summary>
    /// 应用分公司过滤到可查询对象
    /// </summary>
    public static IQueryable<T> ApplyBranchFilter<T>(
        this IQueryable<T> query,
        BranchDataFilter filter,
        Expression<Func<T, int>> branchIdSelector) where T : class
    {
        var branchFilter = filter.GetBranchFilter();
        if (branchFilter.HasValue)
        {
            var parameter = branchIdSelector.Parameters[0];
            var body = Expression.Equal(branchIdSelector.Body, Expression.Constant(branchFilter.Value));
            query = query.Where(Expression.Lambda<Func<T, bool>>(body, parameter));
        }
        return query;
    }
}
