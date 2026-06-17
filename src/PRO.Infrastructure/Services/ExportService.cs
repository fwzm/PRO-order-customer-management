using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRO.Application.DTOs;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;
using Serilog;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 异步导出服务 - 支持后台导出和进度反馈
/// </summary>
public class ExportService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<string, ExportJobInfo> _jobs = new();

    public ExportService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// 创建导出任务
    /// </summary>
    public Task<ExportJobInfo> CreateExportAsync(string exportType, int branchId, int requestedById, string? filters = null)
    {
        var jobId = Guid.NewGuid().ToString("N")[..12];
        var job = new ExportJobInfo
        {
            JobId = jobId,
            ExportType = exportType,
            Status = "Pending",
            Progress = 0,
            RequestedById = requestedById,
            RequestedAt = DateTime.Now,
            Message = "等待开始..."
        };

        _jobs[jobId] = job;

        // 异步执行导出
        _ = Task.Run(() => ExecuteExportAsync(job, branchId, filters));

        return Task.FromResult(job);
    }

    /// <summary>
    /// 获取导出任务状态
    /// </summary>
    public ExportJobInfo? GetJobStatus(string jobId)
    {
        return _jobs.GetValueOrDefault(jobId);
    }

    /// <summary>
    /// 获取用户的所有导出任务
    /// </summary>
    public List<ExportJobInfo> GetUserJobs(int userId)
    {
        return _jobs.Values
            .Where(j => j.RequestedById == userId)
            .OrderByDescending(j => j.RequestedAt)
            .Take(20)
            .ToList();
    }

    /// <summary>
    /// 取消导出任务
    /// </summary>
    public bool CancelJob(string jobId)
    {
        if (_jobs.TryGetValue(jobId, out var job) && job.Status == "Processing")
        {
            job.Status = "Cancelled";
            job.Message = "已取消";
            job.CancellationTokenSource?.Cancel();
            return true;
        }
        return false;
    }

    private async Task ExecuteExportAsync(ExportJobInfo job, int branchId, string? filters)
    {
        job.Status = "Processing";
        job.CancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = job.CancellationTokenSource.Token;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ProDbContext>();

            switch (job.ExportType.ToLower())
            {
                case "orders":
                    await ExportOrdersAsync(dbContext, job, branchId, filters, cancellationToken);
                    break;
                case "customers":
                    await ExportCustomersAsync(dbContext, job, branchId, filters, cancellationToken);
                    break;
                case "products":
                    await ExportProductsAsync(dbContext, job, cancellationToken);
                    break;
                default:
                    throw new NotSupportedException($"不支持的导出类型: {job.ExportType}");
            }

            job.Status = "Completed";
            job.Message = "导出完成";
            job.Progress = 100;
            job.CompletedAt = DateTime.Now;
        }
        catch (OperationCanceledException)
        {
            job.Status = "Cancelled";
            job.Message = "已取消";
        }
        catch (Exception ex)
        {
            job.Status = "Failed";
            job.Message = $"导出失败: {ex.Message}";
            Log.Error(ex, "导出任务失败: {JobId}", job.JobId);
        }
    }

    private async Task ExportOrdersAsync(ProDbContext db, ExportJobInfo job, int branchId, string? filters, CancellationToken ct)
    {
        job.Message = "正在查询订单数据...";
        job.Progress = 10;

        var query = db.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.DeliveryPerson)
            .Where(o => o.BranchId == branchId && o.Status != OrderStatus.Draft);

        // 应用筛选条件
        if (!string.IsNullOrEmpty(filters))
        {
            // 解析筛选条件（简化实现）
        }

        var totalCount = await query.CountAsync(ct);
        job.TotalRecords = totalCount;
        job.Message = $"共 {totalCount} 条订单，正在导出...";
        job.Progress = 20;

        // 分批查询
        var batchSize = 500;
        var processed = 0;
        var allOrders = new List<Domain.Entities.Order>();

        for (int offset = 0; offset < totalCount; offset += batchSize)
        {
            ct.ThrowIfCancellationRequested();

            var batch = await query
                .OrderBy(o => o.Id)
                .Skip(offset)
                .Take(batchSize)
                .ToListAsync(ct);

            allOrders.AddRange(batch);
            processed += batch.Count;
            job.Progress = 20 + (int)(processed * 60.0 / totalCount);
            job.ProcessedRecords = processed;
            job.Message = $"正在处理 {processed}/{totalCount}...";
        }

        job.Progress = 80;
        job.Message = "正在生成Excel文件...";

        // 生成Excel（简化实现，实际应使用ClosedXML）
        var fileName = $"订单导出_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        var filePath = Path.Combine(Path.GetTempPath(), fileName);

        // 这里应该调用实际的Excel生成逻辑
        await Task.Delay(500, ct); // 模拟Excel生成

        job.Progress = 100;
        job.FileName = fileName;
        job.FilePath = filePath;
        job.FileSize = new FileInfo(filePath).Length;
    }

    private async Task ExportCustomersAsync(ProDbContext db, ExportJobInfo job, int branchId, string? filters, CancellationToken ct)
    {
        job.Message = "正在查询客户数据...";
        job.Progress = 10;

        var customers = await db.Customers
            .AsNoTracking()
            .Where(c => c.BranchId == branchId && c.Status != CustomerStatus.Deleted)
            .ToListAsync(ct);

        job.TotalRecords = customers.Count;
        job.Progress = 50;
        job.Message = $"共 {customers.Count} 个客户，正在导出...";

        // 生成Excel
        var fileName = $"客户导出_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        var filePath = Path.Combine(Path.GetTempPath(), fileName);

        await Task.Delay(500, ct); // 模拟Excel生成

        job.Progress = 100;
        job.Status = "Completed";
        job.Message = "导出完成";
        job.FileName = fileName;
        job.FilePath = filePath;
        job.CompletedAt = DateTime.Now;
    }

    private async Task ExportProductsAsync(ProDbContext db, ExportJobInfo job, CancellationToken ct)
    {
        job.Message = "正在查询产品数据...";
        job.Progress = 10;

        var products = await db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.Status == ProductStatus.Active)
            .ToListAsync(ct);

        job.TotalRecords = products.Count;
        job.Progress = 50;
        job.Message = $"共 {products.Count} 个产品，正在导出...";

        var fileName = $"产品导出_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        var filePath = Path.Combine(Path.GetTempPath(), fileName);

        await Task.Delay(500, ct);

        job.Progress = 100;
        job.Status = "Completed";
        job.Message = "导出完成";
        job.FileName = fileName;
        job.FilePath = filePath;
        job.CompletedAt = DateTime.Now;
    }
}

/// <summary>
/// 导出任务信息
/// </summary>
public class ExportJobInfo
{
    public string JobId { get; set; } = "";
    public string ExportType { get; set; } = "";
    public string Status { get; set; } = "Pending"; // Pending/Processing/Completed/Failed/Cancelled
    public int Progress { get; set; }
    public int TotalRecords { get; set; }
    public int ProcessedRecords { get; set; }
    public string? FileName { get; set; }
    public string? FilePath { get; set; }
    public long FileSize { get; set; }
    public string Message { get; set; } = "";
    public int RequestedById { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public CancellationTokenSource? CancellationTokenSource { get; set; }

    public bool CanCancel => Status == "Pending" || Status == "Processing";
    public bool CanDownload => Status == "Completed" && !string.IsNullOrEmpty(FilePath);
    public TimeSpan? Duration => CompletedAt - RequestedAt;
}
