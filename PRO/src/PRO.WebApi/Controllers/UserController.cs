using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;

namespace PRO.WebApi.Controllers;

/// <summary>
/// 用户信息控制器 — 为移动端 App 提供当前登录用户信息
/// </summary>
[ApiController]
[Route("api/user")]
[Authorize]
public class UserController(IEmployeeService employeeService) : ControllerBase
{
    private readonly IEmployeeService _employeeService = employeeService;

    /// <summary>
    /// 获取当前登录用户信息
    /// </summary>
    [HttpGet("profile")]
    [SwaggerOperation(Summary = "获取当前用户信息", Description = "返回当前 JWT Token 对应的用户基本信息，用于 App 个人信息页")]
    public async Task<IActionResult> GetProfile()
    {
        var employeeIdClaim = User.FindFirst("EmployeeId")?.Value
                           ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(employeeIdClaim) || !int.TryParse(employeeIdClaim, out var employeeId))
            return Unauthorized(ApiResponse<object>.Fail("无效的用户身份"));

        var result = await _employeeService.GetByIdAsync(employeeId);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
