using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace PRO.WebApi.Controllers;

/// <summary>
/// API 基础控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
public abstract class BaseApiController : ControllerBase
{
    /// <summary>
    /// 获取当前登录员工ID
    /// </summary>
    protected int CurrentEmployeeId
    {
        get
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null ? int.Parse(claim.Value) : 0;
        }
    }

    /// <summary>
    /// 获取当前登录员工工号
    /// </summary>
    protected string CurrentEmployeeNo
    {
        get
        {
            return User.FindFirst("EmployeeNo")?.Value ?? "";
        }
    }

    /// <summary>
    /// 获取当前登录员工姓名
    /// </summary>
    protected string CurrentEmployeeName
    {
        get
        {
            return User.FindFirst(ClaimTypes.Name)?.Value ?? "";
        }
    }

    /// <summary>
    /// 获取当前分公司ID
    /// </summary>
    protected int CurrentBranchId
    {
        get
        {
            var claim = User.FindFirst("BranchId");
            return claim != null ? int.Parse(claim.Value) : 0;
        }
    }

    /// <summary>
    /// 获取当前角色类型
    /// </summary>
    protected string CurrentRoleType
    {
        get
        {
            return User.FindFirst("RoleType")?.Value ?? "";
        }
    }

    /// <summary>
    /// 是否总部管理员
    /// </summary>
    protected bool IsHeadquartersAdmin => CurrentRoleType == "HeadquartersAdmin";

    /// <summary>
    /// 统一成功响应
    /// </summary>
    protected IActionResult ApiOk<T>(T data, string message = "操作成功")
    {
        return Ok(Application.DTOs.ApiResponse<T>.Ok(data, message));
    }

    /// <summary>
    /// 统一失败响应
    /// </summary>
    protected IActionResult ApiFail(string message, List<string>? errors = null)
    {
        return BadRequest(Application.DTOs.ApiResponse<object>.Fail(message, errors));
    }
}
