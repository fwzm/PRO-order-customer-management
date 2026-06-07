using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using PRO.Infrastructure.Persistence;
using PRO.Infrastructure.Security;
using PRO.Infrastructure.Database;
using PRO.Infrastructure.Configuration;
using PRO.Desktop.ViewModels;
using PRO.Desktop.Views;
using PRO.Desktop.Services;
using Serilog;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using AppInterfaces = PRO.Application.Interfaces;
using PRO.Infrastructure.Repositories;
using PRO.Infrastructure.WeChat;
using PRO.Infrastructure.Services;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;

namespace PRO.Desktop;

public partial class App : System.Windows.Application
{
    private static IServiceProvider? _serviceProvider;
    public static IServiceProvider Services => _serviceProvider!;
    public static Action<string, string?, bool>? ShowToast { get; set; }
    public static Action<string?>? ShowLoading { get; set; }
    public static Action? HideLoading { get; set; }
    /// <summary>连接状态变更：(isConnected, message)</summary>
    public static Action<bool, string>? ConnectionStatusChanged { get; set; }

    private TaskbarIcon? _trayIcon;
    private DispatcherTimer? _autoBackupTimer;
    private DispatcherTimer? _draftConfirmTimer;
    private DispatcherTimer? _healthCheckTimer;
    private MainWindow? _mainWindow;

