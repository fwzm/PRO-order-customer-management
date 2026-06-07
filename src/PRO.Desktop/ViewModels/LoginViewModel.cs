using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRO.Application.Interfaces;
using PRO.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Windows;

namespace PRO.Desktop.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    public event EventHandler? LoginSuccessful;
    /// <summary>触发强制修改密码事件</summary>
    public event Action? MustChangePassword;

    private readonly IEncryptionService _encryptionService;
    private readonly ProDbContext _dbContext;

    private const int MaxLoginFailures = 5;
    private const int LockoutMinutes = 30;
    private const int MinPasswordLength = 8;

    [ObservableProperty]
    private string _employeeNo = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _rememberMe;

    [ObservableProperty]
    private string? _loginError;

    [ObservableProperty]
    private bool _isLoggingIn;

    private int _currentEmployeeId;

    public string VersionText => $"{DateTime.Now:yyMM}01";

    public LoginViewModel(IEncryptionService encryptionService, ProDbContext dbContext)
    {
        _encryptionService = encryptionService;
        _dbContext = dbContext;

        // 异步加载保存的登录信息（不阻塞UI线程）
        _ = LoadRememberedUserAsync();
    }

    private async Task LoadRememberedUserAsync()
    {
        try
        {
            var settings = await _dbContext.LocalSettings
                .FirstOrDefaultAsync(s => s.SettingKey == "RememberedEmployeeNo");
            if (settings != null)
            {
                EmployeeNo = _encryptionService.Decrypt(settings.SettingValue);
                RememberMe = true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载记住工号失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(EmployeeNo))
        {
            LoginError = "请输入工号";
            return;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            LoginError = "请输入密码";
            return;
        }

        LoginError = null;
        IsLoggingIn = true;

        try
        {
            // 查询员工
            var employee = await _dbContext.Employees
                .Include(e => e.Role)
                .Include(e => e.Branch)
                .Include(e => e.Department)
                .FirstOrDefaultAsync(e => e.EmployeeNo == EmployeeNo
                    && e.Status == Domain.Enums.EmployeeStatus.Active);

            if (employee == null)
            {
                LoginError = "工号不存在或已停用";
                return;
            }

            // 检查账号是否被锁定
            if (employee.LockedUntil.HasValue && employee.LockedUntil.Value > DateTime.Now)
            {
                var remainingMinutes = (int)(employee.LockedUntil.Value - DateTime.Now).TotalMinutes;
                LoginError = $"账号已被锁定，请{remainingMinutes}分钟后再试";
                return;
            }

            // 验证密码
            if (!_encryptionService.VerifyPassword(Password, employee.PasswordHash))
            {
                // 累计登录失败次数
                employee.LoginFailCount++;
                if (employee.LoginFailCount >= MaxLoginFailures)
                {
                    employee.LockedUntil = DateTime.Now.AddMinutes(LockoutMinutes);
                    await _dbContext.SaveChangesAsync();
                    LoginError = $"密码错误次数过多，账号已锁定{LockoutMinutes}分钟";
                }
                else
                {
                    await _dbContext.SaveChangesAsync();
                    var remaining = MaxLoginFailures - employee.LoginFailCount;
                    LoginError = $"密码错误，还剩{remaining}次尝试机会";
                }
                return;
            }

            // 密码正确，清除失败计数和锁定状态
            if (employee.LoginFailCount > 0 || employee.LockedUntil.HasValue)
            {
                employee.LoginFailCount = 0;
                employee.LockedUntil = null;
                await _dbContext.SaveChangesAsync();
            }

            _currentEmployeeId = employee.Id;

            // === 强制修改密码检查 ===
            // 条件1：从未修改过密码 (PasswordChangedAt == null)
            // 条件2：当前密码是默认密码 admin123
            if (employee.PasswordChangedAt == null || Password == "admin123")
            {
                // 触发强制修改密码流程，不触发 LoginSuccessful
                MustChangePassword?.Invoke();
                return;
            }

            // 密码过期检查（90天）
            if (employee.PasswordChangedAt.HasValue &&
                (DateTime.Now - employee.PasswordChangedAt.Value).TotalDays > 90)
            {
                var result = MessageBox.Show(
                    "您的密码已超过90天未修改，建议立即修改。\n\n点击“确定”修改密码，点击“取消”稍后修改。",
                    "安全提示", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
                if (result == MessageBoxResult.OK)
                {
                    MustChangePassword?.Invoke();
                    return;
                }
            }

            await CompleteLoginAsync(employee);
        }
        catch (Exception ex)
        {
            LoginError = $"登录失败: {ex.Message}";
        }
        finally
        {
            IsLoggingIn = false;
        }
    }

    /// <summary>
    /// 完成登录流程（创建会话、记录日志）
    /// </summary>
    private async Task CompleteLoginAsync(Domain.Entities.Employee employee)
    {
        // 获取权限列表
        var permissions = await _dbContext.RolePermissions
            .Include(rp => rp.Permission)
            .Where(rp => rp.RoleId == employee.RoleId && rp.IsAllowed && rp.Permission != null)
            .Select(rp => rp.Permission!.Code)
            .ToListAsync();

        // 创建会话
        var session = new UserSession
        {
            EmployeeId = employee.Id,
            EmployeeNo = employee.EmployeeNo,
            Name = employee.Name,
            BranchId = employee.BranchId,
            BranchName = employee.Branch?.Name ?? "",
            DepartmentId = employee.DepartmentId,
            DepartmentName = employee.Department?.Name,
            RoleId = employee.RoleId,
            RoleName = employee.Role?.Name ?? "",
            RoleType = employee.Role?.RoleType ?? Domain.Enums.RoleType.Employee,
            Permissions = permissions,
            LoginTime = DateTime.Now,
            Token = _encryptionService.GenerateToken()
        };

        CurrentSession.SetSession(session);

        // 触发登录成功事件（在设置会话之后，验证密码变更之前）
        // 这样密码修改页面可以正常初始化

        // 保存/清理登录信息
        if (RememberMe)
        {
            var setting = await _dbContext.LocalSettings
                .FirstOrDefaultAsync(s => s.SettingKey == "RememberedEmployeeNo");
            if (setting == null)
            {
                setting = new Domain.Entities.LocalSetting
                {
                    SettingKey = "RememberedEmployeeNo",
                    SettingType = "String"
                };
                _dbContext.LocalSettings.Add(setting);
            }
            setting.SettingValue = _encryptionService.Encrypt(EmployeeNo);
            setting.UpdatedAt = DateTime.Now;
        }
        else
        {
            var setting = await _dbContext.LocalSettings
                .FirstOrDefaultAsync(s => s.SettingKey == "RememberedEmployeeNo");
            if (setting != null)
                _dbContext.LocalSettings.Remove(setting);
        }
        await _dbContext.SaveChangesAsync();

        // 记录登录日志
        var log = new Domain.Entities.OperationLog
        {
            OperatorId = employee.Id,
            OperatorNo = employee.EmployeeNo,
            Module = "系统",
            OperationType = "登录",
            Content = $"员工 {employee.Name} 登录系统",
            Result = "Success"
        };
        _dbContext.OperationLogs.Add(log);
        await _dbContext.SaveChangesAsync();

        LoginSuccessful?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 强制修改密码（登录后调用）
    /// </summary>
    [RelayCommand]
    private async Task ChangePasswordOnLoginAsync(string? newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword))
        {
            ShowError("请输入新密码");
            return;
        }

        if (newPassword.Length < MinPasswordLength)
        {
            ShowError($"密码长度不能少于{MinPasswordLength}位");
            return;
        }

        if (!HasValidPasswordComplexity(newPassword))
        {
            ShowError("密码需包含大写字母、小写字母和数字");
            return;
        }

        if (newPassword == "admin123")
        {
            ShowError("不能使用默认密码作为新密码");
            return;
        }

        try
        {
            var employee = await _dbContext.Employees.FindAsync(_currentEmployeeId);
            if (employee == null)
            {
                ShowError("员工信息不存在");
                return;
            }

            employee.PasswordHash = _encryptionService.HashPassword(newPassword);
            employee.PasswordChangedAt = DateTime.Now;
            employee.LoginFailCount = 0;
            employee.LockedUntil = null;
            await _dbContext.SaveChangesAsync();

            // 密码修改成功后完成登录
            await CompleteLoginAsync(employee);
        }
        catch (Exception ex)
        {
            ShowError($"密码修改失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 旧密码修改（登录后在系统设置中使用）
    /// </summary>
    public async Task<bool> ChangePasswordAsync(int employeeId, string oldPassword, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < MinPasswordLength)
            return false;
        if (!HasValidPasswordComplexity(newPassword))
            return false;

        var employee = await _dbContext.Employees.FindAsync(employeeId);
        if (employee == null)
            return false;
        if (!_encryptionService.VerifyPassword(oldPassword, employee.PasswordHash))
            return false;

        employee.PasswordHash = _encryptionService.HashPassword(newPassword);
        employee.PasswordChangedAt = DateTime.Now;
        employee.LoginFailCount = 0;
        await _dbContext.SaveChangesAsync();
        return true;
    }

    private static bool HasValidPasswordComplexity(string password)
    {
        return password.Any(char.IsUpper)
            && password.Any(char.IsLower)
            && password.Any(char.IsDigit);
    }
}
