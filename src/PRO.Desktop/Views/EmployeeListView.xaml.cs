using System.Windows;
using System.Windows.Controls;
using PRO.Desktop.ViewModels;

namespace PRO.Desktop.Views;

public partial class EmployeeListView : UserControl
{
    public EmployeeListView()
    {
        InitializeComponent();
    }

    private void OpenEmployeeWindow_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is EmployeeListViewModel vm)
        {
            var window = new EmployeeListWindow(vm);
            window.Owner = System.Windows.Application.Current.MainWindow;
            window.ShowDialog();
        }
    }
}
