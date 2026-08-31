using System.Text.Json;
using System.Text.Json.Serialization;

namespace PencePerLitre.Shared;

public class FlexibleBoolConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Null => false,
            JsonTokenType.Number => reader.GetInt32() != 0,
            JsonTokenType.String => bool.TryParse(reader.GetString(), out var b) 
                ? b 
                : (reader.GetString() == "1" || string.Equals(reader.GetString(), "yes", StringComparison.OrdinalIgnoreCase)),
            _ => false
        };
    }

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
    {
        writer.WriteBooleanValue(value);
    }
}

public class FlexibleNullableBoolConverter : JsonConverter<bool?>
{
    public override bool? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Null => null,
            JsonTokenType.Number => reader.GetInt32() != 0,
            JsonTokenType.String => bool.TryParse(reader.GetString(), out var b) 
                ? b 
                : (reader.GetString() == "1" ? true : (reader.GetString() == "0" ? false : null)),
            _ => null
        };
    }

    public override void Write(Utf8JsonWriter writer, bool? value, JsonSerializerOptions options)
    {
        if (value.HasValue) writer.WriteBooleanValue(value.Value);
        else writer.WriteNullValue();
    }
}

public static class SharedJsonOptions
{
    public static readonly JsonSerializerOptions Default = CreateOptions();

    public static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new FlexibleBoolConverter());
        options.Converters.Add(new FlexibleNullableBoolConverter());
        return options;
    }
}

