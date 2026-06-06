using CommunityToolkit.Mvvm.ComponentModel; using CommunityToolkit.Mvvm.Input; using Microsoft.EntityFrameworkCore; using PRO.Domain.Enums; using PRO.Infrastructure.Persistence; using LiveChartsCore; using LiveChartsCore.SkiaSharpView; using LiveChartsCore.SkiaSharpView.Painting; using SkiaSharp; using System.Collections.ObjectModel;

namespace PRO.Desktop.ViewModels;
public partial class ReportCenterViewModel : ViewModelBase
{
    private readonly ProDbContext _db;
    [ObservableProperty] private ObservableCollection<ISeries> _revenueSeries = new();
    [ObservableProperty] private ObservableCollection<ISeries> _productSeries = new();
    [ObservableProperty] private Axis[] _xAxes = { new Axis { Labels = new[] { "1月", "2月", "3月", "4月", "5月", "6月" } } };
    [ObservableProperty] private Axis[] _yAxes = { new Axis { Name = "金额(元)" } };
    [ObservableProperty] private Axis[] _productXAxes = { new Axis { Labels = Array.Empty<string>() } };
    [ObservableProperty] private Axis[] _productYAxes = { new Axis { Name = "销量" } };

    public ReportCenterViewModel()
    {
        _db = App.Services.GetService(typeof(ProDbContext)) as ProDbContext ?? throw new();
        RunInBackground(LoadAsync(), "报表中心加载失败");
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var branchId = CurrentSession.CurrentBranchId;

            var sixMoAgo = DateTime.Now.AddMonths(-6);
            var orders = await _db.Orders.AsNoTracking().Where(o => o.BranchId == branchId && o.CreatedAt >= sixMoAgo && o.Status != OrderStatus.Cancelled).ToListAsync();
            var monthly = orders.GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month }).OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month).ToList();
            var labels = monthly.Any() ? monthly.Select(g => $"{g.Key.Month}月").ToArray() : new[] { "暂无数据" };

            RevenueSeries = new ObservableCollection<ISeries>
            {
                new ColumnSeries<double> { Values = monthly.Any() ? monthly.Select(g => (double)g.Sum(o => o.TotalAmount)).ToArray() : new double[] { 0 }, Fill = new SolidColorPaint(SKColors.DodgerBlue) }
            };
            XAxes = new[] { new Axis { Labels = labels } };

            var recentOrders = await _db.Orders.AsNoTracking().Where(o => o.BranchId == branchId && o.CreatedAt >= sixMoAgo).Select(o => o.Id).ToListAsync();
            var items = await _db.OrderItems.AsNoTracking().Where(i => recentOrders.Contains(i.OrderId)).Include(i => i.Product).GroupBy(i => i.Product!.Name).Select(g => new { Name = g.Key, Qty = g.Sum(i => i.Quantity) }).OrderByDescending(g => g.Qty).Take(10).ToListAsync();
            var productLabels = items.Any() ? items.Select(i => i.Name.Length > 6 ? i.Name[..6] + ".." : i.Name).ToArray() : new[] { "暂无数据" };
            ProductXAxes = new[] { new Axis { Labels = productLabels } };
            ProductSeries = new ObservableCollection<ISeries>
            {
                new ColumnSeries<double> { Values = items.Any() ? items.Select(i => (double)i.Qty).ToArray() : new double[] { 0 }, Fill = new SolidColorPaint(new SKColor(52, 152, 219)) }
            };
        }
        catch (Exception ex) { ShowError($"报表加载失败: {ex.Message}"); }
        finally { IsLoading = false; }
    }

    [RelayCommand] private async Task RefreshAsync() => await LoadAsync();
}
