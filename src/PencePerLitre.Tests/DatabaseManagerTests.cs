using System.Text.Json;
using Microsoft.Data.Sqlite;
using PencePerLitre.Shared;
using PencePerLitre.Sync;

namespace PencePerLitre.Tests;

public class DatabaseManagerTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"ppl-tests-{Guid.NewGuid():N}");

    [Fact]
    public void ExportData_ExcludesClosedStationsAndTheirPrices()
    {
        using var database = CreateDatabase();
        database.UpsertForecourts([
            Station("active"),
            Station("temporary", temporaryClosure: true),
            Station("permanent", permanentClosure: true)
        ]);
        database.UpsertFuelPrices([
            Prices("active", (FuelTypeConstants.E10, 140.1)),
            Prices("temporary", (FuelTypeConstants.E10, 130.1)),
            Prices("permanent", (FuelTypeConstants.E10, 120.1))
        ]);
        var outputDirectory = Path.Combine(_directory, "export");

        database.ExportData(outputDirectory);

        using var stations = JsonDocument.Parse(File.ReadAllText(Path.Combine(outputDirectory, "stations.json")));
        using var prices = JsonDocument.Parse(File.ReadAllText(Path.Combine(outputDirectory, "prices.json")));
        Assert.Equal("active", Assert.Single(stations.RootElement.EnumerateArray()).GetProperty("id").GetString());
        Assert.True(prices.RootElement.TryGetProperty("active", out _));
        Assert.False(prices.RootElement.TryGetProperty("temporary", out _));
        Assert.False(prices.RootElement.TryGetProperty("permanent", out _));
    }

    [Fact]
    public void UpsertFuelPrices_ReplaceExisting_RemovesPricesMissingFromSnapshot()
    {
        using var database = CreateDatabase();
        database.UpsertForecourts([Station("station")]);
        database.UpsertFuelPrices([
            Prices("station", (FuelTypeConstants.E10, 140.1), (FuelTypeConstants.E5, 150.1))
        ]);

        database.UpsertFuelPrices([
            Prices("station", (FuelTypeConstants.E10, 141.2))
        ], replaceExisting: true);
        var outputDirectory = Path.Combine(_directory, "export");
        database.ExportData(outputDirectory);

        using var prices = JsonDocument.Parse(File.ReadAllText(Path.Combine(outputDirectory, "prices.json")));
        var stationPrices = prices.RootElement.GetProperty("station");
        Assert.True(stationPrices.TryGetProperty(FuelTypeConstants.E10, out _));
        Assert.False(stationPrices.TryGetProperty(FuelTypeConstants.E5, out _));
    }

    [Fact]
    public void UpsertFuelPrices_EmptyReplacement_PreservesExistingPrices()
    {
        using var database = CreateDatabase();
        database.UpsertForecourts([Station("station")]);
        database.UpsertFuelPrices([Prices("station", (FuelTypeConstants.E10, 140.1))]);

        Assert.Throws<InvalidOperationException>(() =>
            database.UpsertFuelPrices([], replaceExisting: true));
        var outputDirectory = Path.Combine(_directory, "export");
        database.ExportData(outputDirectory);

        using var prices = JsonDocument.Parse(File.ReadAllText(Path.Combine(outputDirectory, "prices.json")));
        Assert.True(prices.RootElement.GetProperty("station").TryGetProperty(FuelTypeConstants.E10, out _));
    }

    private DatabaseManager CreateDatabase()
    {
        Directory.CreateDirectory(_directory);
        var database = new DatabaseManager(Path.Combine(_directory, $"{Guid.NewGuid():N}.db"));
        database.InitializeSchema();
        return database;
    }

    private static GovPfsStation Station(
        string id,
        bool temporaryClosure = false,
        bool permanentClosure = false) => new()
    {
        NodeId = id,
        TradingName = id,
        TemporaryClosure = temporaryClosure,
        PermanentClosure = permanentClosure,
        Location = new GovLocation { Latitude = 51.5, Longitude = -0.1 }
    };

    private static GovFuelStationPrice Prices(
        string id,
        params (string FuelType, double Price)[] prices) => new()
    {
        NodeId = id,
        FuelPrices = prices.Select(price => new GovFuelPriceItem
        {
            FuelType = price.FuelType,
            Price = price.Price
        }).ToList()
    };

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }
}