using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace PRO.Desktop.Converters;

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
