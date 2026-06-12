using Microsoft.EntityFrameworkCore;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 结算服务实现
/// </summary>
public class SettlementService : ISettlementService
{
    private readonly ProDbContext _dbContext;

    public SettlementService(ProDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ApiResponse<PagedResult<SettlementListItem>>> GetListAsync(PagedRequest request, int? branchId = null)
    {
        try
        {
            var query = _dbContext.Settlements.AsNoTracking()
                .Include(s => s.Branch).Include(s => s.ConfirmedBy).AsQueryable();

            if (branchId.HasValue) query = query.Where(s => s.BranchId == branchId.Value);
            if (!string.IsNullOrWhiteSpace(request.Keyword)) query = query.Where(s => s.SettlementNo.Contains(request.Keyword));

            var totalCount = await query.CountAsync();
            var items = await query.OrderByDescending(s => s.CreatedAt)
                .Skip((request.PageIndex - 1) * request.PageSize).Take(request.PageSize)
                .Select(s => new SettlementListItem
                {
                    Id = s.Id,
                    SettlementNo = s.SettlementNo,
                    StartDate = s.StartDate,
                    EndDate = s.EndDate,
                    BranchId = s.BranchId,
                    BranchName = s.Branch != null ? s.Branch.Name : "",
                    ConfirmedByName = s.ConfirmedBy != null ? s.ConfirmedBy.Name : "",
                    OrderCount = s.OrderCount,
                    TotalAmount = s.TotalAmount,
                    ReceivedAmount = s.ReceivedAmount,
                    UnpaidAmount = s.UnpaidAmount,
                    Status = s.Status,
                    PdfPath = s.PdfPath,
                    CreatedAt = s.CreatedAt
                }).ToListAsync();

            return ApiResponse<PagedResult<SettlementListItem>>.Ok(new PagedResult<SettlementListItem>
            { Items = items, TotalCount = totalCount, PageIndex = request.PageIndex, PageSize = request.PageSize });
        }
        catch (Exception ex) { return ApiResponse<PagedResult<SettlementListItem>>.Fail($"查询结算列表失败: {ex.Message}"); }
    }

    public async Task<ApiResponse<SettlementDetailDto>> GetByIdAsync(int id)
    {
        try
        {
            var settlement = await _dbContext.Settlements.AsNoTracking()
                .Include(s => s.Branch).Include(s => s.ConfirmedBy)
                .Include(s => s.Orders).ThenInclude(o => o.Customer)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (settlement == null) return ApiResponse<SettlementDetailDto>.Fail("结算单不存在");

            return ApiResponse<SettlementDetailDto>.Ok(new SettlementDetailDto
            {
                Id = settlement.Id,
                SettlementNo = settlement.SettlementNo,
                StartDate = settlement.StartDate,
                EndDate = settlement.EndDate,
                BranchId = settlement.BranchId,
                BranchName = settlement.Branch?.Name ?? "",
                ConfirmedById = settlement.ConfirmedById,
                ConfirmedByName = settlement.ConfirmedBy?.Name ?? "",
                OrderCount = settlement.OrderCount,
                TotalAmount = settlement.TotalAmount,
                ReceivedAmount = settlement.ReceivedAmount,
                UnpaidAmount = settlement.UnpaidAmount,
                Status = settlement.Status,
                Remark = settlement.Remark,
                PdfPath = settlement.PdfPath,
                CreatedAt = settlement.CreatedAt,
                Orders = settlement.Orders.OrderByDescending(o => o.CreatedAt).Select(o => new SettlementOrderDto
                {
                    Id = o.Id,
                    OrderNo = o.OrderNo,
                    CustomerName = o.Customer?.Name ?? "",
                    CreatedAt = o.CreatedAt,
                    TotalAmount = o.TotalAmount,
                    ReceivedAmount = o.ReceivedAmount,
                    PaymentStatus = o.PaymentStatus
                }).ToList()
            });
        }
        catch (Exception ex) { return ApiResponse<SettlementDetailDto>.Fail($"查询结算详情失败: {ex.Message}"); }
    }

    public async Task<ApiResponse<SettlementPreviewDto>> PreviewAsync(CreateSettlementRequest request)
    {
        try
        {
            if (request.EndDate.Date < request.StartDate.Date)
                return ApiResponse<SettlementPreviewDto>.Fail("结算结束日期不能早于开始日期");

            var branch = await _dbContext.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == request.BranchId);
            if (branch == null) return ApiResponse<SettlementPreviewDto>.Fail("分公司不存在");

            var orders = await BuildSettlementOrdersQuery(request).AsNoTracking().ToListAsync();
            return ApiResponse<SettlementPreviewDto>.Ok(new SettlementPreviewDto
            {
                BranchId = request.BranchId,
                BranchName = branch.Name,
                StartDate = request.StartDate.Date,
                EndDate = request.EndDate.Date,
                OrderCount = orders.Count,
                TotalAmount = orders.Sum(o => o.TotalAmount),
                ReceivedAmount = orders.Sum(o => o.ReceivedAmount),
                UnpaidAmount = orders.Sum(o => o.TotalAmount - o.ReceivedAmount),
                PaidCount = orders.Count(o => o.PaymentStatus == PaymentStatus.Paid),
                UnpaidCount = orders.Count(o => o.PaymentStatus != PaymentStatus.Paid && o.PaymentStatus != PaymentStatus.Legal),
                LegalCount = orders.Count(o => o.PaymentStatus == PaymentStatus.Legal)
            });
        }
        catch (Exception ex) { return ApiResponse<SettlementPreviewDto>.Fail($"生成结算预览失败: {ex.Message}"); }
    }

