using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PRO.Application.DTOs;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;
using PRO.Application.Interfaces;

namespace PRO.Infrastructure.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly ProDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(ProDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(int id)
    {
        return await _dbSet.FindAsync(id);
    }

    public virtual async Task<List<T>> GetAllAsync()
    {
        return await _dbSet.AsNoTracking().ToListAsync();
    }

    public virtual async Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.AsNoTracking().Where(predicate).ToListAsync();
    }

    public virtual async Task<T> AddAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
        return entity;
    }

    public virtual Task UpdateAsync(T entity)
    {
        _dbSet.Update(entity);
        return Task.CompletedTask;
    }

    public virtual async Task DeleteAsync(int id)
    {
        var entity = await _dbSet.FindAsync(id);
        if (entity != null)
            _dbSet.Remove(entity);
    }

    public virtual async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }
}

// ==================== 组织架构仓储实现 ====================

public class BranchRepository : Repository<Branch>, IBranchRepository
{
    public BranchRepository(ProDbContext context) : base(context) { }

    public async Task<Branch?> GetByCodeAsync(string code)
    {
        return await _dbSet.AsNoTracking().FirstOrDefaultAsync(b => b.Code == code);
    }

    public async Task<List<Branch>> GetActiveListAsync()
    {
        return await _dbSet.AsNoTracking().Where(b => b.Status == EntityStatus.Active).ToListAsync();
    }
}

public class EmployeeRepository : Repository<Employee>, IEmployeeRepository
{
    public EmployeeRepository(ProDbContext context) : base(context) { }

    public async Task<Employee?> GetByEmployeeNoAsync(string employeeNo)
    {
        return await _dbSet.AsNoTracking().Include(e => e.Role).Include(e => e.Branch).Include(e => e.Department)
            .FirstOrDefaultAsync(e => e.EmployeeNo == employeeNo);
    }

    public async Task<Employee?> GetByIdWithDetailsAsync(int id)
    {
        return await _dbSet.AsNoTracking().Include(e => e.Role).Include(e => e.Branch).Include(e => e.Department)
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<List<Employee>> GetByBranchIdAsync(int branchId)
    {
        return await _dbSet.AsNoTracking().Where(e => e.BranchId == branchId && e.Status == EmployeeStatus.Active).ToListAsync();
    }

    public async Task<List<Employee>> GetByDepartmentIdAsync(int departmentId)
    {
        return await _dbSet.AsNoTracking().Where(e => e.DepartmentId == departmentId && e.Status == EmployeeStatus.Active).ToListAsync();
    }

    public async Task<List<Employee>> GetByRoleIdAsync(int roleId)
    {
        return await _dbSet.AsNoTracking().Where(e => e.RoleId == roleId).ToListAsync();
    }

    public async Task<PagedResult<Employee>> GetPagedAsync(int pageIndex, int pageSize, string? keyword, int? branchId)
    {
        var query = _dbSet.AsNoTracking().Include(e => e.Role).Include(e => e.Branch).Include(e => e.Department).AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(e => e.Name.Contains(keyword) || e.EmployeeNo.Contains(keyword));

        if (branchId.HasValue)
            query = query.Where(e => e.BranchId == branchId.Value);

        var totalCount = await query.CountAsync();
        var items = await query.OrderBy(e => e.Id)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<Employee> { Items = items, TotalCount = totalCount, PageIndex = pageIndex, PageSize = pageSize };
    }
}

public class DepartmentRepository : Repository<Department>, IDepartmentRepository
{
    public DepartmentRepository(ProDbContext context) : base(context) { }

    public async Task<List<Department>> GetTreeByBranchIdAsync(int branchId)
    {
        return await _dbSet.AsNoTracking().Where(d => d.BranchId == branchId && d.Status == EntityStatus.Active)
            .OrderBy(d => d.SortOrder).ToListAsync();
    }

    public async Task<List<Department>> GetChildrenAsync(int parentId)
    {
        return await _dbSet.AsNoTracking().Where(d => d.ParentId == parentId && d.Status == EntityStatus.Active)
            .OrderBy(d => d.SortOrder).ToListAsync();
    }
}

// ==================== 业务数据仓储实现 ====================

public class CustomerRepository : Repository<Customer>, ICustomerRepository
{
    public CustomerRepository(ProDbContext context) : base(context) { }

    public async Task<List<Customer>> GetByBranchIdAsync(int branchId)
    {
        return await _dbSet.AsNoTracking().Where(c => c.BranchId == branchId && c.Status == CustomerStatus.Active).ToListAsync();
    }

    public async Task<List<Customer>> GetSubCustomersAsync(int parentCustomerId)
    {
        return await _dbSet.AsNoTracking().Where(c => c.ParentCustomerId == parentCustomerId && c.Status != CustomerStatus.Deleted).ToListAsync();
    }

