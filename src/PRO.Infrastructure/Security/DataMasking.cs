using System.Text.RegularExpressions;

namespace PRO.Infrastructure.Security;

/// <summary>
/// 敏感数据脱敏工具 — 在日志/输出前对密码、Token、证件号等字段进行掩码处理
/// </summary>
public static class DataMasking
{
    /// <summary>
    /// 脱敏密码：返回固定掩码
    /// </summary>
    public static string MaskPassword(string? password) => string.IsNullOrEmpty(password) ? "***" : "******";

    /// <summary>
    /// 脱敏 Token/JWT：保留前8后4字符
    /// </summary>
    public static string MaskToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return "***";
        if (token.Length <= 12) return new string('*', token.Length);
        return token[..8] + "****" + token[^4..];
    }

    /// <summary>
    /// 脱敏身份证号：保留前3后4位
    /// </summary>
    public static string MaskIdCard(string? idCard)
    {
        if (string.IsNullOrWhiteSpace(idCard)) return "***";
        if (idCard.Length <= 7) return new string('*', idCard.Length);
        return idCard[..3] + new string('*', idCard.Length - 7) + idCard[^4..];
    }

    /// <summary>
    /// 脱敏手机号：保留前3后4位
    /// </summary>
    public static string MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return "***";
        if (phone.Length <= 7) return new string('*', phone.Length);
        return phone[..3] + new string('*', phone.Length - 7) + phone[^4..];
    }

    /// <summary>
    /// 脱敏银行卡号：保留后4位
    /// </summary>
    public static string MaskBankCard(string? cardNo)
    {
        if (string.IsNullOrWhiteSpace(cardNo)) return "***";
        if (cardNo.Length <= 4) return new string('*', cardNo.Length);
        return new string('*', cardNo.Length - 4) + cardNo[^4..];
    }

    /// <summary>
    /// 脱敏姓名：保留姓氏
    /// </summary>
    public static string MaskName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "***";
        if (name.Length <= 1) return name;
        return name[0] + new string('*', name.Length - 1);
    }

    /// <summary>
    /// 脱敏邮箱：保留首字符和域名
    /// </summary>
    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "***";
        var atIndex = email.IndexOf('@');
        if (atIndex <= 1) return "***@" + email[(atIndex + 1)..];
        return email[0] + "***" + email[(atIndex - 1)..];
    }

    /// <summary>
    /// 脱敏地址：仅保留省市
    /// </summary>
    public static string MaskAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return "***";
        if (address.Length <= 6) return address[..1] + new string('*', address.Length - 1);
        return address[..6] + "****";
    }

    /// <summary>
    /// 脱敏 API Key：保留前4后4字符
    /// </summary>
    public static string MaskApiKey(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return "***";
        if (apiKey.Length <= 8) return new string('*', apiKey.Length);
        return apiKey[..4] + new string('*', apiKey.Length - 8) + apiKey[^4..];
    }

    /// <summary>
    /// 泛化脱敏：根据字段名自动选择脱敏策略
    /// </summary>
    public static string MaskSensitive(string? fieldName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "***";

        var lowerName = (fieldName ?? "").ToLowerInvariant();

        if (lowerName.Contains("password") || lowerName.Contains("pwd") || lowerName.Contains("passhash"))
            return MaskPassword(value);

        if (lowerName.Contains("token") || lowerName.Contains("jwt") || lowerName.Contains("bearer"))
            return MaskToken(value);

        if (lowerName.Contains("idcard") || lowerName.Contains("id_card") || lowerName.Contains("idnumber"))
            return MaskIdCard(value);

        if (lowerName.Contains("phone") || lowerName.Contains("mobile") || lowerName.Contains("tel"))
            return MaskPhone(value);

        if (lowerName.Contains("bank") || lowerName.Contains("card"))
            return MaskBankCard(value);

        if (lowerName.Contains("email") || lowerName.Contains("mail"))
            return MaskEmail(value);

        if (lowerName.Contains("apikey") || lowerName.Contains("api_key") || lowerName.Contains("secret"))
            return MaskApiKey(value);

        return value;
    }

    /// <summary>
    /// 对键值对集合进行脱敏，生成安全的日志字符串
    /// </summary>
    public static string SafeJoinForLog(Dictionary<string, string?> fields)
    {
        var parts = fields.Select(kv => $"{kv.Key}={MaskSensitive(kv.Key, kv.Value)}");
        return string.Join(", ", parts);
    }
}
