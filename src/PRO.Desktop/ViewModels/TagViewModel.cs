using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using PRO.Domain.Entities;
using PRO.Infrastructure.Persistence;
using Serilog;
using System.Collections.ObjectModel;
namespace PRO.Desktop.ViewModels;
public partial class TagViewModel : ViewModelBase
{
    private readonly ProDbContext _db;
    [ObservableProperty] private ObservableCollection<CustomerTag> _tags = [];
    [ObservableProperty] private string _newTagName = "";
    [ObservableProperty] private string _newTagColor = "#007AFF";

    public TagViewModel() { _db = App.Services.GetService(typeof(ProDbContext)) as ProDbContext ?? throw new(); RunInBackground(LoadAsync(), "加载标签失败"); }

    private async Task LoadAsync() { try { Tags = new ObservableCollection<CustomerTag>(await _db.CustomerTags.Where(t => t.BranchId == null || t.BranchId == CurrentSession.CurrentBranchId).OrderBy(t => t.Name).ToListAsync()); } catch (Exception ex) { Log.Error(ex, "标签加载失败"); } }

    [RelayCommand] private async Task RefreshAsync() => await LoadAsync();

    [RelayCommand] private async Task AddTagAsync() { if (string.IsNullOrWhiteSpace(NewTagName)) return; try { _db.CustomerTags.Add(new CustomerTag { Name = NewTagName, Color = NewTagColor, BranchId = CurrentSession.CurrentBranchId }); await _db.SaveChangesAsync(); NewTagName = ""; await LoadAsync(); ShowSuccess("标签已创建"); } catch (Exception ex) { ShowError(ex.Message); } }

    [RelayCommand] private async Task DeleteTagAsync(CustomerTag? t) { if (t == null) return; _db.CustomerTags.Remove(t); await _db.SaveChangesAsync(); await LoadAsync(); }
}
