using PRO.Mobile.ViewModels;

namespace PRO.Mobile.Views;

public partial class OrderListPage : ContentPage
{
    public OrderListPage(OrderListViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is OrderListViewModel vm && vm.Orders.Count == 0)
            vm.LoadDataCommand.Execute(null);
    }
}
