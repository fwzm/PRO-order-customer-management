using PRO.Application.DTOs;

namespace PRO.Application.Interfaces;

/// <summary>
/// 异步导出服务接口
/// </summary>
public interface IExportService
{
    /// <summary>提交导出任务，立即返回任务ID</summary>
    Task<ApiResponse<ExportTaskDto>> SubmitAsync(ExportRequest request, int requestedById, int branchId);

    /// <summary>获取导出进度</summary>
    Task<ApiResponse<ExportProgress>> GetProgressAsync(int jobId, int userId, int branchId);

    /// <summary>获取导出历史（分页）</summary>
    Task<ApiResponse<PagedResult<ExportTaskDto>>> GetHistoryAsync(PagedRequest request, int userId, int branchId);

    /// <summary>重试失败的导出</summary>
    Task<ApiResponse<ExportTaskDto>> RetryAsync(int jobId, int userId, int branchId);

    /// <summary>取消导出</summary>
    Task<ApiResponse<bool>> CancelAsync(int jobId, int userId, int branchId);

    /// <summary>获取下载路径</summary>
    Task<ApiResponse<string>> GetDownloadPathAsync(int jobId, int userId, int branchId);
}
