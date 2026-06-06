using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;
using Serilog;
using System.Collections.ObjectModel;
using System.Windows;

namespace PRO.Desktop.ViewModels;

public partial class VisitRecordViewModel : ViewModelBase
{
    private readonly ProDbContext _db;
    private readonly int _customerId;

    [ObservableProperty] private ObservableCollection<VisitRecordItem> _records = new();
    [ObservableProperty] private VisitRecordItem? _selectedRecord;
    [ObservableProperty] private string? _editVisitType = "电话";
    [ObservableProperty] private string? _editPurpose;
    [ObservableProperty] private string? _editContent;
    [ObservableProperty] private string? _editCustomerDemand;
    [ObservableProperty] private string? _editCustomerProfile;
    [ObservableProperty] private string? _editResult;
    [ObservableProperty] private string? _editNextAction;
    [ObservableProperty] private DateTime? _editNextVisitDate;
    [ObservableProperty] private bool _isEditing;
    private int? _editId;

    public VisitRecordViewModel(int customerId)
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
            var list = await _db.VisitRecords
                .AsNoTracking()
                .Include(v => v.Visitor)
                .Where(v => v.CustomerId == _customerId)
                .OrderByDescending(v => v.VisitDate)
                .ToListAsync();
            Records = new ObservableCollection<VisitRecordItem>(list.Select(v => new VisitRecordItem
            {
                Id = v.Id, VisitDate = v.VisitDate, VisitType = v.VisitType, Purpose = v.Purpose,
                Content = v.Content, CustomerDemand = v.CustomerDemand,
                CustomerProfile = v.CustomerProfile, Result = v.Result,
                NextAction = v.NextAction, NextVisitDate = v.NextVisitDate,
                VisitorName = v.Visitor?.Name
            }));
        }
        catch (Exception ex) { Log.Error(ex, "拜访记录加载失败"); ShowError($"加载拜访记录失败: {ex.Message}"); }
        finally { IsLoading = false; }
    }

    [RelayCommand] private void New() { _editId = null; EditContent = null; EditPurpose = null; EditResult = null; EditCustomerDemand = null; EditCustomerProfile = null; EditNextAction = null; EditNextVisitDate = DateTime.Now.AddDays(7); EditVisitType = "电话"; IsEditing = true; }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(EditContent)) { ShowError("请输入拜访内容"); return; }
        try
        {
            if (_editId.HasValue)
            {
                var e = await _db.VisitRecords.FindAsync(_editId.Value);
                if (e != null) { e.VisitType = EditVisitType ?? "电话"; e.Purpose = EditPurpose; e.Content = EditContent; e.CustomerDemand = EditCustomerDemand; e.CustomerProfile = EditCustomerProfile; e.Result = EditResult; e.NextAction = EditNextAction; e.NextVisitDate = EditNextVisitDate; }
            }
            else
            {
                _db.VisitRecords.Add(new VisitRecord
                {
                    CustomerId = _customerId, VisitorId = CurrentSession.CurrentEmployeeId,
                    VisitType = EditVisitType ?? "电话", Purpose = EditPurpose, Content = EditContent,
                    CustomerDemand = EditCustomerDemand, CustomerProfile = EditCustomerProfile,
                    Result = EditResult, NextAction = EditNextAction, NextVisitDate = EditNextVisitDate,
                    VisitDate = DateTime.Now, CreatedAt = DateTime.Now
                });
            }
            await _db.SaveChangesAsync();
            ShowSuccess("拜访记录保存成功");
            IsEditing = false;
            await LoadAsync();
        }
        catch (Exception ex) { ShowError($"保存失败: {ex.Message}"); }
    }

    [RelayCommand] private void EditRecord(VisitRecordItem? r) { if (r != null) { _editId = r.Id; EditContent = r.Content; EditPurpose = r.Purpose; EditResult = r.Result; EditCustomerDemand = r.CustomerDemand; EditCustomerProfile = r.CustomerProfile; EditNextAction = r.NextAction; EditNextVisitDate = r.NextVisitDate; EditVisitType = r.VisitType; IsEditing = true; } }
    [RelayCommand] private void Cancel() => IsEditing = false;
}

public class VisitRecordItem
{
    public int Id { get; set; }
    public DateTime VisitDate { get; set; }
    public string VisitType { get; set; } = "电话";
    public string? Purpose { get; set; }
    public string? Content { get; set; }
    public string? CustomerDemand { get; set; }
    public string? CustomerProfile { get; set; }
    public string? Result { get; set; }
    public string? NextAction { get; set; }
    public DateTime? NextVisitDate { get; set; }
    public string? VisitorName { get; set; }
}
