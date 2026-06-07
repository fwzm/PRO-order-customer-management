using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Enums;
using Swashbuckle.AspNetCore.Annotations;

namespace PRO.WebApi.Controllers;

/// <summary>
/// 客户管理控制器
/// </summary>
[Authorize]
public class CustomersController : BaseApiController
{
    private readonly ICustomerService _customerService;

    public CustomersController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    /// <summary>
    /// 获取客户列表
    /// </summary>
    [HttpGet]
    [SwaggerOperation(Summary = "获取客户列表", Description = "分页获取客户列表")]
    public async Task<IActionResult> GetList([FromQuery] PagedRequest request, [FromQuery] CustomerType? customerType)
    {
        int? branchId = IsHeadquartersAdmin ? null : CurrentBranchId;
        var result = await _customerService.GetListAsync(request, branchId, customerType);
        return Ok(result);
    }

    /// <summary>
    /// 获取客户详情
    /// </summary>
    [HttpGet("{id}")]
    [SwaggerOperation(Summary = "获取客户详情")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _customerService.GetByIdAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// 创建客户
    /// </summary>
    [HttpPost]
    [SwaggerOperation(Summary = "创建客户")]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequest request)
    {
        request.BranchId = CurrentBranchId;
        var result = await _customerService.CreateAsync(request);
        return result.Success
            ? CreatedAtAction(nameof(GetById), new { id = result.Data }, result)
            : BadRequest(result);
    }

    /// <summary>
    /// 更新客户
    /// </summary>
    [HttpPut("{id}")]
    [SwaggerOperation(Summary = "更新客户")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCustomerRequest request)
    {
        request.Id = id;
        var result = await _customerService.UpdateAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// 删除客户
    /// </summary>
    [HttpDelete("{id}")]
    [SwaggerOperation(Summary = "删除客户")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _customerService.DeleteAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// 客户查重
    /// </summary>
    [HttpPost("check-duplicates")]
    [SwaggerOperation(Summary = "客户查重")]
    public async Task<IActionResult> CheckDuplicates([FromBody] DuplicateCheckRequest request)
    {
        var result = await _customerService.CheckDuplicatesAsync(request.Phone, request.Name, request.Address, request.LegalPerson);
        return Ok(result);
    }

    /// <summary>
    /// 合并客户
    /// </summary>
    [HttpPost("merge")]
    [SwaggerOperation(Summary = "合并客户")]
    public async Task<IActionResult> Merge([FromBody] MergeCustomerRequest request)
    {
        var result = await _customerService.MergeCustomersAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

public class DuplicateCheckRequest
{
    public string? Phone { get; set; }
    public string? Name { get; set; }
    public string? Address { get; set; }
    public string? LegalPerson { get; set; }
}
