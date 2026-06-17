using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRO.Application.DTOs;
using PRO.Mobile.Services;

namespace PRO.Mobile.ViewModels;

/// <summary>
/// 登录页 ViewModel
/// </summary>
public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IMobileToastService _toast;
    private readonly IConnectivityService _connectivity;

    public LoginViewModel(IAuthService authService, IMobileToastService toast, IConnectivityService connectivity)
    {
        _authService = authService;
        _toast = toast;
        _connectivity = connectivity;
    }

    [ObservableProperty] private string _employeeNo = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (!_connectivity.IsConnected)
        {
            ErrorMessage = "网络不可用，请检查连接";
            await _toast.ShowErrorAsync("网络不可用");
            return;
        }

        if (string.IsNullOrWhiteSpace(EmployeeNo) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "请输入工号和密码";
            return;
        }

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var result = await _authService.LoginAsync(EmployeeNo.Trim(), Password);
            if (result.Success)
            {
                await Shell.Current.GoToAsync("//main/workbench");
            }
            else
            {
                ErrorMessage = result.Message;
                await _toast.ShowErrorAsync(result.Message);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "登录失败，请重试";
            await _toast.ShowErrorAsync(ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }
}
