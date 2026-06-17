using System.ComponentModel;
using System.Runtime.CompilerServices;
using PRO.Application.DTOs;

namespace PRO.Mobile.Stores;

/// <summary>
/// 用户状态存储 — 保存当前登录用户信息
/// </summary>
public class UserStore : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private LoginResponse? _currentUser;
    public LoginResponse? CurrentUser
    {
        get => _currentUser;
        private set { _currentUser = value; OnPropertyChanged(); }
    }

    public bool IsLoggedIn => CurrentUser != null;
    public string? UserName => CurrentUser?.Name;
    public string? BranchName => CurrentUser?.BranchName;
    public string? RoleName => CurrentUser?.RoleName;

    public void SetUser(LoginResponse user)
    {
        CurrentUser = user;
    }

    public void ClearUser()
    {
        CurrentUser = null;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