    public async Task<List<Customer>> GetMajorCustomersAsync(int branchId)
    {
        return await _dbSet.AsNoTracking().Where(c => c.BranchId == branchId && c.CustomerType == CustomerType.Major && c.Status == CustomerStatus.Active)
            .ToListAsync();
    }

    public async Task<List<Customer>> FindDuplicatesAsync(string? phone, string? name, string? address, string? legalPerson)
    {
        // 构建多个独立查重条件，使用 OR 逻辑
        var query = _dbSet.AsNoTracking().Where(c => c.Status == CustomerStatus.Active);

        // 用于构建 OR 条件的表达式
        Expression<Func<Customer, bool>>? combinedPredicate = null;

        if (!string.IsNullOrWhiteSpace(phone))
        {
            Expression<Func<Customer, bool>> phonePredicate = c => c.Phone == phone;
            combinedPredicate = combinedPredicate == null
                ? phonePredicate
                : CombineOr(combinedPredicate, phonePredicate);
        }

        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(address))
        {
            Expression<Func<Customer, bool>> nameAddressPredicate = c => c.Name == name && c.Address == address;
            combinedPredicate = combinedPredicate == null
                ? nameAddressPredicate
                : CombineOr(combinedPredicate, nameAddressPredicate);
        }

        if (!string.IsNullOrWhiteSpace(legalPerson) && !string.IsNullOrWhiteSpace(phone))
        {
            Expression<Func<Customer, bool>> legalPhonePredicate = c => c.LegalPerson == legalPerson && c.Phone == phone;
            combinedPredicate = combinedPredicate == null
                ? legalPhonePredicate
                : CombineOr(combinedPredicate, legalPhonePredicate);
        }

        if (combinedPredicate != null)
        {
            query = query.Where(combinedPredicate);
        }
        else
        {
            // 没有任何查询条件，返回空
            return new List<Customer>();
        }

        return await query.Take(20).ToListAsync();
    }

    private static Expression<Func<T, bool>> CombineOr<T>(Expression<Func<T, bool>> expr1, Expression<Func<T, bool>> expr2)
    {
        var parameter = Expression.Parameter(typeof(T));
        var left = new ParameterReplacer(expr1.Parameters[0], parameter).Visit(expr1.Body);
        var right = new ParameterReplacer(expr2.Parameters[0], parameter).Visit(expr2.Body);
        var body = Expression.OrElse(left, right);
        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    /// <summary>
    /// 表达式参数替换器 - 用于组合多个 Expression 为 OR 逻辑
    /// </summary>
    private class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _oldParameter;
        private readonly ParameterExpression _newParameter;

        public ParameterReplacer(ParameterExpression oldParameter, ParameterExpression newParameter)
        {
            _oldParameter = oldParameter;
            _newParameter = newParameter;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == _oldParameter ? _newParameter : base.VisitParameter(node);
        }
    }

    public async Task<PagedResult<Customer>> GetPagedAsync(int pageIndex, int pageSize, string? keyword, int? branchId, CustomerType? customerType)
    {
        var query = _dbSet.AsNoTracking().Include(c => c.Branch).AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(c => c.Name.Contains(keyword) || (c.Phone != null && c.Phone.Contains(keyword)));

        if (branchId.HasValue)
            query = query.Where(c => c.BranchId == branchId.Value);

        if (customerType.HasValue)
            query = query.Where(c => c.CustomerType == customerType.Value);

        var totalCount = await query.CountAsync();
        var items = await query.OrderByDescending(c => c.CreatedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<Customer> { Items = items, TotalCount = totalCount, PageIndex = pageIndex, PageSize = pageSize };
    }
}

public class OrderRepository : Repository<Order>, IOrderRepository
{
    public OrderRepository(ProDbContext context) : base(context) { }

