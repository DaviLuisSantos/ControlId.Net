using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ControlId.Internal;

/// <summary>
/// Lê string mesmo quando o firmware manda número, booleano ou objeto (ex.: <c>"pin_value": 1324</c>):
/// nesses casos guarda o texto JSON do valor.
/// </summary>
internal sealed class LenientStringConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString();
        }

        using var document = JsonDocument.ParseValue(ref reader);
        return document.RootElement.GetRawText();
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
