namespace PencePerLitre.Shared;

public static class GeoUtils
{
    private const double EarthRadiusMiles = 3958.8;
    private const double EarthRadiusKm = 6371.0;

    /// <summary>
    /// Calculates Great Circle (Haversine) distance between two lat/long points in miles.
    /// </summary>
    public static double HaversineDistanceMiles(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusMiles * c;
    }

    /// <summary>
    /// Calculates Great Circle distance in kilometres.
    /// </summary>
    public static double HaversineDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusKm * c;
    }

    private static double ToRadians(double degrees) => degrees * (Math.PI / 180.0);

    /// <summary>
    /// Normalises UK postcode by trimming whitespace and making uppercase.
    /// </summary>
    public static string NormalizePostcode(string? postcode)
    {
        if (string.IsNullOrWhiteSpace(postcode)) return string.Empty;
        return postcode.Replace(" ", "").Trim().ToUpperInvariant();
    }
}