    protected override async void OnStartup(StartupEventArgs e)
    {
        // 解决 Npgsql 本地时间问题
        AppContext.SetSwitch("Npgsql.EnableDateTimeUtcFix", true);

        // 初始化配置
        HardcodedConfig.Initialize(AppDomain.CurrentDomain.BaseDirectory);

        // 配置日志
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File("logs/pro-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
            .CreateLogger();

        Log.Information("PRO应用启动");

        // 配置依赖注入
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // 初始化数据库
        try
        {
            Log.Information("正在连接 PostgreSQL 数据库...");
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ProDbContext>();
            var encryptionService = scope.ServiceProvider.GetRequiredService<AppInterfaces.IEncryptionService>();
            await DatabaseInitializer.InitializeAsync(dbContext, encryptionService);
            Log.Information("数据库初始化完成");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "数据库初始化失败");
            MessageBox.Show($"数据库连接失败: {ex.Message}\n\n请检查 PostgreSQL 服务是否正常运行。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        // 配置全局异常处理
        SetupExceptionHandling();

        base.OnStartup(e);

        try
        {
            _mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            MainWindow = _mainWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;

            InitializeTrayIcon();

            // 显示登录窗口
            Log.Information("显示登录窗口");
            var loginWindow = _serviceProvider.GetRequiredService<LoginWindow>();
            var loginViewModel = (LoginViewModel)loginWindow.DataContext;

            var loginCompleted = false;
            loginViewModel.LoginSuccessful += (s, args) =>
            {
                loginCompleted = true;
                Log.Information("登录成功事件触发");
                loginWindow.DialogResult = true;
            };

            loginViewModel.MustChangePassword += () =>
            {
                // 强制修改密码 - 不允许跳过
                loginWindow.Dispatcher.Invoke(() =>
                {
                    var result = MessageBox.Show(
                        "首次登录或密码已过期，必须修改密码后才能继续使用系统。\n\n密码要求：至少8位，包含大写字母、小写字母和数字。",
                        "安全要求 - 必须修改密码",
                        MessageBoxButton.OK, MessageBoxImage.Warning);

                    // 弹出简易密码修改对话框
                    var pwdDialog = new System.Windows.Window
                    {
                        Title = "修改密码",
                        Width = 400,
                        Height = 320,
                        WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
                        Owner = loginWindow,
                        ResizeMode = System.Windows.ResizeMode.NoResize,
                        WindowStyle = System.Windows.WindowStyle.ToolWindow
                    };

                    var panel = new System.Windows.Controls.StackPanel { Margin = new System.Windows.Thickness(20) };
                    panel.Children.Add(new System.Windows.Controls.TextBlock
                    {
                        Text = "请输入新密码（至少8位，含大小写字母和数字）：",
                        TextWrapping = System.Windows.TextWrapping.Wrap,
                        Margin = new System.Windows.Thickness(0, 0, 0, 15)
                    });

                    var pwdBox = new System.Windows.Controls.PasswordBox
                    {
                        Margin = new System.Windows.Thickness(0, 0, 0, 10)
                    };
                    panel.Children.Add(pwdBox);

                    var confirmBox = new System.Windows.Controls.PasswordBox
                    {
                        Margin = new System.Windows.Thickness(0, 0, 0, 15)
                    };
                    panel.Children.Add(new System.Windows.Controls.TextBlock { Text = "确认密码：", Margin = new System.Windows.Thickness(0, 5, 0, 3) });
                    panel.Children.Add(confirmBox);

                    var errorBlock = new System.Windows.Controls.TextBlock
                    {
                        Foreground = System.Windows.Media.Brushes.Red,
                        TextWrapping = System.Windows.TextWrapping.Wrap,
                        Margin = new System.Windows.Thickness(0, 0, 0, 10)
                    };
                    panel.Children.Add(errorBlock);

                    var btnPanel = new System.Windows.Controls.StackPanel
                    {
                        Orientation = System.Windows.Controls.Orientation.Horizontal,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Right
                    };

                    var confirmBtn = new System.Windows.Controls.Button
                    {
                        Content = "确认修改",
                        Width = 90,
                        Margin = new System.Windows.Thickness(0, 0, 10, 0),
                        IsDefault = true
                    };
                    btnPanel.Children.Add(confirmBtn);
                    panel.Children.Add(btnPanel);

                    pwdDialog.Content = panel;

                    confirmBtn.Click += async (s2, e2) =>
                    {
                        var pwd = pwdBox.Password;
                        var confirm = confirmBox.Password;

                        if (string.IsNullOrWhiteSpace(pwd))
                        {
                            errorBlock.Text = "请输入新密码";
                            return;
                        }
                        if (pwd.Length < 8)
                        {
                            errorBlock.Text = "密码长度不能少于8位";
                            return;
                        }
                        if (!pwd.Any(char.IsUpper) || !pwd.Any(char.IsLower) || !pwd.Any(char.IsDigit))
                        {
                            errorBlock.Text = "密码需包含大写字母、小写字母和数字";
                            return;
                        }
                        if (pwd == "admin123")
                        {
                            errorBlock.Text = "不能使用默认密码";
                            return;
                        }
                        if (pwd != confirm)
                        {
                            errorBlock.Text = "两次输入的密码不一致";
                            return;
                        }

                        await loginViewModel.ChangePasswordOnLoginCommand.ExecuteAsync(pwd);
                        if (loginCompleted)
                        {
                            pwdDialog.DialogResult = true;
                        }
                        else
                        {
                            errorBlock.Text = "密码修改失败，请重试";
                        }
                    };

                    var showResult = pwdDialog.ShowDialog();
                    if (showResult != true && !loginCompleted)
                    {
                        // 用户强制关闭密码修改窗口 → 关闭整个应用
                        Log.Warning("用户拒绝修改密码，关闭应用");
                        System.Windows.Application.Current.Shutdown();
                    }
                });
            };

            var result = loginWindow.ShowDialog();
            Log.Information($"登录窗口返回: {result}, 登录完成: {loginCompleted}");

            if (loginCompleted && CurrentSession.Current != null)
            {
                Log.Information("显示主窗口");
                var mainViewModel = (MainViewModel)_mainWindow.DataContext;
                mainViewModel.InitializeAfterLogin();

                App.ShowToast = (msg, title, success) => _mainWindow?.Dispatcher.Invoke(() => _mainWindow.ToastCtrl.Show(msg, title, success));
                App.ShowLoading = (text) => _mainWindow?.Dispatcher.Invoke(() => _mainWindow.ShowLoading(text));
                App.HideLoading = () => _mainWindow?.Dispatcher.Invoke(() => _mainWindow.HideLoading());

                // 连接健康监控
                var healthService = _serviceProvider.GetRequiredService<ConnectionHealthService>();
                healthService.ConnectionStatusChanged += (isConnected, message) =>
                {
                    _mainWindow?.Dispatcher.Invoke(() =>
                    {
                        ConnectionStatusChanged?.Invoke(isConnected, message);
                        ShowToast?.Invoke(message, isConnected ? "连接恢复" : "连接警告", isConnected);
                    });
                };
                StartHealthCheckTimer();

                // 检查新版本（异步，不阻塞登录流程）
                _ = CheckNewVersionAsync();

                _mainWindow.Show();
                StartAutoBackupTimer();
                StartDraftConfirmTimer();
                Log.Information("主窗口已显示");
            }
            else
            {
                Log.Information("用户未登录，关闭应用");
                Shutdown();
            }
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "启动过程中出现错误");
            MessageBox.Show($"启动错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void InitializeTrayIcon()
    {
        try
        {
            var icon = CreateTrayIcon();
            if (icon == null)
            {
                Log.Warning("无法创建托盘图标，跳过");
                return;
            }

            _trayIcon = new TaskbarIcon
            {
                Icon = icon,
                ToolTipText = "PRO企业管理系统",
                Visibility = Visibility.Visible
            };

            _trayIcon.TrayMouseDoubleClick += (s, e) =>
            {
                if (_mainWindow != null)
                {
                    _mainWindow.Show();
                    _mainWindow.WindowState = WindowState.Normal;
                    _mainWindow.Activate();
                }
            };

            var contextMenu = new System.Windows.Controls.ContextMenu();
            var showItem = new System.Windows.Controls.MenuItem { Header = "显示主窗口" };
            showItem.Click += (s, e) =>
            {
                if (_mainWindow != null)
                {
                    _mainWindow.Show();
                    _mainWindow.WindowState = WindowState.Normal;
                    _mainWindow.Activate();
                }
            };
            contextMenu.Items.Add(showItem);

            var exitItem = new System.Windows.Controls.MenuItem { Header = "退出" };
            exitItem.Click += (s, e) =>
            {
                _trayIcon?.Dispose();
                System.Windows.Application.Current.Shutdown();
            };
            contextMenu.Items.Add(exitItem);

            _trayIcon.ContextMenu = contextMenu;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "托盘图标初始化失败，跳过");
        }
    }

    private static System.Drawing.Icon? CreateTrayIcon()
    {
        try
        {
            return (System.Drawing.Icon)System.Drawing.SystemIcons.Application.Clone();
        }
        catch
        {
            return null;
        }
    }

    private async Task CheckNewVersionAsync()
    {
        try
        {
            await Task.Delay(2000);
            using var scope = _serviceProvider!.CreateScope();
            var versionCheck = scope.ServiceProvider.GetRequiredService<VersionCheckService>();

            var latest = await versionCheck.CheckLatestVersionAsync();
            if (latest == null) return;

            if (await versionCheck.IsVersionSuppressedAsync(latest.Version))
                return;

            await Dispatcher.InvokeAsync(() =>
            {
                var msg = $"检测到新版本 v{latest.Version}（当前 v{VersionCheckService.LocalVersion}）\n\n";
                if (!string.IsNullOrEmpty(latest.ReleaseDate))
                    msg += $"发布日期：{latest.ReleaseDate}\n";
                if (!string.IsNullOrEmpty(latest.ReleaseNotes))
                    msg += $"更新说明：{latest.ReleaseNotes}\n";
                msg += $"\n请联系管理员获取最新版本进行更新。";

                var result = MessageBox.Show(msg, "发现新版本", MessageBoxButton.OKCancel, MessageBoxImage.Information);
                if (result == MessageBoxResult.Cancel)
                    _ = versionCheck.SuppressVersionAsync(latest.Version);
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "版本检查失败（不影响正常使用）");
        }
    }

    private void StartAutoBackupTimer()
    {
        _autoBackupTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(30)
        };
        _autoBackupTimer.Tick += async (s, e) => await PerformAutoBackupAsync();
        _autoBackupTimer.Start();
    }

    private async Task PerformAutoBackupAsync()
    {
        try
        {
            var backupService = _serviceProvider!.GetRequiredService<DatabaseBackupService>();
            var result = await backupService.PerformAutoBackupAsync();
            
            if (result.Success)
            {
                Log.Information("自动备份完成: {FileName}", result.FileName);
            }
            else
            {
                Log.Warning("自动备份跳过或失败: {Message}", result.Message);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "自动备份异常");
        }
    }

    private void StartDraftConfirmTimer()
    {
        _draftConfirmTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(5)
        };
        _draftConfirmTimer.Tick += async (s, e) => await ConfirmExpiredDraftsAsync();
        _draftConfirmTimer.Start();
    }

    private async Task ConfirmExpiredDraftsAsync()
    {
        try
        {
            using var scope = Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ProDbContext>();

            var now = DateTime.Now;

            // 1. 发送到期前24小时提醒
            var upcomingDrafts = await dbContext.Orders
                .Include(o => o.Customer)
                .Where(o => o.Status == OrderStatus.Draft
                    && o.DraftExpireTime != null
                    && o.DraftExpireTime > now
                    && o.DraftExpireTime <= now.AddHours(24))
                .ToListAsync();

            if (upcomingDrafts.Any())
            {
                foreach (var draft in upcomingDrafts)
                {
                    var hoursLeft = (int)(draft.DraftExpireTime!.Value - now).TotalHours;
                    ShowToast?.Invoke(
                        $"草稿订单 {draft.OrderNo} 将在 {hoursLeft} 小时后自动确认",
                        "草稿到期提醒",
                        false);
                    Log.Information("草稿到期提醒: OrderId={OrderId}, 剩余{Hours}小时", draft.Id, hoursLeft);
                }
            }

            // 2. 自动确认已过期草稿
            var expiredDrafts = await dbContext.Orders
                .Where(o => o.Status == OrderStatus.Draft
                    && o.DraftExpireTime != null
                    && o.DraftExpireTime < now)
                .ToListAsync();

            if (expiredDrafts.Count == 0) return;

            foreach (var draft in expiredDrafts)
            {
                draft.Status = OrderStatus.Pending;
                draft.UpdatedAt = now;

                // 使用系统操作员ID（0表示系统自动操作）
                dbContext.OrderModificationRecords.Add(new OrderModificationRecord
                {
                    OrderId = draft.Id,
                    ModifiedById = CurrentSession.CurrentEmployeeId > 0 ? CurrentSession.CurrentEmployeeId : 0,
                    ModifiedAt = now,
                    Content = "订单草稿到期自动确认",
                    ModificationType = "DraftConfirm"
                });

                Log.Information("草稿自动确认: OrderId={OrderId}, OrderNo={OrderNo}", draft.Id, draft.OrderNo);
            }

            await dbContext.SaveChangesAsync();

            // 发送确认通知
            if (expiredDrafts.Any())
            {
                ShowToast?.Invoke(
                    $"已自动确认 {expiredDrafts.Count} 个过期草稿订单",
                    "草稿自动确认",
                    true);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "自动确认草稿失败");
        }
    }

    public void StopAllTimers()
    {
        _autoBackupTimer?.Stop();
        _draftConfirmTimer?.Stop();
        _healthCheckTimer?.Stop();
    }

    private void StartHealthCheckTimer()
    {
        _healthCheckTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(60) // 每60秒检查一次连接
        };
        _healthCheckTimer.Tick += async (s, e) =>
        {
            try
            {
                var healthService = _serviceProvider!.GetRequiredService<ConnectionHealthService>();
                await healthService.CheckConnectionAsync();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "健康检查定时器异常");
            }
        };
        _healthCheckTimer.Start();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // PostgreSQL 数据库
        var connStr = HardcodedConfig.PostgreSQLConnectionString;
        if (string.IsNullOrEmpty(connStr))
        {
            Log.Fatal("未配置 PostgreSQL 连接字符串");
            throw new InvalidOperationException("请在 appsettings.json 中配置 ConnectionStrings:PostgreSQL");
        }

        services.AddDbContext<ProDbContext>(options =>
            options.UseNpgsql(connStr, npgsql =>
            {
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null);
                npgsql.CommandTimeout(30);
            }));

