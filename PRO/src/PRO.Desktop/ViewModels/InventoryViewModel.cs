using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using PRO.Domain.Entities;
using PRO.Infrastructure.Persistence;
using PRO.Desktop.Prediction;
using PRO.Desktop.Controls;
using System.Collections.ObjectModel;
using System.Windows;

namespace PRO.Desktop.ViewModels;

public partial class InventoryViewModel : ViewModelBase
{
    private readonly ProDbContext _db;
    [ObservableProperty] private ObservableCollection<InventoryItem> _items = [];
    [ObservableProperty] private ObservableCollection<ReplenishItem> _replenishItems = [];
    [ObservableProperty] private ObservableCollection<WarehouseItem> _warehouses = [];
    [ObservableProperty] private WarehouseItem? _selectedWarehouse;
    [ObservableProperty] private string _searchKeyword = "";
    [ObservableProperty] private bool _isChecking;
    [ObservableProperty] private string _checkRemark = "";
    [ObservableProperty] private string _newWarehouseName = "";
    [ObservableProperty] private string _newWarehouseAddress = "";

    // 空状态支持
    [ObservableProperty] private EmptyStateViewModel? _emptyState;
    [ObservableProperty] private bool _showEmptyState;

    public InventoryViewModel()
    {
        _db = App.Services.GetService(typeof(ProDbContext)) as ProDbContext ?? throw new InvalidOperationException("无法获取数据库上下文");
        RunInBackground(InitAsync(), "初始化库存数据失败");
    }

    private void UpdateEmptyState()
    {
        if (IsLoading) { ShowEmptyState = false; return; }
        if (Items.Count > 0) { ShowEmptyState = false; return; }

        ShowEmptyState = true;
        if (!string.IsNullOrWhiteSpace(SearchKeyword))
            EmptyState = EmptyStateViewModel.CreateForSearchNoResults(SearchKeyword, SearchCommand);
        else if (SelectedWarehouse != null)
            EmptyState = EmptyStateViewModel.CreateForNoResults("库存", new RelayCommand(() => { SelectedWarehouse = null; RunInBackground(LoadAsync(), "清除筛选失败"); }));
        else
            EmptyState = EmptyStateViewModel.CreateForEmpty("产品库存");
    }

    private async Task InitAsync()
    {
        try
        {
            await LoadAsync();
            await LoadWarehousesAsync();
        }
        catch (Exception ex) { ShowBusinessException(ex, "初始化库存"); }
    }

