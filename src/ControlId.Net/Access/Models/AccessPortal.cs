using System.Text.Json.Serialization;

namespace ControlId.Access;

/// <summary>Objeto <c>portals</c>: passagem entre duas áreas (porta, sentido da catraca...).</summary>
[ControlIdObject("portals")]
public sealed class AccessPortal
{
    /// <summary>ID no equipamento.</summary>
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    /// <summary>Nome do portal.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Área de origem.</summary>
    [JsonPropertyName("area_from_id")]
    public long? AreaFromId { get; set; }

    /// <summary>Área de destino.</summary>
    [JsonPropertyName("area_to_id")]
    public long? AreaToId { get; set; }
}
