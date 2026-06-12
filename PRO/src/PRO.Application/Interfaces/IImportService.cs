using PRO.Application.DTOs;

namespace PRO.Application.Interfaces;

/// <summary>
/// Excel数据导入服务接口
/// </summary>
public interface IImportService
{
    /// <summary>预览Excel文件内容（前N行）</summary>
    Task<ApiResponse<ImportPreviewDto>> PreviewAsync(string filePath, string importType);

    /// <summary>执行导入</summary>
    Task<ApiResponse<ImportResultDto>> ImportAsync(string filePath, string importType, int importedById, int branchId);
}

/// <summary>导入预览结果</summary>
public class ImportPreviewDto
{
    public string ImportType { get; set; } = string.Empty;
    public List<string> Headers { get; set; } = [];
    public List<Dictionary<string, string>> Rows { get; set; } = [];
    public int TotalRows { get; set; }
    public List<string> ValidationErrors { get; set; } = [];
}

/// <summary>导入结果</summary>
public class ImportResultDto
{
    public int TotalRows { get; set; }
    public int SuccessCount { get; set; }
    public int FailCount { get; set; }
    public int SkipCount { get; set; }
    public List<string> Errors { get; set; } = [];
}
