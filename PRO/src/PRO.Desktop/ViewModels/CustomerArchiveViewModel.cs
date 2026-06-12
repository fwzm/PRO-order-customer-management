using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRO.Application.DTOs;
using PRO.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace PRO.Desktop.ViewModels;

/// <summary>
/// 客户档案 ViewModel - 一屏展示客户全貌
/// </summary>
public partial class CustomerArchiveViewModel : ViewModelBase
{
    private readonly CustomerArchiveService _archiveService;
    private readonly CustomerTimelineService _timelineService;

    [ObservableProperty]
    private CustomerArchiveDto? _archive;

    [ObservableProperty]
    private List<CustomerTimelineEvent> _timelineEvents = [];

    [ObservableProperty]
    private int _customerId;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private int _selectedTabIndex;

    public CustomerArchiveViewModel()
    {
        _archiveService = App.Services.GetService(typeof(CustomerArchiveService)) as CustomerArchiveService
            ?? throw new InvalidOperationException("无法获取客户服务");
        _timelineService = App.Services.GetService(typeof(CustomerTimelineService)) as CustomerTimelineService
            ?? throw new InvalidOperationException("无法获取客户时间线服务");
    }

    public void LoadCustomer(int customerId)
    {
        CustomerId = customerId;
        RunInBackground(LoadArchiveAsync(), "加载客户档案失败");
    }

    private async Task LoadArchiveAsync()
    {
        IsLoading = true;
        try
        {
            var result = await _archiveService.GetCustomerArchiveAsync(CustomerId);
            if (result.Success && result.Data != null)
            {
                Archive = result.Data;
            }
            else
            {
                ShowError(result.Message);
            }

            var timelineResult = await _timelineService.GetTimelineAsync(CustomerId);
            if (timelineResult.Success && timelineResult.Data != null)
            {
                TimelineEvents = timelineResult.Data.Events;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载客户档案失败");
            ShowError($"加载客户档案失败: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadArchiveAsync();
    }

    [RelayCommand]
    private void ViewOrder(int orderId)
    {
        var mainVm = System.Windows.Application.Current.MainWindow?.DataContext as MainViewModel;
        if (mainVm == null) return;

        var navItem = mainVm.NavigationItems
            .SelectMany<NavigationItem, NavigationItem>(n => n.Children.Any() ? n.Children : [n])
            .FirstOrDefault(n => n.Id == "order");

        if (navItem != null)
        {
            mainVm.NavigateToTabCommand.Execute(navItem);
        }
    }

    [RelayCommand]
    private void NavigateTo(string? route)
    {
        if (string.IsNullOrEmpty(route)) return;
        var mainVm = System.Windows.Application.Current.MainWindow?.DataContext as MainViewModel;
        mainVm?.NavigateToTabCommand.Execute(
            mainVm.NavigationItems.SelectMany<NavigationItem, NavigationItem>(n => n.Children.Any() ? n.Children : [n])
            .FirstOrDefault(n => n.Id == route));
    }
}
