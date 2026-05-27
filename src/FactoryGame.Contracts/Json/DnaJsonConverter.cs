using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FactoryGame.Contracts.Json;

/// <summary>Serializes DNA (int64) as JSON string to avoid JavaScript precision loss.</summary>
public sealed class DnaJsonConverter : JsonConverter<long>
{
    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => ParseDnaString(reader.GetString()),
            JsonTokenType.Number => reader.GetInt64(),
            JsonTokenType.Null => 0,
            _ => throw new JsonException($"Expected string or number for DNA, got {reader.TokenType}.")
        };
    }

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));

    internal static long ParseDnaString(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return 0;
        return long.Parse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture);
    }
}

public sealed class NullableDnaJsonConverter : JsonConverter<long?>
{
    public override long? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;
        return reader.TokenType switch
        {
            JsonTokenType.String => DnaJsonConverter.ParseDnaString(reader.GetString()),
            JsonTokenType.Number => reader.GetInt64(),
            _ => throw new JsonException($"Expected string, number, or null for DNA, got {reader.TokenType}.")
        };
    }

    public override void Write(Utf8JsonWriter writer, long? value, JsonSerializerOptions options)
    {
        if (value is null)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(value.Value.ToString(CultureInfo.InvariantCulture));
    }
}
