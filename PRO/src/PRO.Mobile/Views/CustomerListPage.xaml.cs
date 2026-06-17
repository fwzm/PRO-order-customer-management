using PRO.Mobile.ViewModels;

namespace PRO.Mobile.Views;

public partial class CustomerListPage : ContentPage
{
    public CustomerListPage(CustomerListViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is CustomerListViewModel vm && vm.Customers.Count == 0)
            vm.LoadDataCommand.Execute(null);
    }
}
