using PRO.Mobile.ViewModels;

namespace PRO.Mobile.Views;

public partial class WorkbenchPage : ContentPage
{
    public WorkbenchPage(WorkbenchViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is WorkbenchViewModel vm)
            vm.LoadDataCommand.Execute(null);
    }
}
