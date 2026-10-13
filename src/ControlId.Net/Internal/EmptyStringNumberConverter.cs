using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ControlId.Internal;

/// <summary>
/// Para os valores do Monitor, que chegam como texto: <c>""</c> num campo numérico (o iDFace manda assim
/// <c>user_id</c> de rosto não identificado e <c>confidence</c> de cartão) vira <c>null</c>, ou 0 se o campo não for anulável.
/// </summary>
internal sealed class EmptyStringNumberConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        var type = Nullable.GetUnderlyingType(typeToConvert) ?? typeToConvert;
        return type == typeof(int) || type == typeof(long) || type == typeof(double);
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var nullable = Nullable.GetUnderlyingType(typeToConvert) is not null;
        var type = Nullable.GetUnderlyingType(typeToConvert) ?? typeToConvert;
        return type == typeof(int) ? (nullable ? new Converter<int?>(ReadInt32) : new Converter<int>(r => ReadInt32(r) ?? 0))
            : type == typeof(long) ? (nullable ? new Converter<long?>(ReadInt64) : new Converter<long>(r => ReadInt64(r) ?? 0))
            : nullable ? new Converter<double?>(ReadDouble) : new Converter<double>(r => ReadDouble(r) ?? 0);
    }

    private static int? ReadInt32(Utf8JsonReader reader) => checked((int?)ReadInt64(reader));

    private static long? ReadInt64(Utf8JsonReader reader) => reader.TokenType switch
    {
        JsonTokenType.Null => null,
        JsonTokenType.Number => reader.GetInt64(),
        JsonTokenType.String when string.IsNullOrWhiteSpace(reader.GetString()) => null,
        JsonTokenType.String => long.Parse(reader.GetString()!, NumberStyles.Integer, CultureInfo.InvariantCulture),
        _ => throw new JsonException($"Esperado número, recebido {reader.TokenType}."),
    };

    private static double? ReadDouble(Utf8JsonReader reader) => reader.TokenType switch
    {
        JsonTokenType.Null => null,
        JsonTokenType.Number => reader.GetDouble(),
        JsonTokenType.String when string.IsNullOrWhiteSpace(reader.GetString()) => null,
        JsonTokenType.String => double.Parse(reader.GetString()!, NumberStyles.Float, CultureInfo.InvariantCulture),
        _ => throw new JsonException($"Esperado número, recebido {reader.TokenType}."),
    };

    private delegate T ReadValue<T>(Utf8JsonReader reader);

    private sealed class Converter<T> : JsonConverter<T>
    {
        private readonly ReadValue<T> _read;

        public Converter(ReadValue<T> read) => _read = read;

        public override bool HandleNull => true;

        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => _read(reader);

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value, ControlIdJson.Options);
    }
}
