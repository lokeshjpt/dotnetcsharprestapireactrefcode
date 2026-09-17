using System.Text.Json;
using System.Text.Json.Serialization;

namespace PWA.PermitsApi.WebApi.Json;

/// <summary>
/// Deserializes JSON string values but also tolerates JSON numbers and booleans by converting them
/// to their textual representation. This prevents a 400 ("The JSON value could not be converted to
/// System.String") when a client sends an identifier such as <c>appId</c> as a JSON number
/// (e.g. <c>1785991088186</c>) rather than a quoted string. Serialization (writing) is unchanged.
/// </summary>
public sealed class LenientStringConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString();
            case JsonTokenType.Number:
                // Preserve the numeric value as text without precision loss. A 13-digit appId fits
                // in Int64; fall back to decimal for any fractional value.
                if (reader.TryGetInt64(out var l))
                {
                    return l.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                return reader.GetDecimal().ToString(System.Globalization.CultureInfo.InvariantCulture);
            case JsonTokenType.True:
                return "true";
            case JsonTokenType.False:
                return "false";
            case JsonTokenType.Null:
                return null;
            default:
                throw new JsonException($"Unexpected token {reader.TokenType} when reading a string value.");
        }
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value);
        }
    }
}
