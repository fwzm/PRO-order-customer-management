using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using PRO.Infrastructure.Configuration;

namespace PRO.Desktop.Services;

/// <summary>
/// 地图服务接口 - 封装腾讯地图 API 调用
/// </summary>
public interface IMapService
{
    /// <summary>地理编码：地址转坐标</summary>
    Task<MapPoiItem?> GeocodeAsync(string address, CancellationToken ct = default);

    /// <summary>POI搜索</summary>
    Task<List<MapPoiItem>> SearchPoiAsync(string keyword, CancellationToken ct = default);

    /// <summary>坐标反查地址</summary>
    Task<MapPoiItem?> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken ct = default);

    /// <summary>API Key 是否已配置</summary>
    bool IsConfigured { get; }
}

/// <summary>
/// 腾讯地图服务实现
/// </summary>
public class TencentMapService : IMapService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private string ApiKey => HardcodedConfig.TencentMapApiKey;
    private const string BaseUrl = "https://apis.map.qq.com";
    private const string HttpClientName = "TencentMap";

    public bool IsConfigured => !string.IsNullOrEmpty(ApiKey);

    public TencentMapService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<MapPoiItem?> GeocodeAsync(string address, CancellationToken ct = default)
    {
        if (!IsConfigured) return null;

        try
        {
            var url = $"{BaseUrl}/ws/geocoder/v1?address={Uri.EscapeDataString(address)}&key={ApiKey}";
            var client = _httpClientFactory.CreateClient(HttpClientName);
            var json = await client.GetStringAsync(url, ct);
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
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<List<MapPoiItem>> SearchPoiAsync(string keyword, CancellationToken ct = default)
    {
        if (!IsConfigured) return [];

        try
        {
            var encodedKeyword = Uri.EscapeDataString(keyword);
            var url = $"{BaseUrl}/ws/place/v1/search?keyword={encodedKeyword}&key={ApiKey}&boundary=nearby(35,105,1000000)&page_size=20";
            var client = _httpClientFactory.CreateClient(HttpClientName);
            var json = await client.GetStringAsync(url, ct);
            var result = JsonSerializer.Deserialize<PoiSearchResponse>(json);
            if (result?.Status != 0 || result.Data == null) return [];

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
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            return [];
        }
    }

    public async Task<MapPoiItem?> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken ct = default)
    {
        if (!IsConfigured) return null;

        try
        {
            var url = $"{BaseUrl}/ws/geocoder/v1?location={latitude},{longitude}&key={ApiKey}";
            var client = _httpClientFactory.CreateClient(HttpClientName);
            var json = await client.GetStringAsync(url, ct);
            var result = JsonSerializer.Deserialize<GeocodeResponse>(json);
            if (result?.Status != 0 || result.Result == null) return null;

            var comp = result.Result.AddressComponent;
            return new MapPoiItem
            {
                Title = result.Result.Title ?? "",
                Address = result.Result.Address ?? "",
                Province = comp?.Province ?? "",
                City = comp?.City ?? "",
                District = comp?.District ?? "",
                Latitude = latitude,
                Longitude = longitude,
                Source = "坐标反查"
            };
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            return null;
        }
    }
}

// ─── 数据模型 ─────────────────────────────────────────────

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
