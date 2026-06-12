using Microsoft.EntityFrameworkCore;
using PRO.Application.DTOs;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;
using PRO.Application.Interfaces;
using Serilog;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 客户服务实现
/// </summary>
public class CustomerService : ICustomerService
{
    private readonly ProDbContext _dbContext;
    private readonly DataMaskingService _maskingService;

    public CustomerService(ProDbContext dbContext, DataMaskingService maskingService)
    {
        _dbContext = dbContext;
        _maskingService = maskingService;
    }

    /// <summary>
    /// 对客户列表项中的敏感信息进行脱敏
    /// </summary>
    public void MaskSensitiveData(List<CustomerListItem> items)
    {
        foreach (var item in items)
        {
            item.Phone = _maskingService.MaskPhone(item.Phone ?? "");
        }
    }

    public async Task<ApiResponse<PagedResult<CustomerListItem>>> GetListAsync(PagedRequest request, int? branchId = null, CustomerType? customerType = null, bool showMajorOnly = true)
    {
        try
        {
            var query = _dbContext.Customers.AsNoTracking()
                .Include(c => c.Branch)
                .Include(c => c.ParentCustomer)
                .Include(c => c.CustomerManager)
                .Include(c => c.Creator)
                .Where(c => c.Status != CustomerStatus.Deleted)
                .AsQueryable();

            if (branchId.HasValue)
                query = query.Where(c => c.BranchId == branchId.Value);

            if (showMajorOnly)
                query = query.Where(c => c.CustomerType == CustomerType.Major);

            if (customerType.HasValue)
                query = query.Where(c => c.CustomerType == customerType.Value);

            if (!string.IsNullOrWhiteSpace(request.Keyword))
                query = query.Where(c => c.Name.Contains(request.Keyword) || (c.Phone != null && c.Phone.Contains(request.Keyword)));

            var totalCount = await query.CountAsync();
            var items = await query.OrderByDescending(c => c.CreatedAt)
                .Skip((request.PageIndex - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync();

            // 计算订单统计（多重聚合：总数、总金额、最后下单时间、应收余额、未处理配送数）
            var customerIds = items.Select(c => c.Id).ToList();
            var orderStats = await _dbContext.Orders.AsNoTracking()
                .Where(o => customerIds.Contains(o.CustomerId))
                .GroupBy(o => o.CustomerId)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    Count = g.Count(),
                    Total = g.Sum(o => o.TotalAmount),
                    LastDate = g.Max(o => (DateTime?)o.CreatedAt),
                    Receivable = g.Sum(o => o.TotalAmount - o.ReceivedAmount),
                    Unprocessed = g.Count(o =>
                        o.Status == OrderStatus.Pending || o.Status == OrderStatus.Assigned || o.Status == OrderStatus.Delivering)
                })
                .ToDictionaryAsync(x => x.CustomerId);

            var result = items.Select(c =>
            {
                var stats = orderStats.GetValueOrDefault(c.Id);
                var lastOrderDate = stats?.LastDate;
                var daysSince = lastOrderDate.HasValue
                    ? (int)(DateTime.Now - lastOrderDate.Value).TotalDays
                    : int.MaxValue;
                var receivable = stats?.Receivable ?? 0m;

                return new CustomerListItem
                {
                    Id = c.Id,
                    Name = c.Name,
                    CustomerNo = c.CustomerNo,
                    CustomerType = c.CustomerType,
                    CustomerTypeName = c.CustomerType == CustomerType.Major ? "大客户" : "细分客户",
                    Phone = c.Phone,
                    Address = c.FullAddress ?? c.Address,
                    ParentCustomerId = c.ParentCustomerId,
                    ParentCustomerName = c.ParentCustomer?.Name,
                    BranchName = c.Branch?.Name ?? "",
                    BranchId = c.BranchId,
                    Status = c.Status,
                    OrderCount = stats?.Count ?? 0,
                    TotalOrderAmount = stats?.Total ?? 0m,
                    LastOrderDate = lastOrderDate,
                    DaysSinceLastOrder = lastOrderDate.HasValue ? daysSince : 999,
                    ReceivableAmount = receivable > 0 ? receivable : 0,
                    PendingOrderCount = stats?.Unprocessed ?? 0,
                    CreatedAt = c.CreatedAt,
                    CustomerManagerName = c.CustomerManager?.Name,
                    CreatorName = c.Creator?.Name
                };
            }).ToList();

            // 排序支持
            if (!string.IsNullOrWhiteSpace(request.SortField))
            {
                var asc = string.Equals(request.SortOrder, "asc", StringComparison.OrdinalIgnoreCase);
                result = request.SortField switch
                {
                    "Name" => asc ? result.OrderBy(c => c.Name).ToList() : result.OrderByDescending(c => c.Name).ToList(),
                    "CreatedAt" => asc ? result.OrderBy(c => c.CreatedAt).ToList() : result.OrderByDescending(c => c.CreatedAt).ToList(),
                    "LastOrderDate" => asc ? result.OrderBy(c => c.LastOrderDate ?? DateTime.MinValue).ToList() : result.OrderByDescending(c => c.LastOrderDate ?? DateTime.MinValue).ToList(),
                    "TotalOrderAmount" => asc ? result.OrderBy(c => c.TotalOrderAmount).ToList() : result.OrderByDescending(c => c.TotalOrderAmount).ToList(),
                    "ReceivableAmount" => asc ? result.OrderBy(c => c.ReceivableAmount).ToList() : result.OrderByDescending(c => c.ReceivableAmount).ToList(),
                    "OrderCount" => asc ? result.OrderBy(c => c.OrderCount).ToList() : result.OrderByDescending(c => c.OrderCount).ToList(),
                    "PendingOrderCount" => asc ? result.OrderBy(c => c.PendingOrderCount).ToList() : result.OrderByDescending(c => c.PendingOrderCount).ToList(),
                    _ => result
                };
            }

            return ApiResponse<PagedResult<CustomerListItem>>.Ok(new PagedResult<CustomerListItem>
            {
                Items = result,
                TotalCount = totalCount,
                PageIndex = request.PageIndex,
                PageSize = request.PageSize
            });
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<CustomerListItem>>.Fail($"查询客户列表失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<CustomerDetailDto>> GetByIdAsync(int id)
    {
        try
        {
            var customer = await _dbContext.Customers.AsNoTracking()
                .Include(c => c.Branch)
                .Include(c => c.ParentCustomer)
                .Include(c => c.CustomerManager)
                .Include(c => c.Creator)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (customer == null)
                return BusinessMessages.CustomerNotFound.ToResponse<CustomerDetailDto>();

            var dto = new CustomerDetailDto
            {
                Id = customer.Id,
                Name = customer.Name,
                CustomerNo = customer.CustomerNo,
                CustomerType = customer.CustomerType,
                Phone = customer.Phone,
                Address = customer.FullAddress ?? customer.Address,
                Longitude = customer.Longitude,
                Latitude = customer.Latitude,
                LegalPerson = customer.LegalPerson,
                RegisterAddress = customer.RegisterAddress,
                ParentCustomerId = customer.ParentCustomerId,
                ParentCustomerName = customer.ParentCustomer?.Name,
                BranchId = customer.BranchId,
                BranchName = customer.Branch?.Name ?? "",
                WeChatCustomerId = customer.WeChatCustomerId,
                Status = customer.Status,
                Remark = customer.Remark,
                CreatedAt = customer.CreatedAt,
                CreatedById = customer.CreatedById,
                CreatedByName = customer.Creator?.Name ?? "",
                CustomerManagerName = customer.CustomerManager?.Name ?? ""
            };

            return ApiResponse<CustomerDetailDto>.Ok(dto);
        }
        catch (Exception ex)
        {
            return ApiResponse<CustomerDetailDto>.Fail($"查询客户详情失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<CustomerListItem>> GetSubCustomersAsync(int parentCustomerId)
    {
        try
        {
            var subs = await _dbContext.Customers.AsNoTracking()
                .Include(c => c.Branch)
                .Where(c => c.ParentCustomerId == parentCustomerId && c.Status != CustomerStatus.Deleted)
                .ToListAsync();

            // Return first as list item (interface returns single, but actually a list)
            var first = subs.FirstOrDefault();
            return ApiResponse<CustomerListItem>.Ok(first != null ? new CustomerListItem
            {
                Id = first.Id,
                Name = first.Name,
                CustomerNo = first.CustomerNo,
                CustomerType = first.CustomerType,
                BranchName = first.Branch?.Name ?? ""
            } : new CustomerListItem());
        }
        catch (Exception ex)
        {
            return ApiResponse<CustomerListItem>.Fail($"查询子客户失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<int>> CreateAsync(CreateCustomerRequest request)
    {
        try
        {
            var customer = new Customer
            {
                Name = request.Name,
                CustomerType = request.CustomerType,
                Phone = request.Phone,
                Address = request.Address,
                Longitude = request.Longitude,
                Latitude = request.Latitude,
                LegalPerson = request.LegalPerson,
                RegisterAddress = request.RegisterAddress,
                ParentCustomerId = request.ParentCustomerId,
                BranchId = request.BranchId,
                Remark = request.Remark,
                Status = CustomerStatus.Active,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                LocalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            // 生成客户编号
            var branch = await _dbContext.Branches.FindAsync(request.BranchId);
            var branchCode = branch?.Code ?? "0000";
            var today = DateTime.Now.ToString("yyyyMMdd");
            var lastCustomer = await _dbContext.Customers
                .Where(c => c.CustomerNo.StartsWith($"K{today}{branchCode}"))
                .OrderByDescending(c => c.CustomerNo)
                .FirstOrDefaultAsync();
            var seq = 1;
            if (lastCustomer != null && lastCustomer.CustomerNo.Length >= 16)
            {
                if (int.TryParse(lastCustomer.CustomerNo[12..], out var lastSeq))
                    seq = lastSeq + 1;
            }
            customer.CustomerNo = $"K{today}{branchCode}{seq:D4}";

            _dbContext.Customers.Add(customer);
            await _dbContext.SaveChangesAsync();

            return ApiResponse<int>.Ok(customer.Id, "客户创建成功");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"创建客户失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> UpdateAsync(UpdateCustomerRequest request)
    {
        try
        {
            var customer = await _dbContext.Customers.FindAsync(request.Id);
            if (customer == null)
                return BusinessMessages.CustomerNotFound.ToResponse();

            customer.Name = request.Name;
            customer.CustomerType = request.CustomerType;
            customer.Phone = request.Phone;
            customer.Address = request.Address;
            customer.Longitude = request.Longitude;
            customer.Latitude = request.Latitude;
            customer.LegalPerson = request.LegalPerson;
            customer.RegisterAddress = request.RegisterAddress;
            customer.ParentCustomerId = request.ParentCustomerId;
            customer.BranchId = request.BranchId;
            customer.Status = request.Status;
            customer.Remark = request.Remark;
            customer.UpdatedAt = DateTime.Now;
            customer.LocalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            customer.SyncStatus = SyncStatus.Pending;

            await _dbContext.SaveChangesAsync();
            return ApiResponse<bool>.Ok(true, "客户更新成功");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"更新客户失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id)
    {
        try
        {
            var customer = await _dbContext.Customers.FindAsync(id);
            if (customer == null)
                return BusinessMessages.CustomerNotFound.ToResponse();

            var orderCount = await _dbContext.Orders.CountAsync(o => o.CustomerId == id);
            if (orderCount > 0)
                return BusinessMessages.CustomerHasOrders.ToResponse();

            customer.Status = CustomerStatus.Deleted;
            customer.UpdatedAt = DateTime.Now;
            customer.SyncStatus = SyncStatus.Pending;
            await _dbContext.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "客户已删除");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"删除客户失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<CustomerDuplicateCheckResult>> CheckDuplicatesAsync(string? phone, string? name, string? address, string? legalPerson)
    {
        try
        {
            var duplicates = new List<CustomerDuplicateItem>();
            var phoneTerm = NormalizePhone(phone);
            var nameTerm = NormalizeText(name);
            var addressTerm = NormalizeText(address);
            var legalPersonTerm = NormalizeText(legalPerson);

            if (string.IsNullOrWhiteSpace(phoneTerm)
                && string.IsNullOrWhiteSpace(nameTerm)
                && string.IsNullOrWhiteSpace(addressTerm)
                && string.IsNullOrWhiteSpace(legalPersonTerm))
            {
                return ApiResponse<CustomerDuplicateCheckResult>.Ok(new CustomerDuplicateCheckResult());
            }

            var nameSearch = GetSearchToken(nameTerm, 2, 6);
            var addressSearch = GetSearchToken(addressTerm, 3, 8);
            var legalPersonSearch = GetSearchToken(legalPersonTerm, 2, 6);
            var recentCutoff = DateTime.Now.AddDays(-30);

            var query = _dbContext.Customers.AsNoTracking()
                .Where(c => c.Status == CustomerStatus.Active);

            query = query.Where(c =>
                (!string.IsNullOrEmpty(phoneTerm) && c.Phone != null && c.Phone.Contains(phoneTerm)) ||
                (!string.IsNullOrEmpty(nameSearch) && c.Name.Contains(nameSearch)) ||
                (!string.IsNullOrEmpty(addressSearch) && (
                    (c.Address != null && c.Address.Contains(addressSearch)) ||
                    (c.FullAddress != null && c.FullAddress.Contains(addressSearch)))) ||
                (!string.IsNullOrEmpty(legalPersonSearch) && c.LegalPerson != null && c.LegalPerson.Contains(legalPersonSearch)) ||
                (!string.IsNullOrEmpty(nameSearch) && c.CreatedAt >= recentCutoff && c.Name.Contains(nameSearch)));

            var candidates = await query
                .OrderByDescending(c => c.CreatedAt)
                .Take(80)
                .ToListAsync();

            foreach (var candidate in candidates)
            {
                var item = BuildDuplicateItem(candidate, phoneTerm, nameTerm, addressTerm, legalPersonTerm, recentCutoff);
                if (item != null)
                    duplicates.Add(item);
            }

            return ApiResponse<CustomerDuplicateCheckResult>.Ok(new CustomerDuplicateCheckResult
            {
                HasDuplicates = duplicates.Count > 0,
                Duplicates = duplicates
                    .GroupBy(d => d.Id)
                    .Select(g => g.OrderByDescending(d => d.Similarity).First())
                    .OrderByDescending(d => d.Similarity)
                    .ThenBy(d => d.Name)
                    .Take(20)
                    .ToList()
            });
        }
        catch (Exception ex)
        {
            return ApiResponse<CustomerDuplicateCheckResult>.Fail($"查重失败: {ex.Message}");
        }
    }

    private static CustomerDuplicateItem? BuildDuplicateItem(
        Customer candidate,
        string phoneTerm,
        string nameTerm,
        string addressTerm,
        string legalPersonTerm,
        DateTime recentCutoff)
    {
        var candidatePhone = NormalizePhone(candidate.Phone);
        var candidateName = NormalizeText(candidate.Name);
        var candidateAddress = NormalizeText(candidate.FullAddress ?? candidate.Address);
        var candidateLegalPerson = NormalizeText(candidate.LegalPerson);

        var bestScore = 0d;
        var matchType = "";

        if (!string.IsNullOrEmpty(phoneTerm) && !string.IsNullOrEmpty(candidatePhone))
        {
            if (candidatePhone == phoneTerm)
            {
                bestScore = 1.0;
                matchType = "phone";
            }
            else if (phoneTerm.Length >= 4 && candidatePhone.Contains(phoneTerm))
            {
                bestScore = 0.86;
                matchType = "phone_partial";
            }
        }

        if (!string.IsNullOrEmpty(legalPersonTerm)
            && !string.IsNullOrEmpty(phoneTerm)
            && candidateLegalPerson == legalPersonTerm
            && candidatePhone == phoneTerm)
        {
            bestScore = Math.Max(bestScore, 0.98);
            matchType = "legal_phone";
        }

        if (!string.IsNullOrEmpty(nameTerm))
        {
            var nameScore = Similarity(nameTerm, candidateName);
            var addressScore = string.IsNullOrEmpty(addressTerm)
                ? 0d
                : Similarity(addressTerm, candidateAddress);

            var combinedScore = string.IsNullOrEmpty(addressTerm)
                ? nameScore
                : nameScore * 0.72 + addressScore * 0.28;

            if (candidate.CreatedAt >= recentCutoff && nameScore >= 0.70)
            {
                combinedScore = Math.Max(combinedScore, nameScore + 0.05);
                matchType = combinedScore > bestScore ? "name_recent" : matchType;
            }

            if (combinedScore > bestScore && combinedScore >= 0.65)
            {
                bestScore = combinedScore;
                matchType = string.IsNullOrEmpty(addressTerm) ? "name" : "name_address";
            }
        }

        if (bestScore < 0.65)
            return null;

        return new CustomerDuplicateItem
        {
            Id = candidate.Id,
            BranchId = candidate.BranchId,
            Name = candidate.Name,
            Phone = candidate.Phone,
            Address = candidate.FullAddress ?? candidate.Address,
            LegalPerson = candidate.LegalPerson,
            MatchType = matchType,
            Similarity = Math.Round(bestScore, 2)
        };
    }

    private static string NormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return new string(value.Where(char.IsDigit).ToArray());
    }

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return new string(value
            .Trim()
            .ToLowerInvariant()
            .Where(c => !char.IsWhiteSpace(c) && !char.IsPunctuation(c))
            .ToArray());
    }

    private static string GetSearchToken(string value, int minLength, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < minLength)
            return string.Empty;

        return value[..Math.Min(value.Length, maxLength)];
    }

    private static double Similarity(string left, string right)
    {
        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
            return 0d;
        if (left == right)
            return 1d;
        if (left.Contains(right) || right.Contains(left))
        {
            var shorter = Math.Min(left.Length, right.Length);
            var longer = Math.Max(left.Length, right.Length);
            return Math.Max(0.78, shorter * 1.0 / longer);
        }

        var distance = LevenshteinDistance(left, right);
        var maxLen = Math.Max(left.Length, right.Length);
        return maxLen == 0 ? 1d : Math.Max(0d, 1d - distance * 1.0 / maxLen);
    }

    private static int LevenshteinDistance(string left, string right)
    {
        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];

        for (var j = 0; j <= right.Length; j++)
            previous[j] = j;

        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }

    public async Task<ApiResponse<bool>> MergeCustomersAsync(MergeCustomerRequest request)
    {
        try
        {
            var mainCustomer = await _dbContext.Customers.FindAsync(request.MainCustomerId);
            if (mainCustomer == null)
                return BusinessMessages.CustomerNotFound.ToResponse();

            foreach (var mergedId in request.MergedCustomerIds)
            {
                var merged = await _dbContext.Customers.FindAsync(mergedId);
                if (merged == null) continue;

                // 转移订单
                var orders = await _dbContext.Orders.Where(o => o.CustomerId == mergedId).ToListAsync();
                foreach (var o in orders) o.CustomerId = request.MainCustomerId;

                merged.Status = CustomerStatus.Merged;
                merged.Remark = $"已合并至 {mainCustomer.Name}";
                merged.UpdatedAt = DateTime.Now;
                merged.SyncStatus = SyncStatus.Pending;
            }

            await _dbContext.SaveChangesAsync();
            return ApiResponse<bool>.Ok(true, "客户合并成功");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"合并客户失败: {ex.Message}");
        }
    }

    public Task<ApiResponse<string>> ExportToExcelAsync(PagedRequest request, int? branchId = null)
    {
        return Task.FromResult(ApiResponse<string>.Fail("Excel导出功能请在界面层实现"));
    }

    public async Task<ApiResponse<bool>> BulkAssignCustomersAsync(BulkAssignRequest request)
    {
        try
        {
            if (request.CustomerIds.Count == 0)
                return ApiResponse<bool>.Fail("请选择至少一个客户");

            if (request.CustomerIds.Count > 500)
                return ApiResponse<bool>.Fail("单次批量分配不能超过500个客户");

            var customers = await _dbContext.Customers
                .Where(c => request.CustomerIds.Contains(c.Id))
                .ToListAsync();

            if (customers.Count != request.CustomerIds.Count)
                return ApiResponse<bool>.Fail("部分客户不存在");

            foreach (var customer in customers)
            {
                customer.BranchId = request.BranchId;
                customer.UpdatedAt = DateTime.Now;
                if (request.AssignToEmployeeId.HasValue)
                {
                    customer.CreatedById = request.AssignToEmployeeId.Value;
                }
            }

            await _dbContext.SaveChangesAsync();
            return ApiResponse<bool>.Ok(true, $"成功分配 {customers.Count} 个客户");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "批量分配客户失败");
            return ApiResponse<bool>.Fail($"批量分配失败: {ex.Message}");
        }
    }

    /// <summary>批量分配客户（带进度和取消支持）</summary>
    public async Task<ApiResponse<BatchOperationResult>> BulkAssignWithProgressAsync(
        BulkAssignRequest request, BatchOperationContext? context = null,
        IProgress<BatchOperationProgress>? progress = null)
    {
        context ??= new BatchOperationContext { OperationName = "批量分配客户" };
        var progressInfo = context.ProgressInfo;
        var ids = request.CustomerIds.Distinct().ToList();

        if (ids.Count == 0)
            return ApiResponse<BatchOperationResult>.Fail("请选择至少一个客户");
        if (ids.Count > context.MaxBatchSize)
            return ApiResponse<BatchOperationResult>.Fail($"单次批量操作不能超过 {context.MaxBatchSize} 项");

        progressInfo.Start("批量分配客户", ids.Count);
        var result = new BatchOperationResult { TotalCount = ids.Count, Items = new List<BatchOperationItemResult>() };

        foreach (var customerId in ids)
        {
            try
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                var customer = await _dbContext.Customers.FindAsync(customerId);
                if (customer == null)
                {
                    progressInfo.RecordFailure($"ID:{customerId}", "客户不存在", customerId);
                    result.Items.Add(new BatchOperationItemResult
                    { EntityId = customerId, EntityNo = $"ID:{customerId}", Success = false, Message = "客户不存在" });
                }
                else
                {
                    customer.BranchId = request.BranchId;
                    customer.UpdatedAt = DateTime.Now;
                    if (request.AssignToEmployeeId.HasValue)
                        customer.CreatedById = request.AssignToEmployeeId.Value;

                    await _dbContext.SaveChangesAsync();
                    progressInfo.RecordSuccess(customer.Name);
                    result.Items.Add(new BatchOperationItemResult
                    { EntityId = customerId, EntityNo = customer.Name, Success = true, Message = "分配成功" });
                }

                result.SuccessCount = result.Items.Count(i => i.Success);
                progress?.Report(progressInfo);
            }
            catch (OperationCanceledException)
            {
                progressInfo.Cancel();
                progress?.Report(progressInfo);
                return ApiResponse<BatchOperationResult>.Ok(result,
                    $"批量分配客户（已取消）：成功 {result.SuccessCount}，失败 {result.Items.Count(i => !i.Success)}");
            }
            catch (Exception ex)
            {
                progressInfo.RecordFailure($"ID:{customerId}", ex.Message, customerId);
                result.Items.Add(new BatchOperationItemResult
                { EntityId = customerId, EntityNo = $"ID:{customerId}", Success = false, Message = ex.Message });
                progress?.Report(progressInfo);

                if (!context.ContinueOnError) break;
            }
        }

        progressInfo.Complete();
        progress?.Report(progressInfo);
        return ApiResponse<BatchOperationResult>.Ok(result,
            $"批量分配客户完成：成功 {result.SuccessCount}，失败 {result.Items.Count(i => !i.Success)}");
    }

    public async Task<List<BusinessDistrict>> GetBusinessDistrictsAsync(int? branchId = null)
    {
        return await _dbContext.BusinessDistricts
            .AsNoTracking()
            .Where(b => b.Status == "Active" && (b.BranchId == null || b.BranchId == branchId))
            .OrderBy(b => b.Name)
            .ToListAsync();
    }

    public async Task<List<CustomerListItem>> GetMajorCustomersAsync(int branchId)
    {
        return await _dbContext.Customers
            .AsNoTracking()
            .Where(c => c.BranchId == branchId && c.CustomerType == CustomerType.Major && c.Status == CustomerStatus.Active)
            .Select(c => new CustomerListItem { Id = c.Id, Name = c.Name })
            .ToListAsync();
    }

    public async Task<Customer?> GetEntityByIdAsync(int id)
    {
        return await _dbContext.Customers.FindAsync(id);
    }

    public async Task<string> GenerateCustomerNoAsync(int branchId, CancellationToken cancellationToken = default)
    {
        var branch = await _dbContext.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == branchId, cancellationToken);
        var branchCode = branch?.Code ?? "0000";
        if (branchCode.Length != 4)
            branchCode = branchCode.PadLeft(4, '0').Substring(0, 4);

        var datePart = DateTime.Now.ToString("yyyyMMdd");
        var prefix = $"K{datePart}{branchCode}";

        var maxNo = await _dbContext.Customers
            .AsNoTracking()
            .Where(c => c.CustomerNo.StartsWith(prefix))
            .MaxAsync(c => (string?)c.CustomerNo, cancellationToken) ?? "";

        var seq = 1;
        if (maxNo.Length >= prefix.Length + 4)
        {
            var lastSeqStr = maxNo[prefix.Length..(prefix.Length + 4)];
            if (int.TryParse(lastSeqStr, out var parsedSeq))
                seq = parsedSeq;
            seq++;
        }

        return $"{prefix}{seq:D4}";
    }
}
