using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;
using System.Collections.ObjectModel;

namespace PRO.Desktop.ViewModels;

public partial class OpportunityViewModel : ViewModelBase
{
    private readonly ProDbContext _db;

    [ObservableProperty] private ObservableCollection<OpportunityItem> _opportunities = [];
    [ObservableProperty] private OpportunityItem? _selectedOpportunity;
    [ObservableProperty] private ObservableCollection<CustomerItem> _customers = [];
    [ObservableProperty] private CustomerItem? _selectedCustomer;
    [ObservableProperty] private ObservableCollection<EmployeeItem> _employees = [];
    [ObservableProperty] private EmployeeItem? _selectedDeveloper;
    [ObservableProperty] private ObservableCollection<ProductItem> _products = [];
    [ObservableProperty] private string _searchKeyword = string.Empty;
    [ObservableProperty] private OpportunityStage? _filterStage;

    // 编辑字段
    [ObservableProperty] private string? _editTitle;
    [ObservableProperty] private OpportunityStage _editStage = OpportunityStage.Trial;
    [ObservableProperty] private DateTime? _editExpectedCloseDate;
    [ObservableProperty] private decimal? _editExpectedAmount;
    [ObservableProperty] private string? _editRequirements;
    [ObservableProperty] private string? _editTrialProducts;
    [ObservableProperty] private string? _editIntendedProducts;
    [ObservableProperty] private string? _editNotes;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private bool _isBoardView = true;
    [ObservableProperty] private string _selectedStage = "";
    private int? _editId;

    // 看板分组
    [ObservableProperty] private ObservableCollection<OpportunityItem> _stageTrial = [];
    [ObservableProperty] private ObservableCollection<OpportunityItem> _stageComms = [];
    [ObservableProperty] private ObservableCollection<OpportunityItem> _stageQuote = [];
    [ObservableProperty] private ObservableCollection<OpportunityItem> _stageWon = [];
    [ObservableProperty] private ObservableCollection<OpportunityItem> _stageLost = [];

    // 统计
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private decimal _totalExpected;
    [ObservableProperty] private int _wonCount;
    [ObservableProperty] private decimal _wonAmount;

