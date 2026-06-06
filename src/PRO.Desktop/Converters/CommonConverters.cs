using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace PRO.Desktop.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility visibility && visibility == Visibility.Visible;
    }
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return !boolValue;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return !boolValue;
        return false;
    }
}

/// <summary>
/// null → false, 非null → true
/// </summary>
public class NullToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value != null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

/// <summary>
/// Badge 可见性：count > 0 时显示
/// </summary>
public class BadgeVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count)
            return count > 0 ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
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

/// <summary>
/// 搜索匹配可见性：SearchScore > 0 时可见
/// </summary>
public class SearchMatchVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int score)
            return score > 0 ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

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
/// bool → "▼"(展开) / "▶"(折叠)
/// </summary>
public class BoolToArrowConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isExpanded)
            return isExpanded ? "▼" : "▶";
        return "▶";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

/// <summary>
/// bool → ChevronDown(展开) / ChevronRight(折叠) MaterialDesign 图标
/// </summary>
public class BoolToChevronConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isExpanded)
            return isExpanded
                ? MaterialDesignThemes.Wpf.PackIconKind.ChevronDown
                : MaterialDesignThemes.Wpf.PackIconKind.ChevronRight;
        return MaterialDesignThemes.Wpf.PackIconKind.ChevronRight;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isVisible = value != null && !string.IsNullOrWhiteSpace(value.ToString());
        var inverse = string.Equals(parameter?.ToString(), "Inverse", StringComparison.OrdinalIgnoreCase);

        if (inverse)
        {
            isVisible = !isVisible;
        }

        return isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public class BoolToLoginTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isLoggingIn)
            return isLoggingIn ? "登录中..." : "登录";
        return "登录";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public class NavVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string id)
        {
            if (id.StartsWith("sep"))
                return Visibility.Collapsed;
            return Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

/// <summary>
/// 分类标题可见性：ID以cat_开头时显示
/// </summary>
public class CategoryHeaderVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string id && id.StartsWith("cat_"))
            return Visibility.Visible;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>
/// 菜单项可见性：分类标题和分隔符隐藏，其余显示
/// </summary>
public class CategoryItemVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string id)
        {
            if (id.StartsWith("cat_") || id.StartsWith("sep"))
                return Visibility.Collapsed;
            return Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}


public class OrderStatusColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Domain.Enums.OrderStatus status)
        {
            return status switch
            {
                Domain.Enums.OrderStatus.Pending => new SolidColorBrush(Color.FromRgb(243, 156, 18)),    // 橙色
                Domain.Enums.OrderStatus.Assigned => new SolidColorBrush(Color.FromRgb(52, 152, 219)), // 蓝色
                Domain.Enums.OrderStatus.Delivering => new SolidColorBrush(Color.FromRgb(155, 89, 182)),// 紫色
                Domain.Enums.OrderStatus.Completed => new SolidColorBrush(Color.FromRgb(39, 174, 96)),  // 绿色
                Domain.Enums.OrderStatus.Failed => new SolidColorBrush(Color.FromRgb(231, 76, 60)),    // 红色
                Domain.Enums.OrderStatus.Cancelled => new SolidColorBrush(Color.FromRgb(149, 165, 166)),// 灰色
                _ => new SolidColorBrush(Color.FromRgb(149, 165, 166))
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public class PaymentStatusColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Domain.Enums.PaymentStatus status)
        {
            return status switch
            {
                Domain.Enums.PaymentStatus.Paid => new SolidColorBrush(Color.FromRgb(39, 174, 96)),     // 绿色
                Domain.Enums.PaymentStatus.Unpaid => new SolidColorBrush(Color.FromRgb(243, 156, 18)), // 橙色
                Domain.Enums.PaymentStatus.Legal => new SolidColorBrush(Color.FromRgb(231, 76, 60)),  // 红色
                _ => new SolidColorBrush(Color.FromRgb(149, 165, 166))
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public class CustomerTypeColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Domain.Enums.CustomerType type)
        {
            return type switch
            {
                Domain.Enums.CustomerType.Major => new SolidColorBrush(Color.FromRgb(30, 58, 95)),    // 深蓝色
                Domain.Enums.CustomerType.Sub => new SolidColorBrush(Color.FromRgb(74, 144, 217)),   // 浅蓝色
                _ => new SolidColorBrush(Color.FromRgb(149, 165, 166))
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public class DeliveryPersonStatusColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Domain.Enums.DeliveryPersonStatus status)
        {
            return status switch
            {
                Domain.Enums.DeliveryPersonStatus.Available => new SolidColorBrush(Color.FromRgb(39, 174, 96)),  // 绿色
                Domain.Enums.DeliveryPersonStatus.Busy => new SolidColorBrush(Color.FromRgb(243, 156, 18)),        // 橙色
                Domain.Enums.DeliveryPersonStatus.Off => new SolidColorBrush(Color.FromRgb(149, 165, 166)),        // 灰色
                _ => new SolidColorBrush(Color.FromRgb(149, 165, 166))
            };
        }
        return new SolidColorBrush(Colors.Gray);
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

public class ZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count)
        {
            return count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public class ProductStatusColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Domain.Enums.ProductStatus status)
        {
            return status switch
            {
                Domain.Enums.ProductStatus.Active => new SolidColorBrush(Color.FromRgb(39, 174, 96)),   // 绿色
                Domain.Enums.ProductStatus.Inactive => new SolidColorBrush(Color.FromRgb(149, 165, 166)), // 灰色
                _ => new SolidColorBrush(Color.FromRgb(149, 165, 166))
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}


public class ProductStatusTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Domain.Enums.ProductStatus status)
        {
            return status switch
            {
                Domain.Enums.ProductStatus.Active => "启用",
                Domain.Enums.ProductStatus.Inactive => "停用",
                _ => value?.ToString() ?? ""
            };
        }
        return value?.ToString() ?? "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string statusStr)
        {
            return statusStr == "启用" ? Domain.Enums.ProductStatus.Active : Domain.Enums.ProductStatus.Inactive;
        }
        return Domain.Enums.ProductStatus.Active;
    }
}

