using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ControlId.Internal;

/// <summary>Resposta de <c>create_objects</c>, compartilhada entre HTTP e Push.</summary>
internal sealed class IdsResponse
{
    [JsonPropertyName("ids")]
    public List<long>? Ids { get; set; }
}

/// <summary>Resposta de <c>modify_objects</c>/<c>destroy_objects</c>, compartilhada entre HTTP e Push.</summary>
internal sealed class ChangesResponse
{
    [JsonPropertyName("changes")]
    public int Changes { get; set; }
}
