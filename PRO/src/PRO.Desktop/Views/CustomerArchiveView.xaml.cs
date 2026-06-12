using System.Windows.Controls;
using PRO.Desktop.ViewModels;

namespace PRO.Desktop.Views;

public partial class CustomerArchiveView : UserControl
{
    public CustomerArchiveView()
    {
        InitializeComponent();
    }

    public CustomerArchiveView(CustomerArchiveViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
