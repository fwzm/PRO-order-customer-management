using System.Globalization;

namespace PRO.Infrastructure.Common;

/// <summary>
/// 数据脱敏工具
/// </summary>
public static class DataMasking
{
    /// <summary>
    /// 手机号脱敏：138****8000
    /// </summary>
    public static string MaskPhone(string? phone)
    {
        if (string.IsNullOrEmpty(phone) || phone.Length < 7)
            return phone ?? "";

        return $"{phone[..3]}****{phone[^4..]}";
    }

    /// <summary>
    /// 身份证脱敏：110***********1234
    /// </summary>
    public static string MaskIdCard(string? idCard)
    {
        if (string.IsNullOrEmpty(idCard) || idCard.Length < 8)
            return idCard ?? "";

        return $"{idCard[..3]}***********{idCard[^4..]}";
    }

    /// <summary>
    /// 银行卡脱敏：6222 **** **** 1234
    /// </summary>
    public static string MaskBankCard(string? cardNo)
    {
        if (string.IsNullOrEmpty(cardNo) || cardNo.Length < 8)
            return cardNo ?? "";

        return $"{cardNo[..4]} **** **** {cardNo[^4..]}";
    }

    /// <summary>
    /// 姓名脱敏：张*、张*三
    /// </summary>
    public static string MaskName(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return "";

        if (name.Length == 1)
            return "*";

        if (name.Length == 2)
            return $"{name[0]}*";

        return $"{name[0]}*{name[^1]}";
    }

    /// <summary>
    /// 邮箱脱敏：z***@example.com
    /// </summary>
    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrEmpty(email) || !email.Contains('@'))
            return email ?? "";

        var atIndex = email.IndexOf('@');
        if (atIndex <= 1)
            return email;

        return $"{email[0]}***{email[atIndex..]}";
    }

    /// <summary>
    /// 地址脱敏：保留省市区，隐藏详细地址
    /// </summary>
    public static string MaskAddress(string? address)
    {
        if (string.IsNullOrEmpty(address))
            return "";

        // 尝试保留前6个字符（省市区）
        if (address.Length >= 6)
            return $"{address[..6]}***";

        return address;
    }

    /// <summary>
    /// 通用脱敏：保留前后各n个字符
    /// </summary>
    public static string MaskGeneric(string? value, int prefixLen = 3, int suffixLen = 3)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        var totalVisible = prefixLen + suffixLen;
        if (value.Length <= totalVisible)
            return value;

        var maskLen = value.Length - totalVisible;
        var mask = new string('*', Math.Min(maskLen, 10));

        return $"{value[..prefixLen]}{mask}{value[^suffixLen..]}";
    }
}

/// <summary>
/// 脱敏属性标记 - 用于DTO属性标记需要脱敏的字段
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class SensitiveDataAttribute : Attribute
{
    public SensitiveDataType DataType { get; }

    public SensitiveDataAttribute(SensitiveDataType dataType = SensitiveDataType.Generic)
    {
        DataType = dataType;
    }
}

public enum SensitiveDataType
{
    Phone,
    IdCard,
    BankCard,
    Name,
    Email,
    Address,
    Generic
}
