using System.Security.Cryptography;
using System.Text;
using PRO.Application.Interfaces;

namespace PRO.Infrastructure.Security;

/// <summary>
/// 数据加密服务 - 使用Windows DPAPI进行安全加密
/// </summary>
public class EncryptionService : IEncryptionService
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("PRO_Enterprise_Management_System_v1.0");

    /// <summary>
    /// 使用DPAPI加密数据（仅Windows平台有效）
    /// </summary>
    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        if (!OperatingSystem.IsWindows())
        {
            Serilog.Log.Warning("DPAPI 加密仅支持 Windows 平台，数据将以明文存储");
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
    /// </summary>
    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return string.Empty;

        if (!OperatingSystem.IsWindows())
            return cipherText;

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
