using Microsoft.EntityFrameworkCore;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;
using PRO.Infrastructure.Services;
using Moq;
using Xunit;
using PRO.Application.Interfaces;

namespace PRO.WebApi.Tests;

/// <summary>
/// 审计服务测试 — 覆盖各业务模块的操作日志记录
/// </summary>
public class AuditServiceTests
{
    private readonly Mock<IOperationLogService> _mockLogService;
    private readonly AuditService _auditService;

    public AuditServiceTests()
    {
        _mockLogService = new Mock<IOperationLogService>();
        _auditService = new AuditService(_mockLogService.Object);
    }

    #region Order Operations

    [Fact]
    public async Task LogOrderCreateAsync_CallsOperationLogService()
    {
        await _auditService.LogOrderCreateAsync(1, 100, "D2026061000010001", 500m);

        _mockLogService.Verify(
            s => s.CreateAsync(1, "订单", "创建订单",
                It.Is<string>(m => m.Contains("D2026061000010001") && m.Contains("¥500")),
                "Order", 100, It.IsAny<string?>(), It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task LogOrderStatusChangeAsync_WithReason_CallsOperationLogService()
    {
        await _auditService.LogOrderStatusChangeAsync(1, 100, "D2026061000010001",
            OrderStatus.Pending, OrderStatus.Assigned, "手动分配");

        _mockLogService.Verify(
            s => s.CreateAsync(1, "订单", "状态变更",
                It.Is<string>(m => m.Contains("待分配") && m.Contains("已分配") && m.Contains("手动分配")),
                "Order", 100, It.IsAny<string?>(), It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task LogOrderAssignAsync_CallsOperationLogService()
    {
        await _auditService.LogOrderAssignAsync(1, 100, "D2026061000010001", 10, "配送员张三");

        _mockLogService.Verify(
            s => s.CreateAsync(1, "订单", "分配配送员",
                It.Is<string>(m => m.Contains("配送员张三")),
                "Order", 100, It.IsAny<string?>(), It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task LogOrderBatchAssignAsync_CallsOperationLogService()
    {
        await _auditService.LogOrderBatchAssignAsync(1, 5, "配送员李四");

        _mockLogService.Verify(
            s => s.CreateAsync(1, "订单", "批量分配",
                It.Is<string>(m => m.Contains("5") && m.Contains("李四")),
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task LogOrderBatchStatusChangeAsync_CallsOperationLogService()
    {
        await _auditService.LogOrderBatchStatusChangeAsync(1, 8, 10, OrderStatus.Completed, "批量完成");

        _mockLogService.Verify(
            s => s.CreateAsync(1, "订单", "批量状态变更",
                It.Is<string>(m => m.Contains("8/10")),
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task LogOrderDeleteAsync_CallsOperationLogService()
    {
        await _auditService.LogOrderDeleteAsync(1, 100, "D2026061000010001");

        _mockLogService.Verify(
            s => s.CreateAsync(1, "订单", "删除订单",
                It.Is<string>(m => m.Contains("D2026061000010001")),
                "Order", 100, "Warning", It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task LogOrderCancelAsync_CallsOperationLogService()
    {
        await _auditService.LogOrderCancelAsync(1, 100, "D2026061000010001", "客户要求取消");

        _mockLogService.Verify(
            s => s.CreateAsync(1, "订单", "取消订单",
                It.Is<string>(m => m.Contains("客户要求取消")),
                "Order", 100, "Warning", It.IsAny<string?>()),
            Times.Once);
    }

    #endregion

    #region Settlement Operations

    [Fact]
    public async Task LogSettlementCreateAsync_CallsOperationLogService()
    {
        await _auditService.LogSettlementCreateAsync(1, 200, "S2026061000010001", 10, 5000m);

        _mockLogService.Verify(
            s => s.CreateAsync(1, "结算", "创建结算",
                It.Is<string>(m => m.Contains("10") && m.Contains("¥5,000")),
                "Settlement", 200, It.IsAny<string?>(), It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task LogSettlementBatchAsync_CallsOperationLogService()
    {
        await _auditService.LogSettlementBatchAsync(1, 20, 10000m);

        _mockLogService.Verify(
            s => s.CreateAsync(1, "结算", "批量结算",
                It.Is<string>(m => m.Contains("20") && m.Contains("¥10,000")),
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>()),
            Times.Once);
    }

    #endregion

    #region Customer Operations

    [Fact]
    public async Task LogCustomerCreateAsync_CallsOperationLogService()
    {
        await _auditService.LogCustomerCreateAsync(1, 300, "测试客户");

        _mockLogService.Verify(
            s => s.CreateAsync(1, "客户", "创建客户",
                It.Is<string>(m => m.Contains("测试客户")),
                "Customer", 300, It.IsAny<string?>(), It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task LogCustomerMergeAsync_CallsOperationLogService()
    {
        await _auditService.LogCustomerMergeAsync(1, 300, "主客户", ["客户A", "客户B"]);

        _mockLogService.Verify(
            s => s.CreateAsync(1, "客户", "合并客户",
                It.Is<string>(m => m.Contains("主客户") && m.Contains("客户A") && m.Contains("客户B")),
                "Customer", 300, It.IsAny<string?>(), It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task LogCustomerDeleteAsync_CallsOperationLogService()
    {
        await _auditService.LogCustomerDeleteAsync(1, 300, "被删客户");

        _mockLogService.Verify(
            s => s.CreateAsync(1, "客户", "删除客户",
                It.Is<string>(m => m.Contains("被删客户")),
                "Customer", 300, "Warning", It.IsAny<string?>()),
            Times.Once);
    }

    #endregion

    #region Product Operations

    [Fact]
    public async Task LogProductCreateAsync_CallsOperationLogService()
    {
        await _auditService.LogProductCreateAsync(1, 400, "新产品");

        _mockLogService.Verify(
            s => s.CreateAsync(1, "产品", "创建产品",
                It.Is<string>(m => m.Contains("新产品")),
                "Product", 400, It.IsAny<string?>(), It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task LogProductDeleteAsync_CallsOperationLogService()
    {
        await _auditService.LogProductDeleteAsync(1, 400, "下架产品");

        _mockLogService.Verify(
            s => s.CreateAsync(1, "产品", "删除产品",
                It.Is<string>(m => m.Contains("下架产品")),
                "Product", 400, "Warning", It.IsAny<string?>()),
            Times.Once);
    }

    #endregion

    #region System Operations

    [Fact]
    public async Task LogBackupAsync_Success_CallsOperationLogService()
    {
        await _auditService.LogBackupAsync(1, "自动", true, "backup_20260610.bak");

        _mockLogService.Verify(
            s => s.CreateAsync(1, "系统", "备份",
                It.Is<string>(m => m.Contains("成功") && m.Contains("backup_20260610.bak")),
                It.IsAny<string?>(), It.IsAny<int?>(), "Success", It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task LogBackupAsync_Failure_CallsOperationLogService()
    {
        await _auditService.LogBackupAsync(1, "手动", false);

        _mockLogService.Verify(
            s => s.CreateAsync(1, "系统", "备份",
                It.Is<string>(m => m.Contains("失败")),
                It.IsAny<string?>(), It.IsAny<int?>(), "Failed", It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task LogExportAsync_CallsOperationLogService()
    {
        await _auditService.LogExportAsync(1, "订单列表", 500);

        _mockLogService.Verify(
            s => s.CreateAsync(1, "导出", "订单列表",
                It.Is<string>(m => m.Contains("500")),
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task LogConfigChangeAsync_CallsOperationLogService()
    {
        await _auditService.LogConfigChangeAsync(1, "PageSize", "50", "100");

        _mockLogService.Verify(
            s => s.CreateAsync(1, "系统", "配置变更",
                It.Is<string>(m => m.Contains("PageSize") && m.Contains("50") && m.Contains("100")),
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>()),
            Times.Once);
    }

    #endregion

    #region Delivery Operations

    [Fact]
    public async Task LogAutoAssignAsync_CallsOperationLogService()
    {
        await _auditService.LogAutoAssignAsync(1, 1, 8, 10);

        _mockLogService.Verify(
            s => s.CreateAsync(1, "物流", "自动分配",
                It.Is<string>(m => m.Contains("8/10")),
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task LogDeliveryStatusChangeAsync_CallsOperationLogService()
    {
        await _auditService.LogDeliveryStatusChangeAsync(1, 100, "配送中");

        _mockLogService.Verify(
            s => s.CreateAsync(1, "物流", "配送状态变更",
                It.Is<string>(m => m.Contains("配送中")),
                "Order", 100, It.IsAny<string?>(), It.IsAny<string?>()),
            Times.Once);
    }

    #endregion
}
