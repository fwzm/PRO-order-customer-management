using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRO.Mobile.Services;
using PRO.Mobile.Stores;

namespace PRO.Mobile.ViewModels;

/// <summary>
/// 个人信息 ViewModel — 展示用户信息、系统状态、登出/修改密码
/// </summary>
public partial class ProfileViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IHealthCheckService _healthCheck;
    private readonly IMobileToastService _toast;
    private readonly UserStore _userStore;

    public ProfileViewModel(
        IAuthService authService,
        IHealthCheckService healthCheck,
        IMobileToastService toast,
        UserStore userStore)
    {
        _authService = authService;
        _healthCheck = healthCheck;
        _toast = toast;
        _userStore = userStore;

        // 从 UserStore 初始化用户信息
        RefreshUserInfo();

        // 订阅用户变更事件
        _userStore.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(UserStore.UserName)
                or nameof(UserStore.BranchName)
                or nameof(UserStore.RoleName))
            {
                MainThread.BeginInvokeOnMainThread(RefreshUserInfo);
            }
        };
    }

    [ObservableProperty] private string _userName = "";
    [ObservableProperty] private string _employeeNo = "";
    [ObservableProperty] private string _branchName = "";
    [ObservableProperty] private string _roleName = "";
    [ObservableProperty] private string _apiStatus = "未检测";
    [ObservableProperty] private string _apiStatusColor = "#909399";
    [ObservableProperty] private string _oldPassword = "";
    [ObservableProperty] private string _newPassword = "";
    [ObservableProperty] private string _confirmPassword = "";
    [ObservableProperty] private bool _isCheckingHealth;
    [ObservableProperty] private bool _isChangingPassword;

    private void RefreshUserInfo()
    {
        UserName = _userStore.UserName ?? "未知";
        EmployeeNo = _userStore.CurrentUser?.EmployeeNo ?? "";
        BranchName = _userStore.BranchName ?? "未知";
        RoleName = _userStore.RoleName ?? "未知";
    }

    [RelayCommand]
    private async Task CheckApiHealthAsync()
    {
        if (IsCheckingHealth) return;
        IsCheckingHealth = true;
        try
        {
            var result = await _healthCheck.CheckAsync();
            if (result.Success && result.Data != null)
            {
                var data = result.Data;
                if (data.Status?.ToLower() == "healthy" || data.Database?.ToLower() == "connected")
                {
                    ApiStatus = "服务正常";
                    ApiStatusColor = "#67C23A";
                    await _toast.ShowSuccessAsync("服务器连接正常");
                }
                else
                {
                    ApiStatus = "服务异常";
                    ApiStatusColor = "#E6A23C";
                    await _toast.ShowAsync($"状态: {data.Status}, 数据库: {data.Database}", ToastSeverity.Warning);
                }
            }
            else
            {
                ApiStatus = "连接失败";
                ApiStatusColor = "#F56C6C";
                await _toast.ShowErrorAsync(result.Message);
            }
        }
        catch (Exception ex)
        {
            ApiStatus = "网络异常";
            ApiStatusColor = "#F56C6C";
            await _toast.ShowErrorAsync($"网络检测失败: {ex.Message}");
        }
        finally
        {
            IsCheckingHealth = false;
        }
    }

    [RelayCommand]
    private async Task ChangePasswordAsync()
    {
        if (IsChangingPassword) return;

        if (string.IsNullOrWhiteSpace(OldPassword))
        {
            await _toast.ShowAsync("请输入原密码", ToastSeverity.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 6)
        {
            await _toast.ShowAsync("新密码至少 6 位", ToastSeverity.Warning);
            return;
        }
        if (NewPassword != ConfirmPassword)
        {
            await _toast.ShowAsync("两次输入的新密码不一致", ToastSeverity.Warning);
            return;
        }

        var confirmed = await Shell.Current.DisplayAlert("修改密码", "确定要修改登录密码吗？", "确定", "取消");
        if (!confirmed) return;

        IsChangingPassword = true;
        try
        {
            var result = await _authService.ChangePasswordAsync(OldPassword, NewPassword);
            if (result.Success)
            {
                OldPassword = "";
                NewPassword = "";
                ConfirmPassword = "";
                await _toast.ShowSuccessAsync("密码修改成功");
            }
            else
            {
                await _toast.ShowErrorAsync(result.Message);
            }
        }
        catch (Exception ex)
        {
            await _toast.ShowErrorAsync($"密码修改失败: {ex.Message}");
        }
        finally
        {
            IsChangingPassword = false;
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        var confirmed = await Shell.Current.DisplayAlert("退出登录", "确定要退出登录吗？", "确定", "取消");
        if (!confirmed) return;

        await _authService.LogoutAsync();
        await Shell.Current.GoToAsync("//login");
    }
}