public class EnumBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false;
        
        var enumValue = value.ToString();
        var targetValue = parameter.ToString();
        
        return enumValue?.Equals(targetValue, StringComparison.OrdinalIgnoreCase) ?? false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue && boolValue && parameter != null)
        {
            var paramStr = parameter.ToString();
            if (!string.IsNullOrEmpty(paramStr))
                return Enum.Parse(targetType, paramStr);
        }
        return Binding.DoNothing;
    }
}

public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return boolValue ? Visibility.Collapsed : Visibility.Visible;
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility visibility && visibility == Visibility.Collapsed;
    }
}

public class GreaterThanZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count)
            return count > 0 ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
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

public class BoolToStatusColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isConnected)
        {
            return isConnected 
                ? new SolidColorBrush(Color.FromRgb(39, 174, 96))   // 绿色 - 已连接
                : new SolidColorBrush(Color.FromRgb(149, 165, 166));  // 灰色 - 未连接
        }
        return new SolidColorBrush(Color.FromRgb(149, 165, 166));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public class BoolToEnabledConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isEnabled)
        {
            return isEnabled ? "启用" : "停用";
        }
        return "未知";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

/// <summary>
/// 导航菜单图标转换器 - 将导航ID映射为MaterialDesign图标
/// </summary>
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
/// Enum → Visibility（用于Tab控制）
/// </summary>
public class EnumVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return Visibility.Collapsed;

        var enumValue = value.ToString();
        var targetValue = parameter.ToString();

        return enumValue?.Equals(targetValue, StringComparison.OrdinalIgnoreCase) ?? false
            ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

/// <summary>
/// string.equals → Visibility（用于状态列控制）
/// </summary>
public class StringEqualsVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str && parameter is string target)
        {
            return str.Equals(target, StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public class OrderStatusTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Domain.Enums.OrderStatus status)
        {
            return status switch
            {
                Domain.Enums.OrderStatus.Pending => "待分配",
                Domain.Enums.OrderStatus.Assigned => "已分配",
                Domain.Enums.OrderStatus.Delivering => "配送中",
                Domain.Enums.OrderStatus.Completed => "已完成",
                Domain.Enums.OrderStatus.Failed => "配送失败",
                Domain.Enums.OrderStatus.Cancelled => "已取消",
                Domain.Enums.OrderStatus.Draft => "草稿",
                _ => "未知"
            };
        }
        return "未知";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public class PaymentStatusTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Domain.Enums.PaymentStatus status)
        {
            return status switch
            {
                Domain.Enums.PaymentStatus.Unpaid => "未收款",
                Domain.Enums.PaymentStatus.PartialPaid => "部分收款",
                Domain.Enums.PaymentStatus.Paid => "已收款",
                Domain.Enums.PaymentStatus.Legal => "移交法务",
                _ => "未知"
            };
        }
        return "未知";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public class DeliveryPersonStatusTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Domain.Enums.DeliveryPersonStatus status)
        {
            return status switch
            {
                Domain.Enums.DeliveryPersonStatus.Available => "可用",
                Domain.Enums.DeliveryPersonStatus.Busy => "忙碌",
                Domain.Enums.DeliveryPersonStatus.Off => "休息",
                _ => "未知"
            };
        }
        return "未知";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public class CustomerTypeTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Domain.Enums.CustomerType type)
        {
            return type switch
            {
                Domain.Enums.CustomerType.Major => "大客户",
                Domain.Enums.CustomerType.Sub => "细分客户",
                _ => "未知"
            };
        }
        return "未知";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public class DistrictStatusTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string status)
        {
            return status switch
            {
                "Active" => "启用",
                "Inactive" => "停用",
                _ => status
            };
        }
        return value?.ToString() ?? "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string statusStr)
        {
            return statusStr switch
            {
                "启用" => "Active",
                "停用" => "Inactive",
                _ => statusStr
            };
        }
        return "Active";
    }
}

public class DistrictStatusColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string status)
        {
            return status switch
            {
                "Active" => new SolidColorBrush(Color.FromRgb(39, 174, 96)),
                "Inactive" => new SolidColorBrush(Color.FromRgb(149, 165, 166)),
                _ => new SolidColorBrush(Colors.Gray)
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

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

