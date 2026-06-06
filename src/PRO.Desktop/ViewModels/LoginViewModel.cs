using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Infrastructure.Persistence;
using PRO.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using System.Windows;

namespace PRO.Desktop.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    public event EventHandler? LoginSuccessful;
    
    private readonly IEncryptionService _encryptionService;
    private readonly ProDbContext _dbContext;

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
                .FirstOrDefaultAsync(e => e.EmployeeNo == EmployeeNo && e.Status == Domain.Enums.EmployeeStatus.Active);

            if (employee == null)
            {
                LoginError = "工号不存在或已停用";
                return;
            }

            // 验证密码
            if (!_encryptionService.VerifyPassword(Password, employee.PasswordHash))
            {
                LoginError = "密码错误";
                return;
            }

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

            // 默认密码安全提示
            if (Password == "admin123")
            {
                MessageBox.Show("您当前使用的是默认密码(admin123)，为安全起见请尽快修改密码！\n\n路径：系统设置 → 个人设置 → 修改密码",
                    "安全提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            // 触发登录成功事件
            LoginSuccessful?.Invoke(this, EventArgs.Empty);

            // 保存/清理登录信息
            if (RememberMe)
            {
                var setting = await _dbContext.LocalSettings
                    .FirstOrDefaultAsync(s => s.SettingKey == "RememberedEmployeeNo");
                if (setting == null)
                {
                    setting = new Domain.Entities.LocalSetting { SettingKey = "RememberedEmployeeNo", SettingType = "String" };
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
}
