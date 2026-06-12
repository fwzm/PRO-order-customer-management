using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.WebApi.Filters;

namespace PRO.WebApi.Controllers;

/// <summary>
/// 认证控制器 — 登录/登出/修改密码
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService authService, IConfiguration configuration) : ControllerBase
{
    private readonly IAuthService _authService = authService;
    private readonly IConfiguration _configuration = configuration;

    /// <summary>
    /// 用户登录
    /// </summary>
    [HttpPost("login")]
    [ServiceFilter(typeof(AuditLogFilter))]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);
        if (!result.Success)
            return BadRequest(result);

        if (result.Data != null)
            result.Data.Token = GenerateJwtToken(result.Data);

        return Ok(result);
    }

    /// <summary>
    /// 用户登出
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [ServiceFilter(typeof(AuditLogFilter))]
    public async Task<IActionResult> Logout([FromBody] int employeeId)
    {
        var result = await _authService.LogoutAsync(employeeId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// 修改密码
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var result = await _authService.ChangePasswordAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// 刷新Token
    /// </summary>
    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] string token)
    {
        var result = await _authService.RefreshTokenAsync(token);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    private string GenerateJwtToken(LoginResponse user)
    {
        var jwtKey = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key 未配置。");
        if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
            throw new InvalidOperationException("Jwt:Key 长度不足，至少需要 32 字节。");

        var issuer = _configuration["Jwt:Issuer"] ?? "PRO-System";
        var expireHours = int.TryParse(_configuration["Jwt:ExpireHours"], out var hours) && hours > 0
            ? hours
            : 8;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.EmployeeId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(ClaimTypes.NameIdentifier, user.EmployeeId.ToString()),
            new("EmployeeId", user.EmployeeId.ToString()),
            new("EmployeeNo", user.EmployeeNo),
            new(ClaimTypes.Name, user.Name),
            new("BranchId", user.BranchId.ToString()),
            new("RoleId", user.RoleId.ToString()),
            new("RoleType", user.RoleType.ToString()),
            new(ClaimTypes.Role, user.RoleType.ToString())
        };

        claims.AddRange(user.Permissions
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(p => new Claim("Permission", p)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            issuer: issuer,
            audience: issuer,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddHours(expireHours),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}
