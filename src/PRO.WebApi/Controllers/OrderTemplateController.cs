using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.WebApi.Authorization;
using Swashbuckle.AspNetCore.Annotations;

namespace PRO.WebApi.Controllers;

/// <summary>
/// 订单模板管理控制器
/// </summary>
[Authorize]
public class OrderTemplateController(IOrderTemplateService templateService) : BaseApiController
{
    private readonly IOrderTemplateService _templateService = templateService;

    /// <summary>
    /// 获取模板列表
    /// </summary>
    [HttpGet]
    [SwaggerOperation(Summary = "获取用户可用的订单模板列表")]
    public async Task<IActionResult> GetTemplates([FromQuery] int? customerId)
    {
        var result = await _templateService.GetTemplatesAsync(CurrentEmployeeId, customerId);
        return Ok(result);
    }

    /// <summary>
    /// 获取热门模板
    /// </summary>
    [HttpGet("popular")]
    [SwaggerOperation(Summary = "获取使用次数最多的模板")]
    public async Task<IActionResult> GetPopular([FromQuery] int limit = 10)
    {
        var result = await _templateService.GetPopularTemplatesAsync(CurrentEmployeeId, limit);
        return Ok(result);
    }

    /// <summary>
    /// 创建模板
    /// </summary>
    [HttpPost]
    [SwaggerOperation(Summary = "创建新的订单模板")]
    public async Task<IActionResult> Create([FromBody] CreateOrderTemplateRequest request)
    {
        var result = await _templateService.CreateTemplateAsync(request, CurrentEmployeeId);
        return result.Success
            ? CreatedAtAction(nameof(GetTemplates), null, result)
            : BadRequest(result);
    }

    /// <summary>
    /// 从订单创建模板
    /// </summary>
    [HttpPost("from-order")]
    [SwaggerOperation(Summary = "从已有订单创建模板")]
    public async Task<IActionResult> CreateFromOrder([FromBody] CreateTemplateFromOrderRequest request)
    {
        var result = await _templateService.CreateTemplateFromOrderAsync(request, CurrentEmployeeId);
        return result.Success
            ? CreatedAtAction(nameof(GetTemplates), null, result)
            : BadRequest(result);
    }

    /// <summary>
    /// 更新模板
    /// </summary>
    [HttpPut("{id}")]
    [SwaggerOperation(Summary = "更新订单模板")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateOrderTemplateRequest request)
    {
        var result = await _templateService.UpdateTemplateAsync(id, request, CurrentEmployeeId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// 启用/停用模板
    /// </summary>
    [HttpPost("{id}/toggle")]
    [SwaggerOperation(Summary = "启用或停用模板（软删除）")]
    public async Task<IActionResult> ToggleStatus(int id, [FromBody] ToggleStatusRequest request)
    {
        var result = await _templateService.ToggleStatusAsync(id, request.IsActive, CurrentEmployeeId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// 删除模板
    /// </summary>
    [HttpDelete("{id}")]
    [SwaggerOperation(Summary = "删除订单模板（软删除）")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _templateService.DeleteTemplateAsync(id, CurrentEmployeeId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// 从模板创建订单
    /// </summary>
    [HttpPost("{id}/create-order")]
    [Authorize(Policy = PermissionPolicies.OrderCreate)]
    [SwaggerOperation(Summary = "使用模板快速创建订单")]
    public async Task<IActionResult> CreateOrderFromTemplate(int id, [FromQuery] DateTime? deliveryTime)
    {
        var result = await _templateService.CreateOrderFromTemplateAsync(id, CurrentEmployeeId, deliveryTime);
        return result.Success
            ? CreatedAtAction("GetById", "Orders", new { id = result.Data }, result)
            : BadRequest(result);
    }
}

/// <summary>
/// 模板状态切换请求
/// </summary>
public class ToggleStatusRequest
{
    public bool IsActive { get; set; }
}
