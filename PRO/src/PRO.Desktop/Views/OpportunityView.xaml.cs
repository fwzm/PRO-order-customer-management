using System.Windows.Controls;
using System.Windows.Input;
using PRO.Desktop.ViewModels;

namespace PRO.Desktop.Views;

public partial class OpportunityView : UserControl
{
    public OpportunityView()
    {
        InitializeComponent();
    }

    private void Card_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is OpportunityViewModel vm && sender is System.Windows.FrameworkElement fe && fe.DataContext is OpportunityItem item)
        {
            vm.SelectedOpportunity = item;
            vm.EditOpportunityCommand.Execute(item);
        }
    }
}
