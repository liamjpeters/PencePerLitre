using System.Text.Json.Serialization;

namespace PencePerLitre.Shared;

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(List<StationDto>), TypeInfoPropertyName = "StationList")]
[JsonSerializable(
    typeof(Dictionary<string, Dictionary<string, PriceDto>>),
    TypeInfoPropertyName = "PriceLookup")]
public partial class SharedJsonContext : JsonSerializerContext;
