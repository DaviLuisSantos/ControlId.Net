using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ControlId.Internal;

internal static class ControlIdJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,

        // Envia acentos e caracteres como < > + sem escapar (\u00E3, \u003E...): não depende
        // do firmware decodificar escapes, e os operadores do "where" chegam literalmente.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Serialize(object? value) =>
        value is null ? "{}" : JsonSerializer.Serialize(value, value.GetType(), Options);

    public static T Deserialize<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            json = "{}";
        }

        return JsonSerializer.Deserialize<T>(json, Options)!;
    }
}
