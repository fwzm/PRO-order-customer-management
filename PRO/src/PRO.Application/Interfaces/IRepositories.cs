using System.Linq.Expressions;
using PRO.Application.DTOs;
using PRO.Domain.Entities;
using PRO.Domain.Enums;

namespace PRO.Application.Interfaces;

// 通用仓储接口
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id);
    Task<List<T>> GetAllAsync();
    Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task<T> AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(int id);
    Task<int> SaveChangesAsync();
}

// 注: PagedResult<T> 已定义在 PRO.Application.DTOs 命名空间，此处不再重复定义

// ==================== 组织架构仓储 ====================

public interface IBranchRepository : IRepository<Branch>
{
    Task<Branch?> GetByCodeAsync(string code);
    Task<List<Branch>> GetActiveListAsync();
}

public interface IEmployeeRepository : IRepository<Employee>
{
    Task<Employee?> GetByEmployeeNoAsync(string employeeNo);
    Task<Employee?> GetByIdWithDetailsAsync(int id);
    Task<List<Employee>> GetByBranchIdAsync(int branchId);
    Task<List<Employee>> GetByDepartmentIdAsync(int departmentId);
    Task<List<Employee>> GetByRoleIdAsync(int roleId);
    Task<PagedResult<Employee>> GetPagedAsync(int pageIndex, int pageSize, string? keyword, int? branchId);
}

public interface IDepartmentRepository : IRepository<Department>
{
    Task<List<Department>> GetTreeByBranchIdAsync(int branchId);
    Task<List<Department>> GetChildrenAsync(int parentId);
}

// ==================== 业务数据仓储 ====================

public interface ICustomerRepository : IRepository<Customer>
{
    Task<List<Customer>> GetByBranchIdAsync(int branchId);
    Task<List<Customer>> GetSubCustomersAsync(int parentCustomerId);
    Task<List<Customer>> GetMajorCustomersAsync(int branchId);
    Task<List<Customer>> FindDuplicatesAsync(string? phone, string? name, string? address, string? legalPerson);
    Task<PagedResult<Customer>> GetPagedAsync(int pageIndex, int pageSize, string? keyword, int? branchId, CustomerType? customerType);
}

public interface IOrderRepository : IRepository<Order>
{
    Task<Order?> GetByIdWithDetailsAsync(int id);
    Task<Order?> GetByOrderNoAsync(string orderNo);
    Task<List<Order>> GetByCustomerIdAsync(int customerId);
    Task<List<Order>> GetPendingOrdersAsync(int branchId);
    Task<List<Order>> GetByDateRangeAsync(int branchId, DateTime startDate, DateTime endDate);
    Task<PagedResult<Order>> GetPagedAsync(int pageIndex, int pageSize, string? keyword, int? branchId, OrderStatus? status, PaymentStatus? paymentStatus);
}

public interface IProductRepository : IRepository<Product>
{
    Task<Product?> GetBySkuAsync(string sku);
    Task<List<Product>> GetByCategoryIdAsync(int categoryId);
    Task<List<Product>> GetActiveListAsync();
    Task<PagedResult<Product>> GetPagedAsync(int pageIndex, int pageSize, string? keyword, int? categoryId, ProductStatus? status);
}

public interface IDeliveryPersonRepository : IRepository<DeliveryPerson>
{
    Task<List<DeliveryPerson>> GetByBranchIdAsync(int branchId);
    Task<List<DeliveryPerson>> GetAvailableAsync(int branchId);
    Task<PagedResult<DeliveryPerson>> GetPagedAsync(int pageIndex, int pageSize, string? keyword, int? branchId, DeliveryPersonStatus? status);
}

// ==================== 基础设施服务 ====================

public interface IEncryptionService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
    string HashPassword(string password);
    bool VerifyPassword(string password, string hash);
    string GenerateToken();
}
