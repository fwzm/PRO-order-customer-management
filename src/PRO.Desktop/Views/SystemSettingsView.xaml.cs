using System.Windows.Controls;
using PRO.Desktop.ViewModels;

namespace PRO.Desktop.Views;

public partial class SystemSettingsView : UserControl
{
    public SystemSettingsView()
    {
        InitializeComponent();
    }

    private void SyncInterval_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem item &&
            item.Tag is string tag && int.TryParse(tag, out var minutes) &&
            DataContext is SystemSettingsViewModel vm)
        {
            vm.SyncIntervalMinutes = minutes;
        }
    }
}
