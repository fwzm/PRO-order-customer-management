using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Enums;
using PRO.WebApi.Authorization;
using PRO.WebApi.Filters;
using Swashbuckle.AspNetCore.Annotations;

namespace PRO.WebApi.Controllers;

/// <summary>
/// 客户管理控制器（V1 - 已废弃，请使用 api/Customers）
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Obsolete("请使用 CustomersController (api/Customers)。此控制器将在未来版本移除。")]
[ApiExplorerSettings(IgnoreApi = true)]
public class CustomerController(ICustomerService customerService, BranchDataFilter branchFilter) : ControllerBase
{
    private readonly ICustomerService _customerService = customerService;
    private readonly BranchDataFilter _branchFilter = branchFilter;

    /// <summary>获取客户列表（分页+筛选）</summary>
    [HttpGet]
    [Authorize(Policy = PermissionPolicies.CustomerView)]
    public async Task<IActionResult> GetList([FromQuery] PagedRequest request,
        [FromQuery] int? branchId, [FromQuery] CustomerType? customerType, [FromQuery] bool showMajorOnly = true)
    {
        branchId = _branchFilter.ApplyBranchScope(branchId);
        var result = await _customerService.GetListAsync(request, branchId, customerType, showMajorOnly);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>获取客户详情</summary>
    [HttpGet("{id:int}")]
    [Authorize(Policy = PermissionPolicies.CustomerView)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _customerService.GetByIdAsync(id);
        if (result.Success && result.Data != null)
            _branchFilter.EnsureAccessible(result.Data.BranchId, "客户");

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>创建客户</summary>
    [HttpPost]
    [Authorize(Policy = PermissionPolicies.CustomerCreate)]
    [ServiceFilter(typeof(AuditLogFilter))]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequest request)
    {
        var branchId = _branchFilter.ApplyBranchScope(request.BranchId);
        if (!branchId.HasValue)
            return BranchForbidden("当前用户未分配分公司");
        if (branchId.Value <= 0)
            return BadRequest(ApiResponse<object>.Fail("请选择有效分公司"));

        request.BranchId = branchId.Value;

        var parentValidation = await EnsureCustomerAccessibleAsync(request.ParentCustomerId, "上级客户");
        if (parentValidation != null)
            return parentValidation;

        var result = await _customerService.CreateAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>更新客户</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = PermissionPolicies.CustomerEdit)]
    [ServiceFilter(typeof(AuditLogFilter))]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCustomerRequest request)
    {
        var existing = await _customerService.GetByIdAsync(id);
        if (!existing.Success || existing.Data == null)
            return BadRequest(existing);

        _branchFilter.EnsureAccessible(existing.Data.BranchId, "客户");

        var parentValidation = await EnsureCustomerAccessibleAsync(request.ParentCustomerId, "上级客户");
        if (parentValidation != null)
            return parentValidation;

        request.Id = id;
        if (!_branchFilter.IsHeadquartersAdmin())
            request.BranchId = _branchFilter.RequireCurrentBranchId();
        if (request.BranchId <= 0)
            return BadRequest(ApiResponse<object>.Fail("请选择有效分公司"));

        var result = await _customerService.UpdateAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>删除客户</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = PermissionPolicies.CustomerDelete)]
    [ServiceFilter(typeof(AuditLogFilter))]
    public async Task<IActionResult> Delete(int id)
    {
        var customer = await _customerService.GetByIdAsync(id);
        if (!customer.Success || customer.Data == null)
            return BadRequest(customer);

        _branchFilter.EnsureAccessible(customer.Data.BranchId, "客户");

        var result = await _customerService.DeleteAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>客户查重</summary>
    [HttpPost("check-duplicates")]
    [Authorize(Policy = PermissionPolicies.CustomerView)]
    public async Task<IActionResult> CheckDuplicates([FromBody] CheckDuplicateRequest request)
    {
        var result = await _customerService.CheckDuplicatesAsync(request.Phone, request.Name, request.Address, request.LegalPerson);
        var forbiddenResult = FilterDuplicateResult(result);
        if (forbiddenResult != null)
            return forbiddenResult;

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>合并客户</summary>
    [HttpPost("merge")]
    [Authorize(Policy = PermissionPolicies.CustomerMerge)]
    [ServiceFilter(typeof(AuditLogFilter))]
    public async Task<IActionResult> Merge([FromBody] MergeCustomerRequest request)
    {
        var mainValidation = await EnsureCustomerAccessibleAsync(request.MainCustomerId, "主客户");
        if (mainValidation != null)
            return mainValidation;

        foreach (var mergedCustomerId in request.MergedCustomerIds)
        {
            var mergedValidation = await EnsureCustomerAccessibleAsync(mergedCustomerId, "被合并客户");
            if (mergedValidation != null)
                return mergedValidation;
        }

        var result = await _customerService.MergeCustomersAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>导出客户Excel</summary>
    [HttpGet("export")]
    [Authorize(Policy = PermissionPolicies.CustomerExport)]
    public async Task<IActionResult> Export([FromQuery] PagedRequest request, [FromQuery] int? branchId)
    {
        branchId = _branchFilter.ApplyBranchScope(branchId);
        var result = await _customerService.ExportToExcelAsync(request, branchId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    private async Task<IActionResult?> EnsureCustomerAccessibleAsync(int? customerId, string resourceName)
    {
        if (!customerId.HasValue)
            return null;

        var customer = await _customerService.GetByIdAsync(customerId.Value);
        if (!customer.Success || customer.Data == null)
            return BadRequest(ApiResponse<object>.Fail($"{resourceName}不存在"));

        if (!_branchFilter.IsAccessible(customer.Data.BranchId))
            return BranchForbidden($"无权访问其他分公司的{resourceName}");

        return null;
    }

    private IActionResult? FilterDuplicateResult(ApiResponse<CustomerDuplicateCheckResult> result)
    {
        if (_branchFilter.IsHeadquartersAdmin() || !result.Success || result.Data == null)
            return null;

        var branchId = _branchFilter.GetCurrentBranchId();
        if (branchId <= 0)
            return BranchForbidden("当前用户未分配分公司");

        result.Data.Duplicates = [.. result.Data.Duplicates.Where(d => d.BranchId == branchId)];
        result.Data.HasDuplicates = result.Data.Duplicates.Count > 0;
        return null;
    }

    private IActionResult BranchForbidden(string message)
    {
        return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(message));
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
