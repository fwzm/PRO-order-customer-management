using PRO.Mobile.ViewModels;

namespace PRO.Mobile.Views;

public partial class CustomerDetailPage : ContentPage
{
    public CustomerDetailPage(CustomerDetailViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
