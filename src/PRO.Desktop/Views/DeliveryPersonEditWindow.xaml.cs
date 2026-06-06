using System.Windows;

namespace PRO.Desktop.Views;

public partial class DeliveryPersonEditWindow : Window
{
    public DeliveryPersonEditWindow(object viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
