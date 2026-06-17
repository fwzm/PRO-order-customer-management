using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Text;

namespace PRO.WebApi.Security;

/// <summary>
/// JWT 签名密钥安全校验器
/// 在 Production 环境下检测到危险 JWT Key 时拒绝启动
/// 在 Development 环境下输出 Warning 日志
/// </summary>
public static class JwtSecurityValidator
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
    private static readonly string[] DangerousPatterns = new[]
    {
        "CHANGE_ME", "YOUR_", "MUST-CHANGE", "PLACEHOLDER",
        "TODO", "FIXME", "EXAMPLE", "SAMPLE", "TEMPORARY"
    };

    /// <summary>
    /// 校验 JWT Key 安全性
    /// </summary>
    public static bool Validate(string? jwtKey, IHostEnvironment environment, Serilog.ILogger logger)
    {
        var isProduction = environment.IsProduction();
        var issues = new List<string>();

        // 1. 空值检查
        if (string.IsNullOrWhiteSpace(jwtKey))
        {
            issues.Add("JWT Key 为空或未配置");
        }
        else
        {
            // 2. 长度检查（至少 32 字节）
            var byteLength = Encoding.UTF8.GetByteCount(jwtKey);
            if (byteLength < 32)
            {
                issues.Add($"JWT Key 长度不足（当前 {byteLength} 字节，至少需要 32 字节）");
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
        }

        // 7. 生产环境检测 Development 配置
        if (isProduction && environment.IsDevelopment())
        {
            issues.Add("生产环境正在使用 Development 配置，这是严重的安全风险");
        }

        if (issues.Count == 0)
        {
            logger.Information("JWT Key 安全校验通过");
            return true;
        }

        // 输出所有问题
        foreach (var issue in issues)
        {
            if (isProduction)
            {
                logger.Fatal("JWT 安全检查失败: {Issue}", issue);
            }
            else
            {
                logger.Warning("JWT 安全警告: {Issue}", issue);
            }
        }

        if (isProduction)
        {
            logger.Fatal(
                "═══════════════════════════════════════════════════════════\n" +
                "  生产环境检测到不安全的 JWT Key，拒绝启动！\n" +
                "  请通过环境变量 PRO_Jwt__Key 或 appsettings.Production.json\n" +
                "  配置一个至少 32 字节的随机密钥。\n" +
                "  生成方法: openssl rand -base64 48\n" +
                "═══════════════════════════════════════════════════════════");
            return false;
        }

        logger.Warning(
            "JWT Key 存在安全隐患，在开发环境下允许继续运行，但生产环境必须更换。");
        return true;
    }

    /// <summary>
    /// 遮罩 JWT Key（只显示前后各4个字符）
    /// </summary>
    private static string MaskKey(string key)
    {
        if (key.Length <= 8) return "****";
        return $"{key[..4]}****{key[^4..]}";
    }
}


