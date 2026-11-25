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
        return type == typeof(int) ? (nullable ? new NullableInt32Converter() : new Int32Converter())
            : type == typeof(long) ? (nullable ? new NullableInt64Converter() : new Int64Converter())
            : nullable ? new NullableDoubleConverter() : new DoubleConverter();
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

    private sealed class Int32Converter : JsonConverter<int>
    {
        public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => ReadInt32(reader) ?? 0;
        public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) => writer.WriteNumberValue(value);
    }

    private sealed class NullableInt32Converter : JsonConverter<int?>
    {
        public override bool HandleNull => true;
        public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => ReadInt32(reader);
        public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
        { if (value.HasValue) writer.WriteNumberValue(value.Value); else writer.WriteNullValue(); }
    }

    private sealed class Int64Converter : JsonConverter<long>
    {
        public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => ReadInt64(reader) ?? 0;
        public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options) => writer.WriteNumberValue(value);
    }

    private sealed class NullableInt64Converter : JsonConverter<long?>
    {
        public override bool HandleNull => true;
        public override long? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => ReadInt64(reader);
        public override void Write(Utf8JsonWriter writer, long? value, JsonSerializerOptions options)
        { if (value.HasValue) writer.WriteNumberValue(value.Value); else writer.WriteNullValue(); }
    }

    private sealed class DoubleConverter : JsonConverter<double>
    {
        public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => ReadDouble(reader) ?? 0;
        public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options) => writer.WriteNumberValue(value);
    }

    private sealed class NullableDoubleConverter : JsonConverter<double?>
    {
        public override bool HandleNull => true;
        public override double? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => ReadDouble(reader);
        public override void Write(Utf8JsonWriter writer, double? value, JsonSerializerOptions options)
        { if (value.HasValue) writer.WriteNumberValue(value.Value); else writer.WriteNullValue(); }
    }
}