    public async Task<ApiResponse<int>> CreateAsync(CreateSettlementRequest request, int confirmedById)
    {
        try
        {
            if (request.EndDate.Date < request.StartDate.Date)
                return ApiResponse<int>.Fail("结算结束日期不能早于开始日期");

            var orders = await BuildSettlementOrdersQuery(request).ToListAsync();
            if (orders.Count == 0) return ApiResponse<int>.Fail("没有可结算的订单");

            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            var now = DateTime.Now;
            var settlement = new Settlement
            {
                SettlementNo = $"STL{now:yyyyMMddHHmmss}",
                BranchId = request.BranchId,
                StartDate = request.StartDate.Date,
                EndDate = request.EndDate.Date,
                OrderCount = orders.Count,
                TotalAmount = orders.Sum(o => o.TotalAmount),
                ReceivedAmount = orders.Sum(o => o.ReceivedAmount),
                UnpaidAmount = orders.Sum(o => o.TotalAmount - o.ReceivedAmount),
                ConfirmedById = confirmedById,
                Status = SettlementStatus.Completed,
                Remark = request.Remark,
                CreatedAt = now,
                UpdatedAt = now,
                LocalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                SyncStatus = SyncStatus.Pending
            };

            _dbContext.Settlements.Add(settlement);
            await _dbContext.SaveChangesAsync();

            foreach (var order in orders)
            {
                order.SettlementId = settlement.Id; order.UpdatedAt = now;
                order.LocalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); order.SyncStatus = SyncStatus.Pending;
            }

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            return ApiResponse<int>.Ok(settlement.Id, "结算创建成功");
        }
        catch (Exception ex) { return ApiResponse<int>.Fail($"创建结算失败: {ex.Message}"); }
    }

    public async Task<ApiResponse<string>> GeneratePdfAsync(int settlementId)
    {
        var pdfPath = await _dbContext.Settlements.AsNoTracking()
            .Where(s => s.Id == settlementId).Select(s => s.PdfPath).FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(pdfPath))
            return ApiResponse<string>.Fail("该结算单尚未生成PDF，请在桌面端结算页面生成");
        if (!File.Exists(pdfPath))
            return ApiResponse<string>.Fail("结算PDF文件不存在或已被移动");

        return ApiResponse<string>.Ok(pdfPath);
    }

    public Task<ApiResponse<string>> DownloadPdfAsync(int settlementId) => GeneratePdfAsync(settlementId);

    public async Task<ApiResponse<string>> BatchDownloadPdfAsync(List<int> settlementIds)
    {
        var paths = await _dbContext.Settlements.AsNoTracking()
            .Where(s => settlementIds.Contains(s.Id) && !string.IsNullOrWhiteSpace(s.PdfPath))
            .Select(s => s.PdfPath!).ToListAsync();

        var existingPaths = paths.Where(File.Exists).ToList();
        if (existingPaths.Count == 0)
            return ApiResponse<string>.Fail("没有可下载的结算PDF");

        return ApiResponse<string>.Ok(string.Join(Environment.NewLine, existingPaths));
    }

    private IQueryable<Order> BuildSettlementOrdersQuery(CreateSettlementRequest request)
    {
        var start = request.StartDate.Date;
        var endExclusive = request.EndDate.Date.AddDays(1);
        return _dbContext.Orders
            .Where(o => o.BranchId == request.BranchId && o.Status == OrderStatus.Completed
                && o.SettlementId == null && o.CreatedAt >= start && o.CreatedAt < endExclusive);
    }
}
