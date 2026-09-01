using System.Net.Http.Json;
using PencePerLitre.Shared;

namespace PencePerLitre.Client.Services;

public class FuelDataService
{
    private readonly HttpClient _http;
    private List<StationDto>? _stations;
    private Dictionary<string, Dictionary<string, PriceDto>>? _prices;
    private Task? _initializationTask;

    public bool IsLoaded => _stations != null && _prices != null;
    public int TotalStationsCount => _stations?.Count ?? 0;

    public FuelDataService(HttpClient http)
    {
        _http = http;
    }

    public async Task EnsureLoadedAsync()
    {
        if (IsLoaded) return;

        var initializationTask = _initializationTask ??= LoadDataInternalAsync();
        try
        {
            await initializationTask;
        }
        catch
        {
            if (ReferenceEquals(_initializationTask, initializationTask))
            {
                _initializationTask = null;
            }
            throw;
        }
    }

    private async Task LoadDataInternalAsync()
    {
        try
        {
            var stationsTask = _http.GetFromJsonAsync(
                "data/stations.json",
                SharedJsonContext.Default.StationList);
            var pricesTask = _http.GetFromJsonAsync(
                "data/prices.json",
                SharedJsonContext.Default.PriceLookup);

            await Task.WhenAll(stationsTask, pricesTask);

            _stations = await stationsTask ?? new List<StationDto>();
            _prices = await pricesTask ?? new Dictionary<string, Dictionary<string, PriceDto>>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading fuel data: {ex.Message}");
            throw;
        }
    }

    public StationDto? GetStationById(string id)
    {
        return _stations?.FirstOrDefault(s => s.Id == id);
    }

    public Dictionary<string, PriceDto> GetPricesForStation(string stationId)
    {
        if (_prices != null && _prices.TryGetValue(stationId, out var dict))
        {
            return dict;
        }
        return new Dictionary<string, PriceDto>();
    }

    public List<StationViewItem> Search(
        double userLat, 
        double userLon, 
        double radiusMiles = 10.0, 
        string fuelType = FuelTypeConstants.E10,
        bool supermarketOnly = false,
        bool motorwayOnly = false,
        bool open24HoursOnly = false,
        string sortBy = "price")
    {
        if (_stations == null || _prices == null) return new List<StationViewItem>();

        var results = new List<StationViewItem>();

        foreach (var station in _stations)
        {
            // Calculate distance
            var dist = GeoUtils.HaversineDistanceMiles(userLat, userLon, station.Lat, station.Lon);
            if (dist > radiusMiles) continue;

            // Apply optional filters
            if (supermarketOnly && !station.Supermarket) continue;
            if (motorwayOnly && !station.Motorway) continue;
            if (open24HoursOnly)
            {
                var usualDays = station.Opening?.UsualDays;
                var is24h = usualDays is { Count: > 0 } && usualDays.Values.All(d => d.Is24Hours);
                if (!is24h) continue;
            }

            // Retrieve prices
            _prices.TryGetValue(station.Id, out var stationPrices);
            stationPrices ??= new Dictionary<string, PriceDto>();

            // Get selected fuel price
            double? priceValue = null;
            if (stationPrices.TryGetValue(fuelType, out var priceDto))
            {
                priceValue = priceDto.Price;
            }

            results.Add(new StationViewItem
            {
                Station = station,
                Prices = stationPrices,
                DistanceMiles = Math.Round(dist, 1),
                SelectedFuelPrice = priceValue
            });
        }

        // Sort results
        if (sortBy == "price")
        {
            // Stations with a price first (ordered by price ascending, then distance ascending), then unpriced
            results = results
                .OrderBy(r => r.SelectedFuelPrice.HasValue ? 0 : 1)
                .ThenBy(r => r.SelectedFuelPrice ?? double.MaxValue)
                .ThenBy(r => r.DistanceMiles ?? double.MaxValue)
                .ToList();
        }
        else
        {
            // Sort by distance ascending
            results = results
                .OrderBy(r => r.DistanceMiles ?? double.MaxValue)
                .ThenBy(r => r.SelectedFuelPrice ?? double.MaxValue)
                .ToList();
        }

        return results;
    }
}

