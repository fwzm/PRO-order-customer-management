using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
using PRO.Infrastructure.Services;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;

namespace PRO.Desktop;

public partial class App : System.Windows.Application
{
    private static IServiceProvider? _serviceProvider;
    public static IServiceProvider Services => _serviceProvider!;
    public static Action<string, string?, bool>? ShowToast { get; set; }

    private TaskbarIcon? _trayIcon;
    private DispatcherTimer? _autoBackupTimer;
    private DispatcherTimer? _draftConfirmTimer;
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

            var result = loginWindow.ShowDialog();
            Log.Information($"登录窗口返回: {result}, 登录完成: {loginCompleted}");

            if (loginCompleted && CurrentSession.Current != null)
            {
                Log.Information("显示主窗口");
                var mainViewModel = (MainViewModel)_mainWindow.DataContext;
                mainViewModel.InitializeAfterLogin();

                App.ShowToast = (msg, title, success) => _mainWindow?.Dispatcher.Invoke(() => _mainWindow.ToastCtrl.Show(msg, title, success));

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
            using var scope = _serviceProvider!.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ProDbContext>();

            var settings = await dbContext.LocalSettings.ToListAsync();
            var closeSetting = settings.FirstOrDefault(s => s.SettingKey == "CloseBehavior");
            if (closeSetting != null && int.Parse(closeSetting.SettingValue) == 1)
                return;

            var backupDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PRO", "backups");
            Directory.CreateDirectory(backupDir);

            var fileName = $"pro_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db";

            var expiredRecords = await dbContext.BackupRecords
                .Where(r => r.ExpireTime <= DateTime.Now)
                .ToListAsync();
            if (expiredRecords.Any())
            {
                dbContext.BackupRecords.RemoveRange(expiredRecords);
            }

            dbContext.BackupRecords.Add(new BackupRecord
            {
                FileName = fileName,
                FilePath = backupDir,
                BackupType = "自动",
                FileSize = 0,
                BackupTime = DateTime.Now,
                ExpireTime = DateTime.Now.AddDays(30),
                Status = "Success"
            });

            await dbContext.SaveChangesAsync();
            Log.Information($"自动备份记录已添加: {fileName}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "自动备份失败");
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

            var expiredDrafts = await dbContext.Orders
                .Where(o => o.Status == OrderStatus.Draft
                    && o.DraftExpireTime != null
                    && o.DraftExpireTime < DateTime.Now)
                .ToListAsync();

            if (expiredDrafts.Count == 0) return;

            foreach (var draft in expiredDrafts)
            {
                draft.Status = OrderStatus.Pending;
                draft.UpdatedAt = DateTime.Now;

                dbContext.OrderModificationRecords.Add(new OrderModificationRecord
                {
                    OrderId = draft.Id,
                    ModifiedById = 0,
                    ModifiedAt = DateTime.Now,
                    Content = "订单草稿到期自动确认",
                    ModificationType = "DraftConfirm"
                });

                Log.Information("草稿自动确认: OrderId={OrderId}, OrderNo={OrderNo}", draft.Id, draft.OrderNo);
            }

            await dbContext.SaveChangesAsync();
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
            options.UseNpgsql(connStr));

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
        services.AddScoped<AppInterfaces.IOperationLogService, OperationLogService>();

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
