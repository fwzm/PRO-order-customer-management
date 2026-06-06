using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRO.Domain.Entities;
using PRO.Infrastructure.Common;
using PRO.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Collections.ObjectModel;
using System.Windows;

namespace PRO.Desktop.ViewModels;

/// <summary>
/// 商圈管理 ViewModel
/// </summary>
public partial class BusinessDistrictViewModel : ViewModelBase
{
    private readonly ProDbContext _dbContext;

    [ObservableProperty]
    private ObservableCollection<BusinessDistrict> _businessDistricts = new();

    [ObservableProperty]
    private BusinessDistrict? _selectedItem;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _formTitle = "新增商圈";

    [ObservableProperty]
    private string _editName = string.Empty;

    [ObservableProperty]
    private string _editCity = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _cities = new();

    private int? _editingId;

    public BusinessDistrictViewModel()
    {
        _dbContext = App.Services.GetService(typeof(ProDbContext)) as ProDbContext
            ?? throw new InvalidOperationException("无法获取数据库上下文");
        _ = LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            int branchId = CurrentSession.CurrentBranchId;
            var list = await _dbContext.BusinessDistricts
                .Where(b => b.BranchId == null || b.BranchId == branchId)
                .OrderBy(b => b.Name)
                .ToListAsync();
            BusinessDistricts = new ObservableCollection<BusinessDistrict>(list);

            Cities = new ObservableCollection<string>(ChinaDivisionData.Provinces
                .SelectMany(p => ChinaDivisionData.GetCities(p))
                .Distinct()
                .OrderBy(c => c));
        }
        catch (Exception ex) { Log.Error(ex, "商圈数据加载失败"); }
    }

    [RelayCommand]
    private void NewBusinessDistrict()
    {
        _editingId = null;
        EditName = string.Empty;
        EditCity = string.Empty;
        FormTitle = "新增商圈";
        IsEditing = true;
    }

    [RelayCommand]
    private void Edit(BusinessDistrict item)
    {
        if (item == null) return;
        _editingId = item.Id;
        EditName = item.Name;
        EditCity = item.City;
        FormTitle = "编辑商圈";
        IsEditing = true;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadDataAsync();

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(EditName))
        {
            ShowError("请输入商圈名称");
            return;
        }

        try
        {
            int branchId = CurrentSession.CurrentBranchId;

            if (_editingId.HasValue)
            {
                var entity = await _dbContext.BusinessDistricts.FindAsync(_editingId.Value);
                if (entity != null)
                {
                    entity.Name = EditName;
                    entity.City = EditCity;
                }
            }
            else
            {
                _dbContext.BusinessDistricts.Add(new BusinessDistrict
                {
                    Name = EditName,
                    City = EditCity,
                    BranchId = branchId,
                    Status = "Active",
                    CreatedAt = DateTime.Now
                });
            }

            await _dbContext.SaveChangesAsync();
            ShowSuccess("保存成功");
            IsEditing = false;
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            ShowError($"保存失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(BusinessDistrict item)
    {
        if (item == null) return;
        var result = MessageBox.Show($"确定要删除商圈「{item.Name}」吗？", "确认删除",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        try
        {
            _dbContext.BusinessDistricts.Remove(item);
            await _dbContext.SaveChangesAsync();
            ShowSuccess("删除成功");
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            ShowError($"删除失败: {ex.Message}");
        }
    }
}
