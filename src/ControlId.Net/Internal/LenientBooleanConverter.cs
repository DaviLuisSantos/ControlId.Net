using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ControlId.Internal;

/// <summary>
/// Para campos documentados como <c>bool</c>: escreve <c>true</c>/<c>false</c>, mas aceita 0/1 na leitura,
/// porque o banco do equipamento costuma devolver inteiros.
/// </summary>
internal sealed class LenientBooleanConverter : JsonConverter<bool>
{
    private static readonly IntBooleanConverter Reader = new();

    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        Reader.Read(ref reader, typeToConvert, options);

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options) =>
        writer.WriteBooleanValue(value);
}
