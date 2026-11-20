using System.Text.Json.Serialization;

namespace ControlId.Access;

/// <summary>
/// Objeto <c>group_compatibility</c>: grupos aceitos como segunda pessoa na autenticação de 2 pessoas. Apenas iDFace Max HID.
/// A regra é unidirecional: para valer nos dois sentidos, crie também o registro inverso.
/// </summary>
[ControlIdObject("group_compatibility", Key = null)]
public sealed class GroupCompatibility
{
    /// <summary>Grupo que apresenta a credencial primeiro.</summary>
    [JsonPropertyName("group_id")]
    public long GroupId { get; set; }

    /// <summary>Grupo aceito como segunda pessoa.</summary>
    [JsonPropertyName("compatible_group_id")]
    public long CompatibleGroupId { get; set; }
}
