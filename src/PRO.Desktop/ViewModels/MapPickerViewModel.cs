using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Net.Http;
using PRO.Desktop.Services;

namespace PRO.Desktop.ViewModels;

/// <summary>
/// 腾讯地图选点弹出窗口 ViewModel
/// 仅负责用户交互，HTTP 调用委托给 IMapService
/// </summary>
public partial class MapPickerViewModel : ViewModelBase
{
    private readonly IMapService _mapService;
    private CancellationTokenSource? _searchCts;

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    [ObservableProperty]
    private ObservableCollection<MapPoiItem> _searchResults = [];

    [ObservableProperty]
    private MapPoiItem? _selectedResult;

    [ObservableProperty]
    private bool _isSearching;

    public event Action<MapPoiItem>? LocationSelected;

    public MapPickerViewModel()
    {
        _mapService = App.Services.GetRequiredService<IMapService>();
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchKeyword))
        {
            ShowError("请输入搜索关键词");
            return;
        }

        if (!_mapService.IsConfigured)
        {
            ShowError("腾讯地图 API Key 未配置，请联系管理员在 appsettings.json 中设置 TencentMap:ApiKey");
            return;
        }

        // 取消上一次搜索
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        IsSearching = true;
        try
        {
            // 并行执行地理编码和 POI 搜索
            var geocodeTask = _mapService.GeocodeAsync(SearchKeyword, ct);
            var poiTask = _mapService.SearchPoiAsync(SearchKeyword, ct);

            await Task.WhenAll(geocodeTask, poiTask);

            if (ct.IsCancellationRequested) return;

            var geocodeResult = await geocodeTask;
            var poiResults = await poiTask;

            var allResults = new List<MapPoiItem>();
            if (geocodeResult != null)
                allResults.Add(geocodeResult);
            allResults.AddRange(poiResults);

            if (allResults.Count == 0)
            {
                ShowError("未找到匹配的位置");
            }
            else
            {
                SearchResults = new ObservableCollection<MapPoiItem>(allResults);
            }
        }
        catch (TaskCanceledException)
        {
            ShowError("搜索超时，请稍后重试");
        }
        catch (HttpRequestException ex)
        {
            ShowError($"网络请求失败，请检查网络连接: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            // 搜索被取消，忽略
        }
        catch (Exception ex)
        {
            ShowError($"搜索失败: {ex.Message}");
        }
        finally
        {
            IsSearching = false;
        }
    }

    [RelayCommand]
    private void ConfirmSelection()
    {
        if (SelectedResult == null)
        {
            ShowError("请先选择一个位置");
            return;
        }
        LocationSelected?.Invoke(SelectedResult);
    }

    [RelayCommand]
    private void Cancel()
    {
        _searchCts?.Cancel();
        // 由窗口关闭处理
    }
}
