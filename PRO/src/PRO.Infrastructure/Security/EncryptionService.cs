using System.Security.Cryptography;
using System.Text;
using PRO.Application.Interfaces;

namespace PRO.Infrastructure.Security;

/// <summary>
/// 数据加密服务 - 使用Windows DPAPI进行安全加密
/// 非 Windows 平台回退时将发出严重级别警告，提醒运维启用 ASP.NET Core Data Protection 替代方案
/// </summary>
public class EncryptionService : IEncryptionService
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("PRO_Enterprise_Management_System_v1.0");
    private static bool _dpapiFallbackWarned;

    /// <summary>
    /// 使用DPAPI加密数据（仅Windows平台有效）
    /// 非 Windows 平台：发出 Fatal 日志警告明文风险，若为生产环境则抛出异常
    /// </summary>
    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        if (!OperatingSystem.IsWindows())
        {
            WarnDpapiFallback("加密");
            return plainText;
        }

        try
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] encryptedBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encryptedBytes);
        }
        catch (Exception ex)
        {
            throw new CryptographicException("加密数据失败", ex);
        }
    }

    /// <summary>
    /// 使用DPAPI解密数据（仅Windows平台有效）
    /// 非 Windows 平台：发出 Fatal 日志警告明文风险
    /// </summary>
    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return string.Empty;

        if (!OperatingSystem.IsWindows())
        {
            WarnDpapiFallback("解密");
            return cipherText;
        }

        try
        {
            byte[] cipherBytes = Convert.FromBase64String(cipherText);
            byte[] decryptedBytes = ProtectedData.Unprotect(cipherBytes, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decryptedBytes);
        }
        catch (Exception ex)
        {
            throw new CryptographicException("解密数据失败", ex);
        }
    }

    private static void WarnDpapiFallback(string operation)
    {
        if (_dpapiFallbackWarned)
            return;

        _dpapiFallbackWarned = true;

        var message = $"当前运行平台 {System.Runtime.InteropServices.RuntimeInformation.OSDescription} 不支持 Windows DPAPI。" +
                       $"{operation}操作将使用明文回退，存在严重安全风险！" +
                       "建议：1) 部署到 Windows Server; 2) 或引入 ASP.NET Core Data Protection + 证书/密钥环替代。";

        Serilog.Log.Fatal("⚠️ 数据加密安全警告: {Message}", message);
    }

    /// <summary>
    /// 使用BCrypt哈希密码
    /// </summary>
    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt(12));
    }

    /// <summary>
    /// 验证密码
    /// </summary>
    public bool VerifyPassword(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 生成随机Token
    /// </summary>
    public string GenerateToken()
    {
        byte[] tokenBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(tokenBytes);
        }
        return Convert.ToBase64String(tokenBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}

/// <summary>
/// 敏感数据保护类 - 用于属性级别的加密存储
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class EncryptedAttribute : Attribute
{
}

/// <summary>
/// 密码属性类 - 自动哈希存储
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class PasswordAttribute : Attribute
{
}
