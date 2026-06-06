using System.Windows.Controls;
using PRO.Desktop.ViewModels;

namespace PRO.Desktop.Views;

public partial class WeChatCustomerView : UserControl
{
    private WeChatCustomerViewModel? _viewModel;

    public WeChatCustomerView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_viewModel == null && DataContext is WeChatCustomerViewModel vm)
        {
            _viewModel = vm;
            await vm.InitAsync();
            await vm.SimulateMockDataAsync();
        }
    }
}
