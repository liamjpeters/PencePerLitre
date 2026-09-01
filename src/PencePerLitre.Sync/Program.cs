using PencePerLitre.Sync;

Console.WriteLine("=================================================");
Console.WriteLine("  Pence Per Litre (PPL) - Data Sync & ETL Engine ");
Console.WriteLine("  Gov.UK Fuel Finder Data Ingestion & Exporter   ");
Console.WriteLine("=================================================\n");

// 1. Load Environment Variables (.env supported for local dev)
EnvLoader.Load();

var clientId = Environment.GetEnvironmentVariable("FUEL_FINDER_CLIENT_ID") 
               ?? Environment.GetEnvironmentVariable("ClientID");
var clientSecret = Environment.GetEnvironmentVariable("FUEL_FINDER_CLIENT_SECRET") 
                   ?? Environment.GetEnvironmentVariable("ClientSecret");

static string RedactSecret(string? value, int visiblePrefix = 4, int visibleSuffix = 4)
{
    if (string.IsNullOrEmpty(value))
    {
        return "<empty>";
    }

    if (value.Length <= visiblePrefix + visibleSuffix)
    {
        return new string('*', value.Length);
    }

    var prefix = value[..visiblePrefix];
    var suffix = value[^visibleSuffix..];
    var middle = new string('*', Math.Max(0, value.Length - visiblePrefix - visibleSuffix));
    return $"{prefix}{middle}{suffix}";
}

Console.WriteLine($"Loaded credentials: ClientId={RedactSecret(clientId)} (len={clientId?.Length ?? 0}), ClientSecret={RedactSecret(clientSecret)} (len={clientSecret?.Length ?? 0})");

// Parse CLI Arguments
var argsList = args.ToList();
bool isFullSync = argsList.Contains("--full");
bool isPricesOnly = argsList.Contains("--prices-only");
bool isPfsOnly = argsList.Contains("--pfs-only");
bool isExportOnly = argsList.Contains("--export-only");

string dbPath = GetArgValue(argsList, "--db") ?? "penceperlitre.db";
string outputDir = GetArgValue(argsList, "--output") ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "PencePerLitre.Client", "wwwroot", "data"));

Console.WriteLine($"Database Path: {Path.GetFullPath(dbPath)}");
Console.WriteLine($"Output Directory: {outputDir}\n");

using var db = new DatabaseManager(dbPath);
db.InitializeSchema();

int existingStations = db.GetForecourtCount();
int existingPrices = db.GetFuelPriceCount();
Console.WriteLine($"Current DB State: {existingStations} forecourts, {existingPrices} fuel prices recorded.");

if (isExportOnly)
{
    Console.WriteLine("Running in --export-only mode. Generating JSON files...");
    db.ExportData(outputDir);
    Console.WriteLine("\nSync operation completed successfully.");
    return 0;
}

if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("ERROR: Missing Gov.UK Fuel Finder API credentials.");
    Console.WriteLine("Please set FUEL_FINDER_CLIENT_ID (or ClientID) and FUEL_FINDER_CLIENT_SECRET (or ClientSecret) in your environment or .env file.");
    Console.ResetColor();
    return 1;
}

using var apiClient = new GovFuelFinderClient(clientId, clientSecret);

