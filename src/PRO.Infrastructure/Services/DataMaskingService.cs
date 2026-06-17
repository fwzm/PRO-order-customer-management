using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using PRO.Infrastructure.Configuration;
using PRO.Infrastructure.Common;
using Serilog;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 数据脱敏服务 - 对敏感字段进行脱敏处理
/// 手机号、身份证、银行卡、姓名等敏感信息在普通用户视图中脱敏显示
/// 管理员可查看完整数据，日志绝不记录Token/密码/JWT Key
/// </summary>
public class DataMaskingService
{
    private readonly DataMaskingOptions _options;
    private static readonly Regex PhoneRegex = new(@"^1[3-9]\d{9}$", RegexOptions.Compiled);
    private static readonly Regex IdCardRegex = new(@"^\d{15}|\d{18}$", RegexOptions.Compiled);
    private static readonly Regex BankCardRegex = new(@"^\d{16,19}$", RegexOptions.Compiled);

    public DataMaskingService(IOptions<DataMaskingOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// 脱敏手机号：保留前N后M位，中间用*替换
    /// </summary>
    public string MaskPhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return phone;
        if (!_options.Enabled) return phone;

        try
        {
            if (!PhoneRegex.IsMatch(phone)) return phone;
            var prefix = _options.PhoneRule.KeepPrefixLength;
            var suffix = _options.PhoneRule.KeepSuffixLength;
            var maskChar = _options.PhoneRule.MaskChar;

            if (phone.Length <= prefix + suffix) return phone;
            var masked = phone[..prefix] +
                         new string(maskChar, phone.Length - prefix - suffix) +
                         phone[^suffix..];
            return masked;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "手机号脱敏失败");
            return phone;
        }
    }

    /// <summary>
    /// 脱敏身份证号：保留前N后M位
    /// </summary>
    public string MaskIdCard(string idCard)
    {
        if (string.IsNullOrWhiteSpace(idCard)) return idCard;
        if (!_options.Enabled) return idCard;

        try
        {
            if (!IdCardRegex.IsMatch(idCard)) return idCard;
            var prefix = _options.IdCardRule.KeepPrefixLength;
            var suffix = _options.IdCardRule.KeepSuffixLength;
            var maskChar = _options.IdCardRule.MaskChar;

            if (idCard.Length <= prefix + suffix) return idCard;
            var masked = idCard[..prefix] +
                         new string(maskChar, idCard.Length - prefix - suffix) +
                         idCard[^suffix..];
            return masked;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "身份证号脱敏失败");
            return idCard;
        }
    }

    /// <summary>
    /// 脱敏银行卡号：保留前N后M位
    /// </summary>
    public string MaskBankCard(string bankCard)
    {
        if (string.IsNullOrWhiteSpace(bankCard)) return bankCard;
        if (!_options.Enabled) return bankCard;

        try
        {
            if (!BankCardRegex.IsMatch(bankCard)) return bankCard;
            var prefix = _options.BankCardRule.KeepPrefixLength;
            var suffix = _options.BankCardRule.KeepSuffixLength;
            var maskChar = _options.BankCardRule.MaskChar;

            if (bankCard.Length <= prefix + suffix) return bankCard;
            var masked = bankCard[..prefix] +
                         new string(maskChar, bankCard.Length - prefix - suffix) +
                         bankCard[^suffix..];
            return masked;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "银行卡号脱敏失败");
            return bankCard;
        }
    }

    /// <summary>
    /// 脱敏姓名：保留姓氏
    /// </summary>
    public string MaskName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return name;
        if (!_options.Enabled) return name;

        try
        {
            return DataMasking.MaskName(name);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "姓名脱敏失败");
            return name;
        }
    }

    /// <summary>
    /// 脱敏地址：保留省市区
    /// </summary>
    public string MaskAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address)) return address;
        if (!_options.Enabled) return address;

        try
        {
            return DataMasking.MaskAddress(address);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "地址脱敏失败");
            return address;
        }
    }

    /// <summary>
    /// 检查是否为敏感信息（用于日志过滤）
    /// </summary>
    public static bool IsSensitive(string key)
    {
        var lower = key.ToLowerInvariant();
        return lower.Contains("password") ||
               lower.Contains("token") ||
               lower.Contains("secret") ||
               lower.Contains("jwt") ||
               lower.Contains("apikey") ||
               lower.Contains("api_key") ||
               lower.Contains("encryptionkey") ||
               lower.Contains("authorization") ||
               lower.Contains("phone") ||
               lower.Contains("mobile") ||
               lower.Contains("idcard") ||
               lower.Contains("id_card") ||
               lower.Contains("card_no") ||
               lower.Contains("bankcard");
    }

    /// <summary>
    /// 脱敏日志中的敏感字段值
    /// </summary>
    public static string MaskSensitiveForLog(string key, string value)
    {
        if (IsSensitive(key)) return "***REDACTED***";
        return value;
    }
}

/// <summary>
/// 数据脱敏权限扩展方法 - 基于用户角色决定是否应用脱敏
/// </summary>
public static class DataMaskingPermissionExtensions
{
    /// <summary>
    /// 根据当前用户角色决定是否脱敏手机号
    /// 管理员（总部管理员/分公司管理员）可查看完整数据，普通用户看到脱敏数据
    /// </summary>
    public static string MaskPhoneWithPermission(this DataMaskingService service, string phone, bool isAdmin)
    {
        if (isAdmin) return phone;
        return service.MaskPhone(phone);
    }

    /// <summary>
    /// 根据当前用户角色决定是否脱敏身份证号
    /// </summary>
    public static string MaskIdCardWithPermission(this DataMaskingService service, string idCard, bool isAdmin)
    {
        if (isAdmin) return idCard;
        return service.MaskIdCard(idCard);
    }

    /// <summary>
    /// 根据当前用户角色决定是否脱敏银行卡号
    /// </summary>
    public static string MaskBankCardWithPermission(this DataMaskingService service, string bankCard, bool isAdmin)
    {
        if (isAdmin) return bankCard;
        return service.MaskBankCard(bankCard);
    }

    /// <summary>
    /// 根据当前用户角色决定是否脱敏姓名
    /// </summary>
    public static string MaskNameWithPermission(this DataMaskingService service, string name, bool isAdmin)
    {
        if (isAdmin) return name;
        return service.MaskName(name);
    }

    /// <summary>
    /// 根据当前用户角色决定是否脱敏地址
    /// </summary>
    public static string MaskAddressWithPermission(this DataMaskingService service, string address, bool isAdmin)
    {
        if (isAdmin) return address;
        return service.MaskAddress(address);
    }
}
