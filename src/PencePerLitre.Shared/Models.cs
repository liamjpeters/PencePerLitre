using System.Text.Json.Serialization;

namespace PencePerLitre.Shared;

/// <summary>
/// Raw Petrol Fuel Station (PFS) metadata returned from Gov.UK Fuel Finder API.
/// </summary>
public class GovPfsStation
{
    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("trading_name")]
    public string? TradingName { get; set; }

    [JsonPropertyName("brand_name")]
    public string? BrandName { get; set; }

    [JsonPropertyName("is_same_trading_and_brand_name")]
    [JsonConverter(typeof(FlexibleNullableBoolConverter))]
    public bool? IsSameTradingAndBrandName { get; set; }

    [JsonPropertyName("temporary_closure")]
    [JsonConverter(typeof(FlexibleBoolConverter))]
    public bool TemporaryClosure { get; set; }

    [JsonPropertyName("permanent_closure")]
    [JsonConverter(typeof(FlexibleBoolConverter))]
    public bool PermanentClosure { get; set; }

    [JsonPropertyName("permanent_closure_date")]
    public string? PermanentClosureDate { get; set; }

    [JsonPropertyName("is_motorway_service_station")]
    [JsonConverter(typeof(FlexibleBoolConverter))]
    public bool IsMotorwayServiceStation { get; set; }

    [JsonPropertyName("is_supermarket_service_station")]
    [JsonConverter(typeof(FlexibleBoolConverter))]
    public bool IsSupermarketServiceStation { get; set; }

    [JsonPropertyName("public_phone_number")]
    public string? PublicPhoneNumber { get; set; }

    [JsonPropertyName("location")]
    public GovLocation? Location { get; set; }

    [JsonPropertyName("amenities")]
    public List<string>? Amenities { get; set; }

    [JsonPropertyName("opening_times")]
    public GovOpeningTimes? OpeningTimes { get; set; }

    [JsonPropertyName("fuel_types")]
    public List<string>? FuelTypes { get; set; }
}

public class GovLocation
{
    [JsonPropertyName("address_line_1")]
    public string? AddressLine1 { get; set; }

    [JsonPropertyName("address_line_2")]
    public string? AddressLine2 { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("county")]
    public string? County { get; set; }

    [JsonPropertyName("country")]
    public string? Country { get; set; }

    [JsonPropertyName("postcode")]
    public string? Postcode { get; set; }

    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }
}

public class GovOpeningTimes
{
    [JsonPropertyName("usual_days")]
    public Dictionary<string, GovDayHours>? UsualDays { get; set; }

    [JsonPropertyName("bank_holiday")]
    public GovBankHolidayHours? BankHoliday { get; set; }
}

public class GovDayHours
{
    [JsonPropertyName("open")]
    public string? Open { get; set; }

    [JsonPropertyName("close")]
    public string? Close { get; set; }

    [JsonPropertyName("is_24_hours")]
    [JsonConverter(typeof(FlexibleBoolConverter))]
    public bool Is24Hours { get; set; }
}

public class GovBankHolidayHours
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("open_time")]
    public string? OpenTime { get; set; }

    [JsonPropertyName("close_time")]
    public string? CloseTime { get; set; }

    [JsonPropertyName("is_24_hours")]
    [JsonConverter(typeof(FlexibleBoolConverter))]
    public bool Is24Hours { get; set; }
}

/// <summary>
/// Raw Fuel Price payload from Gov.UK Fuel Finder API.
/// </summary>
public class GovFuelStationPrice
{
    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("trading_name")]
    public string? TradingName { get; set; }

    [JsonPropertyName("fuel_prices")]
    public List<GovFuelPriceItem> FuelPrices { get; set; } = new();
}

public class GovFuelPriceItem
{
    [JsonPropertyName("fuel_type")]
    public string FuelType { get; set; } = string.Empty;

    [JsonPropertyName("price")]
    public double Price { get; set; }

    [JsonPropertyName("price_last_updated")]
    public string? PriceLastUpdated { get; set; }

    [JsonPropertyName("price_change_effective_timestamp")]
    public string? PriceChangeEffectiveTimestamp { get; set; }
}

/// <summary>
/// OAuth response from Gov.UK Fuel Finder.
/// </summary>
public class GovOAuthResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public GovOAuthData? Data { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

public class GovOAuthData
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("refresh_token_expires_in")]
    public int RefreshTokenExpiresIn { get; set; }
}

// -------------------------------------------------------------
// Optimized Client-Facing DTOs (used in Blazor Client)
// -------------------------------------------------------------

public class StationDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("brand")]
    public string Brand { get; set; } = string.Empty;

    [JsonPropertyName("postcode")]
    public string Postcode { get; set; } = string.Empty;

    [JsonPropertyName("address")]
    public string Address { get; set; } = string.Empty;

    [JsonPropertyName("city")]
    public string City { get; set; } = string.Empty;

    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lon")]
    public double Lon { get; set; }

    [JsonPropertyName("motorway")]
    public bool Motorway { get; set; }

    [JsonPropertyName("supermarket")]
    public bool Supermarket { get; set; }

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("amenities")]
    public List<string>? Amenities { get; set; }

    [JsonPropertyName("fuelTypes")]
    public List<string>? FuelTypes { get; set; }

    [JsonPropertyName("opening")]
    public GovOpeningTimes? Opening { get; set; }
}

public class PriceDto
{
    [JsonPropertyName("p")]
    public double Price { get; set; }

    [JsonPropertyName("u")]
    public string? Updated { get; set; }

    [JsonPropertyName("e")]
    public string? Effective { get; set; }
}

/// <summary>
/// Merged Station and Price record used for display and sorting in Blazor UI.
/// </summary>
public class StationViewItem
{
    public StationDto Station { get; set; } = null!;
    public Dictionary<string, PriceDto> Prices { get; set; } = new();
    public double? DistanceMiles { get; set; }
    public double? SelectedFuelPrice { get; set; }
}

public static class FuelTypeConstants
{
    public const string E10 = "E10";
    public const string E5 = "E5";
    public const string B7Standard = "B7_STANDARD";
    public const string B7Premium = "B7_PREMIUM";
    public const string HVO = "HVO";
    public const string B10 = "B10";

    public static readonly Dictionary<string, string> DisplayNames = new()
    {
        { E10, "Unleaded (E10)" },
        { E5, "Super Unleaded (E5)" },
        { B7Standard, "Diesel (B7)" },
        { B7Premium, "Premium Diesel" },
        { HVO, "HVO Renewable Diesel" },
        { B10, "Diesel (B10)" }
    };

    public static string GetDisplayName(string fuelType)
    {
        return DisplayNames.TryGetValue(fuelType, out var name) ? name : fuelType;
    }
}
