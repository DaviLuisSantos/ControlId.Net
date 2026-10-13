using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ControlId.Internal;

/// <summary>
/// Para chaves estrangeiras opcionais: o equipamento devolve 0 quando o campo está nulo, mas recusa 0 na gravação
/// (<c>FOREIGN KEY constraint failed</c>). Lê 0 como <c>null</c>; um <c>null</c> não é enviado.
/// </summary>
internal sealed class ZeroAsNullConverter : JsonConverter<int?>
{
    public override bool HandleNull => true;

    public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.Number => reader.GetInt32() is var value && value != 0 ? value : null,
            JsonTokenType.String => int.TryParse(reader.GetString(), out var value) && value != 0 ? value : null,
            _ => throw new JsonException($"Esperado número, recebido {reader.TokenType}."),
        };

    public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
    {
        if (value is { } number)
        {
            writer.WriteNumberValue(number);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