        // 连接健康监控
        services.AddSingleton<ConnectionHealthService>();

        // 数据库备份服务
        services.AddSingleton<DatabaseBackupService>();

        // 草稿自动保存服务
        services.AddScoped<DraftService>();

        // 业务配置服务
        services.AddSingleton<BusinessConfigService>();

        // 基础设施服务
        services.AddSingleton<AppInterfaces.IEncryptionService, EncryptionService>();

        services.AddScoped<VersionCheckService>();

        // 数据仓储
        services.AddScoped<AppInterfaces.IBranchRepository, BranchRepository>();
        services.AddScoped<AppInterfaces.IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<AppInterfaces.IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<AppInterfaces.ICustomerRepository, CustomerRepository>();
        services.AddScoped<AppInterfaces.IOrderRepository, OrderRepository>();
        services.AddScoped<AppInterfaces.IProductRepository, ProductRepository>();
        services.AddScoped<AppInterfaces.IDeliveryPersonRepository, DeliveryPersonRepository>();

        // 业务服务
        services.AddScoped<AppInterfaces.IAuthService, AuthService>();
        services.AddScoped<AppInterfaces.IBranchService, BranchService>();
        services.AddScoped<AppInterfaces.IEmployeeService, EmployeeService>();
        services.AddScoped<AppInterfaces.ICustomerService, CustomerService>();
        services.AddScoped<AppInterfaces.IOrderService, OrderService>();
        services.AddScoped<AppInterfaces.IProductService, ProductService>();
        services.AddScoped<AppInterfaces.IDeliveryPersonService, DeliveryPersonService>();
        services.AddScoped<AppInterfaces.ISettlementService, SettlementService>();
        services.AddScoped<AppInterfaces.IWorkScheduleService, WorkScheduleService>();
        services.AddScoped<AppInterfaces.IWorkPlanService, WorkPlanService>();
        services.AddScoped<AppInterfaces.IPlanDraftService, PlanDraftService>();
        services.AddScoped<AppInterfaces.IOperationLogService, OperationLogService>();
        services.AddScoped<AppInterfaces.IOrderDistributionService, DeliveryDistributionService>();
        services.AddScoped<AppInterfaces.IWeChatService, WeChatService>();

