using PRO.WebApi.Security;
using Xunit;

namespace PRO.WebApi.Tests;

/// <summary>
/// JWT 密钥安全校验测试 — 覆盖空值、长度不足、危险Key、生产环境拒绝等场景
/// </summary>
public class JwtSecurityTests
{
    private readonly JwtKeyValidatorService _validator = new();

    [Fact]
    public void Validate_EmptyKey_ReturnsInvalid()
    {
        var (isValid, issues) = _validator.Validate(null, isProduction: false);
        Assert.False(isValid);
        Assert.Contains(issues, i => i.Contains("为空"));
    }

    [Fact]
    public void Validate_WhitespaceKey_ReturnsInvalid()
    {
        var (isValid, issues) = _validator.Validate("   ", isProduction: false);
        Assert.False(isValid);
        Assert.Contains(issues, i => i.Contains("为空"));
    }

    [Fact]
    public void Validate_KeyTooShort_ReturnsWarning()
    {
        var (isValid, issues) = _validator.Validate("short", isProduction: false);
        // 短 key 标记为不安全，issues 包含长度不足警告
        Assert.False(isValid);
        Assert.NotEmpty(issues);
        Assert.Contains(issues, i => i.Contains("长度不足"));
    }

    [Fact]
    public void Validate_KnownDangerousKey_ReturnsInvalid()
    {
        var (isValid, issues) = _validator.Validate("CHANGE_ME_JWT_KEY_AT_LEAST_32_CHARS", isProduction: false);
        Assert.False(isValid);
        Assert.Contains(issues, i => i.Contains("默认示例值"));
    }

    [Fact]
    public void Validate_KeyWithChangeMe_ReturnsInvalid()
    {
        var (isValid, issues) = _validator.Validate("my-key-with-CHANGE_ME-in-the-middle-so-it-is-long-enough", isProduction: false);
        Assert.False(isValid);
        Assert.Contains(issues, i => i.Contains("CHANGE_ME"));
    }

    [Fact]
    public void Validate_KeyWithYourPlaceholder_ReturnsInvalid()
    {
        var (isValid, issues) = _validator.Validate("YOUR_SECRET_KEY_THAT_NEEDS_CHANGING_12345", isProduction: false);
        Assert.False(isValid);
        Assert.Contains(issues, i => i.Contains("YOUR_"));
    }

    [Fact]
    public void Validate_KeyWithTodo_ReturnsInvalid()
    {
        var (isValid, issues) = _validator.Validate("TODO-replace-with-real-key-later-please", isProduction: false);
        Assert.False(isValid);
        Assert.Contains(issues, i => i.Contains("TODO"));
    }

    [Fact]
    public void Validate_RepeatedCharKey_ReturnsWarning()
    {
        var (isValid, issues) = _validator.Validate("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", isProduction: false);
        Assert.False(isValid);
        Assert.Contains(issues, i => i.Contains("重复度"));
    }

    [Fact]
    public void Validate_TestPrefixKey_ReturnsWarning()
    {
        var (isValid, issues) = _validator.Validate("test-key-for-development-use-only-ok", isProduction: false);
        Assert.False(isValid);
        Assert.Contains(issues, i => i.Contains("测试用值"));
    }

    [Fact]
    public void Validate_DevPrefixKey_ReturnsWarning()
    {
        var (isValid, issues) = _validator.Validate("dev-environment-key-that-is-32-bytes", isProduction: false);
        Assert.False(isValid);
        Assert.Contains(issues, i => i.Contains("测试用值"));
    }

    [Fact]
    public void Validate_ValidKey_ReturnsValid()
    {
        var validKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
        var (isValid, issues) = _validator.Validate(validKey, isProduction: false);
        Assert.True(isValid);
        Assert.Empty(issues);
    }

    [Fact]
    public void Validate_ProductionDangerousKey_StillReportsIssues()
    {
        var (isValid, issues) = _validator.Validate("CHANGE_ME_JWT_KEY_AT_LEAST_32_CHARS", isProduction: true);
        Assert.False(isValid);
        Assert.NotEmpty(issues);
    }

    [Fact]
    public void Validate_ProductionValidKey_ReturnsValid()
    {
        var validKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
        var (isValid, issues) = _validator.Validate(validKey, isProduction: true);
        Assert.True(isValid);
        Assert.Empty(issues);
    }

    [Fact]
    public void Validate_KeyExactly32Bytes_ReturnsValid()
    {
        var key32 = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24))[..32];
        var (isValid, issues) = _validator.Validate(key32, isProduction: false);
        // 32 字节 = 256 bit 恰好满足最低要求
        Assert.True(isValid);
    }

    [Fact]
    public void GenerateSecureKey_ReturnsBase64String()
    {
        var key = _validator.GenerateSecureKey(48);
        Assert.NotNull(key);
        Assert.True(key.Length >= 64); // 48 bytes in base64 = 64 chars
        var (isValid, _) = _validator.Validate(key, isProduction: true);
        Assert.True(isValid);
    }
}
