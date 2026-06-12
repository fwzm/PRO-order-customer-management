using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Moq;
using PRO.WebApi.Security;
using Serilog;
using Xunit;

namespace PRO.WebApi.Tests;

/// <summary>
/// JWT 安全校验器测试 — 5个核心场景
/// </summary>
public class JwtSecurityValidatorTests
{
    private readonly ILogger _logger;

    public JwtSecurityValidatorTests()
    {
        _logger = new LoggerConfiguration().CreateLogger();
    }

    private static IHostEnvironment CreateEnvironment(string environmentName)
    {
        var mock = new Mock<IHostEnvironment>();
        mock.Setup(e => e.EnvironmentName).Returns(environmentName);
        return mock.Object;
    }

    // ═══ 场景1: Production 环境下 JWT Key 为空时启动失败 ═══

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_Production_EmptyKey_ReturnsFalse(string? jwtKey)
    {
        var env = CreateEnvironment("Production");
        var result = JwtSecurityValidator.Validate(jwtKey, env, _logger);
        result.Should().BeFalse("Production 环境下 JWT Key 为空应该拒绝启动");
    }

    // ═══ 场景2: Production 环境下 JWT Key 为默认值时启动失败 ═══

    [Theory]
    [InlineData("CHANGE_ME_JWT_KEY_AT_LEAST_32_CHARS")]
    [InlineData("CHANGE_ME")]
    [InlineData("your-secret-key")]
    [InlineData("secret")]
    [InlineData("supersecret")]
    [InlineData("default")]
    [InlineData("example")]
    public void Validate_Production_DangerousDefaultKey_ReturnsFalse(string jwtKey)
    {
        var env = CreateEnvironment("Production");
        var result = JwtSecurityValidator.Validate(jwtKey, env, _logger);
        result.Should().BeFalse($"Production 环境下使用危险默认值 '{jwtKey}' 应该拒绝启动");
    }

    // ═══ 场景3: Production 环境下 JWT Key 长度不足时启动失败 ═══

    [Theory]
    [InlineData("short-key")]
    [InlineData("this-is-only-20-bytes!!")]
    [InlineData("a-21-byte-key-1234567")]
    public void Validate_Production_ShortKey_ReturnsFalse(string jwtKey)
    {
        var env = CreateEnvironment("Production");
        var result = JwtSecurityValidator.Validate(jwtKey, env, _logger);
        result.Should().BeFalse($"Production 环境下长度不足的 Key (len={jwtKey.Length}) 应该拒绝启动");
    }

    // ═══ 场景4: Development 环境下使用默认值时输出警告但允许启动 ═══

    [Theory]
    [InlineData("CHANGE_ME_JWT_KEY_AT_LEAST_32_CHARS")]
    [InlineData("short-dev-key")]
    [InlineData(null)]
    public void Validate_Development_DangerousKey_ReturnsTrue(string? jwtKey)
    {
        var env = CreateEnvironment("Development");
        var result = JwtSecurityValidator.Validate(jwtKey, env, _logger);
        result.Should().BeTrue("Development 环境下即使 Key 不安全也应该允许启动（仅输出警告）");
    }

    // ═══ 场景5: 合法 JWT Key 可以正常启动 ═══

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    [InlineData("Staging")]
    public void Validate_ValidKey_ReturnsTrue(string environmentName)
    {
        // 生成一个安全的随机 Key（48字节 Base64）
        var validKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
        var env = CreateEnvironment(environmentName);

        var result = JwtSecurityValidator.Validate(validKey, env, _logger);
        result.Should().BeTrue($"环境 {environmentName} 下合法的 JWT Key 应该允许启动");
    }

    // ═══ 额外边界测试 ═══

    [Fact]
    public void Validate_Production_KeyWithDangerousPattern_ReturnsFalse()
    {
        var env = CreateEnvironment("Production");
        // Key 包含 CHANGE_ME 关键词，即使长度足够也应拒绝
        var keyWithPattern = "CHANGE_ME_this_is_a_very_long_key_that_is_more_than_32_bytes";
        var result = JwtSecurityValidator.Validate(keyWithPattern, env, _logger);
        result.Should().BeFalse("包含危险关键词的 Key 应该被拒绝");
    }

    [Fact]
    public void Validate_Production_RepetitiveKey_ReturnsFalse()
    {
        var env = CreateEnvironment("Production");
        // 全重复字符的 Key
        var repetitiveKey = new string('a', 64);
        var result = JwtSecurityValidator.Validate(repetitiveKey, env, _logger);
        result.Should().BeFalse("全重复字符的 Key 应该被拒绝");
    }

    [Fact]
    public void Validate_Production_DevPrefixedKey_ReturnsFalse()
    {
        var env = CreateEnvironment("Production");
        var devKey = "dev-key-that-is-long-enough-for-32-bytes-minimum-requirement";
        var result = JwtSecurityValidator.Validate(devKey, env, _logger);
        result.Should().BeFalse("dev- 前缀的 Key 在 Production 环境应该被拒绝");
    }

    [Fact]
    public void Validate_Development_ValidKey_ReturnsTrue()
    {
        var validKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
        var env = CreateEnvironment("Development");

        var result = JwtSecurityValidator.Validate(validKey, env, _logger);
        result.Should().BeTrue();
    }
}