    public async Task<Order?> GetByIdWithDetailsAsync(int id)
    {
        return await _dbSet.AsNoTracking().Include(o => o.Customer).Include(o => o.Branch).Include(o => o.DeliveryPerson)
            .Include(o => o.Creator).Include(o => o.Items).ThenInclude(i => i.Product)
            .Include(o => o.ModificationRecords).ThenInclude(r => r.ModifiedBy)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<Order?> GetByOrderNoAsync(string orderNo)
    {
        return await _dbSet.AsNoTracking().FirstOrDefaultAsync(o => o.OrderNo == orderNo);
    }

    public async Task<List<Order>> GetByCustomerIdAsync(int customerId)
    {
        return await _dbSet.AsNoTracking().Where(o => o.CustomerId == customerId).OrderByDescending(o => o.CreatedAt).ToListAsync();
    }

    public async Task<List<Order>> GetPendingOrdersAsync(int branchId)
    {
        return await _dbSet.AsNoTracking().Include(o => o.Customer).Where(o => o.BranchId == branchId && o.Status == OrderStatus.Pending)
            .OrderBy(o => o.CreatedAt).ToListAsync();
    }

    public async Task<List<Order>> GetByDateRangeAsync(int branchId, DateTime startDate, DateTime endDate)
    {
        return await _dbSet.AsNoTracking().Include(o => o.Items).ThenInclude(i => i.Product)
            .Where(o => o.BranchId == branchId && o.CreatedAt >= startDate && o.CreatedAt <= endDate && o.SettlementId == null)
            .OrderBy(o => o.CreatedAt).ToListAsync();
    }

    public async Task<PagedResult<Order>> GetPagedAsync(int pageIndex, int pageSize, string? keyword, int? branchId, OrderStatus? status, PaymentStatus? paymentStatus)
    {
        var query = _dbSet.AsNoTracking().Include(o => o.Customer).Include(o => o.Branch).Include(o => o.DeliveryPerson).Include(o => o.Creator).AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(o => o.OrderNo.Contains(keyword) || (o.Customer != null && o.Customer.Name.Contains(keyword)));

        if (branchId.HasValue)
            query = query.Where(o => o.BranchId == branchId.Value);

        if (status.HasValue)
            query = query.Where(o => o.Status == status.Value);

        if (paymentStatus.HasValue)
            query = query.Where(o => o.PaymentStatus == paymentStatus.Value);

        var totalCount = await query.CountAsync();
        var items = await query.OrderByDescending(o => o.CreatedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<Order> { Items = items, TotalCount = totalCount, PageIndex = pageIndex, PageSize = pageSize };
    }
}

public class ProductRepository : Repository<Product>, IProductRepository
{
    public ProductRepository(ProDbContext context) : base(context) { }

    public async Task<Product?> GetBySkuAsync(string sku)
    {
        return await _dbSet.AsNoTracking().FirstOrDefaultAsync(p => p.SKU == sku);
    }

    public async Task<List<Product>> GetByCategoryIdAsync(int categoryId)
    {
        return await _dbSet.AsNoTracking().Where(p => p.CategoryId == categoryId && p.Status == ProductStatus.Active).ToListAsync();
    }

    public async Task<List<Product>> GetActiveListAsync()
    {
        return await _dbSet.AsNoTracking().Where(p => p.Status == ProductStatus.Active).ToListAsync();
    }

    public async Task<PagedResult<Product>> GetPagedAsync(int pageIndex, int pageSize, string? keyword, int? categoryId, ProductStatus? status)
    {
        var query = _dbSet.AsNoTracking().Include(p => p.Category).AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(p => p.Name.Contains(keyword) || p.SKU.Contains(keyword));

        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryId == categoryId.Value);

        if (status.HasValue)
            query = query.Where(p => p.Status == status.Value);

        var totalCount = await query.CountAsync();
        var items = await query.OrderBy(p => p.SKU)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<Product> { Items = items, TotalCount = totalCount, PageIndex = pageIndex, PageSize = pageSize };
    }
}

public class DeliveryPersonRepository : Repository<DeliveryPerson>, IDeliveryPersonRepository
{
    public DeliveryPersonRepository(ProDbContext context) : base(context) { }

    public async Task<List<DeliveryPerson>> GetByBranchIdAsync(int branchId)
    {
        return await _dbSet.AsNoTracking().Where(d => d.BranchId == branchId).ToListAsync();
    }

    public async Task<List<DeliveryPerson>> GetAvailableAsync(int branchId)
    {
        return await _dbSet.AsNoTracking().Where(d => d.BranchId == branchId && d.Status == DeliveryPersonStatus.Available && d.CurrentLoad < d.MaxLoad)
            .OrderByDescending(d => d.MaxLoad - d.CurrentLoad).ToListAsync();
    }

    public async Task<PagedResult<DeliveryPerson>> GetPagedAsync(int pageIndex, int pageSize, string? keyword, int? branchId, DeliveryPersonStatus? status)
    {
        var query = _dbSet.AsNoTracking().Include(d => d.Branch).AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(d => d.Name.Contains(keyword) || d.Phone.Contains(keyword));

        if (branchId.HasValue)
            query = query.Where(d => d.BranchId == branchId.Value);

        if (status.HasValue)
            query = query.Where(d => d.Status == status.Value);

        var totalCount = await query.CountAsync();
        var items = await query.OrderBy(d => d.Name)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<DeliveryPerson> { Items = items, TotalCount = totalCount, PageIndex = pageIndex, PageSize = pageSize };
    }
}
