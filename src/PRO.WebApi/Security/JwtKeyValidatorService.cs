using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Text;

namespace PRO.WebApi.Security;

/// <summary>
/// JWT 密钥校验服务 — 实现 IValidateOptions 模式，支持 DI 注册与单元测试
/// 生产环境遇到危险 Key 拒绝启动，开发环境输出 Warning
/// </summary>
public interface IJwtKeyValidatorService
{
    /// <summary>
    /// 校验 JWT Key 安全性，返回 (IsValid, Issues)
    /// </summary>
    (bool IsValid, IReadOnlyList<string> Issues) Validate(string? jwtKey, bool isProduction);

    /// <summary>
    /// 生成安全的随机密钥（用于帮助用户替换）
    /// </summary>
    string GenerateSecureKey(int byteLength = 48);
}

public class JwtKeyValidatorService : IJwtKeyValidatorService
{
    /// <summary>已知的危险 JWT Key 列表（默认示例值、开发占位符等）</summary>
    private static readonly HashSet<string> DangerousKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "CHANGE_ME_JWT_KEY_AT_LEAST_32_CHARS",
        "CHANGE_ME",
        "your-secret-key",
        "your-secret-key-here",
        "secret",
        "supersecret",
        "test-key",
        "dev-key",
        "development-key",
        "placeholder",
        "TODO",
        "FIXME",
        "temp",
        "temporary",
        "default",
        "example",
        "sample",
        "mysecretkey12345678901234567890",
    };

    /// <summary>危险 Key 中包含的关键词</summary>
    private static readonly string[] DangerousPatterns =
    {
        "CHANGE_ME", "YOUR_", "MUST-CHANGE", "PLACEHOLDER",
        "TODO", "FIXME", "EXAMPLE", "SAMPLE", "TEMPORARY"
    };

    /// <inheritdoc/>
    public (bool IsValid, IReadOnlyList<string> Issues) Validate(string? jwtKey, bool isProduction)
    {
        var issues = new List<string>();

        // 1. 空值检查
        if (string.IsNullOrWhiteSpace(jwtKey))
        {
            issues.Add("JWT Key 为空或未配置");
            return (false, issues);
        }

        // 2. 长度检查（至少 32 字节 = 256 bit）
        var byteLength = Encoding.UTF8.GetByteCount(jwtKey);
        if (byteLength < 32)
        {
            issues.Add($"JWT Key 长度不足（当前 {byteLength} 字节，至少需要 32 字节 / 256 bit）");
        }

        // 3. 精确匹配危险值
        if (DangerousKeys.Contains(jwtKey))
        {
            issues.Add($"JWT Key 是已知的默认示例值 \"{MaskKey(jwtKey)}\"");
        }

        // 4. 关键词匹配
        foreach (var pattern in DangerousPatterns)
        {
            if (jwtKey.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add($"JWT Key 包含危险关键词 \"{pattern}\"");
                break;
            }
        }

        // 5. 检测全重复字符（如 "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"）
        if (jwtKey.Length >= 32 && jwtKey.Distinct().Count() <= 3)
        {
            issues.Add("JWT Key 字符重复度过高，请使用随机字符串");
        }

        // 6. 检测是否仍为开发占位符格式
        if (jwtKey.StartsWith("dev-", StringComparison.OrdinalIgnoreCase) ||
            jwtKey.StartsWith("test-", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add($"JWT Key 看起来是开发/测试用值 \"{MaskKey(jwtKey)}\"");
        }

        return (issues.Count == 0, issues);
    }

    /// <inheritdoc/>
    public string GenerateSecureKey(int byteLength = 48)
    {
        var bytes = new byte[byteLength];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>遮罩 JWT Key（只显示前后各4个字符）</summary>
    private static string MaskKey(string key)
    {
        if (key.Length <= 8) return "****";
        return $"{key[..4]}****{key[^4..]}";
    }
}

/// <summary>
/// JWT 选项配置 — 支持 IValidateOptions 模式，在启动时自动校验
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "PRO-System";
    public int ExpireMinutes { get; set; } = 1440;
}

/// <summary>
/// JWT 选项校验器 — 实现 IValidateOptions{JwtOptions}
/// 在 .NET Options 管道中自动调用，Production 环境不安全的 Key 会阻止启动
/// </summary>
public class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    private readonly IJwtKeyValidatorService _validator;
    private readonly IHostEnvironment _environment;
    private readonly Serilog.ILogger _logger;

    public JwtOptionsValidator(
        IJwtKeyValidatorService validator,
        IHostEnvironment environment,
        Serilog.ILogger logger)
    {
        _validator = validator;
        _environment = environment;
        _logger = logger;
    }

    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        if (options == null)
        {
            return ValidateOptionsResult.Fail("JwtOptions 未配置");
        }

        var isProduction = _environment.IsProduction();
        var (isValid, issues) = _validator.Validate(options.Key, isProduction);

        if (isValid)
        {
            _logger.Information("JWT Key 安全校验通过 (Issuer: {Issuer}, ExpireMinutes: {ExpireMinutes})",
                options.Issuer, options.ExpireMinutes);
            return ValidateOptionsResult.Success;
        }

        foreach (var issue in issues)
        {
            if (isProduction)
            {
                _logger.Fatal("JWT 安全检查失败: {Issue}", issue);
            }
            else
            {
                _logger.Warning("JWT 安全警告: {Issue}", issue);
            }
        }

        if (isProduction)
        {
            _logger.Fatal(
                "═══════════════════════════════════════════════════════════\n" +
                "  生产环境检测到不安全的 JWT Key，拒绝启动！\n" +
                "  请通过环境变量 PRO_Jwt__Key 或 appsettings.Production.json\n" +
                "  配置一个至少 32 字节的随机密钥。\n" +
                "  生成方法: openssl rand -base64 48\n" +
                "═══════════════════════════════════════════════════════════");

            return ValidateOptionsResult.Fail(
                $"JWT Key 安全校验失败（共 {issues.Count} 个问题）：{string.Join("; ", issues)}");
        }

        _logger.Warning("JWT Key 存在安全隐患，在开发环境下允许继续运行，但生产环境必须更换。");
        return ValidateOptionsResult.Success;
    }
}
