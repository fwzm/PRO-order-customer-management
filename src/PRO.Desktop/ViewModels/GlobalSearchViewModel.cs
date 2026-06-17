using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRO.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using Serilog;

namespace PRO.Desktop.ViewModels;

/// <summary>
/// 全局搜索 ViewModel - 使用 Desktop GlobalSearchService
/// </summary>
public partial class GlobalSearchViewModel : ViewModelBase
{
    private readonly GlobalSearchService _searchService;

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    [ObservableProperty]
    private ObservableCollection<SearchResult> _results = [];

    [ObservableProperty]
    private SearchResult? _selectedItem;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private string _resultCountText = "输入关键词搜索客户、订单、产品...";

    /// <summary>搜索完成事件：参数为选中的结果项</summary>
    public event Action<SearchResult>? SearchItemSelected;

    private CancellationTokenSource? _searchCts;

    public GlobalSearchViewModel()
    {
        _searchService = App.Services.GetService(typeof(GlobalSearchService)) as GlobalSearchService
            ?? throw new InvalidOperationException("无法获取搜索服务");
    }

    partial void OnSearchKeywordChanged(string value)
    {
        _searchCts?.Cancel();
        if (string.IsNullOrWhiteSpace(value) || value.Length < 2)
        {
            Results.Clear();
            ResultCountText = "输入至少2个字符开始搜索";
            return;
        }

        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;
        _ = SearchDebouncedAsync(value, token);
    }

    private async Task SearchDebouncedAsync(string keyword, CancellationToken token)
    {
        try
        {
            await Task.Delay(300, token); // 300ms 防抖
            if (token.IsCancellationRequested) return;

            IsSearching = true;
            var items = await _searchService.SearchAsync(keyword, CurrentSession.CurrentBranchId);

            if (token.IsCancellationRequested) return;

            Results = new ObservableCollection<SearchResult>(items);
            ResultCountText = items.Count > 0
                ? $"找到 {items.Count} 个结果"
                : "未找到匹配结果";
        }
        catch (OperationCanceledException) { /* 防抖取消是正常行为，无需处理 */ }
        catch (Exception ex)
        {
            Log.Error(ex, "全局搜索失败: {Keyword}", keyword);
            ResultCountText = $"搜索出错：{ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }

    [RelayCommand]
    private void SelectItem(SearchResult? item)
    {
        if (item == null) return;
        SearchItemSelected?.Invoke(item);
    }
}
