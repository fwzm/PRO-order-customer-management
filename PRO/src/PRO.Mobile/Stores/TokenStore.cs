using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PRO.Mobile.Stores;

/// <summary>
/// Token 状态存储 — 内存中保存当前 Token，支持持久化到 SecureStorage
/// </summary>
public class TokenStore : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? Unauthorized;

    private string? _currentToken;
    public string? CurrentToken
    {
        get => _currentToken;
        private set { _currentToken = value; OnPropertyChanged(); }
    }

    public bool HasToken => !string.IsNullOrEmpty(CurrentToken);

    public void SetToken(string token)
    {
        CurrentToken = token;
    }

    public void ClearToken()
    {
        CurrentToken = null;
    }

    public void NotifyUnauthorized()
    {
        ClearToken();
        Unauthorized?.Invoke();
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
