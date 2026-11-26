using System.Text.Json.Serialization;

namespace ControlId.Access;

/// <summary>Objeto <c>wiegand_modes</c>: formatos Wiegand suportados (tabela de consulta, somente leitura). Apenas V7.12.0 ou superior.</summary>
[ControlIdObject("wiegand_modes")]
public sealed class WiegandMode
{
    /// <summary>ID do formato; referenciado por <see cref="AccessCard.WiegandFormatId"/>.</summary>
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    /// <summary>Nome do formato (ex.: "26", "34", "37(H10302)", "custom").</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Número total de bits do formato.</summary>
    [JsonPropertyName("size")]
    public int? Size { get; set; }

    /// <summary>Layout dos campos de dados, ex.: <c>F[2:9], C[10:25]</c>.</summary>
    [JsonPropertyName("format")]
    public string? Format { get; set; }

    /// <summary>Layout dos bits de paridade; vazio quando não há paridade.</summary>
    [JsonPropertyName("parities")]
    public string? Parities { get; set; }

    /// <summary>Ordem de cálculo dos bits de paridade, ex.: <c>1, 26</c>.</summary>
    [JsonPropertyName("parities_calculation_order")]
    public string? ParitiesCalculationOrder { get; set; }

    /// <summary>Bits do código de área (F) lidos em ordem invertida (0 ou 1).</summary>
    [JsonPropertyName("inverted_facility")]
    public int? InvertedFacility { get; set; }

    /// <summary>Bits do número do cartão (C) lidos em ordem invertida (0 ou 1).</summary>
    [JsonPropertyName("inverted_card")]
    public int? InvertedCard { get; set; }
}
