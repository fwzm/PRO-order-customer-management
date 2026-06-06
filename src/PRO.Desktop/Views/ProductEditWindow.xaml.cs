using System.Windows;

namespace PRO.Desktop.Views;

public partial class ProductEditWindow : Window
{
    public ProductEditWindow(object viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
