using FluentAssertions;
using PRO.Domain.Enums;
using PRO.WebApi.Security;
using Xunit;

namespace PRO.WebApi.Tests;

/// <summary>
/// OrderStatusManager 状态流转规则测试
/// </summary>
public class OrderStatusManagerTests
{
    // ═══ 合法转换测试 ═══

    [Theory]
    [InlineData(OrderStatus.Draft, OrderStatus.Pending)]
    [InlineData(OrderStatus.Draft, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Pending, OrderStatus.Assigned)]
    [InlineData(OrderStatus.Pending, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Assigned, OrderStatus.Delivering)]
    [InlineData(OrderStatus.Assigned, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Assigned, OrderStatus.Pending)]
    [InlineData(OrderStatus.Delivering, OrderStatus.Completed)]
    [InlineData(OrderStatus.Delivering, OrderStatus.Failed)]
    [InlineData(OrderStatus.Failed, OrderStatus.Pending)]
    [InlineData(OrderStatus.Failed, OrderStatus.Assigned)]
    [InlineData(OrderStatus.Failed, OrderStatus.Cancelled)]
    public void IsValidTransition_ValidTransition_ReturnsTrue(OrderStatus from, OrderStatus to)
    {
        var result = OrderStatusManager.IsValidTransition(from, to);
        result.Should().BeTrue($"从 {from} 到 {to} 应该是合法的转换");
    }

    [Fact]
    public void IsValidTransition_SameStatus_ReturnsTrue()
    {
        foreach (var status in Enum.GetValues<OrderStatus>())
        {
            var result = OrderStatusManager.IsValidTransition(status, status);
            result.Should().BeTrue($"同状态 {status} 应该始终返回 true");
        }
    }

    // ═══ 非法转换测试 ═══

    [Theory]
    [InlineData(OrderStatus.Draft, OrderStatus.Assigned)]
    [InlineData(OrderStatus.Draft, OrderStatus.Delivering)]
    [InlineData(OrderStatus.Draft, OrderStatus.Completed)]
    [InlineData(OrderStatus.Pending, OrderStatus.Completed)]
    [InlineData(OrderStatus.Pending, OrderStatus.Delivering)]
    [InlineData(OrderStatus.Delivering, OrderStatus.Assigned)]
    [InlineData(OrderStatus.Delivering, OrderStatus.Pending)]
    [InlineData(OrderStatus.Completed, OrderStatus.Pending)]
    [InlineData(OrderStatus.Completed, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Completed, OrderStatus.Draft)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Pending)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Draft)]
    public void IsValidTransition_InvalidTransition_ReturnsFalse(OrderStatus from, OrderStatus to)
    {
        var result = OrderStatusManager.IsValidTransition(from, to);
        result.Should().BeFalse($"从 {from} 到 {to} 应该是非法的转换");
    }

    // ═══ GetValidNextStatuses 测试 ═══

    [Fact]
    public void GetValidNextStatuses_Draft_ReturnsPendingAndCancelled()
    {
        var result = OrderStatusManager.GetValidNextStatuses(OrderStatus.Draft);
        result.Should().Contain(OrderStatus.Pending);
        result.Should().Contain(OrderStatus.Cancelled);
        result.Should().HaveCount(2);
    }

    [Fact]
    public void GetValidNextStatuses_Completed_ReturnsEmpty()
    {
        var result = OrderStatusManager.GetValidNextStatuses(OrderStatus.Completed);
        result.Should().BeEmpty("已完成状态不允许再变更");
    }

    [Fact]
    public void GetValidNextStatuses_Cancelled_ReturnsEmpty()
    {
        var result = OrderStatusManager.GetValidNextStatuses(OrderStatus.Cancelled);
        result.Should().BeEmpty("已取消状态不允许再变更");
    }

    [Fact]
    public void GetValidNextStatuses_Pending_ReturnsAssignedAndCancelled()
    {
        var result = OrderStatusManager.GetValidNextStatuses(OrderStatus.Pending);
        result.Should().Contain(OrderStatus.Assigned);
        result.Should().Contain(OrderStatus.Cancelled);
    }

    [Fact]
    public void GetValidNextStatuses_Delivering_ReturnsCompletedAndFailed()
    {
        var result = OrderStatusManager.GetValidNextStatuses(OrderStatus.Delivering);
        result.Should().Contain(OrderStatus.Completed);
        result.Should().Contain(OrderStatus.Failed);
        result.Should().HaveCount(2);
    }

    // ═══ GetStatusName 测试 ═══

    [Theory]
    [InlineData(OrderStatus.Draft, "草稿")]
    [InlineData(OrderStatus.Pending, "待分配")]
    [InlineData(OrderStatus.Assigned, "已分配")]
    [InlineData(OrderStatus.Delivering, "配送中")]
    [InlineData(OrderStatus.Completed, "已完成")]
    [InlineData(OrderStatus.Failed, "配送失败")]
    [InlineData(OrderStatus.Cancelled, "已取消")]
    public void GetStatusName_AllStatuses_ReturnsCorrectChineseName(OrderStatus status, string expectedName)
    {
        var result = OrderStatusManager.GetStatusName(status);
        result.Should().Be(expectedName);
    }

    // ═══ GetInvalidTransitionMessage 测试 ═══

    [Fact]
    public void GetInvalidTransitionMessage_ReturnsDescriptiveMessage()
    {
        var message = OrderStatusManager.GetInvalidTransitionMessage(OrderStatus.Completed, OrderStatus.Pending);
        message.Should().Contain("已完成");
        message.Should().Contain("待分配");
    }

    // ═══ IsTerminalState 测试 ═══

    [Fact]
    public void IsTerminalState_Completed_ReturnsTrue()
    {
        OrderStatusManager.IsTerminalState(OrderStatus.Completed).Should().BeTrue();
    }

    [Fact]
    public void IsTerminalState_Cancelled_ReturnsTrue()
    {
        OrderStatusManager.IsTerminalState(OrderStatus.Cancelled).Should().BeTrue();
    }

    [Theory]
    [InlineData(OrderStatus.Draft)]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Assigned)]
    [InlineData(OrderStatus.Delivering)]
    [InlineData(OrderStatus.Failed)]
    public void IsTerminalState_NonTerminal_ReturnsFalse(OrderStatus status)
    {
        OrderStatusManager.IsTerminalState(status).Should().BeFalse();
    }

    // ═══ IsDeletable 测试 ═══

    [Fact]
    public void IsDeletable_Draft_ReturnsTrue()
    {
        OrderStatusManager.IsDeletable(OrderStatus.Draft).Should().BeTrue();
    }

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Assigned)]
    [InlineData(OrderStatus.Delivering)]
    [InlineData(OrderStatus.Completed)]
    public void IsDeletable_NonDraft_ReturnsFalse(OrderStatus status)
    {
        OrderStatusManager.IsDeletable(status).Should().BeFalse();
    }

    // ═══ IsEditable 测试 ═══

    [Theory]
    [InlineData(OrderStatus.Draft)]
    [InlineData(OrderStatus.Pending)]
    public void IsEditable_DraftOrPending_ReturnsTrue(OrderStatus status)
    {
        OrderStatusManager.IsEditable(status).Should().BeTrue();
    }

    [Theory]
    [InlineData(OrderStatus.Assigned)]
    [InlineData(OrderStatus.Delivering)]
    [InlineData(OrderStatus.Completed)]
    [InlineData(OrderStatus.Cancelled)]
    public void IsEditable_NonEditable_ReturnsFalse(OrderStatus status)
    {
        OrderStatusManager.IsEditable(status).Should().BeFalse();
    }
}
