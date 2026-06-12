using System.Windows.Controls;
using PRO.Desktop.ViewModels;

namespace PRO.Desktop.Views;

public partial class HeadquartersAdminView : UserControl
{
    public HeadquartersAdminView()
    {
        InitializeComponent();
    }

    private void SyncInterval_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem item &&
            item.Tag is string tag && int.TryParse(tag, out var minutes) &&
            DataContext is HeadquartersAdminViewModel vm && vm.SyncConfig != null)
        {
            vm.SyncConfig.SyncIntervalMinutes = minutes;
        }
    }
}
