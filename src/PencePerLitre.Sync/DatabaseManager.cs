using System.Text.Json;
using Microsoft.Data.Sqlite;
using PencePerLitre.Shared;

namespace PencePerLitre.Sync;

public class DatabaseManager : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _dbPath;

    public DatabaseManager(string dbPath)
    {
        _dbPath = dbPath;
        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();
    }

    public void InitializeSchema()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = @"
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;

            CREATE TABLE IF NOT EXISTS forecourts (
                id TEXT PRIMARY KEY,
                trading_name TEXT,
                brand_name TEXT,
                is_same_trading_brand INTEGER,
                temporary_closure INTEGER,
                permanent_closure INTEGER,
                permanent_closure_date TEXT,
                is_motorway INTEGER,
                is_supermarket INTEGER,
                phone TEXT,
                address_line_1 TEXT,
                address_line_2 TEXT,
                city TEXT,
                county TEXT,
                country TEXT,
                postcode TEXT,
                latitude REAL,
                longitude REAL,
                amenities_json TEXT,
                opening_json TEXT,
                fuel_types_json TEXT,
                updated_at TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_forecourts_postcode ON forecourts(postcode);
            CREATE INDEX IF NOT EXISTS idx_forecourts_coords ON forecourts(latitude, longitude);

            CREATE TABLE IF NOT EXISTS fuel_prices (
                forecourt_id TEXT,
                fuel_type TEXT,
                price REAL,
                price_last_updated TEXT,
                price_change_effective_timestamp TEXT,
                PRIMARY KEY (forecourt_id, fuel_type)
            );

            CREATE INDEX IF NOT EXISTS idx_fuel_prices_type ON fuel_prices(fuel_type);

            CREATE TABLE IF NOT EXISTS sync_state (
                key TEXT PRIMARY KEY,
                value TEXT
            );
        ";
        command.ExecuteNonQuery();
    }

    public void UpsertForecourts(IEnumerable<GovPfsStation> stations)
    {
        using var transaction = _connection.BeginTransaction();
        using var cmd = _connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = @"
            INSERT INTO forecourts (
                id, trading_name, brand_name, is_same_trading_brand,
                temporary_closure, permanent_closure, permanent_closure_date,
                is_motorway, is_supermarket, phone,
                address_line_1, address_line_2, city, county, country, postcode,
                latitude, longitude, amenities_json, opening_json, fuel_types_json, updated_at
            ) VALUES (
                $id, $trading_name, $brand_name, $is_same_trading_brand,
                $temporary_closure, $permanent_closure, $permanent_closure_date,
                $is_motorway, $is_supermarket, $phone,
                $address_line_1, $address_line_2, $city, $county, $country, $postcode,
                $latitude, $longitude, $amenities_json, $opening_json, $fuel_types_json, $updated_at
            ) ON CONFLICT(id) DO UPDATE SET
                trading_name = excluded.trading_name,
                brand_name = excluded.brand_name,
                is_same_trading_brand = excluded.is_same_trading_brand,
                temporary_closure = excluded.temporary_closure,
                permanent_closure = excluded.permanent_closure,
                permanent_closure_date = excluded.permanent_closure_date,
                is_motorway = excluded.is_motorway,
                is_supermarket = excluded.is_supermarket,
                phone = excluded.phone,
                address_line_1 = excluded.address_line_1,
                address_line_2 = excluded.address_line_2,
                city = excluded.city,
                county = excluded.county,
                country = excluded.country,
                postcode = excluded.postcode,
                latitude = excluded.latitude,
                longitude = excluded.longitude,
                amenities_json = excluded.amenities_json,
                opening_json = excluded.opening_json,
                fuel_types_json = excluded.fuel_types_json,
                updated_at = excluded.updated_at;
        ";

        var pId = cmd.Parameters.Add("$id", SqliteType.Text);
        var pTradingName = cmd.Parameters.Add("$trading_name", SqliteType.Text);
        var pBrandName = cmd.Parameters.Add("$brand_name", SqliteType.Text);
        var pSameBrand = cmd.Parameters.Add("$is_same_trading_brand", SqliteType.Integer);
        var pTempClose = cmd.Parameters.Add("$temporary_closure", SqliteType.Integer);
        var pPermClose = cmd.Parameters.Add("$permanent_closure", SqliteType.Integer);
        var pPermCloseDate = cmd.Parameters.Add("$permanent_closure_date", SqliteType.Text);
        var pMotorway = cmd.Parameters.Add("$is_motorway", SqliteType.Integer);
        var pSupermarket = cmd.Parameters.Add("$is_supermarket", SqliteType.Integer);
        var pPhone = cmd.Parameters.Add("$phone", SqliteType.Text);
        var pAddress1 = cmd.Parameters.Add("$address_line_1", SqliteType.Text);
        var pAddress2 = cmd.Parameters.Add("$address_line_2", SqliteType.Text);
        var pCity = cmd.Parameters.Add("$city", SqliteType.Text);
        var pCounty = cmd.Parameters.Add("$county", SqliteType.Text);
        var pCountry = cmd.Parameters.Add("$country", SqliteType.Text);
        var pPostcode = cmd.Parameters.Add("$postcode", SqliteType.Text);
        var pLat = cmd.Parameters.Add("$latitude", SqliteType.Real);
        var pLon = cmd.Parameters.Add("$longitude", SqliteType.Real);
        var pAmenities = cmd.Parameters.Add("$amenities_json", SqliteType.Text);
        var pOpening = cmd.Parameters.Add("$opening_json", SqliteType.Text);
        var pFuelTypes = cmd.Parameters.Add("$fuel_types_json", SqliteType.Text);
        var pUpdatedAt = cmd.Parameters.Add("$updated_at", SqliteType.Text);

        var nowIso = DateTime.UtcNow.ToString("o");

        foreach (var s in stations)
        {
            if (string.IsNullOrWhiteSpace(s.NodeId)) continue;

            pId.Value = s.NodeId;
            pTradingName.Value = (object?)s.TradingName ?? DBNull.Value;
            pBrandName.Value = (object?)s.BrandName ?? DBNull.Value;
            pSameBrand.Value = s.IsSameTradingAndBrandName == true ? 1 : 0;
            pTempClose.Value = s.TemporaryClosure ? 1 : 0;
            pPermClose.Value = s.PermanentClosure ? 1 : 0;
            pPermCloseDate.Value = (object?)s.PermanentClosureDate ?? DBNull.Value;
            pMotorway.Value = s.IsMotorwayServiceStation ? 1 : 0;
            pSupermarket.Value = s.IsSupermarketServiceStation ? 1 : 0;
            pPhone.Value = (object?)s.PublicPhoneNumber ?? DBNull.Value;

            pAddress1.Value = (object?)s.Location?.AddressLine1 ?? DBNull.Value;
            pAddress2.Value = (object?)s.Location?.AddressLine2 ?? DBNull.Value;
            pCity.Value = (object?)s.Location?.City ?? DBNull.Value;
            pCounty.Value = (object?)s.Location?.County ?? DBNull.Value;
            pCountry.Value = (object?)s.Location?.Country ?? DBNull.Value;
            pPostcode.Value = (object?)s.Location?.Postcode ?? DBNull.Value;
            pLat.Value = s.Location?.Latitude ?? 0.0;
            pLon.Value = s.Location?.Longitude ?? 0.0;

            pAmenities.Value = s.Amenities != null ? JsonSerializer.Serialize(s.Amenities) : DBNull.Value;
            pOpening.Value = s.OpeningTimes != null ? JsonSerializer.Serialize(s.OpeningTimes) : DBNull.Value;
            pFuelTypes.Value = s.FuelTypes != null ? JsonSerializer.Serialize(s.FuelTypes) : DBNull.Value;
            pUpdatedAt.Value = nowIso;

            cmd.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void UpsertFuelPrices(IEnumerable<GovFuelStationPrice> priceBatches, bool replaceExisting = false)
    {
        var batches = priceBatches as IReadOnlyCollection<GovFuelStationPrice> ?? priceBatches.ToList();
        if (replaceExisting && batches.Count == 0)
        {
            throw new InvalidOperationException("Cannot replace fuel prices with an empty snapshot.");
        }

        using var transaction = _connection.BeginTransaction();

        if (replaceExisting)
        {
            using var deleteCommand = _connection.CreateCommand();
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = "DELETE FROM fuel_prices";
            deleteCommand.ExecuteNonQuery();
        }

        using var cmd = _connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = @"
            INSERT INTO fuel_prices (
                forecourt_id, fuel_type, price, price_last_updated, price_change_effective_timestamp
            ) VALUES (
                $forecourt_id, $fuel_type, $price, $price_last_updated, $price_change_effective_timestamp
            ) ON CONFLICT(forecourt_id, fuel_type) DO UPDATE SET
                price = excluded.price,
                price_last_updated = excluded.price_last_updated,
                price_change_effective_timestamp = excluded.price_change_effective_timestamp;
        ";

        var pForecourtId = cmd.Parameters.Add("$forecourt_id", SqliteType.Text);
        var pFuelType = cmd.Parameters.Add("$fuel_type", SqliteType.Text);
        var pPrice = cmd.Parameters.Add("$price", SqliteType.Real);
        var pLastUpdated = cmd.Parameters.Add("$price_last_updated", SqliteType.Text);
        var pEffective = cmd.Parameters.Add("$price_change_effective_timestamp", SqliteType.Text);

        foreach (var stationPrice in batches)
        {
            if (string.IsNullOrWhiteSpace(stationPrice.NodeId) || stationPrice.FuelPrices == null) continue;

            pForecourtId.Value = stationPrice.NodeId;

            foreach (var fp in stationPrice.FuelPrices)
            {
                if (string.IsNullOrWhiteSpace(fp.FuelType)) continue;

                pFuelType.Value = fp.FuelType.ToUpperInvariant();
                pPrice.Value = fp.Price;
                pLastUpdated.Value = (object?)fp.PriceLastUpdated ?? DBNull.Value;
                pEffective.Value = (object?)fp.PriceChangeEffectiveTimestamp ?? DBNull.Value;

                cmd.ExecuteNonQuery();
            }
        }

        transaction.Commit();
    }

    public string? GetSyncState(string key)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT value FROM sync_state WHERE key = $key";
        cmd.Parameters.AddWithValue("$key", key);
        var result = cmd.ExecuteScalar();
        return result?.ToString();
    }

    public void SetSyncState(string key, string value)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "INSERT INTO sync_state(key, value) VALUES($key, $value) ON CONFLICT(key) DO UPDATE SET value = excluded.value";
        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$value", value);
        cmd.ExecuteNonQuery();
    }

    public int GetForecourtCount()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM forecourts WHERE permanent_closure = 0";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public int GetFuelPriceCount()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM fuel_prices";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    /// <summary>
    /// Exports optimized JSON files for the Blazor WebAssembly frontend.
    /// </summary>
    public void ExportData(string outputDirectory, DateTime? lastFetchedAtUtc = null)
    {
        Directory.CreateDirectory(outputDirectory);

        // 1. Fetch active forecourts
        var stations = new List<StationDto>();
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT id, trading_name, brand_name, address_line_1, city, postcode,
                       latitude, longitude, is_motorway, is_supermarket, phone,
                       amenities_json, fuel_types_json, opening_json
                FROM forecourts
                                WHERE permanent_closure = 0 AND temporary_closure = 0
                                    AND latitude != 0 AND longitude != 0
                ORDER BY id
            ";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetString(0);
                var tradingName = reader.IsDBNull(1) ? "" : reader.GetString(1);
                var brandName = reader.IsDBNull(2) ? "" : reader.GetString(2);
                var address = reader.IsDBNull(3) ? "" : reader.GetString(3);
                var city = reader.IsDBNull(4) ? "" : reader.GetString(4);
                var postcode = reader.IsDBNull(5) ? "" : reader.GetString(5);
                var lat = reader.GetDouble(6);
                var lon = reader.GetDouble(7);
                var motorway = reader.GetInt32(8) == 1;
                var supermarket = reader.GetInt32(9) == 1;
                var phone = reader.IsDBNull(10) ? null : reader.GetString(10);

                List<string>? amenities = null;
                if (!reader.IsDBNull(11))
                {
                    try { amenities = JsonSerializer.Deserialize<List<string>>(reader.GetString(11)); } catch { }
                }

                List<string>? fuelTypes = null;
                if (!reader.IsDBNull(12))
                {
                    try { fuelTypes = JsonSerializer.Deserialize<List<string>>(reader.GetString(12)); } catch { }
                }

                GovOpeningTimes? opening = null;
                if (!reader.IsDBNull(13))
                {
                    try { opening = JsonSerializer.Deserialize<GovOpeningTimes>(reader.GetString(13)); } catch { }
                }

                stations.Add(new StationDto
                {
                    Id = id,
                    Name = !string.IsNullOrWhiteSpace(tradingName) ? tradingName : brandName,
                    Brand = brandName,
                    Address = address,
                    City = city,
                    Postcode = postcode,
                    Lat = Math.Round(lat, 6),
                    Lon = Math.Round(lon, 6),
                    Motorway = motorway,
                    Supermarket = supermarket,
                    Phone = phone,
                    Amenities = amenities,
                    FuelTypes = fuelTypes,
                    Opening = opening
                });
            }
        }

        // 2. Fetch prices
        var pricesDict = new Dictionary<string, Dictionary<string, PriceDto>>();
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = @"
                                SELECT fp.forecourt_id, fp.fuel_type, fp.price,
                                             fp.price_last_updated, fp.price_change_effective_timestamp
                                FROM fuel_prices fp
                                INNER JOIN forecourts f ON f.id = fp.forecourt_id
                                WHERE fp.price > 0
                                    AND f.permanent_closure = 0
                                    AND f.temporary_closure = 0
            ";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var forecourtId = reader.GetString(0);
                var fuelType = reader.GetString(1);
                var price = reader.GetDouble(2);
                var updated = reader.IsDBNull(3) ? null : reader.GetString(3);
                var effective = reader.IsDBNull(4) ? null : reader.GetString(4);

                if (!pricesDict.TryGetValue(forecourtId, out var stationPrices))
                {
                    stationPrices = new Dictionary<string, PriceDto>();
                    pricesDict[forecourtId] = stationPrices;
                }

                stationPrices[fuelType] = new PriceDto
                {
                    Price = price,
                    Updated = updated,
                    Effective = effective
                };
            }
        }

        // 3. Write files
        var jsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false // minified for minimal payload size
        };

        var stationsJsonPath = Path.Combine(outputDirectory, "stations.json");
        var pricesJsonPath = Path.Combine(outputDirectory, "prices.json");
        var metaJsonPath = Path.Combine(outputDirectory, "metadata.json");

        File.WriteAllText(stationsJsonPath, JsonSerializer.Serialize(stations, jsonOptions));
        File.WriteAllText(pricesJsonPath, JsonSerializer.Serialize(pricesDict, jsonOptions));

        var meta = new
        {
            generatedAtUtc = DateTime.UtcNow.ToString("o"),
            lastFetchedAtUtc = lastFetchedAtUtc?.ToUniversalTime().ToString("o"),
            totalStations = stations.Count,
            stationsWithPrices = pricesDict.Count,
            totalPriceRecords = pricesDict.Values.Sum(p => p.Count),
            fuelTypesReported = pricesDict.Values
                .SelectMany(p => p.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(fuelType => fuelType)
                .ToArray()
        };
        File.WriteAllText(metaJsonPath, JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true }));

        var stationsInfo = new FileInfo(stationsJsonPath);
        var pricesInfo = new FileInfo(pricesJsonPath);

        Console.WriteLine($"\nExported Data Summary:");
        Console.WriteLine($"  - {stations.Count} active stations exported to {stationsJsonPath} ({stationsInfo.Length / 1024} KB uncompressed)");
        Console.WriteLine($"  - {pricesDict.Count} station price records exported to {pricesJsonPath} ({pricesInfo.Length / 1024} KB uncompressed)");
        Console.WriteLine($"  - Metadata written to {metaJsonPath}");
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}

