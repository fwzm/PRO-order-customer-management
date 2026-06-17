using PRO.Mobile.ViewModels;

namespace PRO.Mobile.Views;

public partial class OrderNewPage : ContentPage
{
    public OrderNewPage(OrderNewViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is OrderNewViewModel vm)
            vm.Initialize();
    }
}
