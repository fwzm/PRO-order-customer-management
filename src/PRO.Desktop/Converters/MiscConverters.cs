using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace PRO.Desktop.Converters;

/// <summary>
/// 展开状态 → 旋转角度 (false=0°, true=90°)
/// </summary>
public class ExpandedToAngleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isExpanded)
            return isExpanded ? 90.0 : 0.0;
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

/// <summary>
/// Badge 文本：超过 99 显示 "99+"
/// </summary>
public class BadgeTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count)
            return count > 99 ? "99+" : count.ToString();
        return "0";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public class CloseBehaviorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int closeBehavior && parameter is string paramStr && int.TryParse(paramStr, out int param))
        {
            return closeBehavior == param;
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter is string paramStr && int.TryParse(paramStr, out int param))
        {
            return param;
        }
        return Binding.DoNothing;
    }
}

public class PasswordHintConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isEdit)
            return isEdit ? "留空则不修改密码" : "留空则默认使用工号";
        return "留空则默认使用工号";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

/// <summary>
/// 将颜色十六进制字符串(#RRGGBB)转换为 SolidColorBrush
/// </summary>
public class ColorStringToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string colorStr && !string.IsNullOrEmpty(colorStr))
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(colorStr);
                return new SolidColorBrush(color);
            }
            catch
            {
                return new SolidColorBrush(Colors.Gray);
            }
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

/// <summary>
/// 导航菜单图标转换器 - 将导航ID映射为MaterialDesign图标
/// </summary>
public class NavIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string id)
        {
            return id switch
            {
                "dashboard" => MaterialDesignThemes.Wpf.PackIconKind.ViewDashboard,
                "customer" => MaterialDesignThemes.Wpf.PackIconKind.AccountGroup,
                "customer_add" => MaterialDesignThemes.Wpf.PackIconKind.AccountPlus,
                "business_district" => MaterialDesignThemes.Wpf.PackIconKind.MapMarkerRadius,
                "wechat_scrm" => MaterialDesignThemes.Wpf.PackIconKind.Wechat,
                "wechat_visit_sync" => MaterialDesignThemes.Wpf.PackIconKind.Sync,
                "opportunity" => MaterialDesignThemes.Wpf.PackIconKind.TrendingUp,
                "tags" => MaterialDesignThemes.Wpf.PackIconKind.TagText,
                "prediction" => MaterialDesignThemes.Wpf.PackIconKind.ChartBellCurve,
                "order" => MaterialDesignThemes.Wpf.PackIconKind.ClipboardText,
                "product" => MaterialDesignThemes.Wpf.PackIconKind.PackageVariantClosed,
                "logistics" => MaterialDesignThemes.Wpf.PackIconKind.TruckDelivery,
                "inventory" => MaterialDesignThemes.Wpf.PackIconKind.ClipboardList,
                "settlement" => MaterialDesignThemes.Wpf.PackIconKind.CurrencyCny,
                "workplan" => MaterialDesignThemes.Wpf.PackIconKind.CalendarCheck,
                "ar" => MaterialDesignThemes.Wpf.PackIconKind.CashMultiple,
                "reports" => MaterialDesignThemes.Wpf.PackIconKind.ChartBar,
                "org_structure" => MaterialDesignThemes.Wpf.PackIconKind.AccountGroupOutline,
                "system" => MaterialDesignThemes.Wpf.PackIconKind.Cog,
                "system_advanced" => MaterialDesignThemes.Wpf.PackIconKind.Tune,
                "visit_opportunity" => MaterialDesignThemes.Wpf.PackIconKind.Handshake,
                "district_tag" => MaterialDesignThemes.Wpf.PackIconKind.MapMarkerDistance,
                _ => MaterialDesignThemes.Wpf.PackIconKind.CircleSmall
            };
        }
        return MaterialDesignThemes.Wpf.PackIconKind.CircleSmall;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
