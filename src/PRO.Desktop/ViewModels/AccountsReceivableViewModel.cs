using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;
using System.Collections.ObjectModel;

namespace PRO.Desktop.ViewModels;

public partial class AccountsReceivableViewModel : ViewModelBase
{
    private readonly ProDbContext _db;

    [ObservableProperty] private ObservableCollection<ARItem> _items = new();
    [ObservableProperty] private decimal _totalReceivable;
    [ObservableProperty] private decimal _totalOverdue30;
    [ObservableProperty] private decimal _totalOverdue60;
    [ObservableProperty] private decimal _totalOverdue90;
    [ObservableProperty] private int _overdueCount;
    [ObservableProperty] private string _searchKeyword = "";

    public AccountsReceivableViewModel()
    {
        _db = App.Services.GetService(typeof(ProDbContext)) as ProDbContext ?? throw new InvalidOperationException();
        RunInBackground(LoadAsync(), "应收账款加载失败");
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var branchId = CurrentSession.CurrentBranchId;
            var orders = await _db.Orders
                .AsNoTracking()
                .Include(o => o.Customer!).ThenInclude(c => c.CustomerManager)
                .Where(o => o.BranchId == branchId && o.PaymentStatus != PaymentStatus.Paid && o.Status != OrderStatus.Cancelled)
                .OrderBy(o => o.CreatedAt)
                .ToListAsync();

            var now = DateTime.Now;
            var list = orders.Select(o =>
            {
                var daysOverdue = (int)(now - o.CreatedAt).TotalDays;
                var unpaid = o.TotalAmount - o.ReceivedAmount;
                return new ARItem
                {
                    OrderNo = o.OrderNo, CustomerName = o.Customer?.Name ?? "未知",
                    CustomerManagerName = o.Customer?.CustomerManager?.Name ?? "",
                    TotalAmount = o.TotalAmount, ReceivedAmount = o.ReceivedAmount,
                    UnpaidAmount = unpaid, CreatedAt = o.CreatedAt,
                    DaysOverdue = daysOverdue,
                    StatusBadge = daysOverdue switch { >= 90 => "90+天", >= 60 => "60+天", >= 30 => "30+天", >= 15 => "15+天", _ => "正常" },
                    StatusColor = daysOverdue switch { >= 90 => "#E74C3C", >= 60 => "#E67E22", >= 30 => "#F39C12", >= 15 => "#3498DB", _ => "#27AE60" }
                };
            }).ToList();

            if (!string.IsNullOrWhiteSpace(SearchKeyword))
            {
                var kw = SearchKeyword.Trim();
                list = list.Where(i =>
                    (i.OrderNo?.Contains(kw) == true) ||
                    (i.CustomerName?.Contains(kw) == true) ||
                    (i.CustomerManagerName?.Contains(kw) == true)
                ).ToList();
            }

            Items = new ObservableCollection<ARItem>(list);
            TotalReceivable = list.Sum(i => i.UnpaidAmount);
            TotalOverdue30 = list.Where(i => i.DaysOverdue >= 30).Sum(i => i.UnpaidAmount);
            TotalOverdue60 = list.Where(i => i.DaysOverdue >= 60).Sum(i => i.UnpaidAmount);
            TotalOverdue90 = list.Where(i => i.DaysOverdue >= 90).Sum(i => i.UnpaidAmount);
            OverdueCount = list.Count(i => i.DaysOverdue >= 30);
        }
        catch (Exception ex) { ShowError($"加载失败: {ex.Message}"); }
        finally { IsLoading = false; }
    }

    [RelayCommand] private async Task RefreshAsync() => await LoadAsync();
    [RelayCommand] private async Task SearchAsync() => await LoadAsync();
}

public class ARItem
{
    public string OrderNo { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string CustomerManagerName { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public decimal ReceivedAmount { get; set; }
    public decimal UnpaidAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public int DaysOverdue { get; set; }
    public string StatusBadge { get; set; } = "";
    public string StatusColor { get; set; } = "#27AE60";
}
