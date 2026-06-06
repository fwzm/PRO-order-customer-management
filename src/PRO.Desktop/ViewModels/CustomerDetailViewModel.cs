using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;
using PRO.Desktop.Prediction;
using System.Collections.ObjectModel;
using System.Windows;

namespace PRO.Desktop.ViewModels;

public partial class CustomerDetailViewModel : ViewModelBase
{
    private readonly ProDbContext _db;
    private readonly int _customerId;
    private Window? _owningWindow;

    [ObservableProperty] private Customer? _customer;
    [ObservableProperty] private string _customerNo = "", _customerName = "", _customerTypeStr = "", _customerPhone = "", _customerAddress = "", _customerManagerName = "未分配", _customerCreatorName = "";
    [ObservableProperty] private ObservableCollection<OrderSummary> _recentOrders = new();
    [ObservableProperty] private ObservableCollection<VisitRecordItem> _recentVisits = new();
    [ObservableProperty] private ObservableCollection<OpportunityItem> _opportunities = new();
    [ObservableProperty] private ObservableCollection<CustomerPriceItem> _customerPrices = new();
    [ObservableProperty] private int _totalOrders;
    [ObservableProperty] private decimal _totalAmount;
    [ObservableProperty] private decimal _totalReceivable;
    [ObservableProperty] private double _avgInterval;
    [ObservableProperty] private double _predictedAmount;
    [ObservableProperty] private double _predictedConfidence;
    [ObservableProperty] private string _churnRisk = "无";
    [ObservableProperty] private ObservableCollection<EmployeeItem> _employeeList = new();
    [ObservableProperty] private EmployeeItem? _selectedManager;

    public Action? OnCustomerUpdated;

