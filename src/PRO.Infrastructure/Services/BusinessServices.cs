using Microsoft.EntityFrameworkCore;
using PRO.Application.DTOs;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;
using PRO.Application.Interfaces;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 客户服务实现
/// </summary>
public class CustomerService : ICustomerService
{
    private readonly ProDbContext _dbContext;

    public CustomerService(ProDbContext dbContext)
    {
        _dbContext = dbContext;
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

            // 计算订单统计
            var customerIds = items.Select(c => c.Id).ToList();
            var orderStats = await _dbContext.Orders.AsNoTracking()
                .Where(o => customerIds.Contains(o.CustomerId))
                .GroupBy(o => o.CustomerId)
                .Select(g => new { CustomerId = g.Key, Count = g.Count(), Total = g.Sum(o => o.TotalAmount) })
                .ToDictionaryAsync(x => x.CustomerId, x => new { x.Count, x.Total });

            var result = items.Select(c =>
            {
                var stats = orderStats.GetValueOrDefault(c.Id);
                return new CustomerListItem
                {
                    Id = c.Id, Name = c.Name, CustomerNo = c.CustomerNo, CustomerType = c.CustomerType,
                    CustomerTypeName = c.CustomerType == CustomerType.Major ? "大客户" : "细分客户",
                    Phone = c.Phone, Address = c.FullAddress ?? c.Address,
                    ParentCustomerId = c.ParentCustomerId, ParentCustomerName = c.ParentCustomer?.Name,
                    BranchName = c.Branch?.Name ?? "", BranchId = c.BranchId, Status = c.Status,
                    OrderCount = stats?.Count ?? 0, TotalOrderAmount = stats?.Total ?? 0m,
                    CreatedAt = c.CreatedAt,
                    CustomerManagerName = c.CustomerManager?.Name, CreatorName = c.Creator?.Name
                };
            }).ToList();

            return ApiResponse<PagedResult<CustomerListItem>>.Ok(new PagedResult<CustomerListItem>
            {
                Items = result, TotalCount = totalCount, PageIndex = request.PageIndex, PageSize = request.PageSize
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
                return ApiResponse<CustomerDetailDto>.Fail("客户不存在");

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
                Id = first.Id, Name = first.Name, CustomerNo = first.CustomerNo,
                CustomerType = first.CustomerType, BranchName = first.Branch?.Name ?? ""
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
                return ApiResponse<bool>.Fail("客户不存在");

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
                return ApiResponse<bool>.Fail("客户不存在");

            var orderCount = await _dbContext.Orders.CountAsync(o => o.CustomerId == id);
            if (orderCount > 0)
                return ApiResponse<bool>.Fail("该客户已关联订单，无法删除");

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

            if (!string.IsNullOrWhiteSpace(phone))
            {
                var phoneMatches = await _dbContext.Customers.AsNoTracking()
                    .Where(c => c.Phone == phone && c.Status == CustomerStatus.Active)
                    .Take(5).ToListAsync();
                duplicates.AddRange(phoneMatches.Select(c => new CustomerDuplicateItem
                {
                    Id = c.Id, Name = c.Name, Phone = c.Phone, Address = c.Address,
                    LegalPerson = c.LegalPerson, MatchType = "phone", Similarity = 1.0
                }));
            }

            return ApiResponse<CustomerDuplicateCheckResult>.Ok(new CustomerDuplicateCheckResult
            {
                HasDuplicates = duplicates.Count > 0,
                Duplicates = duplicates.DistinctBy(d => d.Id).ToList()
            });
        }
        catch (Exception ex)
        {
            return ApiResponse<CustomerDuplicateCheckResult>.Fail($"查重失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> MergeCustomersAsync(MergeCustomerRequest request)
    {
        try
        {
            var mainCustomer = await _dbContext.Customers.FindAsync(request.MainCustomerId);
            if (mainCustomer == null)
                return ApiResponse<bool>.Fail("主客户不存在");

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
}

/// <summary>
/// 订单服务实现
/// </summary>
public class OrderService : IOrderService
{
    private readonly ProDbContext _dbContext;

    public OrderService(ProDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ApiResponse<PagedResult<OrderListItem>>> GetListAsync(PagedRequest request, int? branchId = null, OrderStatus? status = null, PaymentStatus? paymentStatus = null)
    {
        try
        {
            var query = _dbContext.Orders.AsNoTracking()
                .Include(o => o.Customer)
                .Include(o => o.Branch)
                .Include(o => o.DeliveryPerson)
                .Include(o => o.Creator)
                .AsQueryable();

            if (branchId.HasValue)
                query = query.Where(o => o.BranchId == branchId.Value);
            if (status.HasValue)
                query = query.Where(o => o.Status == status.Value);
            if (paymentStatus.HasValue)
                query = query.Where(o => o.PaymentStatus == paymentStatus.Value);
            if (!string.IsNullOrWhiteSpace(request.Keyword))
                query = query.Where(o => o.OrderNo.Contains(request.Keyword) || (o.Customer != null && o.Customer.Name.Contains(request.Keyword)));

            var totalCount = await query.CountAsync();
            var items = await query.OrderByDescending(o => o.CreatedAt)
                .Skip((request.PageIndex - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(o => new OrderListItem
                {
                    Id = o.Id, OrderNo = o.OrderNo, CustomerId = o.CustomerId,
                    CustomerName = o.Customer != null ? o.Customer.Name : "未知",
                    BranchId = o.BranchId, BranchName = o.Branch != null ? o.Branch.Name : "",
                    TotalAmount = o.TotalAmount, Status = o.Status, PaymentStatus = o.PaymentStatus,
                    CreatedAt = o.CreatedAt, DeliveryPersonName = o.DeliveryPerson != null ? o.DeliveryPerson.Name : null,
                    CreatedByName = o.Creator != null ? o.Creator.Name : ""
                })
                .ToListAsync();

            return ApiResponse<PagedResult<OrderListItem>>.Ok(new PagedResult<OrderListItem>
            {
                Items = items, TotalCount = totalCount, PageIndex = request.PageIndex, PageSize = request.PageSize
            });
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<OrderListItem>>.Fail($"查询订单列表失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<OrderDetailDto>> GetByIdAsync(int id)
    {
        try
        {
            var order = await _dbContext.Orders.AsNoTracking()
                .Include(o => o.Customer).Include(o => o.Branch)
                .Include(o => o.DeliveryPerson).Include(o => o.Creator)
                .Include(o => o.Items).ThenInclude(i => i.Product)
                .Include(o => o.ModificationRecords).ThenInclude(r => r.ModifiedBy)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
                return ApiResponse<OrderDetailDto>.Fail("订单不存在");

            var dto = new OrderDetailDto
            {
                Id = order.Id, OrderNo = order.OrderNo, CustomerId = order.CustomerId,
                CustomerName = order.Customer?.Name ?? "", BranchId = order.BranchId,
                BranchName = order.Branch?.Name ?? "", TotalAmount = order.TotalAmount,
                ReceivedAmount = order.ReceivedAmount,
                Status = order.Status, PaymentStatus = order.PaymentStatus,
                DeliveryAddress = order.DeliveryAddress, Remark = order.Remark,
                CreatedAt = order.CreatedAt, UpdatedAt = order.UpdatedAt,
                CreatedByName = order.Creator?.Name ?? "",
                DeliveryPersonName = order.DeliveryPerson?.Name,
                DeliveryPersonId = order.DeliveryPersonId,
                CreatedById = order.CreatedById,
                SettlementId = order.SettlementId,
                DraftExpireTime = order.DraftExpireTime,
                Items = order.Items.Select(i => new OrderItemDto
                {
                    Id = i.Id, OrderId = i.OrderId, ProductId = i.ProductId,
                    ProductName = i.Product?.Name ?? "",
                    Quantity = i.Quantity, UnitPrice = i.UnitPrice, Amount = i.Amount,
                    DiscountType = i.DiscountType, DiscountValue = i.DiscountValue,
                    Remark = i.Remark
                }).ToList(),
                ModificationRecords = order.ModificationRecords.Select(r => new OrderModificationRecordDto
                {
                    Id = r.Id, OrderId = r.OrderId,
                    ModifiedAt = r.ModifiedAt, Content = r.Content,
                    ModificationType = r.ModificationType,
                    ModifiedByName = r.ModifiedBy?.Name ?? "",
                    ModifiedById = r.ModifiedById
                }).ToList()
            };

            return ApiResponse<OrderDetailDto>.Ok(dto);
        }
        catch (Exception ex)
        {
            return ApiResponse<OrderDetailDto>.Fail($"查询订单详情失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<int>> CreateAsync(CreateOrderRequest request, int createdById)
    {
        try
        {
            var branch = await _dbContext.Employees.Where(e => e.Id == createdById)
                .Select(e => e.BranchId).FirstOrDefaultAsync();

            var order = new Order
            {
                CustomerId = request.CustomerId,
                BranchId = branch,
                CreatedById = createdById,
                DeliveryAddress = request.DeliveryAddress,
                Remark = request.Remark,
                Status = request.IsDraft ? OrderStatus.Draft : OrderStatus.Pending,
                PaymentStatus = PaymentStatus.Unpaid,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                LocalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            // 生成订单号
            var today = DateTime.Now.ToString("yyyyMMdd");
            var branchCode = (await _dbContext.Branches.FindAsync(branch))?.Code ?? "0000";
            var lastOrder = await _dbContext.Orders
                .Where(o => o.OrderNo.StartsWith($"D{today}{branchCode}"))
                .OrderByDescending(o => o.OrderNo).FirstOrDefaultAsync();
            var seq = 1;
            if (lastOrder != null && lastOrder.OrderNo.Length >= 16 && int.TryParse(lastOrder.OrderNo[12..], out var lastSeq))
                seq = lastSeq + 1;
            order.OrderNo = $"D{today}{branchCode}{seq:D4}";

            decimal total = 0;
            foreach (var item in request.Items)
            {
                var orderItem = new OrderItem
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    Amount = item.Quantity * item.UnitPrice,
                    Remark = item.Remark
                };
                total += orderItem.Amount;
                order.Items.Add(orderItem);
            }
            order.TotalAmount = total;

            _dbContext.Orders.Add(order);
            await _dbContext.SaveChangesAsync();

            return ApiResponse<int>.Ok(order.Id, "订单创建成功");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"创建订单失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> UpdateAsync(UpdateOrderRequest request, int modifiedById)
    {
        try
        {
            var order = await _dbContext.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == request.Id);
            if (order == null)
                return ApiResponse<bool>.Fail("订单不存在");

            order.CustomerId = request.CustomerId;
            order.DeliveryAddress = request.DeliveryAddress;
            order.Remark = request.Remark;
            order.UpdatedAt = DateTime.Now;
            order.LocalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            order.SyncStatus = SyncStatus.Pending;

            _dbContext.OrderItems.RemoveRange(order.Items);
            decimal total = 0;
            foreach (var item in request.Items)
            {
                var orderItem = new OrderItem
                {
                    OrderId = order.Id, ProductId = item.ProductId,
                    Quantity = item.Quantity, UnitPrice = item.UnitPrice,
                    Amount = item.Quantity * item.UnitPrice, Remark = item.Remark
                };
                total += orderItem.Amount;
                _dbContext.OrderItems.Add(orderItem);
            }
            order.TotalAmount = total;

            _dbContext.OrderModificationRecords.Add(new OrderModificationRecord
            {
                OrderId = order.Id, ModifiedById = modifiedById,
                ModifiedAt = DateTime.Now, Content = "订单更新", ModificationType = "Update"
            });

            await _dbContext.SaveChangesAsync();
            return ApiResponse<bool>.Ok(true, "订单更新成功");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"更新订单失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id)
    {
        try
        {
            var order = await _dbContext.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
            if (order == null)
                return ApiResponse<bool>.Fail("订单不存在");
            if (order.SettlementId != null)
                return ApiResponse<bool>.Fail("已结算的订单不能删除");

            _dbContext.OrderItems.RemoveRange(order.Items);
            _dbContext.Orders.Remove(order);
            await _dbContext.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "订单已删除");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"删除订单失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> AssignAsync(AssignOrderRequest request, int assignedById)
    {
        try
        {
            var order = await _dbContext.Orders.FindAsync(request.OrderId);
            if (order == null)
                return ApiResponse<bool>.Fail("订单不存在");

            order.DeliveryPersonId = request.DeliveryPersonId;
            order.Status = OrderStatus.Assigned;
            order.UpdatedAt = DateTime.Now;
            order.SyncStatus = SyncStatus.Pending;

            _dbContext.OrderModificationRecords.Add(new OrderModificationRecord
            {
                OrderId = order.Id, ModifiedById = assignedById,
                ModifiedAt = DateTime.Now, Content = $"分配配送员", ModificationType = "Assign"
            });

            await _dbContext.SaveChangesAsync();
            return ApiResponse<bool>.Ok(true, "订单分配成功");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"分配订单失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> UpdateStatusAsync(UpdateOrderStatusRequest request, int modifiedById)
    {
        try
        {
            var order = await _dbContext.Orders.FindAsync(request.OrderId);
            if (order == null)
                return ApiResponse<bool>.Fail("订单不存在");

            order.Status = request.NewStatus;
            order.UpdatedAt = DateTime.Now;
            order.SyncStatus = SyncStatus.Pending;

            if (request.NewStatus == OrderStatus.Completed)
                order.SignedTime = DateTime.Now;
            if (request.NewStatus == OrderStatus.Cancelled)
                order.CancelReason = request.Reason;

            _dbContext.OrderModificationRecords.Add(new OrderModificationRecord
            {
                OrderId = order.Id, ModifiedById = modifiedById,
                ModifiedAt = DateTime.Now, Content = $"状态变更为{request.NewStatus}", ModificationType = "StatusChange"
            });

            await _dbContext.SaveChangesAsync();
            return ApiResponse<bool>.Ok(true, "订单状态更新成功");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"更新订单状态失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> ConfirmDraftAsync(int orderId, int modifiedById)
    {
        try
        {
            var order = await _dbContext.Orders.FindAsync(orderId);
            if (order == null)
                return ApiResponse<bool>.Fail("订单不存在");
            if (order.Status != OrderStatus.Draft)
                return ApiResponse<bool>.Fail("只有草稿订单可以确认");

            order.Status = OrderStatus.Pending;
            order.UpdatedAt = DateTime.Now;
            order.SyncStatus = SyncStatus.Pending;

            _dbContext.OrderModificationRecords.Add(new OrderModificationRecord
            {
                OrderId = order.Id, ModifiedById = modifiedById,
                ModifiedAt = DateTime.Now, Content = "草稿确认", ModificationType = "DraftConfirm"
            });

            await _dbContext.SaveChangesAsync();
            return ApiResponse<bool>.Ok(true, "草稿已确认");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"确认草稿失败: {ex.Message}");
        }
    }

    public Task<ApiResponse<string>> ExportToExcelAsync(PagedRequest request, int? branchId = null, bool forGaode = false)
        => Task.FromResult(ApiResponse<string>.Fail("Excel导出功能请在界面层实现"));

    public Task<ApiResponse<string>> ExportDeliveryPlanAsync(List<int> orderIds, string groupName)
        => Task.FromResult(ApiResponse<string>.Fail("配送计划导出功能请在界面层实现"));
}

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
                .Include(s => s.Branch)
                .Include(s => s.ConfirmedBy)
                .AsQueryable();

            if (branchId.HasValue)
                query = query.Where(s => s.BranchId == branchId.Value);

            if (!string.IsNullOrWhiteSpace(request.Keyword))
                query = query.Where(s => s.SettlementNo.Contains(request.Keyword));

            var totalCount = await query.CountAsync();
            var items = await query.OrderByDescending(s => s.CreatedAt)
                .Skip((request.PageIndex - 1) * request.PageSize)
                .Take(request.PageSize)
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
                })
                .ToListAsync();

            return ApiResponse<PagedResult<SettlementListItem>>.Ok(new PagedResult<SettlementListItem>
            {
                Items = items,
                TotalCount = totalCount,
                PageIndex = request.PageIndex,
                PageSize = request.PageSize
            });
        }
        catch (Exception ex)
        {
            return ApiResponse<PagedResult<SettlementListItem>>.Fail($"查询结算列表失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<SettlementDetailDto>> GetByIdAsync(int id)
    {
        try
        {
            var settlement = await _dbContext.Settlements.AsNoTracking()
                .Include(s => s.Branch)
                .Include(s => s.ConfirmedBy)
                .Include(s => s.Orders).ThenInclude(o => o.Customer)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (settlement == null)
                return ApiResponse<SettlementDetailDto>.Fail("结算单不存在");

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
        catch (Exception ex)
        {
            return ApiResponse<SettlementDetailDto>.Fail($"查询结算详情失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<SettlementPreviewDto>> PreviewAsync(CreateSettlementRequest request)
    {
        try
        {
            if (request.EndDate.Date < request.StartDate.Date)
                return ApiResponse<SettlementPreviewDto>.Fail("结算结束日期不能早于开始日期");

            var branch = await _dbContext.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == request.BranchId);
            if (branch == null)
                return ApiResponse<SettlementPreviewDto>.Fail("分公司不存在");

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
        catch (Exception ex)
        {
            return ApiResponse<SettlementPreviewDto>.Fail($"生成结算预览失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<int>> CreateAsync(CreateSettlementRequest request, int confirmedById)
    {
        try
        {
            if (request.EndDate.Date < request.StartDate.Date)
                return ApiResponse<int>.Fail("结算结束日期不能早于开始日期");

            var orders = await BuildSettlementOrdersQuery(request).ToListAsync();
            if (orders.Count == 0)
                return ApiResponse<int>.Fail("没有可结算的订单");

            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            var now = DateTime.Now;
            var localTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
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
                LocalTimestamp = localTimestamp,
                SyncStatus = SyncStatus.Pending
            };

            _dbContext.Settlements.Add(settlement);
            await _dbContext.SaveChangesAsync();

            foreach (var order in orders)
            {
                order.SettlementId = settlement.Id;
                order.UpdatedAt = now;
                order.LocalTimestamp = localTimestamp;
                order.SyncStatus = SyncStatus.Pending;
            }

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            return ApiResponse<int>.Ok(settlement.Id, "结算创建成功");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"创建结算失败: {ex.Message}");
        }
    }

    public async Task<ApiResponse<string>> GeneratePdfAsync(int settlementId)
    {
        var pdfPath = await _dbContext.Settlements.AsNoTracking()
            .Where(s => s.Id == settlementId)
            .Select(s => s.PdfPath)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(pdfPath))
            return ApiResponse<string>.Fail("该结算单尚未生成PDF，请在桌面端结算页面生成");

        if (!File.Exists(pdfPath))
            return ApiResponse<string>.Fail("结算PDF文件不存在或已被移动");

        return ApiResponse<string>.Ok(pdfPath);
    }

    public Task<ApiResponse<string>> DownloadPdfAsync(int settlementId)
    {
        return GeneratePdfAsync(settlementId);
    }

    public async Task<ApiResponse<string>> BatchDownloadPdfAsync(List<int> settlementIds)
    {
        var paths = await _dbContext.Settlements.AsNoTracking()
            .Where(s => settlementIds.Contains(s.Id) && !string.IsNullOrWhiteSpace(s.PdfPath))
            .Select(s => s.PdfPath!)
            .ToListAsync();

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
            .Where(o => o.BranchId == request.BranchId
                && o.Status == OrderStatus.Completed
                && o.SettlementId == null
                && o.CreatedAt >= start
                && o.CreatedAt < endExclusive);
    }
}

/// <summary>
/// 产品服务实现
/// </summary>
public class ProductService : IProductService
{
    private readonly ProDbContext _dbContext;

    public ProductService(ProDbContext dbContext) { _dbContext = dbContext; }

    public async Task<ApiResponse<PagedResult<ProductListItem>>> GetListAsync(PagedRequest request, int? categoryId = null, ProductStatus? status = null)
    {
        try
        {
            var query = _dbContext.Products.AsNoTracking().Include(p => p.Category).AsQueryable();
            if (categoryId.HasValue) query = query.Where(p => p.CategoryId == categoryId.Value);
            if (status.HasValue) query = query.Where(p => p.Status == status.Value);
            if (!string.IsNullOrWhiteSpace(request.Keyword))
                query = query.Where(p => p.Name.Contains(request.Keyword) || p.SKU.Contains(request.Keyword));

            var totalCount = await query.CountAsync();
            var items = await query.OrderBy(p => p.SKU)
                .Skip((request.PageIndex - 1) * request.PageSize).Take(request.PageSize)
                .Select(p => new ProductListItem
                {
                    Id = p.Id, SKU = p.SKU, Name = p.Name, Specification = p.Specification,
                    AverageSalePrice = p.AverageSalePrice, ReferencePrice = p.ReferencePrice,
                    Stock = p.Stock, Unit = p.Unit, Status = p.Status,
                    CategoryName = p.Category != null ? p.Category.Name : null
                }).ToListAsync();

            return ApiResponse<PagedResult<ProductListItem>>.Ok(new PagedResult<ProductListItem>
            { Items = items, TotalCount = totalCount, PageIndex = request.PageIndex, PageSize = request.PageSize });
        }
        catch (Exception ex) { return ApiResponse<PagedResult<ProductListItem>>.Fail($"查询产品列表失败: {ex.Message}"); }
    }

    public async Task<ApiResponse<ProductListItem>> GetByIdAsync(int id)
    {
        var p = await _dbContext.Products.AsNoTracking().Include(x => x.Category).FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return ApiResponse<ProductListItem>.Fail("产品不存在");
        return ApiResponse<ProductListItem>.Ok(new ProductListItem
        {
            Id = p.Id, SKU = p.SKU, Name = p.Name, Specification = p.Specification,
            AverageSalePrice = p.AverageSalePrice, ReferencePrice = p.ReferencePrice,
            Stock = p.Stock, Unit = p.Unit, Status = p.Status, CategoryName = p.Category?.Name
        });
    }

    public async Task<ApiResponse<List<ProductCategoryDto>>> GetCategoriesAsync()
    {
        var cats = await _dbContext.ProductCategories.AsNoTracking().OrderBy(c => c.SortOrder).ToListAsync();
        return ApiResponse<List<ProductCategoryDto>>.Ok(cats.Select(c => new ProductCategoryDto { Id = c.Id, Name = c.Name }).ToList());
    }

    public async Task<ApiResponse<int>> CreateAsync(CreateProductRequest request)
    {
        try
        {
            if (await _dbContext.Products.AnyAsync(p => p.SKU == request.SKU))
                return ApiResponse<int>.Fail("SKU编码已存在");

            var product = new Product
            {
                SKU = request.SKU, Name = request.Name, Specification = request.Specification,
                AverageSalePrice = request.AverageSalePrice, ReferencePrice = request.ReferencePrice,
                Stock = request.Stock, Unit = request.Unit, CategoryId = request.CategoryId,
                Status = ProductStatus.Active, CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now,
                LocalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            _dbContext.Products.Add(product);
            await _dbContext.SaveChangesAsync();
            return ApiResponse<int>.Ok(product.Id, "产品创建成功");
        }
        catch (Exception ex) { return ApiResponse<int>.Fail($"创建产品失败: {ex.Message}"); }
    }

    public async Task<ApiResponse<bool>> UpdateAsync(UpdateProductRequest request)
    {
        try
        {
            var product = await _dbContext.Products.FindAsync(request.Id);
            if (product == null) return ApiResponse<bool>.Fail("产品不存在");

            product.Name = request.Name; product.Specification = request.Specification;
            product.AverageSalePrice = request.AverageSalePrice; product.ReferencePrice = request.ReferencePrice;
            product.Unit = request.Unit; product.CategoryId = request.CategoryId;
            product.Status = request.Status; product.UpdatedAt = DateTime.Now;
            product.LocalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            product.SyncStatus = SyncStatus.Pending;

            await _dbContext.SaveChangesAsync();
            return ApiResponse<bool>.Ok(true, "产品更新成功");
        }
        catch (Exception ex) { return ApiResponse<bool>.Fail($"更新产品失败: {ex.Message}"); }
    }

    public async Task<ApiResponse<bool>> UpdateStockAsync(int id, int quantity)
    {
        var product = await _dbContext.Products.FindAsync(id);
        if (product == null) return ApiResponse<bool>.Fail("产品不存在");
        product.Stock = quantity; product.UpdatedAt = DateTime.Now; product.SyncStatus = SyncStatus.Pending;
        await _dbContext.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id)
    {
        var product = await _dbContext.Products.FindAsync(id);
        if (product == null) return ApiResponse<bool>.Fail("产品不存在");
        var hasOrders = await _dbContext.OrderItems.AnyAsync(i => i.ProductId == id);
        if (hasOrders) return ApiResponse<bool>.Fail("该产品已关联订单，无法删除");
        _dbContext.Products.Remove(product);
        await _dbContext.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true, "产品已删除");
    }
}

/// <summary>
/// 配送员服务实现
/// </summary>
public class DeliveryPersonService : IDeliveryPersonService
{
    private readonly ProDbContext _dbContext;
    public DeliveryPersonService(ProDbContext dbContext) { _dbContext = dbContext; }

    public async Task<ApiResponse<PagedResult<DeliveryPersonListItem>>> GetListAsync(PagedRequest request, int? branchId = null, DeliveryPersonStatus? status = null)
    {
        var query = _dbContext.DeliveryPersons.AsNoTracking().Include(d => d.Branch).AsQueryable();
        if (branchId.HasValue) query = query.Where(d => d.BranchId == branchId.Value);
        if (status.HasValue) query = query.Where(d => d.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
            query = query.Where(d => d.Name.Contains(request.Keyword) || d.Phone.Contains(request.Keyword));

        var totalCount = await query.CountAsync();
        var items = await query.OrderBy(d => d.Name).Skip((request.PageIndex - 1) * request.PageSize).Take(request.PageSize)
            .Select(d => new DeliveryPersonListItem
            {
                Id = d.Id, Name = d.Name, Phone = d.Phone,
                BranchId = d.BranchId, BranchName = d.Branch != null ? d.Branch.Name : "",
                ServiceArea = d.ServiceArea, VehicleNumber = d.VehicleNumber, WeChatId = d.WeChatId,
                CurrentLoad = d.CurrentLoad, MaxLoad = d.MaxLoad, Status = d.Status,
                StatusName = GetDeliveryPersonStatusName(d.Status)
            }).ToListAsync();

        return ApiResponse<PagedResult<DeliveryPersonListItem>>.Ok(new PagedResult<DeliveryPersonListItem>
        { Items = items, TotalCount = totalCount, PageIndex = request.PageIndex, PageSize = request.PageSize });
    }

    public async Task<ApiResponse<DeliveryPersonListItem>> GetByIdAsync(int id)
    {
        var d = await _dbContext.DeliveryPersons.AsNoTracking().Include(x => x.Branch).FirstOrDefaultAsync(x => x.Id == id);
        if (d == null) return ApiResponse<DeliveryPersonListItem>.Fail("配送员不存在");
        return ApiResponse<DeliveryPersonListItem>.Ok(new DeliveryPersonListItem
        {
            Id = d.Id, Name = d.Name, Phone = d.Phone,
            BranchId = d.BranchId, BranchName = d.Branch?.Name ?? "",
            ServiceArea = d.ServiceArea, VehicleNumber = d.VehicleNumber, WeChatId = d.WeChatId,
            CurrentLoad = d.CurrentLoad, MaxLoad = d.MaxLoad, Status = d.Status,
            StatusName = GetDeliveryPersonStatusName(d.Status)
        });
    }

    public async Task<ApiResponse<List<DeliveryPersonListItem>>> GetAvailableAsync(int branchId)
    {
        var items = await _dbContext.DeliveryPersons.AsNoTracking().Include(d => d.Branch)
            .Where(d => d.BranchId == branchId && d.Status == DeliveryPersonStatus.Available && d.CurrentLoad < d.MaxLoad)
            .OrderByDescending(d => d.MaxLoad - d.CurrentLoad)
            .Select(d => new DeliveryPersonListItem
            {
                Id = d.Id, Name = d.Name, Phone = d.Phone,
                BranchId = d.BranchId, BranchName = d.Branch != null ? d.Branch.Name : "",
                ServiceArea = d.ServiceArea, VehicleNumber = d.VehicleNumber, WeChatId = d.WeChatId,
                CurrentLoad = d.CurrentLoad, MaxLoad = d.MaxLoad, Status = d.Status,
                StatusName = GetDeliveryPersonStatusName(d.Status)
            }).ToListAsync();
        return ApiResponse<List<DeliveryPersonListItem>>.Ok(items);
    }

    public async Task<ApiResponse<int>> CreateAsync(CreateDeliveryPersonRequest request)
    {
        var dp = new DeliveryPerson
        {
            Name = request.Name, Phone = request.Phone, BranchId = request.BranchId,
            ServiceArea = request.ServiceArea, VehicleNumber = request.VehicleNumber, WeChatId = request.WeChatId,
            MaxLoad = request.MaxLoad, Status = DeliveryPersonStatus.Available,
            CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now,
            LocalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        _dbContext.DeliveryPersons.Add(dp);
        await _dbContext.SaveChangesAsync();
        return ApiResponse<int>.Ok(dp.Id, "配送员创建成功");
    }

    public async Task<ApiResponse<bool>> UpdateAsync(UpdateDeliveryPersonRequest request)
    {
        var dp = await _dbContext.DeliveryPersons.FindAsync(request.Id);
        if (dp == null) return ApiResponse<bool>.Fail("配送员不存在");
        dp.Name = request.Name; dp.Phone = request.Phone; dp.BranchId = request.BranchId;
        dp.ServiceArea = request.ServiceArea; dp.VehicleNumber = request.VehicleNumber; dp.WeChatId = request.WeChatId;
        dp.MaxLoad = request.MaxLoad; dp.Status = request.Status;
        dp.UpdatedAt = DateTime.Now; dp.SyncStatus = SyncStatus.Pending;
        await _dbContext.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true, "配送员更新成功");
    }

    public async Task<ApiResponse<bool>> UpdateLoadAsync(int id, int currentLoad)
    {
        var dp = await _dbContext.DeliveryPersons.FindAsync(id);
        if (dp == null) return ApiResponse<bool>.Fail("配送员不存在");
        dp.CurrentLoad = currentLoad;
        dp.Status = currentLoad >= dp.MaxLoad ? DeliveryPersonStatus.Busy : DeliveryPersonStatus.Available;
        dp.UpdatedAt = DateTime.Now;
        await _dbContext.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id)
    {
        var dp = await _dbContext.DeliveryPersons.FindAsync(id);
        if (dp == null) return ApiResponse<bool>.Fail("配送员不存在");
        var hasOrders = await _dbContext.Orders.AnyAsync(o => o.DeliveryPersonId == id && o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled);
        if (hasOrders) return ApiResponse<bool>.Fail("该配送员有未完成订单，无法删除");
        _dbContext.DeliveryPersons.Remove(dp);
        await _dbContext.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true, "配送员已删除");
    }

    private static string GetDeliveryPersonStatusName(DeliveryPersonStatus status) => status switch
    {
        DeliveryPersonStatus.Available => "可用",
        DeliveryPersonStatus.Busy => "忙碌",
        DeliveryPersonStatus.Off => "休息",
        _ => "未知"
    };
}

/// <summary>
/// 分公司服务实现
/// </summary>
public class BranchService : IBranchService
{
    private readonly ProDbContext _dbContext;
    public BranchService(ProDbContext dbContext) { _dbContext = dbContext; }

    public async Task<ApiResponse<PagedResult<BranchListItem>>> GetListAsync(PagedRequest request)
    {
        var query = _dbContext.Branches.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Keyword))
            query = query.Where(b => b.Name.Contains(request.Keyword) || b.Code.Contains(request.Keyword));

        var totalCount = await query.CountAsync();
        var items = await query.OrderBy(b => b.Code).Skip((request.PageIndex - 1) * request.PageSize).Take(request.PageSize)
            .Select(b => new BranchListItem
            {
                Id = b.Id, Name = b.Name, Code = b.Code, Address = b.Address,
                Phone = b.Phone, RegionId = b.RegionId, Status = b.Status,
                DepartmentCount = b.Departments.Count, EmployeeCount = b.Employees.Count
            }).ToListAsync();

        return ApiResponse<PagedResult<BranchListItem>>.Ok(new PagedResult<BranchListItem>
        { Items = items, TotalCount = totalCount, PageIndex = request.PageIndex, PageSize = request.PageSize });
    }

    public async Task<ApiResponse<BranchListItem>> GetByIdAsync(int id)
    {
        var b = await _dbContext.Branches.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (b == null) return ApiResponse<BranchListItem>.Fail("分公司不存在");
        return ApiResponse<BranchListItem>.Ok(new BranchListItem
        { Id = b.Id, Name = b.Name, Code = b.Code, Address = b.Address, Phone = b.Phone, Status = b.Status });
    }

    public async Task<ApiResponse<int>> CreateAsync(string name, string code, int? regionId, string? address, string? phone)
    {
        if (await _dbContext.Branches.AnyAsync(b => b.Code == code))
            return ApiResponse<int>.Fail("分公司编码已存在");
        var branch = new Branch { Name = name, Code = code, RegionId = regionId, Address = address, Phone = phone, Status = EntityStatus.Active };
        _dbContext.Branches.Add(branch);
        await _dbContext.SaveChangesAsync();
        return ApiResponse<int>.Ok(branch.Id, "分公司创建成功");
    }

    public async Task<ApiResponse<bool>> UpdateAsync(int id, string name, string code, int? regionId, string? address, string? phone, int status)
    {
        var b = await _dbContext.Branches.FindAsync(id);
        if (b == null) return ApiResponse<bool>.Fail("分公司不存在");
        b.Name = name; b.Code = code; b.RegionId = regionId; b.Address = address; b.Phone = phone;
        b.Status = (EntityStatus)status; b.UpdatedAt = DateTime.Now;
        await _dbContext.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true, "分公司更新成功");
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id)
    {
        var b = await _dbContext.Branches.FindAsync(id);
        if (b == null) return ApiResponse<bool>.Fail("分公司不存在");
        var hasEmployees = await _dbContext.Employees.AnyAsync(e => e.BranchId == id);
        if (hasEmployees) return ApiResponse<bool>.Fail("该分公司下有员工，无法删除");
        _dbContext.Branches.Remove(b);
        await _dbContext.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true, "分公司已删除");
    }
}

/// <summary>
/// 员工服务实现
/// </summary>
public class EmployeeService : IEmployeeService
{
    private readonly ProDbContext _dbContext;
    private readonly IEncryptionService _encryptionService;

    public EmployeeService(ProDbContext dbContext, IEncryptionService encryptionService)
    {
        _dbContext = dbContext;
        _encryptionService = encryptionService;
    }

    public async Task<ApiResponse<PagedResult<EmployeeListItem>>> GetListAsync(PagedRequest request)
    {
        var query = _dbContext.Employees.AsNoTracking()
            .Include(e => e.Role).Include(e => e.Branch).Include(e => e.Department).AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Keyword))
            query = query.Where(e => e.Name.Contains(request.Keyword) || e.EmployeeNo.Contains(request.Keyword));

        var totalCount = await query.CountAsync();
        var items = await query.OrderBy(e => e.Id).Skip((request.PageIndex - 1) * request.PageSize).Take(request.PageSize)
            .Select(e => new EmployeeListItem
            {
                Id = e.Id, Name = e.Name, EmployeeNo = e.EmployeeNo,
                DepartmentName = e.Department != null ? e.Department.Name : "",
                DepartmentId = e.DepartmentId, BranchName = e.Branch != null ? e.Branch.Name : "",
                BranchId = e.BranchId, RoleName = e.Role != null ? e.Role.Name : "",
                Phone = e.Phone, Status = e.Status, CreatedAt = e.CreatedAt
            }).ToListAsync();

        return ApiResponse<PagedResult<EmployeeListItem>>.Ok(new PagedResult<EmployeeListItem>
        { Items = items, TotalCount = totalCount, PageIndex = request.PageIndex, PageSize = request.PageSize });
    }

    public async Task<ApiResponse<EmployeeListItem>> GetByIdAsync(int id)
    {
        var e = await _dbContext.Employees.AsNoTracking()
            .Include(x => x.Role).Include(x => x.Branch).Include(x => x.Department)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (e == null) return ApiResponse<EmployeeListItem>.Fail("员工不存在");
        return ApiResponse<EmployeeListItem>.Ok(new EmployeeListItem
        {
            Id = e.Id, Name = e.Name, EmployeeNo = e.EmployeeNo,
            DepartmentName = e.Department?.Name ?? "", DepartmentId = e.DepartmentId,
            BranchName = e.Branch?.Name ?? "", BranchId = e.BranchId,
            RoleName = e.Role?.Name ?? "", Phone = e.Phone, Status = e.Status, CreatedAt = e.CreatedAt
        });
    }

    public async Task<ApiResponse<int>> CreateAsync(CreateEmployeeRequest request)
    {
        if (await _dbContext.Employees.AnyAsync(e => e.EmployeeNo == request.EmployeeNo))
            return ApiResponse<int>.Fail("工号已存在");
        var emp = new Employee
        {
            Name = request.Name, EmployeeNo = request.EmployeeNo,
            PasswordHash = _encryptionService.HashPassword(request.Password),
            DepartmentId = request.DepartmentId, BranchId = request.BranchId,
            RoleId = request.RoleId, Phone = request.Phone, Email = request.Email,
            Gender = request.Gender, Status = EmployeeStatus.Active
        };
        _dbContext.Employees.Add(emp);
        await _dbContext.SaveChangesAsync();
        return ApiResponse<int>.Ok(emp.Id, "员工创建成功");
    }

    public async Task<ApiResponse<bool>> UpdateAsync(UpdateEmployeeRequest request)
    {
        var emp = await _dbContext.Employees.FindAsync(request.Id);
        if (emp == null) return ApiResponse<bool>.Fail("员工不存在");
        emp.Name = request.Name; emp.DepartmentId = request.DepartmentId;
        emp.RoleId = request.RoleId; emp.Phone = request.Phone;
        emp.Email = request.Email; emp.Gender = request.Gender;
        emp.Status = request.Status; emp.UpdatedAt = DateTime.Now;
        await _dbContext.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true, "员工更新成功");
    }

    public async Task<ApiResponse<bool>> UpdatePasswordAsync(int id, string newPassword)
    {
        var emp = await _dbContext.Employees.FindAsync(id);
        if (emp == null) return ApiResponse<bool>.Fail("员工不存在");
        emp.PasswordHash = _encryptionService.HashPassword(newPassword);
        await _dbContext.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true, "密码修改成功");
    }

    public async Task<ApiResponse<bool>> UpdateStatusAsync(int id, EmployeeStatus status)
    {
        var emp = await _dbContext.Employees.FindAsync(id);
        if (emp == null) return ApiResponse<bool>.Fail("员工不存在");
        emp.Status = status; emp.UpdatedAt = DateTime.Now;
        await _dbContext.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<bool>> ResetPasswordAsync(int id, string newPassword)
    {
        var emp = await _dbContext.Employees.FindAsync(id);
        if (emp == null) return ApiResponse<bool>.Fail("员工不存在");
        emp.PasswordHash = _encryptionService.HashPassword(newPassword);
        await _dbContext.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true, "密码已重置");
    }
}
