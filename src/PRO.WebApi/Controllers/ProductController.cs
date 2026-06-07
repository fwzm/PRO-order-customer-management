using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Enums;

namespace PRO.WebApi.Controllers;

/// <summary>
/// 产品管理控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductController(IProductService productService)
    {
        _productService = productService;
    }

    /// <summary>获取产品列表</summary>
    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] PagedRequest request,
        [FromQuery] int? categoryId, [FromQuery] ProductStatus? status)
    {
        var result = await _productService.GetListAsync(request, categoryId, status);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>获取产品详情</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _productService.GetByIdAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>获取产品分类</summary>
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var result = await _productService.GetCategoriesAsync();
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>创建产品</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest request)
    {
        var result = await _productService.CreateAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>更新产品</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateProductRequest request)
    {
        request.Id = id;
        var result = await _productService.UpdateAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>更新库存</summary>
    [HttpPut("{id:int}/stock")]
    public async Task<IActionResult> UpdateStock(int id, [FromBody] int quantity)
    {
        var result = await _productService.UpdateStockAsync(id, quantity);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>删除产品</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _productService.DeleteAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
