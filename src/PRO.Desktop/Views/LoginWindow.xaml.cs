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
}
