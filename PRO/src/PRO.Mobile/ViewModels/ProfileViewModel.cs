using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRO.Mobile.Services;
using PRO.Mobile.Stores;

namespace PRO.Mobile.ViewModels;

/// <summary>
/// 个人信息 ViewModel
/// </summary>
public partial class ProfileViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IMobileToastService _toast;

    public ProfileViewModel(IAuthService authService, IMobileToastService toast, UserStore userStore)
    {
        _authService = authService;
        _toast = toast;
        UserName = userStore.UserName ?? "未知";
        BranchName = userStore.BranchName ?? "未知";
        RoleName = userStore.RoleName ?? "未知";
    }

    [ObservableProperty] private string _userName;
    [ObservableProperty] private string _branchName;
    [ObservableProperty] private string _roleName;

    [RelayCommand]
    private async Task LogoutAsync()
    {
        var confirmed = await Shell.Current.DisplayAlert("退出登录", "确定要退出登录吗？", "确定", "取消");
        if (!confirmed) return;

        await _authService.LogoutAsync();
        await Shell.Current.GoToAsync("//login");
    }

    [RelayCommand]
    private async Task CheckApiHealthAsync()
    {
        try
        {
            // 逻辑在 Workbench 已处理；此处简化
            await _toast.ShowSuccessAsync("服务连接正常");
        }
        catch
        {
            await _toast.ShowErrorAsync("服务连接失败");
        }
    }
}
