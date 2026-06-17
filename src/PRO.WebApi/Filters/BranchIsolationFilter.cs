using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PRO.Application.DTOs;

namespace PRO.WebApi.Filters;

/// <summary>
/// 分公司数据隔离特性
/// 自动验证请求数据是否属于当前用户分公司
/// </summary>
/// <remarks>
/// 分公司数据隔离
/// </remarks>
/// <param name="branchIdProperty">请求对象中的分公司ID属性名，默认为"BranchId"</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class BranchIsolationAttribute(string branchIdProperty = "BranchId") : ActionFilterAttribute
{
    private readonly string _branchIdProperty = branchIdProperty;

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var branchFilter = context.HttpContext.RequestServices.GetRequiredService<BranchDataFilter>();

        // 总部管理员跳过检查
        if (branchFilter.IsHeadquartersAdmin())
        {
            base.OnActionExecuting(context);
            return;
        }

        var currentBranchId = branchFilter.GetCurrentBranchId();
        if (currentBranchId <= 0)
        {
            context.Result = Forbidden("当前用户未分配分公司");
            return;
        }

        // 检查请求参数中的分公司ID
        foreach (var param in context.ActionArguments.Values)
        {
            if (param == null) continue;

            var prop = param.GetType().GetProperty(_branchIdProperty);
            if (prop != null && prop.PropertyType == typeof(int))
            {
                var requestBranchId = (int)prop.GetValue(param)!;
                if (requestBranchId > 0 && requestBranchId != currentBranchId)
                {
                    context.Result = Forbidden("无权操作其他分公司数据");
                    return;
                }
            }

            // 检查可空int类型
            var nullableProp = param.GetType().GetProperty(_branchIdProperty);
            if (nullableProp != null && nullableProp.PropertyType == typeof(int?))
            {
                var requestBranchId = (int?)nullableProp.GetValue(param);
                if (requestBranchId.HasValue && requestBranchId.Value != currentBranchId)
                {
                    context.Result = Forbidden("无权操作其他分公司数据");
                    return;
                }
            }
        }

        base.OnActionExecuting(context);
    }

    private static ObjectResult Forbidden(string message)
    {
        return new ObjectResult(ApiResponse<object>.Fail(message))
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
    }
}

/// <summary>
/// 数据归属验证特性
/// 验证查询结果的数据是否属于当前用户分公司
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class VerifyDataOwnershipAttribute(string responseDataBranchIdProperty = "BranchId") : ActionFilterAttribute
{
    private readonly string _responseDataBranchIdProperty = responseDataBranchIdProperty;

    public override void OnActionExecuted(ActionExecutedContext context)
    {
        if (context.Result is ObjectResult objectResult && objectResult.Value != null)
        {
            var branchFilter = context.HttpContext.RequestServices.GetRequiredService<BranchDataFilter>();

            // 总部管理员跳过检查
            if (branchFilter.IsHeadquartersAdmin())
            {
                base.OnActionExecuted(context);
                return;
            }

            // 检查响应数据中的分公司ID
            var valueType = objectResult.Value.GetType();

            // 处理 ApiResponse<T> 包装
            var dataProp = valueType.GetProperty("Data");
            if (dataProp != null)
            {
                var data = dataProp.GetValue(objectResult.Value);
                if (data != null)
                {
                    var branchProp = data.GetType().GetProperty(_responseDataBranchIdProperty);
                    if (branchProp != null && branchProp.PropertyType == typeof(int))
                    {
                        var dataBranchId = (int)branchProp.GetValue(data)!;
                        if (!branchFilter.IsAccessible(dataBranchId))
                        {
                            context.Result = new ObjectResult(ApiResponse<object>.Fail("无权访问该数据"))
                            {
                                StatusCode = StatusCodes.Status403Forbidden
                            };
                            return;
                        }
                    }
                }
            }
        }

        base.OnActionExecuted(context);
    }
}

/// <summary>
/// 分公司范围查询特性
/// 自动为查询添加分公司过滤条件
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class AutoBranchFilterAttribute(string branchIdParameter = "branchId") : ActionFilterAttribute
{
    private readonly string _branchIdParameter = branchIdParameter;

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var branchFilter = context.HttpContext.RequestServices.GetRequiredService<BranchDataFilter>();

        // 总部管理员可以指定分公司或查看全部
        if (branchFilter.IsHeadquartersAdmin())
        {
            base.OnActionExecuting(context);
            return;
        }

        // 非总部管理员：强制使用当前分公司ID
        var currentBranchId = branchFilter.GetCurrentBranchId();
        if (currentBranchId <= 0)
        {
            context.Result = new ObjectResult(ApiResponse<object>.Fail("当前用户未分配分公司"))
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        if (context.ActionArguments.ContainsKey(_branchIdParameter))
        {
            context.ActionArguments[_branchIdParameter] = currentBranchId;
        }
        else
        {
            // 尝试找到包含branchId参数的请求对象
            foreach (var param in context.ActionArguments.Values)
            {
                if (param == null) continue;

                var prop = param.GetType().GetProperty("BranchId",
                    System.Reflection.BindingFlags.IgnoreCase |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Instance);

                if (prop != null && (prop.PropertyType == typeof(int) || prop.PropertyType == typeof(int?)))
                {
                    prop.SetValue(param, currentBranchId);
                    break;
                }
            }
        }

        base.OnActionExecuting(context);
    }
}
