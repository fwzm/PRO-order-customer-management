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
/// 产品管理控制器
/// </summary>
[Authorize]
[ServiceFilter(typeof(BranchDataFilter))]
public class ProductsController(IProductService productService, AuditService auditService) : BaseApiController
{
    private readonly IProductService _productService = productService;
    private readonly AuditService _auditService = auditService;

    /// <summary>
    /// 获取产品列表
    /// </summary>
    [HttpGet]
    [Authorize(Policy = PermissionPolicies.ProductView)]
    [SwaggerOperation(Summary = "获取产品列表")]
    public async Task<IActionResult> GetList([FromQuery] PagedRequest request, [FromQuery] int? categoryId, [FromQuery] ProductStatus? status)
    {
        var result = await _productService.GetListAsync(request, categoryId, status);
        return Ok(result);
    }

    /// <summary>
    /// 获取产品详情
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Policy = PermissionPolicies.ProductView)]
    [SwaggerOperation(Summary = "获取产品详情")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _productService.GetByIdAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// 创建产品
    /// </summary>
    [HttpPost]
    [Authorize(Policy = PermissionPolicies.ProductCreate)]
    [SwaggerOperation(Summary = "创建产品")]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest request)
    {
        var result = await _productService.CreateAsync(request);

        if (result.Success)
        {
            await _auditService.LogProductCreateAsync(CurrentEmployeeId, result.Data, request.Name);
        }

        return result.Success
            ? CreatedAtAction(nameof(GetById), new { id = result.Data }, result)
            : BadRequest(result);
    }

    /// <summary>
    /// 更新产品
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Policy = PermissionPolicies.ProductEdit)]
    [SwaggerOperation(Summary = "更新产品")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateProductRequest request)
    {
        request.Id = id;
        var result = await _productService.UpdateAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// 删除产品
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = PermissionPolicies.ProductDelete)]
    [SwaggerOperation(Summary = "删除产品")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _productService.DeleteAsync(id);

        if (result.Success)
        {
            await _auditService.LogProductDeleteAsync(CurrentEmployeeId, id, "");
        }

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// 获取产品分类
    /// </summary>
    [HttpGet("categories")]
    [SwaggerOperation(Summary = "获取产品分类列表")]
    public async Task<IActionResult> GetCategories()
    {
        var result = await _productService.GetCategoriesAsync();
        return Ok(result);
    }

    /// <summary>
    /// 更新库存
    /// </summary>
    [HttpPut("{id}/stock")]
    [Authorize(Policy = PermissionPolicies.ProductEdit)]
    [SwaggerOperation(Summary = "更新产品库存")]
    public async Task<IActionResult> UpdateStock(int id, [FromBody] UpdateStockRequest request)
    {
        var result = await _productService.UpdateStockAsync(id, request.Quantity);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

public class UpdateStockRequest
{
    public int Quantity { get; set; }
}
