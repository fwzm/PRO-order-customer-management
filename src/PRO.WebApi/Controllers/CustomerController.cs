using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Enums;
using PRO.WebApi.Filters;

namespace PRO.WebApi.Controllers;

/// <summary>
/// 客户管理控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CustomerController : ControllerBase
{
    private readonly ICustomerService _customerService;

    public CustomerController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    /// <summary>获取客户列表（分页+筛选）</summary>
    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] PagedRequest request,
        [FromQuery] int? branchId, [FromQuery] CustomerType? customerType, [FromQuery] bool showMajorOnly = true)
    {
        var result = await _customerService.GetListAsync(request, branchId, customerType, showMajorOnly);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>获取客户详情</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _customerService.GetByIdAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>创建客户</summary>
    [HttpPost]
    [ServiceFilter(typeof(AuditLogFilter))]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequest request)
    {
        var result = await _customerService.CreateAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>更新客户</summary>
    [HttpPut("{id:int}")]
    [ServiceFilter(typeof(AuditLogFilter))]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCustomerRequest request)
    {
        request.Id = id;
        var result = await _customerService.UpdateAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>删除客户</summary>
    [HttpDelete("{id:int}")]
    [ServiceFilter(typeof(AuditLogFilter))]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _customerService.DeleteAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>客户查重</summary>
    [HttpPost("check-duplicates")]
    public async Task<IActionResult> CheckDuplicates([FromBody] CheckDuplicateRequest request)
    {
        var result = await _customerService.CheckDuplicatesAsync(request.Phone, request.Name, request.Address, request.LegalPerson);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>合并客户</summary>
    [HttpPost("merge")]
    [ServiceFilter(typeof(AuditLogFilter))]
    public async Task<IActionResult> Merge([FromBody] MergeCustomerRequest request)
    {
        var result = await _customerService.MergeCustomersAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>导出客户Excel</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] PagedRequest request, [FromQuery] int? branchId)
    {
        var result = await _customerService.ExportToExcelAsync(request, branchId);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

/// <summary>查重请求体</summary>
public class CheckDuplicateRequest
{
    public string? Phone { get; set; }
    public string? Name { get; set; }
    public string? Address { get; set; }
    public string? LegalPerson { get; set; }
}
