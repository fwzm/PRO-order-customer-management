using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;
using PRO.Desktop.ViewModels;

namespace PRO.Desktop.Views;

public partial class WorkScheduleView : UserControl
{
    public WorkScheduleView()
    {
        InitializeComponent();
    }

    private async void DayBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
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
}
