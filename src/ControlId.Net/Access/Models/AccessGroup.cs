using System.Text.Json.Serialization;

namespace ControlId.Access;

/// <summary>Objeto <c>groups</c> (departamentos) da linha de acesso.</summary>
public sealed class AccessGroup
{
    /// <summary>ID no equipamento.</summary>
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    /// <summary>Nome do grupo.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
