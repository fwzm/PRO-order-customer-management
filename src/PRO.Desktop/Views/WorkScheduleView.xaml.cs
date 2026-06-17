using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;
using PRO.Desktop.ViewModels;
using Serilog;

namespace PRO.Desktop.Views;

public partial class WorkScheduleView : UserControl
{
    public WorkScheduleView()
    {
        InitializeComponent();
    }

    private async void DayBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (sender is not FrameworkElement element ||
                element.DataContext is not CalendarScheduleItem item ||
                DataContext is not WorkScheduleViewModel vm)
            {
                return;
            }

            if (vm.LoadDailyPlanCommand.CanExecute(item.Date))
            {
                await vm.LoadDailyPlanCommand.ExecuteAsync(item.Date);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "工作日历点击处理失败");
        }
    }
}
