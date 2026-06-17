using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace PRO.Desktop.ViewModels;

public partial class VisitOpportunityViewModel : ViewModelBase
{
    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private WeChatVisitSyncViewModel _visitSyncVm;

    [ObservableProperty]
    private OpportunityViewModel _opportunityVm;

    [ObservableProperty]
    private ObservableCollection<string> _tabHeaders = ["拜访同步", "商机管理"];

    public VisitOpportunityViewModel()
    {
        VisitSyncVm = App.Services.GetService(typeof(WeChatVisitSyncViewModel)) as WeChatVisitSyncViewModel
            ?? new WeChatVisitSyncViewModel();
        OpportunityVm = App.Services.GetService(typeof(OpportunityViewModel)) as OpportunityViewModel
            ?? new OpportunityViewModel();
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
            await VisitSyncVm.RefreshCommand.ExecuteAsync(null);
        else
            await OpportunityVm.RefreshCommand.ExecuteAsync(null);
    }
}