using PRO.Mobile.Stores;
using PRO.Mobile.Views;

namespace PRO.Mobile;

public partial class AppShell : Shell
{
    private readonly TokenStore _tokenStore;

    public AppShell(TokenStore tokenStore)
    {
        InitializeComponent();
        _tokenStore = tokenStore;

        // 监听 401 事件 → 跳转登录页
        _tokenStore.Unauthorized += OnUnauthorized;

        // 注册导航路由
        Routing.RegisterRoute("customerDetail", typeof(CustomerDetailPage));
        Routing.RegisterRoute("orderDetail", typeof(OrderDetailPage));
        Routing.RegisterRoute("orderNew", typeof(OrderNewPage));
    }

    private async void OnUnauthorized()
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await Shell.Current.GoToAsync("//login");
        });
    }
}
