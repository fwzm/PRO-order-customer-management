using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Enums;
using PRO.Infrastructure.Services;
using PRO.WebApi.Authorization;
using PRO.WebApi.Filters;
using Swashbuckle.AspNetCore.Annotations;

namespace PRO.WebApi.Controllers;

/// <summary>
/// 客户管理控制器
/// </summary>
[Authorize]
public class CustomersController(ICustomerService customerService, AuditService auditService, BranchDataFilter branchFilter) : BaseApiController
{
    private readonly ICustomerService _customerService = customerService;
    private readonly AuditService _auditService = auditService;
    private readonly BranchDataFilter _branchFilter = branchFilter;

    /// <summary>
    /// 获取客户列表
    /// </summary>
    [HttpGet]
    [Authorize(Policy = PermissionPolicies.CustomerView)]
    [AutoBranchFilter] // 自动应用分公司过滤
    [SwaggerOperation(Summary = "获取客户列表", Description = "分页获取客户列表")]
    public async Task<IActionResult> GetList([FromQuery] PagedRequest request, [FromQuery] int? branchId, [FromQuery] CustomerType? customerType)
    {
        branchId = _branchFilter.ApplyBranchScope(branchId);
        var result = await _customerService.GetListAsync(request, branchId, customerType);
        return Ok(result);
    }

    /// <summary>
    /// 获取客户详情
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Policy = PermissionPolicies.CustomerView)]
    [SwaggerOperation(Summary = "获取客户详情")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _customerService.GetByIdAsync(id);

        // 验证数据归属
        if (result.Success && result.Data != null)
        {
            _branchFilter.EnsureAccessible(result.Data.BranchId, "客户");
        }

        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// 创建客户
    /// </summary>
    [HttpPost]
    [Authorize(Policy = PermissionPolicies.CustomerCreate)]
    [SwaggerOperation(Summary = "创建客户")]
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

        if (result.Success && result.Data > 0)
        {
            await _auditService.LogCustomerCreateAsync(CurrentEmployeeId, result.Data, request.Name);
        }

        return result.Success
            ? CreatedAtAction(nameof(GetById), new { id = result.Data }, result)
            : BadRequest(result);
    }

    /// <summary>
    /// 更新客户
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Policy = PermissionPolicies.CustomerEdit)]
    [SwaggerOperation(Summary = "更新客户")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCustomerRequest request)
    {
        var existing = await _customerService.GetByIdAsync(id);
        if (!existing.Success || existing.Data == null)
            return NotFound(existing);

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

    /// <summary>
    /// 删除客户
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = PermissionPolicies.CustomerDelete)]
    [SwaggerOperation(Summary = "删除客户")]
    public async Task<IActionResult> Delete(int id)
    {
        // 获取客户信息用于审计
        var customer = await _customerService.GetByIdAsync(id);
        if (!customer.Success || customer.Data == null)
            return NotFound(customer);

        _branchFilter.EnsureAccessible(customer.Data.BranchId, "客户");

        var result = await _customerService.DeleteAsync(id);

        if (result.Success)
        {
            await _auditService.LogCustomerDeleteAsync(CurrentEmployeeId, id, customer.Data.Name);
        }

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// 客户查重
    /// </summary>
    [HttpPost("check-duplicates")]
    [Authorize(Policy = PermissionPolicies.CustomerView)]
    [SwaggerOperation(Summary = "客户查重")]
    public async Task<IActionResult> CheckDuplicates([FromBody] DuplicateCheckRequest request)
    {
        var result = await _customerService.CheckDuplicatesAsync(request.Phone, request.Name, request.Address, request.LegalPerson);
        var forbiddenResult = FilterDuplicateResult(result);
        if (forbiddenResult != null)
            return forbiddenResult;

        return Ok(result);
    }

    /// <summary>
    /// 合并客户
    /// </summary>
    [HttpPost("merge")]
    [Authorize(Policy = PermissionPolicies.CustomerMerge)]
    [SwaggerOperation(Summary = "合并客户")]
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

        if (result.Success)
        {
            await _auditService.LogCustomerMergeAsync(
                CurrentEmployeeId,
                request.MainCustomerId,
                $"客户ID:{request.MainCustomerId}",
                [.. request.MergedCustomerIds.Select(id => $"客户ID:{id}")]);
        }

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

public class DuplicateCheckRequest
{
    public string? Phone { get; set; }
    public string? Name { get; set; }
    public string? Address { get; set; }
    public string? LegalPerson { get; set; }
}
