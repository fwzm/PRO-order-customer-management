using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRO.Application.DTOs;
using PRO.Infrastructure.Services;
using PRO.Desktop.Controls;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using Serilog;

namespace PRO.Desktop.ViewModels;

/// <summary>
/// 数据质量检测 ViewModel
/// </summary>
public partial class DataQualityViewModel : ViewModelBase
{
    private readonly DataQualityService _qualityService;

    [ObservableProperty]
    private DataQualityReportDto? _report;

    [ObservableProperty]
    private bool _isReportLoaded;

    // 空状态支持
    [ObservableProperty] private EmptyStateViewModel? _emptyState;
    [ObservableProperty] private bool _showEmptyState;

    // 客户质量
    [ObservableProperty]
    private int _customerScore;

    [ObservableProperty]
    private string _customerLevel = "";

    [ObservableProperty]
    private int _duplicateCustomerCount;

    [ObservableProperty]
    private int _emptyPhoneCount;

    [ObservableProperty]
    private int _emptyAddressCount;

    [ObservableProperty]
    private int _inactiveCustomerCount;

    [ObservableProperty]
    private ObservableCollection<DuplicateGroupDto> _duplicateGroups = [];

    // 订单质量
    [ObservableProperty]
    private int _orderScore;

    [ObservableProperty]
    private string _orderLevel = "";

    [ObservableProperty]
    private int _staleDraftCount;

    [ObservableProperty]
    private int _longPendingCount;

    [ObservableProperty]
    private int _unsettledCompletedCount;

    // 产品质量
    [ObservableProperty]
    private int _productScore;

    [ObservableProperty]
    private string _productLevel = "";

    [ObservableProperty]
    private int _negativeStockCount;

    [ObservableProperty]
    private int _duplicateSkuCount;

    // 总体评分
    [ObservableProperty]
    private int _overallScore;

    [ObservableProperty]
    private string _overallLevel = "";

    [ObservableProperty]
    private string _overallDescription = "";

    // 选中的重复组
    [ObservableProperty]
    private DuplicateGroupDto? _selectedDuplicateGroup;

    [ObservableProperty]
    private bool _showDuplicateDetails;

    public DataQualityViewModel()
    {
        _qualityService = App.Services.GetService(typeof(DataQualityService)) as DataQualityService
            ?? throw new InvalidOperationException("无法获取数据质量服务");

        RunInBackground(GenerateReportAsync(), "生成数据质量报告失败");
    }

    [RelayCommand]
    private async Task GenerateReportAsync()
    {
        IsLoading = true;
        try
        {
            var branchId = CurrentSession.CurrentBranchId;
            var result = await _qualityService.GenerateReportAsync(branchId);

            if (result.Success && result.Data != null)
            {
                Report = result.Data;
                UpdateProperties();
                IsReportLoaded = true;
                ShowEmptyState = false;
            }
            else
            {
                IsReportLoaded = false;
                ShowEmptyState = true;
                EmptyState = EmptyStateViewModel.CreateForLoadFailed(GenerateReportCommand);
                ShowError(result.Message);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "数据质量报告生成失败");
            IsReportLoaded = false;
            ShowEmptyState = true;
            EmptyState = EmptyStateViewModel.CreateForLoadFailed(GenerateReportCommand);
            ShowError($"生成报告失败: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void UpdateProperties()
    {
        if (Report == null) return;

        // 客户质量
        CustomerScore = Report.CustomerQuality.Score;
        CustomerLevel = GetLevelText(CustomerScore);
        DuplicateCustomerCount = Report.CustomerQuality.DuplicateCount;
        EmptyPhoneCount = Report.CustomerQuality.EmptyPhoneCount;
        EmptyAddressCount = Report.CustomerQuality.EmptyAddressCount;
        InactiveCustomerCount = Report.CustomerQuality.InactiveCustomerCount;
        DuplicateGroups = new ObservableCollection<DuplicateGroupDto>(Report.CustomerQuality.DuplicateGroups);

        // 订单质量
        OrderScore = Report.OrderQuality.Score;
        OrderLevel = GetLevelText(OrderScore);
        StaleDraftCount = Report.OrderQuality.StaleDraftCount;
        LongPendingCount = Report.OrderQuality.LongPendingCount;
        UnsettledCompletedCount = Report.OrderQuality.UnsettledCompletedCount;

        // 产品质量
        ProductScore = Report.ProductQuality.Score;
        ProductLevel = GetLevelText(ProductScore);
        NegativeStockCount = Report.ProductQuality.NegativeStockCount;
        DuplicateSkuCount = Report.ProductQuality.DuplicateSkuCount;

        // 总体评分
        OverallScore = Report.OverallScore;
        OverallLevel = Report.OverallLevel;
        OverallDescription = GetOverallDescription(OverallScore);
    }

    private string GetLevelText(int score) => score switch
    {
        >= 80 => "良好",
        >= 60 => "警告",
        _ => "严重"
    };

    private string GetOverallDescription(int score) => score switch
    {
        >= 80 => "数据质量良好，建议定期检查",
        >= 60 => "数据质量有待提升，建议处理标记的问题",
        _ => "数据质量严重，建议立即处理所有问题"
    };

    [RelayCommand]
    private void ViewDuplicateDetails(DuplicateGroupDto? group)
    {
        if (group == null) return;
        SelectedDuplicateGroup = group;
        ShowDuplicateDetails = true;
    }

    [RelayCommand]
    private void CloseDuplicateDetails()
    {
        ShowDuplicateDetails = false;
        SelectedDuplicateGroup = null;
    }

    [RelayCommand]
    private async Task MergeDuplicateAsync(DuplicateGroupDto? group)
    {
        if (group == null || group.Customers.Count < 2)
        {
            ShowError("请选择至少两个重复客户");
            return;
        }

        // 选择保留的客户
        var keepCustomer = group.Customers.OrderByDescending(c => c.OrderCount).First();
        var mergeIds = group.Customers.Where(c => c.Id != keepCustomer.Id).Select(c => c.Id).ToList();

        var confirmed = ConfirmAction(
            "合并重复客户",
            $"确定保留「{keepCustomer.Name}」（订单数最多），合并其他 {mergeIds.Count} 个客户？\n\n合并后订单将转移到保留客户。");

        if (!confirmed) return;

        try
        {
            var result = await _qualityService.MergeDuplicateCustomersAsync(keepCustomer.Id, mergeIds);

            if (result.Success)
            {
                ShowSuccess(result.Message);
                await GenerateReportAsync();
            }
            else
            {
                ShowError(result.Message);
            }
        }
        catch (Exception ex)
        {
            ShowError($"合并失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private Task FixEmptyPhonesAsync()
    {
        // 打开批量补全手机号窗口
        ShowInfo("批量补全手机号功能开发中...");
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task CleanupInactiveCustomersAsync()
    {
        var confirmed = ConfirmDangerousAction(
            "清理长期未下单客户",
            $"确定将 {InactiveCustomerCount} 个超过60天未下单的客户标记为非活跃？");

        if (!confirmed) return Task.CompletedTask;

        ShowInfo("清理功能开发中...");
        return Task.CompletedTask;
    }

    private void ShowInfo(string message)
    {
        App.ShowToast?.Invoke(message, "提示", true);
    }
}
