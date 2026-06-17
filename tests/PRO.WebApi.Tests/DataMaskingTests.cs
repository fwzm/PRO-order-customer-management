using PRO.Infrastructure.Common;
using PRO.Infrastructure.Services;
using Xunit;

namespace PRO.WebApi.Tests;

/// <summary>
/// 数据脱敏验证测试
/// 验证敏感字段不会明文进入日志、Token/密码完全隐藏
/// </summary>
public class DataMaskingTests
{
    // ═══════════════════════════════════════════════════════════
    //  DataMasking 静态工具类测试
    // ═══════════════════════════════════════════════════════════

    [Theory]
    [InlineData("13812348000", "138****8000")]
    [InlineData("15987654321", "159****4321")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("123", "123")] // 太短不脱敏
    public void MaskPhone_ShouldMaskCorrectly(string? input, string expected)
    {
        var result = DataMasking.MaskPhone(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("110101199001011234", "110***********1234")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("12345", "12345")] // 太短不脱敏
    public void MaskIdCard_ShouldMaskCorrectly(string? input, string expected)
    {
        var result = DataMasking.MaskIdCard(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("6222021234567890123", "6222 **** **** 0123")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void MaskBankCard_ShouldMaskCorrectly(string? input, string expected)
    {
        var result = DataMasking.MaskBankCard(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("张三", "张*")]
    [InlineData("李四五", "李*五")]
    [InlineData("欧阳", "欧*")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("王", "*")] // 单字全掩
    public void MaskName_ShouldMaskCorrectly(string? input, string expected)
    {
        var result = DataMasking.MaskName(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("test@example.com", "t***@example.com")]
    [InlineData("a@b.com", "a@b.com")] // 太短不脱敏
    [InlineData("", "")]
    [InlineData("no-at-sign", "no-at-sign")]
    public void MaskEmail_ShouldMaskCorrectly(string? input, string expected)
    {
        var result = DataMasking.MaskEmail(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("北京市朝阳区建国路88号", "北京市朝阳区***")]
    [InlineData("上海市浦东新区", "上海市浦东新***")]
    [InlineData("上海市", "上海市")]
    [InlineData("", "")]
    [InlineData("abc", "abc")]
    public void MaskAddress_ShouldMaskCorrectly(string? input, string expected)
    {
        var result = DataMasking.MaskAddress(input);
        Assert.Equal(expected, result);
    }

    // ═══════════════════════════════════════════════════════════
    //  DataMaskingService.IsSensitive 测试（日志敏感字段检测）
    // ═══════════════════════════════════════════════════════════

    [Theory]
    [InlineData("password", true)]
    [InlineData("Password", true)]
    [InlineData("token", true)]
    [InlineData("access_token", true)]
    [InlineData("secret", true)]
    [InlineData("jwt", true)]
    [InlineData("jwt_key", true)]
    [InlineData("apikey", true)]
    [InlineData("api_key", true)]
    [InlineData("encryptionkey", true)]
    [InlineData("user_name", false)]
    [InlineData("order_no", false)]
    [InlineData("product_name", false)]
    [InlineData("amount", false)]
    [InlineData("", false)]
    public void IsSensitive_ShouldDetectSensitiveKeys(string key, bool expected)
    {
        var result = DataMaskingService.IsSensitive(key);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("password", "mysecret123", "***REDACTED***")]
    [InlineData("token", "eyJhbGciOiJIUzI1NiJ9...", "***REDACTED***")]
    [InlineData("jwt_key", "super-secret-key-32-bytes!", "***REDACTED***")]
    [InlineData("api_key", "sk-abc123def456", "***REDACTED***")]
    [InlineData("user_name", "zhangsan", "zhangsan")] // 非敏感，原样返回
    [InlineData("order_no", "ORD-2024-001", "ORD-2024-001")]
    public void MaskSensitiveForLog_ShouldRedactSensitiveValues(string key, string value, string expected)
    {
        var result = DataMaskingService.MaskSensitiveForLog(key, value);
        Assert.Equal(expected, result);
    }

    // ═══════════════════════════════════════════════════════════
    //  日志上下文脱敏测试
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void SensitiveFieldsInLogContext_ShouldBeDetected()
    {
        // 模拟日志上下文中可能出现的敏感键
        var logContext = new Dictionary<string, string>
        {
            ["UserId"] = "123",
            ["OrderNo"] = "ORD-001",
            ["Token"] = "bearer-token-abc",
            ["Authorization"] = "Bearer xyz",
            ["Password"] = "should-not-log",
            ["CustomerName"] = "张三"
        };

        var sensitiveKeys = logContext.Keys.Where(k => DataMaskingService.IsSensitive(k)).ToList();
        Assert.Contains("Token", sensitiveKeys);
        Assert.Contains("Password", sensitiveKeys);
        Assert.DoesNotContain("UserId", sensitiveKeys);
        Assert.DoesNotContain("OrderNo", sensitiveKeys);
        Assert.DoesNotContain("CustomerName", sensitiveKeys);
    }

    [Fact]
    public void AllKeys_ContainingPasswordOrToken_ShouldBeFlagged()
    {
        var variations = new[]
        {
            "password", "Password", "PASSWORD",
            "old_password", "new_password", "confirm_password",
            "token", "Token", "access_token", "refresh_token",
            "jwt", "JWT", "jwt_key", "jwt_secret",
            "apikey", "api_key", "API_KEY",
            "secret", "SECRET", "client_secret",
            "encryptionkey", "EncryptionKey"
        };

        foreach (var key in variations)
        {
            Assert.True(DataMaskingService.IsSensitive(key),
                $"Key '{key}' should be flagged as sensitive but was not");
        }
    }

    [Fact]
    public void RedactedValue_ShouldNeverContainOriginalSecret()
    {
        var secretValues = new[]
        {
            ("password", "SuperSecret123!"),
            ("token", "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ"),
            ("jwt_key", "this-is-a-very-long-jwt-signing-key-at-least-32-bytes"),
            ("api_key", "sk-proj-abcdefghijklmnopqrstuvwxyz123456"),
        };

        foreach (var (key, value) in secretValues)
        {
            var redacted = DataMaskingService.MaskSensitiveForLog(key, value);
            Assert.Equal("***REDACTED***", redacted);
            Assert.DoesNotContain(value, redacted);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  综合脱敏场景测试
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void CompositeMasking_ShouldHandleAllSensitiveTypes()
    {
        // 模拟一个包含多种敏感信息的业务对象
        var phone = DataMasking.MaskPhone("13812348000");
        var idCard = DataMasking.MaskIdCard("110101199001011234");
        var bankCard = DataMasking.MaskBankCard("6222021234567890123");
        var name = DataMasking.MaskName("张三");
        var email = DataMasking.MaskEmail("zhangsan@example.com");
        var address = DataMasking.MaskAddress("北京市朝阳区建国路88号SOHO现代城A座");

        Assert.Equal("138****8000", phone);
        Assert.Equal("110***********1234", idCard);
        Assert.Equal("6222 **** **** 0123", bankCard);
        Assert.Equal("张*", name);
        Assert.Equal("z***@example.com", email);
        Assert.Equal("北京市朝阳区***", address);

        // 验证脱敏后的值不包含完整的原始信息
        Assert.DoesNotContain("13812348000", phone);
        Assert.DoesNotContain("199001011234", idCard);
        Assert.DoesNotContain("1234567890123", bankCard);
    }

    [Fact]
    public void EmptyAndNullInputs_ShouldNotThrow()
    {
        Assert.Equal("", DataMasking.MaskPhone(""));
        Assert.Equal("", DataMasking.MaskIdCard(null));
        Assert.Equal("", DataMasking.MaskBankCard(""));
        Assert.Equal("", DataMasking.MaskName(null));
        Assert.Equal("", DataMasking.MaskEmail(""));
        Assert.Equal("", DataMasking.MaskAddress(null));
    }

    [Fact]
    public void MaskGeneric_ShouldPreservePrefixAndSuffix()
    {
        var result = DataMasking.MaskGeneric("ABCDEFGHIJKLMNOP", 3, 3);
        Assert.StartsWith("ABC", result);
        Assert.EndsWith("NOP", result);
        Assert.Contains("*", result);
    }
}
