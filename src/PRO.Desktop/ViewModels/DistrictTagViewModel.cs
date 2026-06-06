using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace PRO.Desktop.ViewModels;

public partial class DistrictTagViewModel : ViewModelBase
{
    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private BusinessDistrictViewModel _businessDistrictVm;

    [ObservableProperty]
    private TagViewModel _tagVm;

    [ObservableProperty]
    private ObservableCollection<string> _tabHeaders = new() { "商圈管理", "标签管理" };

    public DistrictTagViewModel()
    {
        BusinessDistrictVm = App.Services.GetService(typeof(BusinessDistrictViewModel)) as BusinessDistrictViewModel
            ?? new BusinessDistrictViewModel();
        TagVm = App.Services.GetService(typeof(TagViewModel)) as TagViewModel
            ?? new TagViewModel();
    }

    [RelayCommand]
    private void SelectTab(string tabIndex)
    {
        if (int.TryParse(tabIndex, out var index))
        {
            SelectedTabIndex = index;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (SelectedTabIndex == 0)
            await BusinessDistrictVm.RefreshCommand.ExecuteAsync(null);
        else
            await TagVm.RefreshCommand.ExecuteAsync(null);
    }
}