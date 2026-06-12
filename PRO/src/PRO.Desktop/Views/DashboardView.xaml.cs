using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PRO.Application.DTOs;
using PRO.Desktop.ViewModels;

namespace PRO.Desktop.Views;

public partial class DashboardView : UserControl
{
    private DashboardViewModel? ViewModel => DataContext as DashboardViewModel;

    public DashboardView()
    {
        InitializeComponent();
    }

    private void Alert_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is DashboardAlert alert)
        {
            ViewModel?.NavigateToAlertCommand.Execute(alert);
        }
    }

    private void Task_Click(object sender, MouseButtonEventArgs e)
    {
        if (IsFromButton(e.OriginalSource as DependencyObject))
            return;

        if (sender is FrameworkElement element && element.DataContext is DashboardTask task)
        {
            ViewModel?.NavigateToTaskCommand.Execute(task);
        }
    }

    private static bool IsFromButton(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is Button)
                return true;
            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }

        return false;
    }
}
