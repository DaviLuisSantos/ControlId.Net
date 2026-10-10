using System.Text.Json.Serialization;

namespace ControlId.Access;

/// <summary>Objeto <c>time_zones</c>: um horário nomeado, composto por intervalos (<see cref="AccessTimeSpan"/>).</summary>
public sealed class AccessTimeZone
{
    /// <summary>ID no equipamento.</summary>
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    /// <summary>Nome do horário.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
