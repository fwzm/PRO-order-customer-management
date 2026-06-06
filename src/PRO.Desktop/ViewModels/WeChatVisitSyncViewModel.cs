using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRO.Domain.Entities;
using PRO.Infrastructure.Persistence;
using PRO.Infrastructure.WeChat;
using System.Collections.ObjectModel;
using System.Windows;

namespace PRO.Desktop.ViewModels;

public partial class WeChatVisitSyncViewModel : ViewModelBase
{
    private readonly ProDbContext _db;
    [ObservableProperty] private ObservableCollection<WeChatVisitItem> _records = new();
    [ObservableProperty] private WeChatVisitItem? _selectedRecord;
    [ObservableProperty] private ObservableCollection<CustomerItem> _customers = new();
    [ObservableProperty] private CustomerItem? _selectedCustomer;
    [ObservableProperty] private string? _syncStatus;

    public WeChatVisitSyncViewModel()
    {
        _db = App.Services.GetService(typeof(ProDbContext)) as ProDbContext ?? throw new InvalidOperationException("无法获取数据库上下文");
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var list = await _db.WeChatVisitRecords.AsNoTracking().Where(v => v.Status == "Unlinked").OrderByDescending(v => v.SyncedAt).ToListAsync();
            var customerIds = list.Where(v => v.LinkedCustomerId.HasValue).Select(v => v.LinkedCustomerId!.Value).Distinct().ToList();
            var customerManagers = await _db.Customers
                .Where(c => customerIds.Contains(c.Id))
                .Select(c => new { c.Id, ManagerName = c.CustomerManager != null ? c.CustomerManager.Name : "" })
                .ToDictionaryAsync(x => x.Id, x => x.ManagerName);

            Records = new ObservableCollection<WeChatVisitItem>(list.Select(v => new WeChatVisitItem
            {
                Id = v.Id, SourceRecordId = v.SourceRecordId, CustomerName = v.CustomerName, VisitorName = v.VisitorName,
                VisitDate = v.VisitDate, VisitType = v.VisitType, Content = v.Content, Purpose = v.Purpose,
                CustomerDemand = v.CustomerDemand, NextAction = v.NextAction, SyncedAt = v.SyncedAt,
                CustomerManagerName = v.LinkedCustomerId.HasValue && customerManagers.ContainsKey(v.LinkedCustomerId.Value)
                    ? customerManagers[v.LinkedCustomerId.Value] : (v.VisitorName ?? "")
            }));

            var custs = await _db.Customers.Where(c => c.BranchId == CurrentSession.CurrentBranchId && c.Status == Domain.Enums.CustomerStatus.Active).OrderBy(c => c.Name).ToListAsync();
            Customers = new ObservableCollection<CustomerItem>(custs.Select(c => new CustomerItem { Id = c.Id, Name = c.Name, CustomerNo = c.CustomerNo }));
        }
        catch (Exception ex) { ShowError($"加载失败: {ex.Message}"); }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task SyncFromWeChatAsync()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var httpFactory = scope.ServiceProvider.GetRequiredService<System.Net.Http.IHttpClientFactory>();
            var service = new SmartTableSyncService(httpFactory, _db);
            var count = await service.SyncVisitRecordsAsync();
            SyncStatus = $"同步完成：新增 {count} 条记录";
            ShowSuccess(SyncStatus);
            await LoadAsync();
        }
        catch (Exception ex) { ShowError($"同步失败: {ex.Message}。请确认已配置企业微信并设置智能表格 docid/sheet_id。"); }
    }

    [RelayCommand]
    private async Task LinkToCustomerAsync(WeChatVisitItem? item)
    {
        if (item == null) return;
        var selected = await Views.CustomerPickerWindow.ShowAsync(
            owner: System.Windows.Application.Current.MainWindow);

        if (selected != null)
        {
            await DoLinkAsync(item, selected.Id);
        }
    }

    private async Task DoLinkAsync(WeChatVisitItem item, int customerId)
    {
        try
        {
            // 1. 创建正式拜访记录
            _db.VisitRecords.Add(new VisitRecord
            {
                CustomerId = customerId, VisitorId = CurrentSession.CurrentEmployeeId,
                VisitType = item.VisitType ?? "电话", Purpose = item.Purpose,
                Content = item.Content, CustomerDemand = item.CustomerDemand,
                NextAction = item.NextAction, VisitDate = item.VisitDate ?? DateTime.Now,
                CreatedAt = DateTime.Now
            });

            // 2. 更新同步记录状态 + 删除企业微信智能表格记录
            var syncRecord = await _db.WeChatVisitRecords.FindAsync(item.Id);
            if (syncRecord != null)
            {
                syncRecord.Status = "Linked";
                syncRecord.LinkedCustomerId = customerId;

                // 删除智能表格中的记录
                try
                {
                    using var scope = App.Services.CreateScope();
                    var httpFactory = scope.ServiceProvider.GetRequiredService<System.Net.Http.IHttpClientFactory>();
                    var service = new SmartTableSyncService(httpFactory, _db);
                    await service.DeleteRecordsAsync(new List<string> { item.SourceRecordId });
                }
                catch { /* 静默失败 */ }
            }

            await _db.SaveChangesAsync();
            ShowSuccess("拜访记录已关联并同步到应用");
            await LoadAsync();
        }
        catch (Exception ex) { ShowError($"关联失败: {ex.Message}"); }
    }

    [RelayCommand]
    private async Task QuickCreateCustomerAsync(WeChatVisitItem? item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.CustomerName)) return;
        try
        {
            var branchId = CurrentSession.CurrentBranchId;
            var customerNo = "K" + DateTime.Now.ToString("yyyyMMdd") + "0000" + new Random().Next(1000, 9999);
            var customer = new Customer
            {
                Name = item.CustomerName, CustomerNo = customerNo, CustomerType = Domain.Enums.CustomerType.Sub,
                BranchId = branchId, CreatedById = CurrentSession.CurrentEmployeeId,
                Status = Domain.Enums.CustomerStatus.Active, CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now,
                SyncStatus = Domain.Enums.SyncStatus.Pending, LocalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            _db.Customers.Add(customer);
            await _db.SaveChangesAsync();
            await DoLinkAsync(item, customer.Id);
        }
        catch (Exception ex) { ShowError($"快速创建失败: {ex.Message}"); }
    }

    [RelayCommand] private async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private void ViewDetail(WeChatVisitItem? item)
    {
        if (item == null) return;
        var msg = $"客户: {item.CustomerName ?? "-"}\n" +
                  $"拜访人: {item.VisitorName ?? "-"}\n" +
                  $"日期: {item.VisitDate:yyyy-MM-dd}\n" +
                  $"方式: {item.VisitType ?? "-"}\n" +
                  $"目的: {item.Purpose ?? "-"}\n" +
                  $"内容: {item.Content ?? "-"}\n" +
                  $"客户需求: {item.CustomerDemand ?? "-"}\n" +
                  $"下一步: {item.NextAction ?? "-"}";
        MessageBox.Show(msg, "拜访详情", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}

public class WeChatVisitItem
{
    public int Id { get; set; }
    public string SourceRecordId { get; set; } = "";
    public string? CustomerName { get; set; }
    public string? VisitorName { get; set; }
    public string? CustomerManagerName { get; set; }
    public DateTime? VisitDate { get; set; }
    public string? VisitType { get; set; }
    public string? Content { get; set; }
    public string? Purpose { get; set; }
    public string? CustomerDemand { get; set; }
    public string? NextAction { get; set; }
    public DateTime SyncedAt { get; set; }
}
