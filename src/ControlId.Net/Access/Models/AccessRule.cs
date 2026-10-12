using System.Text.Json.Serialization;

namespace ControlId.Access;

/// <summary>Objeto <c>access_rules</c>: liga quem (usuários/grupos) a quando (horários) e onde (portais).</summary>
[ControlIdObject("access_rules")]
public sealed class AccessRule
{
    /// <summary>ID no equipamento.</summary>
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    /// <summary>Nome da regra.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Se a regra libera ou bloqueia.</summary>
    [JsonPropertyName("type")]
    public AccessRuleType? Type { get; set; }

    /// <summary>Prioridade (campo reservado em boa parte dos firmwares; use 0).</summary>
    [JsonPropertyName("priority")]
    public int? Priority { get; set; }
}

/// <summary>Valor de <see cref="AccessRule.Type"/>.</summary>
public enum AccessRuleType
{
    /// <summary>Bloqueia o acesso.</summary>
    Block = 0,

    /// <summary>Libera o acesso.</summary>
    Allow = 1,
}
