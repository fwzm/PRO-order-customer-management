using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRO.Mobile.Services;

namespace PRO.Mobile.ViewModels;

/// <summary>
/// 登录 ViewModel — 工号/密码登录，支持防重复提交
/// </summary>
public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IConnectivityService _connectivity;
    private readonly IMobileToastService _toast;

    public LoginViewModel(IAuthService authService, IConnectivityService connectivity, IMobileToastService toast)
    {
        _authService = authService;
        _connectivity = connectivity;
        _toast = toast;
    }

    [ObservableProperty] private string _employeeNo = "";
    [ObservableProperty] private string _password = "";
    [ObservableProperty] private string _errorMessage = "";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _rememberPassword;

    [RelayCommand]
    private async Task LoginAsync()
    {
        // 防重复点击
        if (IsLoading) return;
        ErrorMessage = "";

        // 输入校验
        if (string.IsNullOrWhiteSpace(EmployeeNo))
        {
            ErrorMessage = "请输入工号";
            return;
        }
        if (string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "请输入密码";
            return;
        }

        if (!_connectivity.IsConnected)
        {
            ErrorMessage = "网络不可用，请检查网络连接";
            return;
        }

        IsLoading = true;
        try
        {
            var result = await _authService.LoginAsync(EmployeeNo.Trim(), Password);

            if (result.Success)
            {
                await _toast.ShowSuccessAsync("登录成功");
                await Shell.Current.GoToAsync("//main/workbench");
            }
            else
            {
                ErrorMessage = result.Message ?? "登录失败";
                await _toast.ShowErrorAsync(result.Message ?? "登录失败");
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"登录异常: {ex.Message}";
            await _toast.ShowErrorAsync(ErrorMessage);
        }
        finally
        {
            IsLoading = false;
        }
    }
}