    private async Task LoadWarehousesAsync()
    {
        try
        {
            var branchId = CurrentSession.CurrentBranchId;
            var list = await _db.Set<Warehouse>()
                .AsNoTracking()
                .Where(w => w.BranchId == branchId)
                .OrderBy(w => w.Name)
                .ToListAsync();
            Warehouses = new ObservableCollection<WarehouseItem>(list.Select(w => new WarehouseItem
            {
                Id = w.Id,
                Name = w.Name,
                Address = w.Address,
                Status = w.Status,
                CreatedAt = w.CreatedAt
            }));
        }
        catch (Exception ex) { ShowBusinessException(ex, "加载仓库"); }
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var branchId = CurrentSession.CurrentBranchId;
            var query = _db.Products.AsNoTracking().Where(p => p.Status == Domain.Enums.ProductStatus.Active);
            if (SelectedWarehouse != null)
            {
                query = query.Where(p => p.WarehouseId == SelectedWarehouse.Id);
            }
            if (!string.IsNullOrWhiteSpace(SearchKeyword))
            {
                query = query.Where(p => p.Name.Contains(SearchKeyword) || p.SKU!.Contains(SearchKeyword));
            }
            var products = await query.ToListAsync();

            var warehouseMap = await _db.Set<Warehouse>()
                .AsNoTracking()
                .Where(w => w.BranchId == branchId)
                .ToDictionaryAsync(w => w.Id, w => w.Name);

            Items = new ObservableCollection<InventoryItem>(products.Select(p => new InventoryItem
            {
                ProductId = p.Id,
                ProductName = p.Name,
                SKU = p.SKU,
                SystemStock = p.Stock,
                Unit = p.Unit,
                WarehouseName = p.WarehouseId.HasValue && warehouseMap.ContainsKey(p.WarehouseId.Value)
                    ? warehouseMap[p.WarehouseId.Value] : ""
            }));

            UpdateEmptyState();

            var engine = new PredictionEngine(_db);
            // 批量预测 — 消除 N+1 查询
            var demandPreds = await engine.PredictProductDemandBatchAsync(products.Take(50).ToList());
            var replenishList = new List<ReplenishItem>();
            foreach (var p in products.Take(50))
            {
                var pred = demandPreds.FirstOrDefault(x => x.ProductId == p.Id);
                if (pred != null && pred.PredictedSales > 0 && pred.SuggestedStock > p.Stock)
                {
                    replenishList.Add(new ReplenishItem { ProductId = p.Id, ProductName = p.Name, CurrentStock = p.Stock, PredictedDemand = pred.PredictedSales, SuggestedReplenish = (int)(pred.SuggestedStock - p.Stock), Confidence = pred.Confidence });
                }
            }
            ReplenishItems = new ObservableCollection<ReplenishItem>(replenishList.OrderByDescending(r => r.SuggestedReplenish).Take(20));
        }
        catch (Exception ex) { ShowBusinessException(ex, "加载库存"); ShowEmptyState = true; EmptyState = EmptyStateViewModel.CreateForLoadFailed(RefreshCommand); }
        finally { IsLoading = false; }
    }

    [RelayCommand] private async Task RefreshAsync() => await LoadAsync();
    [RelayCommand] private async Task SearchAsync() => await LoadAsync();
    [RelayCommand] private void StartCheck() { IsChecking = true; }

    [RelayCommand]
    private async Task SubmitCheckAsync()
    {
        try
        {
            foreach (var item in Items.Where(i => i.ActualStock.HasValue))
            {
                _db.InventoryChecks.Add(new InventoryCheck
                { BranchId = CurrentSession.CurrentBranchId, ProductId = item.ProductId, SystemStock = item.SystemStock, ActualStock = item.ActualStock!.Value, Variance = item.ActualStock.Value - item.SystemStock, Remark = CheckRemark, CheckedById = CurrentSession.CurrentEmployeeId, CreatedAt = DateTime.Now });
                var product = await _db.Products.FindAsync(item.ProductId);
                if (product != null) { product.Stock = item.ActualStock.Value; }
            }
            await _db.SaveChangesAsync();
            ShowSuccess("盘点提交成功");
            IsChecking = false;
            await LoadAsync();
        }
        catch (Exception ex) { ShowError($"盘点失败: {ex.Message}"); }
    }

    [RelayCommand] private void CancelCheck() => IsChecking = false;

    [RelayCommand]
    private async Task AddWarehouseAsync()
    {
        if (string.IsNullOrWhiteSpace(NewWarehouseName)) { ShowError("请输入仓库名称"); return; }
        try
        {
            _db.Set<Warehouse>().Add(new Warehouse
            {
                Name = NewWarehouseName,
                Address = NewWarehouseAddress,
                BranchId = CurrentSession.CurrentBranchId,
                Status = "Active",
                CreatedAt = DateTime.Now
            });
            await _db.SaveChangesAsync();
            ShowSuccess("仓库添加成功");
            NewWarehouseName = ""; NewWarehouseAddress = "";
            await LoadWarehousesAsync();
        }
        catch (Exception ex) { ShowError($"添加失败: {ex.Message}"); }
    }

    [RelayCommand]
    private async Task EditWarehouseAsync(WarehouseItem? item)
    {
        if (item == null) return;
        var wh = await _db.Set<Warehouse>().FindAsync(item.Id);
        if (wh == null) return;

        var name = Microsoft.VisualBasic.Interaction.InputBox("仓库名称:", "编辑仓库", wh.Name);
        if (string.IsNullOrWhiteSpace(name)) return;
        var addr = Microsoft.VisualBasic.Interaction.InputBox("仓库地址:", "编辑仓库", wh.Address ?? "");

        wh.Name = name;
        wh.Address = addr;
        await _db.SaveChangesAsync();
        ShowSuccess("仓库编辑成功");
        await LoadWarehousesAsync();
    }

    [RelayCommand]
    private async Task DeleteWarehouseAsync(WarehouseItem? item)
    {
        if (item == null) return;
        var result = MessageBox.Show($"确定删除仓库 \"{item.Name}\" 吗?", "确认删除",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        try
        {
            var wh = await _db.Set<Warehouse>().FindAsync(item.Id);
            if (wh != null)
            {
                wh.Status = "Deleted";
                await _db.SaveChangesAsync();
            }
            ShowSuccess("仓库已删除");
            await LoadWarehousesAsync();
        }
        catch (Exception ex) { ShowError($"删除失败: {ex.Message}"); }
    }
}

public class WarehouseItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Address { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime CreatedAt { get; set; }
}

public class InventoryItem
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string? SKU { get; set; }
    public int SystemStock { get; set; }
    public int? ActualStock { get; set; }
    public string? Unit { get; set; }
    public string WarehouseName { get; set; } = "";
    public string VarianceText => ActualStock.HasValue ? (ActualStock.Value - SystemStock).ToString() : "";
}

public class ReplenishItem
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public int CurrentStock { get; set; }
    public double PredictedDemand { get; set; }
    public int SuggestedReplenish { get; set; }
    public double Confidence { get; set; }
}
