using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ControlId.Rep;

/// <summary>Empregador cadastrado no REP (<c>load_company.fcgi</c> e <c>edit_company.fcgi</c>).</summary>
public sealed class RepCompany
{
    /// <summary>Razão social.</summary>
    [JsonPropertyName("name")] public string? Name { get; set; }

    /// <summary>Endereço.</summary>
    [JsonPropertyName("address")] public string? Address { get; set; }

    /// <summary>Se <see cref="Document"/> é CNPJ ou CPF.</summary>
    [JsonPropertyName("tipo_doc")] public RepCompanyDocumentType DocumentType { get; set; }

    /// <summary>
    /// CNPJ ou CPF, conforme <see cref="DocumentType"/>. Aceita pontuação e o CNPJ alfanumérico. O firmware anterior à
    /// revisão 104 só aceita número: só dígitos vão como número; com letras, como texto (exige a revisão 104).
    /// </summary>
    [JsonPropertyName("cpf_cnpj"), JsonConverter(typeof(RepDocumentNumberConverter))]
    public string? Document { get; set; }

    /// <summary>CEI; na Portaria 671, também CAEPF ou CNO.</summary>
    [JsonPropertyName("cei")] public long? Cei { get; set; }

    /// <summary>CPF do responsável.</summary>
    [JsonPropertyName("cpf")] public long? ResponsibleCpf { get; set; }
}

/// <summary>Tipo do documento do empregador (<c>tipo_doc</c>).</summary>
public enum RepCompanyDocumentType
{
    /// <summary>Não informado.</summary>
    None = 0,

    /// <summary>CNPJ.</summary>
    Cnpj = 1,

    /// <summary>CPF.</summary>
    Cpf = 2,
}

/// <summary>
/// <c>cpf_cnpj</c>: o firmware devolve número (antes da revisão 104) ou texto, e só aceita texto a partir da 104. Lê os
/// dois; grava como número quando, sem pontuação, só há dígitos (funciona em todas as revisões) e como texto quando há
/// letras (CNPJ alfanumérico).
/// </summary>
internal sealed class RepDocumentNumberConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType switch
    {
        JsonTokenType.Null => null,
        JsonTokenType.String => reader.GetString(),
        JsonTokenType.Number => reader.GetInt64().ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => throw new JsonException($"cpf_cnpj inesperado: {reader.TokenType}."),
    };

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        var digits = value.Replace(".", string.Empty).Replace("/", string.Empty).Replace("-", string.Empty).Trim();
        if (digits.Length is > 0 and <= 18 && IsDigits(digits))
        {
            writer.WriteNumberValue(long.Parse(digits, System.Globalization.CultureInfo.InvariantCulture));
        }
        else
        {
            writer.WriteStringValue(value);
        }
    }

    private static bool IsDigits(string value)
    {
        foreach (var c in value)
        {
            if (c is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }
}