    public CustomerDetailViewModel(int customerId)
    {
        _customerId = customerId;
        _db = App.Services.GetService(typeof(ProDbContext)) as ProDbContext ?? throw new InvalidOperationException("无法获取数据库上下文");
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var c = await _db.Customers.Include(x => x.Creator).Include(x => x.CustomerManager).Include(x => x.BusinessDistrict).AsNoTracking().FirstOrDefaultAsync(x => x.Id == _customerId);
            if (c == null) return;
            Customer = c; CustomerNo = c.CustomerNo ?? ""; CustomerName = c.Name;
            CustomerTypeStr = c.CustomerType == CustomerType.Major ? "大客户" : "细分客户";
            CustomerPhone = c.Phone ?? ""; CustomerAddress = c.Address ?? "";
            CustomerManagerName = c.CustomerManager?.Name ?? "未分配";
            CustomerCreatorName = c.Creator?.Name ?? "";

            var orders = await _db.Orders.Where(o => o.CustomerId == _customerId).OrderByDescending(o => o.CreatedAt).ToListAsync();
            TotalOrders = orders.Count; TotalAmount = orders.Sum(o => o.TotalAmount);
            TotalReceivable = orders.Where(o => o.PaymentStatus != PaymentStatus.Paid).Sum(o => o.TotalAmount - o.ReceivedAmount);
            RecentOrders = new ObservableCollection<OrderSummary>(orders.Take(10).Select(o => new OrderSummary
            { OrderNo = o.OrderNo, TotalAmount = o.TotalAmount, PaymentStatus = o.PaymentStatus switch { PaymentStatus.Paid => "已收款", PaymentStatus.Unpaid => "未收款", PaymentStatus.Legal => "法务", _ => o.PaymentStatus.ToString() }, CreatedAt = o.CreatedAt }));

            if (orders.Count >= 2)
            { var intervals = new List<double>(); for (int i = 0; i < orders.Count - 1; i++) { var d = (orders[i].CreatedAt - orders[i + 1].CreatedAt).TotalDays; if (d > 0 && d < 365) intervals.Add(d); } AvgInterval = intervals.Any() ? intervals.Average() : 0; }

            var visits = await _db.VisitRecords.Include(v => v.Visitor).Where(v => v.CustomerId == _customerId).OrderByDescending(v => v.VisitDate).Take(10).ToListAsync();
            RecentVisits = new ObservableCollection<VisitRecordItem>(visits.Select(v => new VisitRecordItem { Id = v.Id, VisitDate = v.VisitDate, VisitType = v.VisitType, Purpose = v.Purpose, Content = v.Content, CustomerDemand = v.CustomerDemand, NextAction = v.NextAction, VisitorName = v.Visitor?.Name }));

            var opps = await _db.Opportunities.Where(o => o.CustomerId == _customerId).OrderByDescending(o => o.CreatedAt).ToListAsync();
            Opportunities = new ObservableCollection<OpportunityItem>(opps.Select(o => new OpportunityItem { Id = o.Id, Title = o.Title, Stage = o.Stage, ExpectedAmount = o.ExpectedAmount, CreatedAt = o.CreatedAt }));

            var prices = await _db.CustomerPrices.Include(p => p.Product).Where(p => p.CustomerId == _customerId).ToListAsync();
            CustomerPrices = new ObservableCollection<CustomerPriceItem>(prices.Select(p => new CustomerPriceItem { ProductName = p.Product?.Name ?? "", Price = p.Price }));

            // 预测
            try { var engine = new PredictionEngine(_db); var pred = await engine.PredictCustomerOrderAsync(_customerId); PredictedAmount = pred.PredictedAmount; PredictedConfidence = pred.ConfidenceScore; }
            catch { PredictedAmount = 0; }

            // 流失风险
            if (orders.Any()) { var last = orders.First().CreatedAt; if ((DateTime.Now - last).TotalDays > AvgInterval * 2 && AvgInterval > 0) ChurnRisk = "高"; else if ((DateTime.Now - last).TotalDays > AvgInterval * 1.5) ChurnRisk = "中"; }

            var emps = await _db.Employees.Where(e => e.BranchId == c.BranchId && e.Status == EmployeeStatus.Active).OrderBy(e => e.Name).ToListAsync();
            EmployeeList = new ObservableCollection<EmployeeItem>(emps.Select(e => new EmployeeItem { Id = e.Id, Name = e.Name }));
            SelectedManager = EmployeeList.FirstOrDefault(e => e.Id == c.CustomerManagerId);
        }
        catch (Exception ex) { ShowError($"加载失败: {ex.Message}"); }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task ChangeManagerAsync()
    {
        if (SelectedManager == null) return;
        try
        {
            var c = await _db.Customers.FindAsync(_customerId);
            if (c != null) { c.CustomerManagerId = SelectedManager.Id; c.UpdatedAt = DateTime.Now; await _db.SaveChangesAsync(); CustomerManagerName = SelectedManager.Name; ShowSuccess("客户担当已更新"); OnCustomerUpdated?.Invoke(); }
        }
        catch (Exception ex) { ShowError($"更新失败: {ex.Message}"); }
    }

    [RelayCommand] private async Task RefreshAsync() => await LoadAsync();

    public void SetOwningWindow(Window window) => _owningWindow = window;

    [RelayCommand]
    private void EditCustomer()
    {
        var editVm = App.Services.GetService(typeof(CustomerEditViewModel)) as CustomerEditViewModel
            ?? throw new InvalidOperationException("无法创建编辑视图模型");
        editVm.LoadCustomer(_customerId);
        editVm.OnSaveCompleted = async () => { await LoadAsync(); OnCustomerUpdated?.Invoke(); };
        var dialog = new Views.CustomerEditWindow(editVm) { Owner = _owningWindow ?? System.Windows.Application.Current.MainWindow };
        dialog.ShowDialog();
    }

    [RelayCommand]
    private void GoBack()
    {
        _owningWindow?.Close();
    }
}

public class OrderSummary
{
    public string OrderNo { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public string PaymentStatus { get; set; } = "";
    public decimal ReceivedAmount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CustomerPriceItem
{
    public string ProductName { get; set; } = "";
    public decimal Price { get; set; }
}
