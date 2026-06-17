using System.Windows;
using PRO.Desktop.ViewModels;

namespace PRO.Desktop.Views;

public partial class ProductEditWindow : Window
{
    private readonly ProductEditViewModel _viewModel;

    public ProductEditWindow(object viewModel)
    {
        InitializeComponent();
        _viewModel = (ProductEditViewModel)viewModel;
        DataContext = viewModel;
        Closing += OnClosing;
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_viewModel.IsDirty && DialogResult != true)
        {
            var result = MessageBox.Show(
                "有未保存的修改，是否放弃？",
                "确认关闭",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Cancel)
                e.Cancel = true;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
