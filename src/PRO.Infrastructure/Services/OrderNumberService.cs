using Microsoft.EntityFrameworkCore;
using PRO.Application.Interfaces;
using PRO.Infrastructure.Persistence;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 统一订单号生成服务。数据库唯一索引负责最终一致性，调用方在保存冲突时有限重试。
/// </summary>
public class OrderNumberService : IOrderNumberService
{
    private readonly ProDbContext _dbContext;

    public OrderNumberService(ProDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<string> GenerateAsync(int branchId, DateTime now, CancellationToken cancellationToken = default)
    {
        var branchCode = await _dbContext.Branches
            .AsNoTracking()
            .Where(b => b.Id == branchId)
            .Select(b => b.Code)
            .FirstOrDefaultAsync(cancellationToken) ?? "0000";

        branchCode = NormalizeBranchCode(branchCode);
        var prefix = $"D{now:yyyyMMdd}{branchCode}";

        var lastOrderNo = await _dbContext.Orders
            .AsNoTracking()
            .Where(o => o.OrderNo.StartsWith(prefix))
            .OrderByDescending(o => o.OrderNo)
            .Select(o => o.OrderNo)
            .FirstOrDefaultAsync(cancellationToken);

        var nextSeq = 1;
        if (!string.IsNullOrWhiteSpace(lastOrderNo) &&
            lastOrderNo.Length >= prefix.Length + 4 &&
            int.TryParse(lastOrderNo.Substring(prefix.Length, 4), out var lastSeq))
        {
            nextSeq = lastSeq + 1;
        }

        for (var i = 0; i < 100; i++)
        {
            var candidate = $"{prefix}{nextSeq + i:D4}";
            var exists = await _dbContext.Orders
                .AsNoTracking()
                .AnyAsync(o => o.OrderNo == candidate, cancellationToken);
            if (!exists)
                return candidate;
        }

        return $"{prefix}{now:HHmmss}{Random.Shared.Next(100, 999)}";
    }

    private static string NormalizeBranchCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return "0000";

        var normalized = new string(code.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (normalized.Length >= 4)
            return normalized[..4];

        return normalized.PadLeft(4, '0');
    }
}
