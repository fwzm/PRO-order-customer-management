using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;

namespace PRO.Desktop.Services;

/// <summary>
/// Excel数据导入服务
/// </summary>
public class ImportService : IImportService
{
    private readonly ProDbContext _db;

    public ImportService(ProDbContext db) { _db = db; }

    public Task<ApiResponse<ImportPreviewDto>> PreviewAsync(string filePath, string importType)
    {
        try
        {
            using var workbook = new XLWorkbook(filePath);
            var worksheet = workbook.Worksheet(1);

            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
            if (lastRow < 2)
                return Task.FromResult(ApiResponse<ImportPreviewDto>.Fail("Excel文件为空，至少需要包含标题行和一行数据"));

            var headers = new List<string>();
            var headerRow = worksheet.Row(1);
            for (int col = 1; col <= 20; col++)
            {
                var val = headerRow.Cell(col).GetString().Trim();
                if (string.IsNullOrEmpty(val)) break;
                headers.Add(val);
            }

            if (headers.Count == 0)
                return Task.FromResult(ApiResponse<ImportPreviewDto>.Fail("未检测到表头，请确保第一行为列标题"));

            var previewRows = new List<Dictionary<string, string>>();
            var previewCount = Math.Min(lastRow - 1, 10);
            for (int row = 2; row <= previewCount + 1; row++)
            {
                var rowData = new Dictionary<string, string>();
                for (int col = 0; col < headers.Count; col++)
                {
                    rowData[headers[col]] = worksheet.Row(row).Cell(col + 1).GetString().Trim();
                }
                previewRows.Add(rowData);
            }

            var errors = ValidateHeaders(headers, importType);

            return Task.FromResult(ApiResponse<ImportPreviewDto>.Ok(new ImportPreviewDto
            {
                ImportType = importType,
                Headers = headers,
                Rows = previewRows,
                TotalRows = lastRow - 1,
                ValidationErrors = errors
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ApiResponse<ImportPreviewDto>.Fail($"读取Excel失败: {ex.Message}"));
        }
    }

    public async Task<ApiResponse<ImportResultDto>> ImportAsync(string filePath, string importType, int importedById, int branchId)
    {
        var result = new ImportResultDto();
        try
        {
            using var workbook = new XLWorkbook(filePath);
            var worksheet = workbook.Worksheet(1);

            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
            var headers = new List<string>();
            for (int col = 1; col <= 20; col++)
            {
                var val = worksheet.Row(1).Cell(col).GetString().Trim();
                if (string.IsNullOrEmpty(val)) break;
                headers.Add(val);
            }

            result.TotalRows = lastRow - 1;

            for (int row = 2; row <= lastRow; row++)
            {
                try
                {
                    var rowData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    for (int col = 0; col < headers.Count; col++)
                    {
                        rowData[headers[col]] = worksheet.Row(row).Cell(col + 1).GetString().Trim();
                    }

                    var success = importType switch
                    {
                        "Customer" => await ImportCustomerRowAsync(rowData, importedById, branchId),
                        "Product" => await ImportProductRowAsync(rowData),
                        _ => false
                    };

                    if (success)
                        result.SuccessCount++;
                    else
                        result.SkipCount++;
                }
                catch (Exception ex)
                {
                    result.FailCount++;
                    result.Errors.Add($"第{row}行: {ex.Message}");
                }
            }

            await _db.SaveChangesAsync();

            var msg = $"导入完成：成功 {result.SuccessCount} 条，跳过 {result.SkipCount} 条，失败 {result.FailCount} 条";
            return ApiResponse<ImportResultDto>.Ok(result, msg);
        }
        catch (Exception ex)
        {
            return ApiResponse<ImportResultDto>.Fail($"导入失败: {ex.Message}");
        }
    }

    private async Task<bool> ImportCustomerRowAsync(Dictionary<string, string> row, int importedById, int branchId)
    {
        var name = GetValue(row, "客户名称", "名称", "姓名");
        if (string.IsNullOrWhiteSpace(name)) return false;

        var phone = GetValue(row, "手机号", "电话", "联系电话");
        var address = GetValue(row, "地址", "详细地址");
        var remark = GetValue(row, "备注");

        var exists = await _db.Customers.AnyAsync(c =>
            c.BranchId == branchId && c.Name == name &&
            c.Status == CustomerStatus.Active &&
            (string.IsNullOrEmpty(phone) || c.Phone == phone));

        if (exists) return false;

        var today = DateTime.Now.ToString("yyyyMMdd");
        var lastCustomer = await _db.Customers
            .Where(c => c.CustomerNo.StartsWith($"C{today}"))
            .OrderByDescending(c => c.CustomerNo)
            .Select(c => c.CustomerNo)
            .FirstOrDefaultAsync();
        var seq = 1;
        if (lastCustomer != null && lastCustomer.Length >= 16 && int.TryParse(lastCustomer[10..], out var lastSeq))
            seq = lastSeq + 1;
        var customerNo = $"C{today}{seq:D6}";

        var customer = new Customer
        {
            CustomerNo = customerNo,
            Name = name,
            Phone = string.IsNullOrWhiteSpace(phone) ? null : phone,
            Address = string.IsNullOrWhiteSpace(address) ? null : address,
            Remark = string.IsNullOrWhiteSpace(remark) ? null : remark,
            BranchId = branchId,
            CreatedById = importedById,
            Status = CustomerStatus.Active,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            LocalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            SyncStatus = SyncStatus.Pending
        };

        _db.Customers.Add(customer);
        return true;
    }

    private async Task<bool> ImportProductRowAsync(Dictionary<string, string> row)
    {
        var name = GetValue(row, "产品名称", "名称", "商品名称");
        if (string.IsNullOrWhiteSpace(name)) return false;

        var sku = GetValue(row, "SKU", "产品编码", "编码");
        if (string.IsNullOrWhiteSpace(sku))
        {
            sku = $"IMP-{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..6]}";
        }

        var exists = await _db.Products.AnyAsync(p => p.SKU == sku && p.Status == ProductStatus.Active);
        if (exists) return false;

        var spec = GetValue(row, "规格", "产品规格");
        var unit = GetValue(row, "单位");
        var priceStr = GetValue(row, "参考价", "价格", "参考价格");
        var stockStr = GetValue(row, "库存", "库存数量", "初始库存");

        decimal.TryParse(priceStr, out var price);
        int.TryParse(stockStr, out var stock);

        var product = new Product
        {
            SKU = sku,
            Name = name,
            Specification = string.IsNullOrWhiteSpace(spec) ? null : spec,
            Unit = string.IsNullOrWhiteSpace(unit) ? null : unit,
            ReferencePrice = price > 0 ? price : null,
            Stock = stock,
            Status = ProductStatus.Active,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            LocalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            SyncStatus = SyncStatus.Pending
        };

        _db.Products.Add(product);
        return true;
    }

    private static string? GetValue(Dictionary<string, string> row, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (row.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val))
                return val;
        }
        return null;
    }

    private static List<string> ValidateHeaders(List<string> headers, string importType)
    {
        var errors = new List<string>();
        var requiredColumns = importType switch
        {
            "Customer" => new[] { "客户名称", "名称", "姓名" },
            "Product" => new[] { "产品名称", "名称", "商品名称" },
            _ => Array.Empty<string>()
        };

        var hasRequired = requiredColumns.Any(r => headers.Any(h => h.Contains(r, StringComparison.OrdinalIgnoreCase)));
        if (!hasRequired)
            errors.Add($"缺少必填列，请确保包含以下列之一：{string.Join("、", requiredColumns)}");

        return errors;
    }
}
