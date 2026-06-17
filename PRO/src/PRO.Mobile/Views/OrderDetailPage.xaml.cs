using PRO.Mobile.ViewModels;

namespace PRO.Mobile.Views;

public partial class OrderDetailPage : ContentPage
{
    public OrderDetailPage(OrderDetailViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
