using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using PRO.Mobile.Services;
using PRO.Mobile.Stores;
using PRO.Mobile.ViewModels;
using PRO.Mobile.Views;

namespace PRO.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // ─── HttpClient ────────────────────────────────
        builder.Services.AddHttpClient("PRO.WebApi", client =>
        {
            client.BaseAddress = new Uri(AppConfig.ApiBaseUrl);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // ─── Services (移动端专属) ──────────────────────
        builder.Services.AddSingleton<IConnectivityService, ConnectivityService>();
        builder.Services.AddSingleton<ISecureTokenStore, SecureTokenStore>();
        builder.Services.AddSingleton<IMobileToastService, MobileToastService>();
        builder.Services.AddSingleton<IApiClient, ApiClient>();
        builder.Services.AddSingleton<IAuthService, AuthServiceProxy>();
        builder.Services.AddSingleton<IDashboardMobileService, DashboardMobileService>();
        builder.Services.AddSingleton<IHealthCheckService, HealthCheckService>();
        builder.Services.AddSingleton<ICustomerMobileService, CustomerMobileService>();
        builder.Services.AddSingleton<IOrderMobileService, OrderMobileService>();
        builder.Services.AddSingleton<IProductMobileService, ProductMobileService>();
        builder.Services.AddSingleton<IBranchMobileService, BranchMobileService>();

        // ─── Stores (状态管理) ─────────────────────────
        builder.Services.AddSingleton<TokenStore>();
        builder.Services.AddSingleton<UserStore>();

        // ─── ViewModels ────────────────────────────────
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<WorkbenchViewModel>();
        builder.Services.AddTransient<CustomerListViewModel>();
        builder.Services.AddTransient<CustomerDetailViewModel>();
        builder.Services.AddTransient<OrderListViewModel>();
        builder.Services.AddTransient<OrderDetailViewModel>();
        builder.Services.AddTransient<OrderNewViewModel>();
        builder.Services.AddTransient<ProfileViewModel>();

        // ─── Views ─────────────────────────────────────
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<WorkbenchPage>();
        builder.Services.AddTransient<CustomerListPage>();
        builder.Services.AddTransient<CustomerDetailPage>();
        builder.Services.AddTransient<OrderListPage>();
        builder.Services.AddTransient<OrderDetailPage>();
        builder.Services.AddTransient<OrderNewPage>();
        builder.Services.AddTransient<ProfilePage>();

        // ─── Shell ────────────────────────────────────
        builder.Services.AddTransient<AppShell>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
