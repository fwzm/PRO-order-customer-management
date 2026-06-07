using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace PRO.Desktop.ViewModels;

/// <summary>
/// 腾讯地图选点弹出窗口 ViewModel
/// </summary>
public partial class MapPickerViewModel : ViewModelBase
{
    private readonly HttpClient _httpClient;
    private const string ApiKey = "REDACTED_TENCENT_MAP_API_KEY";
    private const string BaseUrl = "https://apis.map.qq.com";

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    [ObservableProperty]
    private ObservableCollection<MapPoiItem> _searchResults = new();

    [ObservableProperty]
    private MapPoiItem? _selectedResult;

    [ObservableProperty]
    private bool _isSearching;

    public event Action<MapPoiItem>? LocationSelected;

    public MapPickerViewModel()
    {
        _httpClient = new HttpClient();
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchKeyword))
        {
            ShowError("请输入搜索关键词");
            return;
        }

        IsSearching = true;
        try
        {
            // 先尝试地理编码
            var geocodeResult = await GeocodeAsync(SearchKeyword);
            if (geocodeResult != null)
            {
                SearchResults = new ObservableCollection<MapPoiItem>(new[] { geocodeResult });
            }

            // 再搜索POI
            var poiResults = await SearchPoiAsync(SearchKeyword);
            if (poiResults.Count > 0)
            {
                if (geocodeResult != null)
                    poiResults.Insert(0, geocodeResult);
                SearchResults = new ObservableCollection<MapPoiItem>(poiResults);
            }
            else if (geocodeResult == null)
            {
                ShowError("未找到匹配的位置");
            }
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
        // 由窗口关闭处理
    }

    /// <summary>地理编码：地址转坐标</summary>
    private async Task<MapPoiItem?> GeocodeAsync(string address)
    {
        var url = $"{BaseUrl}/ws/geocoder/v1?address={Uri.EscapeDataString(address)}&key={ApiKey}";
        var json = await _httpClient.GetStringAsync(url);
        var result = JsonSerializer.Deserialize<GeocodeResponse>(json);
        if (result?.Status != 0 || result.Result?.Location == null) return null;

        var loc = result.Result.Location;
        var comp = result.Result.AddressComponent;
        return new MapPoiItem
        {
            Title = result.Result.Title ?? address,
            Address = result.Result.Address ?? address,
            Province = comp?.Province ?? "",
            City = comp?.City ?? "",
            District = comp?.District ?? "",
            Latitude = loc.Lat,
            Longitude = loc.Lng,
            Source = "地理编码"
        };
    }

    /// <summary>POI搜索（自动包含中国区域边界）</summary>
    private async Task<List<MapPoiItem>> SearchPoiAsync(string keyword)
    {
        // 使用 region 参数限定搜索范围为中国，配合 boundary=nearby 覆盖全国范围
        var encodedKeyword = Uri.EscapeDataString(keyword);
        // 先用大范围全国boundary，确保绝大多数搜索都能有结果
        var url = $"{BaseUrl}/ws/place/v1/search?keyword={encodedKeyword}&key={ApiKey}&boundary=nearby(35,105,1000000)&page_size=20";
        var json = await _httpClient.GetStringAsync(url);
        var result = JsonSerializer.Deserialize<PoiSearchResponse>(json);
        if (result?.Status != 0 || result.Data == null) return new();

        return result.Data.Select(d => new MapPoiItem
        {
            Title = d.Title ?? "",
            Address = d.Address ?? "",
            Province = d.Province ?? "",
            City = d.City ?? "",
            District = d.District ?? "",
            Latitude = d.Location?.Lat ?? 0,
            Longitude = d.Location?.Lng ?? 0,
            Source = "POI搜索"
        }).ToList();
    }
}

public class MapPoiItem
{
    public string Title { get; set; } = "";
    public string Address { get; set; } = "";
    public string Province { get; set; } = "";
    public string City { get; set; } = "";
    public string District { get; set; } = "";
    public double Longitude { get; set; }
    public double Latitude { get; set; }
    public string Source { get; set; } = "";
    public string DisplayText => $"{Title}  [{City}{District} {Address}]";
}

#region Tencent Maps API Response Models

public class GeocodeResponse
{
    [JsonPropertyName("status")] public int Status { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("result")] public GeocodeResult? Result { get; set; }
}

public class GeocodeResult
{
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("address")] public string? Address { get; set; }
    [JsonPropertyName("location")] public MapLocation? Location { get; set; }
    [JsonPropertyName("address_components")] public AddressComponent? AddressComponent { get; set; }
}

public class AddressComponent
{
    [JsonPropertyName("province")] public string? Province { get; set; }
    [JsonPropertyName("city")] public string? City { get; set; }
    [JsonPropertyName("district")] public string? District { get; set; }
}

public class MapLocation
{
    [JsonPropertyName("lat")] public double Lat { get; set; }
    [JsonPropertyName("lng")] public double Lng { get; set; }
}

public class PoiSearchResponse
{
    [JsonPropertyName("status")] public int Status { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("data")] public List<PoiData>? Data { get; set; }
}

public class PoiData
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("address")] public string? Address { get; set; }
    [JsonPropertyName("province")] public string? Province { get; set; }
    [JsonPropertyName("city")] public string? City { get; set; }
    [JsonPropertyName("district")] public string? District { get; set; }
    [JsonPropertyName("location")] public MapLocation? Location { get; set; }
}

#endregion