try
{
    bool needFullPfs = isFullSync || existingStations == 0 || (!isPricesOnly && ShouldRunDailyPfs(db));
    bool needFullPrices = isFullSync || existingPrices == 0 || ShouldRunDailyFullPrices(db);

    // -------------------------------------------------------------
    // Step 1: Sync Forecourts (PFS Metadata)
    // -------------------------------------------------------------
    if (!isPricesOnly)
    {
        if (needFullPfs)
        {
            Console.WriteLine("\n[1/2] Starting Full Forecourt (PFS) Metadata Sync...");
            var stations = await apiClient.FetchAllForecourtsAsync();
            Console.WriteLine($"Persisting {stations.Count} forecourts to SQLite...");
            db.UpsertForecourts(stations);
            db.SetSyncState("last_pfs_sync_utc", DateTime.UtcNow.ToString("o"));
        }
        else
        {
            var lastPfsSyncStr = db.GetSyncState("last_pfs_sync_utc");
            var effectiveDate = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
            if (DateTime.TryParse(lastPfsSyncStr, out var lastPfsSync))
            {
                effectiveDate = lastPfsSync.ToString("yyyy-MM-dd");
            }

            Console.WriteLine($"\n[1/2] Starting Incremental Forecourt Metadata Sync (since {effectiveDate})...");
            var stations = await apiClient.FetchAllForecourtsAsync(effectiveDate);
            if (stations.Count > 0)
            {
                Console.WriteLine($"Persisting {stations.Count} updated forecourts to SQLite...");
                db.UpsertForecourts(stations);
            }
            db.SetSyncState("last_pfs_sync_utc", DateTime.UtcNow.ToString("o"));
        }
    }

    // -------------------------------------------------------------
    // Step 2: Sync Fuel Prices
    // -------------------------------------------------------------
    if (!isPfsOnly)
    {
        if (needFullPrices)
        {
            Console.WriteLine("\n[2/2] Starting Full Fuel Prices Sync...");
            var prices = await apiClient.FetchFuelPricesAsync();
            Console.WriteLine($"Persisting fuel prices for {prices.Count} station records to SQLite...");
            db.UpsertFuelPrices(prices, replaceExisting: true);
            var syncedAt = DateTime.UtcNow.ToString("o");
            db.SetSyncState("last_price_sync_utc", syncedAt);
            db.SetSyncState("last_full_price_sync_utc", syncedAt);
        }
        else
        {
            var lastPriceSyncStr = db.GetSyncState("last_price_sync_utc");
            // If syncing incrementally, fetch prices effective from today (or previous sync date)
            var effectiveDate = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
            if (DateTime.TryParse(lastPriceSyncStr, out var lastPriceSync))
            {
                effectiveDate = lastPriceSync.ToString("yyyy-MM-dd");
            }

            Console.WriteLine($"\n[2/2] Starting Incremental Fuel Prices Sync (since {effectiveDate})...");
            var prices = await apiClient.FetchFuelPricesAsync(effectiveDate);
            if (prices.Count > 0)
            {
                Console.WriteLine($"Persisting fuel prices for {prices.Count} updated station records to SQLite...");
                db.UpsertFuelPrices(prices);
            }
            db.SetSyncState("last_price_sync_utc", DateTime.UtcNow.ToString("o"));
        }
    }

    // -------------------------------------------------------------
    // Step 3: Export Optimized Static JSON for Blazor Client
    // -------------------------------------------------------------
    Console.WriteLine("\n[3/3] Exporting optimized static datasets for client...");
    db.ExportData(outputDir);

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("\n✔ Data sync and export finished successfully.");
    Console.ResetColor();
    return 0;
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"\nFATAL ERROR during sync: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    Console.ResetColor();
    return 1;
}

static bool ShouldRunDailyPfs(DatabaseManager db)
{
    var lastSyncStr = db.GetSyncState("last_pfs_sync_utc");
    if (string.IsNullOrEmpty(lastSyncStr)) return true;
    if (DateTime.TryParse(lastSyncStr, out var lastSync))
    {
        return (DateTime.UtcNow - lastSync).TotalHours >= 20;
    }
    return true;
}

static bool ShouldRunDailyFullPrices(DatabaseManager db)
{
    var lastSyncStr = db.GetSyncState("last_full_price_sync_utc");
    if (string.IsNullOrEmpty(lastSyncStr)) return true;
    if (DateTime.TryParse(lastSyncStr, out var lastSync))
    {
        return (DateTime.UtcNow - lastSync).TotalHours >= 20;
    }
    return true;
}

static string? GetArgValue(List<string> args, string paramName)
{
    int idx = args.IndexOf(paramName);
    if (idx >= 0 && idx + 1 < args.Count)
    {
        return args[idx + 1];
    }
    return null;
}
