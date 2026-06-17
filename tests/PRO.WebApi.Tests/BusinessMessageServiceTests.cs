using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Update;
using Npgsql;
using PRO.Domain.Entities;
using PRO.Infrastructure.Services;
using System.Net;
using System.Net.Http;
using Xunit;

namespace PRO.WebApi.Tests;

/// <summary>
/// BusinessMessageService 单元测试 — 覆盖异常翻译、简短消息、事件触发等场景
/// </summary>
public class BusinessMessageServiceTests
{
    private readonly BusinessMessageService _service;

    public BusinessMessageServiceTests()
    {
        _service = new BusinessMessageService();
    }

    [Fact]
    public void Translate_DbUpdateConcurrencyException_ReturnsConcurrencyMessage()
    {
        var ex = new DbUpdateConcurrencyException("并发冲突");
        var result = _service.Translate(ex);

        result.Title.Should().Be("数据已被修改");
        result.ErrorCode.Should().Be("CONCURRENCY_CONFLICT");
    }

    [Fact]
    public void Translate_UnauthorizedAccessException_ReturnsPermissionDenied()
    {
        var ex = new UnauthorizedAccessException("无权访问");
        var result = _service.Translate(ex);

        result.ErrorCode.Should().Be("PERMISSION_DENIED");
    }

    [Fact]
    public void Translate_OperationCanceledException_ReturnsCancelled()
    {
        var ex = new OperationCanceledException("操作取消");
        var result = _service.Translate(ex);

        result.ErrorCode.Should().Be("CANCELLED");
    }

    [Fact]
    public void Translate_TaskCanceledException_ReturnsCancelled()
    {
        var ex = new TaskCanceledException("任务取消");
        var result = _service.Translate(ex);

        result.ErrorCode.Should().Be("CANCELLED");
    }

    [Fact]
    public void Translate_TimeoutException_ReturnsTimeout()
    {
        var ex = new TimeoutException("超时");
        var result = _service.Translate(ex);

        result.ErrorCode.Should().Be("TIMEOUT");
    }

    [Fact]
    public void Translate_ArgumentException_ReturnsValidationError()
    {
        var ex = new ArgumentException("参数无效");
        var result = _service.Translate(ex);

        result.ErrorCode.Should().Be("VALIDATION_ERROR");
        result.UserMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Translate_HttpRequestException_Unauthorized_ReturnsPermissionDenied()
    {
        var ex = new HttpRequestException("未授权", null, HttpStatusCode.Unauthorized);
        var result = _service.Translate(ex);

        result.ErrorCode.Should().Be("PERMISSION_DENIED");
    }

    [Fact]
    public void Translate_HttpRequestException_NotFound_ReturnsNotFound()
    {
        var ex = new HttpRequestException("未找到", null, HttpStatusCode.NotFound);
        var result = _service.Translate(ex);

        result.ErrorCode.Should().Be("NOT_FOUND");
    }

    [Fact]
    public void Translate_HttpRequestException_Timeout_ReturnsTimeout()
    {
        var ex = new HttpRequestException("超时", null, HttpStatusCode.RequestTimeout);
        var result = _service.Translate(ex);

        result.ErrorCode.Should().Be("TIMEOUT");
    }

    [Fact]
    public void Translate_HttpRequestException_ServiceUnavailable_ReturnsServiceUnavailable()
    {
        var ex = new HttpRequestException("服务不可用", null, HttpStatusCode.ServiceUnavailable);
        var result = _service.Translate(ex);

        result.ErrorCode.Should().Be("SERVICE_UNAVAILABLE");
    }

    [Fact]
    public void Translate_HttpRequestException_Other_ReturnsNetworkError()
    {
        var ex = new HttpRequestException("网络错误", null, HttpStatusCode.InternalServerError);
        var result = _service.Translate(ex);

        result.ErrorCode.Should().Be("NETWORK_ERROR");
    }

    [Fact]
    public void Translate_UnknownException_ReturnsUnknownError()
    {
        var ex = new Exception("未知错误");
        var result = _service.Translate(ex);

        result.ErrorCode.Should().Be("UNKNOWN_ERROR");
    }

    [Fact]
    public void Translate_InvalidOperationException_ReturnsInvalidOperation()
    {
        var ex = new InvalidOperationException("无效操作");
        var result = _service.Translate(ex);

        result.ErrorCode.Should().Be("INVALID_OPERATION");
    }

    [Fact]
    public void TranslateToShortMessage_ReturnsFormattedString()
    {
        var ex = new TimeoutException("超时");
        var shortMsg = _service.TranslateToShortMessage(ex);

        shortMsg.Should().Contain(":");
    }

    [Fact]
    public void Translate_EmptyException_ReturnsUnknownError()
    {
        var ex = new Exception("");
        var result = _service.Translate(ex);

        result.ErrorCode.Should().Be("UNKNOWN_ERROR");
    }

    [Fact]
    public void Translate_WithContext_DoesNotThrow()
    {
        var ex = new Exception("测试");
        var action = () => _service.Translate(ex, "订单-创建");

        action.Should().NotThrow();
    }

    [Fact]
    public void Translate_TriggersOnErrorTranslatedEvent()
    {
        BusinessErrorMetric? capturedMetric = null;
        _service.OnErrorTranslated += metric => capturedMetric = metric;

        var ex = new TimeoutException("超时");
        _service.Translate(ex);

        capturedMetric.Should().NotBeNull();
        capturedMetric!.ErrorCode.Should().Be("TIMEOUT");
    }

    [Fact]
    public void Translate_NpgsqlException_ReturnsDatabaseError()
    {
        var ex = new NpgsqlException("数据库连接失败");
        var result = _service.Translate(ex);

        result.ErrorCode.Should().Be("DB_CONNECTION_ERROR");
    }
}
