using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ControlId.Internal;

/// <summary>O equipamento usa 0/1 para flags; na leitura aceita também true/false.</summary>
internal sealed class IntBooleanConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Number => reader.GetInt64() != 0,
            JsonTokenType.String => reader.GetString() is "1" or "true",
            _ => throw new JsonException($"Esperado 0/1, recebido {reader.TokenType}."),
        };

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value ? 1 : 0);
}
