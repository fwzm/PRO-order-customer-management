using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PRO.Desktop.ViewModels;

namespace PRO.Desktop.Views;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        _viewModel.MustChangePassword += ShowChangePasswordPanel;
        txtEmployeeNo.Focus();
        MouseDown += (s, e) => { if (e.ChangedButton == System.Windows.Input.MouseButton.Left) DragMove(); };
    }

    private void txtPassword_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox)
        {
            _viewModel.Password = passwordBox.Password;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void txtEmployeeNo_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            txtPassword.Focus();
    }

    private void txtPassword_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _viewModel.LoginCommand.CanExecute(null))
            _viewModel.LoginCommand.Execute(null);
    }

    private void ShowChangePasswordPanel()
    {
        Dispatcher.Invoke(() =>
        {
            _viewModel.LoginError = null;
            LoginPanel.Visibility = Visibility.Collapsed;
            ChangePasswordPanel.Visibility = Visibility.Visible;
            txtNewPassword.Clear();
            txtConfirmPassword.Clear();
            txtNewPassword.Focus();
        });
    }

    private void ChangePasswordButton_Click(object sender, RoutedEventArgs e)
    {
        TryChangePassword();
    }

    private void txtNewPassword_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            txtConfirmPassword.Focus();
    }

    private void txtConfirmPassword_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            TryChangePassword();
    }

    private void TryChangePassword()
    {
        if (txtNewPassword.Password != txtConfirmPassword.Password)
        {
            _viewModel.LoginError = "两次输入的新密码不一致";
            return;
        }

        if (_viewModel.ChangePasswordOnLoginCommand.CanExecute(txtNewPassword.Password))
            _viewModel.ChangePasswordOnLoginCommand.Execute(txtNewPassword.Password);
    }
}