    public OpportunityViewModel()
    {
        _db = App.Services.GetService(typeof(ProDbContext)) as ProDbContext
            ?? throw new InvalidOperationException("无法获取数据库上下文");
        RunInBackground(LoadAsync(), "商机加载失败");
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var branchId = CurrentSession.CurrentBranchId;
            var query = _db.Opportunities
                .Include(o => o.Customer!).ThenInclude(c => c.CustomerManager)
                .Include(o => o.Developer)
                .Where(o => o.BranchId == branchId)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchKeyword))
                query = query.Where(o => o.Title.Contains(SearchKeyword) || (o.Customer != null && o.Customer.Name.Contains(SearchKeyword)));
            if (FilterStage.HasValue)
                query = query.Where(o => o.Stage == FilterStage.Value);

            var list = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();
            var items = list.Select(o => new OpportunityItem
            {
                Id = o.Id,
                Title = o.Title,
                CustomerId = o.CustomerId,
                CustomerName = o.Customer?.Name,
                Stage = o.Stage,
                ExpectedCloseDate = o.ExpectedCloseDate,
                ExpectedAmount = o.ExpectedAmount,
                Requirements = o.Requirements,
                DeveloperName = o.Developer?.Name,
                CustomerManagerName = o.Customer?.CustomerManager?.Name ?? o.Developer?.Name ?? "",
                CreatedAt = o.CreatedAt
            }).ToList();

            Opportunities = new ObservableCollection<OpportunityItem>(items);
            StageTrial = new ObservableCollection<OpportunityItem>(items.Where(x => x.Stage == OpportunityStage.Trial));
            StageComms = new ObservableCollection<OpportunityItem>(items.Where(x => x.Stage == OpportunityStage.Communication));
            StageQuote = new ObservableCollection<OpportunityItem>(items.Where(x => x.Stage is OpportunityStage.Quotation or OpportunityStage.Negotiation));
            StageWon = new ObservableCollection<OpportunityItem>(items.Where(x => x.Stage == OpportunityStage.Won));
            StageLost = new ObservableCollection<OpportunityItem>(items.Where(x => x.Stage == OpportunityStage.Lost));

            TotalCount = items.Count;
            TotalExpected = items.Sum(i => i.ExpectedAmount ?? 0);
            WonCount = StageWon.Count;
            WonAmount = StageWon.Sum(i => i.ExpectedAmount ?? 0);

            // 加载客户和员工列表
            var customers = await _db.Customers.Where(c => c.BranchId == branchId && c.Status == CustomerStatus.Active).OrderBy(c => c.Name).ToListAsync();
            Customers = new ObservableCollection<CustomerItem>(customers.Select(c => new CustomerItem { Id = c.Id, Name = c.Name, CustomerNo = c.CustomerNo }));

            var employees = await _db.Employees.Where(e => e.BranchId == branchId && e.Status == EmployeeStatus.Active).OrderBy(e => e.Name).ToListAsync();
            Employees = new ObservableCollection<EmployeeItem>(employees.Select(e => new EmployeeItem { Id = e.Id, Name = e.Name }));
        }
        catch (Exception ex) { ShowError($"加载失败: {ex.Message}"); }
        finally { IsLoading = false; }
    }

    [RelayCommand] private async Task RefreshAsync() => await LoadAsync();
    [RelayCommand] private async Task SearchAsync() => await LoadAsync();

    [RelayCommand]
    private void NewOpportunity()
    {
        _editId = null; EditTitle = null; EditStage = OpportunityStage.Trial;
        EditExpectedCloseDate = DateTime.Now.AddDays(30); EditExpectedAmount = null;
        EditRequirements = null; EditTrialProducts = null; EditIntendedProducts = null;
        EditNotes = null; SelectedCustomer = null; SelectedDeveloper = null;
        IsEditing = true;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(EditTitle)) { ShowError("请输入商机标题"); return; }
        if (SelectedCustomer == null) { ShowError("请选择客户"); return; }

        try
        {
            if (_editId.HasValue)
            {
                var entity = await _db.Opportunities.FindAsync(_editId.Value);
                if (entity != null)
                {
                    entity.Title = EditTitle; entity.Stage = EditStage;
                    entity.ExpectedCloseDate = EditExpectedCloseDate; entity.ExpectedAmount = EditExpectedAmount ?? 0;
                    entity.Requirements = EditRequirements; entity.TrialProducts = EditTrialProducts;
                    entity.IntendedProducts = EditIntendedProducts; entity.Notes = EditNotes;
                    entity.CustomerId = SelectedCustomer.Id;
                    if (SelectedDeveloper != null) entity.DeveloperId = SelectedDeveloper.Id;
                    entity.UpdatedAt = DateTime.Now;
                }
            }
            else
            {
                _db.Opportunities.Add(new Opportunity
                {
                    Title = EditTitle,
                    Stage = EditStage,
                    ExpectedCloseDate = EditExpectedCloseDate,
                    ExpectedAmount = EditExpectedAmount ?? 0,
                    Requirements = EditRequirements,
                    TrialProducts = EditTrialProducts,
                    IntendedProducts = EditIntendedProducts,
                    Notes = EditNotes,
                    CustomerId = SelectedCustomer.Id,
                    DeveloperId = SelectedDeveloper?.Id ?? CurrentSession.CurrentEmployeeId,
                    BranchId = CurrentSession.CurrentBranchId,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                });
            }
            await _db.SaveChangesAsync();
            ShowSuccess("商机保存成功");
            IsEditing = false;
            await LoadAsync();
        }
        catch (Exception ex) { ShowError($"保存失败: {ex.Message}"); }
    }

    [RelayCommand]
    private void EditOpportunity(OpportunityItem? item)
    {
        if (item == null) return;
        _editId = item.Id; EditTitle = item.Title; EditStage = item.Stage;
        EditExpectedCloseDate = item.ExpectedCloseDate; EditExpectedAmount = item.ExpectedAmount;
        EditRequirements = item.Requirements;
        SelectedCustomer = Customers.FirstOrDefault(c => c.Id == item.CustomerId);
        IsEditing = true;
    }

    [RelayCommand]
    private async Task ChangeStageAsync(string stageStr)
    {
        if (SelectedOpportunity == null || string.IsNullOrWhiteSpace(stageStr)) return;
        if (!Enum.TryParse<OpportunityStage>(stageStr, out var stage)) return;
        try
        {
            var entity = await _db.Opportunities.FindAsync(SelectedOpportunity.Id);
            if (entity != null) { entity.Stage = stage; entity.UpdatedAt = DateTime.Now; await _db.SaveChangesAsync(); }
            await LoadAsync();
        }
        catch (Exception ex) { ShowError($"更新失败: {ex.Message}"); }
    }

    [RelayCommand] private void CancelEdit() => IsEditing = false;
    [RelayCommand] private void ToggleView() => IsBoardView = !IsBoardView;
}

public class OpportunityItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public OpportunityStage Stage { get; set; }
    public string StageName => Stage switch
    {
        OpportunityStage.Trial => "试用",
        OpportunityStage.Communication => "沟通",
        OpportunityStage.Quotation => "报价",
        OpportunityStage.Negotiation => "谈判",
        OpportunityStage.Won => "成交",
        OpportunityStage.Lost => "流失",
        _ => ""
    };
    public DateTime? ExpectedCloseDate { get; set; }
    public decimal? ExpectedAmount { get; set; }
    public string? Requirements { get; set; }
    public string? DeveloperName { get; set; }
    public string CustomerManagerName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class CustomerItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? CustomerNo { get; set; }
}

public class EmployeeItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class ProductItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
