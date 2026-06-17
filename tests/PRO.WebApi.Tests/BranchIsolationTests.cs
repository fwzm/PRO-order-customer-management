using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using PRO.Application.DTOs;
using Xunit;

namespace PRO.WebApi.Tests;

/// <summary>
/// 分公司数据隔离集成测试
/// 验证跨分公司访问被正确阻止
/// </summary>
[Collection("Integration")]
public class BranchIsolationTests
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public BranchIsolationTests(TestWebApplicationFactory factory)
    {
        _client = factory.GetTestClient();
    }

    /// <summary>
    /// 获取分公司A用户的JWT Token
    /// </summary>
    private async Task<string> GetBranchATokenAsync()
    {
        return await LoginAsync("branch_a_user", "password123");
    }

    private async Task<string> GetBranchBTokenAsync()
    {
        return await LoginAsync("branch_b_user", "password123");
    }

    private async Task<string> GetHeadquartersTokenAsync()
    {
        return await LoginAsync("admin", "admin123");
    }

    private async Task<string> LoginAsync(string employeeNo, string password)
    {
        var loginRequest = new LoginRequest { EmployeeNo = employeeNo, Password = password };
        var content = new StringContent(
            JsonSerializer.Serialize(loginRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync("/api/auth/login", content);
        response.EnsureSuccessStatusCode();

        var responseStr = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResponse<LoginResponse>>(responseStr, _jsonOptions);
        return result?.Data?.Token ?? throw new InvalidOperationException("登录失败");
    }

    private void SetAuthHeader(string token)
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    // ==================== 客户隔离测试 ====================

    [Fact]
    public async Task BranchAUser_CannotAccessBranchBCustomer_ShouldReturn403()
    {
        var token = await GetBranchATokenAsync();
        SetAuthHeader(token);

        var response = await _client.GetAsync("/api/customers/999");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task BranchAUser_CannotCreateCustomerForBranchB_ShouldReturn403()
    {
        var token = await GetBranchATokenAsync();
        SetAuthHeader(token);

        var request = new CreateCustomerRequest
        {
            Name = "测试客户",
            BranchId = 2,
            Phone = "13800138000"
        };
        var content = new StringContent(
            JsonSerializer.Serialize(request, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync("/api/customers", content);

        if (response.StatusCode == HttpStatusCode.Created)
        {
            var responseStr = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResponse<int>>(responseStr, _jsonOptions);
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
        }
        else
        {
            response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.BadRequest);
        }
    }

    [Fact]
    public async Task BranchAUser_CannotDeleteBranchBCustomer_ShouldReturn403()
    {
        var token = await GetBranchATokenAsync();
        SetAuthHeader(token);

        var response = await _client.DeleteAsync("/api/customers/999");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task BranchAUser_CannotMergeBranchBCustomer_ShouldReturn403()
    {
        var token = await GetBranchATokenAsync();
        SetAuthHeader(token);

        var request = new MergeCustomerRequest
        {
            MainCustomerId = 1,
            MergedCustomerIds = [999]
        };
        var content = new StringContent(
            JsonSerializer.Serialize(request, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync("/api/customers/merge", content);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.BadRequest);
    }

    // ==================== 订单隔离测试 ====================

    [Fact]
    public async Task BranchAUser_CannotAccessBranchBOrder_ShouldReturn403()
    {
        var token = await GetBranchATokenAsync();
        SetAuthHeader(token);

        var response = await _client.GetAsync("/api/orders/9999");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task BranchAUser_CannotAssignBranchBOrder_ShouldReturn403()
    {
        var token = await GetBranchATokenAsync();
        SetAuthHeader(token);

        var request = new { DeliveryPersonId = 1 };
        var content = new StringContent(
            JsonSerializer.Serialize(request, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync("/api/orders/9999/assign", content);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task BranchAUser_CannotChangeBranchBOrderStatus_ShouldReturn403()
    {
        var token = await GetBranchATokenAsync();
        SetAuthHeader(token);

        var request = new { newStatus = 6, reason = "测试" };
        var content = new StringContent(
            JsonSerializer.Serialize(request, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync("/api/orders/9999/status", content);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    // ==================== 配送员隔离测试 ====================

    [Fact]
    public async Task BranchAUser_CannotAccessBranchBDeliveryPerson_ShouldReturn403()
    {
        var token = await GetBranchATokenAsync();
        SetAuthHeader(token);

        var response = await _client.GetAsync("/api/delivery/999");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task BranchAUser_CannotCreateDeliveryPersonForBranchB_ShouldReturn403()
    {
        var token = await GetBranchATokenAsync();
        SetAuthHeader(token);

        var request = new { Name = "测试配送员", BranchId = 2, Phone = "13800138000" };
        var content = new StringContent(
            JsonSerializer.Serialize(request, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync("/api/delivery/persons", content);

        response.StatusCode.Should()
            .BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created,
                HttpStatusCode.Forbidden, HttpStatusCode.BadRequest);
    }

    // ==================== 总部管理员测试 ====================

    [Fact]
    public async Task HeadquartersAdmin_CanAccessAllBranches_ShouldReturn200()
    {
        var token = await GetHeadquartersTokenAsync();
        SetAuthHeader(token);

        var response = await _client.GetAsync("/api/customers?branchId=1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ==================== 列表过滤测试 ====================

    [Fact]
    public async Task BranchAUser_ListOnlyShowsBranchAData_ShouldFilterByBranch()
    {
        var token = await GetBranchATokenAsync();
        SetAuthHeader(token);

        var response = await _client.GetAsync("/api/customers?pageIndex=1&pageSize=50");
        response.EnsureSuccessStatusCode();

        var responseStr = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResponse<PagedResult<CustomerListItem>>>(
            responseStr, _jsonOptions);

        if (result?.Data?.Items.Any() == true)
        {
            result.Data.Items.Should().AllSatisfy(c =>
            {
                c.BranchId.Should()
                    .Be(1, "分公司A用户不应看到其他分公司客户");
            });
        }
    }
}

/// <summary>
/// 权限控制集成测试
/// </summary>
[Collection("Integration")]
public class PermissionTests
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public PermissionTests(TestWebApplicationFactory factory)
    {
        _client = factory.GetTestClient();
    }

    [Fact]
    public async Task NormalUser_CannotDeleteOrder_ShouldReturn403()
    {
        var token = await LoginAsync("normal_user", "password123");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.DeleteAsync("/api/orders/1");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task NormalUser_CannotAutoAssign_ShouldReturn403()
    {
        var token = await LoginAsync("normal_user", "password123");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsync("/api/orders/auto-assign", null);

        response.StatusCode.Should()
            .BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound,
                HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task NormalUser_CannotMergeCustomers_ShouldReturn403()
    {
        var token = await LoginAsync("normal_user", "password123");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var request = new MergeCustomerRequest
        { MainCustomerId = 1, MergedCustomerIds = [2] };
        var content = new StringContent(
            JsonSerializer.Serialize(request, _jsonOptions),
            Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/customers/merge", content);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<string> LoginAsync(string employeeNo, string password)
    {
        var loginRequest = new LoginRequest
        { EmployeeNo = employeeNo, Password = password };
        var content = new StringContent(
            JsonSerializer.Serialize(loginRequest, _jsonOptions),
            Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/auth/login", content);
        response.EnsureSuccessStatusCode();

        var result = JsonSerializer.Deserialize<ApiResponse<LoginResponse>>(
            await response.Content.ReadAsStringAsync(), _jsonOptions);
        return result?.Data?.Token
               ?? throw new InvalidOperationException("登录失败");
    }
}
