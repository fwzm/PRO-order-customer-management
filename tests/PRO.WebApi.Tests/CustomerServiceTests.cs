using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using PRO.Application.DTOs;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Configuration;
using PRO.Infrastructure.Persistence;
using PRO.Infrastructure.Services;
using Xunit;

namespace PRO.WebApi.Tests;

/// <summary>
/// CustomerService 核心业务测试 — 使用 InMemory 数据库
/// </summary>
public class CustomerServiceTests : IDisposable
{
    private readonly ProDbContext _dbContext;
    private readonly CustomerService _customerService;

    public CustomerServiceTests()
    {
        var options = new DbContextOptionsBuilder<ProDbContext>()
            .UseInMemoryDatabase(databaseName: $"CustomerTests_{Guid.NewGuid()}")
            .Options;
        _dbContext = new ProDbContext(options);

        var maskingOptions = Options.Create(new DataMaskingOptions());
        var maskingService = new DataMaskingService(maskingOptions);
        _customerService = new CustomerService(_dbContext, maskingService);

        SeedData();
    }

    private void SeedData()
    {
        var branch = new Branch { Id = 1, Name = "北京分公司", Code = "0002", Status = EntityStatus.Active };
        _dbContext.Branches.Add(branch);

        _dbContext.Customers.AddRange(
            new Customer
            {
                Id = 1,
                Name = "测试客户A",
                CustomerNo = "K2026010100020001",
                CustomerType = CustomerType.Major,
                Phone = "13800001111",
                Address = "北京市朝阳区",
                BranchId = 1,
                Status = CustomerStatus.Active,
                CreatedAt = DateTime.Now
            },
            new Customer
            {
                Id = 2,
                Name = "测试客户B",
                CustomerNo = "K2026010100020002",
                CustomerType = CustomerType.Sub,
                Phone = "13800002222",
                Address = "北京市海淀区",
                BranchId = 1,
                Status = CustomerStatus.Active,
                CreatedAt = DateTime.Now
            },
            new Customer
            {
                Id = 3,
                Name = "已删除客户",
                CustomerNo = "K2026010100020003",
                CustomerType = CustomerType.Major,
                Phone = "13800003333",
                BranchId = 1,
                Status = CustomerStatus.Deleted,
                CreatedAt = DateTime.Now
            }
        );
        _dbContext.SaveChanges();
    }

    // ═══ 客户创建测试 ═══

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesCustomer()
    {
        var request = new CreateCustomerRequest
        {
            Name = "新客户",
            CustomerType = CustomerType.Major,
            Phone = "13800004444",
            Address = "北京市西城区",
            BranchId = 1
        };

        var result = await _customerService.CreateAsync(request);

        result.Success.Should().BeTrue();
        result.Data.Should().BeGreaterThan(0);

        var customer = await _dbContext.Customers.FindAsync(result.Data);
        customer.Should().NotBeNull();
        customer!.Name.Should().Be("新客户");
        customer.Status.Should().Be(CustomerStatus.Active);
    }

    // ═══ 客户软删除测试 ═══

    [Fact]
    public async Task DeleteAsync_ActiveCustomer_SoftDeletes()
    {
        var result = await _customerService.DeleteAsync(1);
        result.Success.Should().BeTrue();

        var customer = await _dbContext.Customers.FindAsync(1);
        customer!.Status.Should().Be(CustomerStatus.Deleted, "软删除应该将状态改为 Deleted");
    }

    [Fact]
    public async Task DeleteAsync_NonExistingCustomer_ReturnsFail()
    {
        var result = await _customerService.DeleteAsync(999);
        result.Success.Should().BeFalse();
    }

    // ═══ 重复检测测试 ═══

    [Fact]
    public async Task CheckDuplicatesAsync_SamePhone_DetectsDuplicate()
    {
        var result = await _customerService.CheckDuplicatesAsync(
            phone: "13800001111", name: null, address: null, legalPerson: null);

        result.Success.Should().BeTrue();
        result.Data!.HasDuplicates.Should().BeTrue();
        result.Data.Duplicates.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task CheckDuplicatesAsync_NoMatch_ReturnsNoDuplicates()
    {
        var result = await _customerService.CheckDuplicatesAsync(
            phone: "19999999999", name: "不存在的客户", address: null, legalPerson: null);

        result.Success.Should().BeTrue();
        result.Data!.HasDuplicates.Should().BeFalse();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
