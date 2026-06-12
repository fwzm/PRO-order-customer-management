using System.Windows;
using PRO.Desktop.ViewModels;

namespace PRO.Desktop.Views;

public partial class CustomerDetailWindow : Window
{
    public CustomerDetailWindow(object viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        if (viewModel is CustomerDetailViewModel detailVm)
            detailVm.SetOwningWindow(this);
    }
}
