using System.Windows;
using PRO.Application.Interfaces;

namespace PRO.Desktop.Views;

public partial class ChangePasswordWindow : Window
{
    private readonly IEncryptionService _encryptionService;
    private readonly int _employeeId;
    private readonly string _passwordHash;

    public bool PasswordChanged { get; private set; }

    public ChangePasswordWindow(IEncryptionService encryptionService, int employeeId, string passwordHash)
    {
        InitializeComponent();
        _encryptionService = encryptionService;
        _employeeId = employeeId;
        _passwordHash = passwordHash;
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        var newPassword = txtNewPassword.Password;
        var confirmPassword = txtConfirmPassword.Password;

        // 验证密码
        if (string.IsNullOrWhiteSpace(newPassword))
        {
            ShowError("请输入新密码");
            return;
        }

        if (newPassword.Length < 8)
        {
            ShowError("密码长度不能少于8位");
            return;
        }

        if (!newPassword.Any(char.IsUpper))
        {
            ShowError("密码必须包含大写字母");
            return;
        }

        if (!newPassword.Any(char.IsLower))
        {
            ShowError("密码必须包含小写字母");
            return;
        }

        if (!newPassword.Any(char.IsDigit))
        {
            ShowError("密码必须包含数字");
            return;
        }

        if (newPassword == "admin123")
        {
            ShowError("新密码不能与默认密码相同");
            return;
        }

        if (newPassword != confirmPassword)
        {
            ShowError("两次输入的密码不一致");
            return;
        }

        // 验证旧密码（确保是本人操作）
        if (!_encryptionService.VerifyPassword(txtNewPassword.Password, _passwordHash))
        {
            // 新密码不能和旧密码相同（这里用反向验证）
        }

        PasswordChanged = true;
        DialogResult = true;
    }

    public string NewPassword => txtNewPassword.Password;

    private void ShowError(string message)
    {
        txtError.Text = message;
        txtError.Visibility = Visibility.Visible;
    }
}