        // HTTP 客户端（企微 API）
        services.AddHttpClient("WeChatWork", client =>
        {
            client.BaseAddress = new Uri("https://qyapi.weixin.qq.com/");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // 视图模型
        services.AddTransient<LoginViewModel>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<CustomerListViewModel>();
        services.AddTransient<CustomerEditViewModel>();
        services.AddTransient<OrderListViewModel>();
        services.AddTransient<OrderEditViewModel>();
        services.AddTransient<ProductListViewModel>();
        services.AddTransient<ProductEditViewModel>();
        services.AddTransient<DeliveryPersonListViewModel>();
        services.AddTransient<DeliveryPersonEditViewModel>();
        services.AddTransient<SettlementListViewModel>();
        services.AddTransient<SettlementDetailViewModel>();
        services.AddTransient<WorkScheduleViewModel>();
        services.AddTransient<WorkPlanViewModel>();
        services.AddTransient<DepartmentTreeViewModel>();
        services.AddTransient<EmployeeListViewModel>();
        services.AddTransient<EmployeeEditViewModel>();
        services.AddTransient<SystemSettingsViewModel>();
        services.AddTransient<HeadquartersAdminViewModel>();
        services.AddTransient<SyncStatusViewModel>();
        services.AddTransient<BusinessDistrictViewModel>();
        services.AddTransient<MapPickerViewModel>();
        services.AddTransient<WeChatCustomerViewModel>();
        services.AddTransient<WeChatSyncLogViewModel>();
        services.AddTransient<PredictionDashboardViewModel>();
        services.AddTransient<OpportunityViewModel>();
        services.AddTransient<InventoryViewModel>();
        services.AddTransient<WeChatVisitSyncViewModel>();
        services.AddTransient<AccountsReceivableViewModel>();
        services.AddTransient<TagViewModel>();
        services.AddTransient<ReportCenterViewModel>();
        services.AddTransient<WeChatScrmViewModel>();
        services.AddTransient<VisitOpportunityViewModel>();
        services.AddTransient<DistrictTagViewModel>();
        services.AddTransient<OperationLogViewModel>();
        services.AddTransient<WeChatSetupWizardViewModel>();

        // 视图
        services.AddTransient<LoginWindow>();
        services.AddTransient<MainWindow>();
        services.AddTransient<DepartmentManagementWindow>();
        services.AddTransient<EmployeeListWindow>();
    }

    private void SetupExceptionHandling()
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            Log.Fatal(ex, "未处理的域异常");
            MessageBox.Show($"发生严重错误: {ex?.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        DispatcherUnhandledException += (s, e) =>
        {
            Log.Error(e.Exception, "未处理的UI线程异常");
            System.Windows.MessageBox.Show($"发生错误: {e.Exception.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            Log.Error(e.Exception, "未观察的任务异常");
            e.SetObserved();
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _autoBackupTimer?.Stop();
        _draftConfirmTimer?.Stop();
        _trayIcon?.Dispose();
        Log.Information("PRO应用退出");
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
