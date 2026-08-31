using System.Net.Http;
using Microsoft.JSInterop;
using PencePerLitre.Shared;

namespace PencePerLitre.Client.Services;

public class PostcodeLookupResult
{
    public bool Success { get; set; }
    public string? Postcode { get; set; }
    public double Lat { get; set; }
    public double Lon { get; set; }
    public string? Error { get; set; }
}

public class GeoPositionResult
{
    public bool Success { get; set; }
    public double Lat { get; set; }
    public double Lon { get; set; }
    public double? Accuracy { get; set; }
    public string? Error { get; set; }
}

public class GeoLocationService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private readonly HttpClient _http;
    private readonly PostcodeLookupEngine _postcodeEngine = new();
    private IJSObjectReference? _module;
    private Task<bool>? _engineInitTask;

    public GeoLocationService(IJSRuntime js, HttpClient http)
    {
        _js = js;
        _http = http;
    }

    private async Task<IJSObjectReference> GetModuleAsync()
    {
        return _module ??= await _js.InvokeAsync<IJSObjectReference>("import", "./js/app-interop.js");
    }

    public Task<bool> InitPostcodeEngineAsync()
    {
        if (_postcodeEngine.IsInitialized) return Task.FromResult(true);
        _engineInitTask ??= LoadPostcodePackInternalAsync();
        return _engineInitTask;
    }

    private async Task<bool> LoadPostcodePackInternalAsync()
    {
        try
        {
            var packBytes = await _http.GetByteArrayAsync("postcodes.pack");
            _postcodeEngine.LoadPack(packBytes);
            Console.WriteLine($"[C# PostcodeEngine] Successfully loaded postcode pack ({packBytes.Length / 1024} KB) on-device.");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[C# PostcodeEngine] Error loading postcode pack: {ex.Message}");
            return false;
        }
    }

    public async Task<PostcodeLookupResult> LookupPostcodeAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new PostcodeLookupResult { Success = false, Error = "Please enter a valid UK postcode." };
        }

        var isInit = await InitPostcodeEngineAsync();
        if (!isInit)
        {
            return new PostcodeLookupResult { Success = false, Error = "Postcode database could not be loaded." };
        }

        // 100% On-Device Binary Lookup in C#
        var (found, canonical, lat, lon) = _postcodeEngine.Lookup(query.Trim());
        if (found)
        {
            return new PostcodeLookupResult
            {
                Success = true,
                Postcode = canonical,
                Lat = lat,
                Lon = lon
            };
        }

        return new PostcodeLookupResult
        {
            Success = false,
            Error = $"Postcode not found: '{query.Trim()}'. Please enter a valid UK postcode (e.g. PO14 3LG, S1 1AA)."
        };
    }

    public async Task<GeoPositionResult> GetCurrentLocationAsync()
    {
        try
        {
            var module = await GetModuleAsync();
            return await module.InvokeAsync<GeoPositionResult>("getCurrentLocation");
        }
        catch (Exception ex)
        {
            return new GeoPositionResult { Success = false, Error = ex.Message };
        }
    }

    public async Task InitMapAsync(string elementId, DotNetObjectReference<object> dotNetRef, double lat, double lon, int zoom = 12)
    {
        try
        {
            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("initMap", elementId, dotNetRef, lat, lon, zoom);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing map: {ex.Message}");
        }
    }

    public async Task InvalidateMapSizeAsync()
    {
        try
        {
            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("invalidateMapSize");
        }
        catch { }
    }

    public async Task SetMapViewAsync(double lat, double lon, int? zoom = null)
    {
        try
        {
            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("setMapView", lat, lon, zoom);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error setting map view: {ex.Message}");
        }
    }

    public async Task FitMapBoundsAsync(IEnumerable<StationDto> stations)
    {
        try
        {
            var module = await GetModuleAsync();
            var points = stations.Select(s => new { lat = s.Lat, lon = s.Lon }).ToArray();
            await module.InvokeVoidAsync("fitMapBounds", (object)points);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fitting map bounds: {ex.Message}");
        }
    }

    public async Task UpdateMapMarkersAsync(IEnumerable<StationViewItem> stations, (double lat, double lon)? userLocation)
    {
        try
        {
            var module = await GetModuleAsync();
            object? userLocObj = userLocation.HasValue 
                ? new { lat = userLocation.Value.lat, lon = userLocation.Value.lon }
                : null;

            var items = stations.Select(s => new
            {
                station = new { id = s.Station.Id, lat = s.Station.Lat, lon = s.Station.Lon, name = s.Station.Name },
                selectedFuelPrice = s.SelectedFuelPrice
            }).ToArray();

            await module.InvokeVoidAsync("updateMapMarkers", items, userLocObj);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating map markers: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module != null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch { }
        }
    }
}
