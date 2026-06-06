using System.Windows;
using PRO.Desktop.ViewModels;

namespace PRO.Desktop.Views;

public partial class EmployeeListWindow : Window
{
    private readonly EmployeeListViewModel _viewModel;

    public EmployeeListWindow(EmployeeListViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
