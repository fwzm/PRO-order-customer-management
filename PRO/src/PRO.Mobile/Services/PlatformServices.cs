namespace PRO.Mobile.Services;

/// <summary>
/// 网络连接检测（使用 MAUI Connectivity API）
/// </summary>
public class ConnectivityService : IConnectivityService
{
    public bool IsConnected => Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

    public event EventHandler<bool>? ConnectivityChanged;

    public ConnectivityService()
    {
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        var isConnected = e.NetworkAccess == NetworkAccess.Internet;
        ConnectivityChanged?.Invoke(this, isConnected);
    }
}

/// <summary>
/// 安全 Token 存储 — 使用 MAUI SecureStorage (iOS Keychain / Android KeyStore)
/// </summary>
public class SecureTokenStore : ISecureTokenStore
{
    public Task SaveAsync(string key, string value)
    {
        return SecureStorage.Default.SetAsync(key, value);
    }

    public Task<string?> GetAsync(string key)
    {
        return SecureStorage.Default.GetAsync(key);
    }

    public Task RemoveAsync(string key)
    {
        SecureStorage.Default.Remove(key);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Toast 提示服务 — 使用 CommunityToolkit.Maui Toast
/// </summary>
public class MobileToastService : IMobileToastService
{
    public async Task ShowAsync(string message, ToastSeverity severity = ToastSeverity.Info)
    {
        // CommunityToolkit.Maui Toast
        var duration = severity == ToastSeverity.Error ? CommunityToolkit.Maui.Core.ToastDuration.Long
            : CommunityToolkit.Maui.Core.ToastDuration.Short;

        var toast = CommunityToolkit.Maui.Alerts.Toast.Make(message, duration);
        await toast.Show();
    }

    public Task ShowErrorAsync(string message) => ShowAsync(message, ToastSeverity.Error);

    public Task ShowSuccessAsync(string message) => ShowAsync(message, ToastSeverity.Success);
}
