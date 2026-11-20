using System.Text.Json.Serialization;

namespace ControlId.Access;

/// <summary>Objeto <c>groups</c> (departamentos) da linha de acesso.</summary>
[ControlIdObject("groups")]
public sealed class AccessGroup
{
    /// <summary>ID no equipamento.</summary>
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    /// <summary>Nome do grupo.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Modo de identificação dos usuários do grupo: 0 = 1:N, 1 = 1:1, 2 = Template no Cartão (ToC); <c>null</c> usa a configuração global. Apenas iDFace Max HID.</summary>
    [JsonPropertyName("identification_mode")]
    public int? IdentificationMode { get; set; }
}
