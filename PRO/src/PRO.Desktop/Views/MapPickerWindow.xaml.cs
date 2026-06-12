using System.Windows;
using System.Windows.Input;
using PRO.Desktop.ViewModels;
using PRO.Desktop.Services;
using PRO.Infrastructure.Services;

namespace PRO.Desktop.Views;

public partial class MapPickerWindow : Window
{
    private readonly MapPickerViewModel _viewModel;

    public MapPickerWindow(MapPickerViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.LocationSelected += OnLocationSelected;
    }

    private void OnLocationSelected(MapPoiItem item)
    {
        DialogResult = true;
        Close();
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _viewModel.SearchCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
