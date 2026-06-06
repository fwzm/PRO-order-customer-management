using System.Windows;
using System.Windows.Input;
using PRO.Desktop.ViewModels;
using PRO.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using PRO.Domain.Enums;

namespace PRO.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    /// <summary>显示全局加载覆盖层</summary>
    public void ShowLoading(string? text = null) => LoadingCtrl.Show(text);

    /// <summary>隐藏全局加载覆盖层</summary>
    public void HideLoading() => LoadingCtrl.Hide();

    // ======== 标签栏 ========

    private void TabBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is ViewModels.TabItem tab)
        {
            foreach (var t in _viewModel.TabItems)
                t.IsSelected = false;
            tab.IsSelected = true;
            _viewModel.SelectedTab = tab;
        }
    }

    // ======== 键盘快捷键 ========

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F5)
        {
            _viewModel.RefreshCurrentTabCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            _viewModel.ToggleSearchCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _viewModel.IsSearchVisible)
        {
            _viewModel.ClearSearchCommand.Execute(null);
            e.Handled = true;
        }
    }

    // ======== 窗口状态 ========

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Normal)
            ShowInTaskbar = true;
    }

    protected override async void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ProDbContext>();
            var settings = await dbContext.LocalSettings.ToListAsync();
            var closeBehavior = settings.FirstOrDefault(s => s.SettingKey == "CloseBehavior");

            if (closeBehavior == null || closeBehavior.SettingValue == "0")
            {
                var pendingCount = await dbContext.OperationLogs
                    .CountAsync(o => o.SyncStatus == SyncStatus.Pending);

                if (pendingCount > 0)
                {
                    var pendingLogs = await dbContext.OperationLogs
                        .Where(o => o.SyncStatus == SyncStatus.Pending)
                        .ToListAsync();
                    foreach (var log in pendingLogs)
                    {
                        log.SyncStatus = SyncStatus.Synced;
                    }
                    await dbContext.SaveChangesAsync();
                }

                e.Cancel = true;
                WindowState = WindowState.Minimized;
                Hide();
                ShowInTaskbar = false;
                return;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"窗口关闭异常: {ex.Message}");
        }

        base.OnClosing(e);
    }
}