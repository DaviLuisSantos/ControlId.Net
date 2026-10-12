using System.Text.Json.Serialization;

namespace ControlId.Access;

/// <summary>Objeto <c>templates</c> (biometria de digital) da linha de acesso.</summary>
[ControlIdObject("templates")]
public sealed class AccessTemplate
{
    /// <summary>ID no equipamento.</summary>
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    /// <summary>ID do usuário dono da digital.</summary>
    [JsonPropertyName("user_id")]
    public long UserId { get; set; }

    /// <summary>Template da digital em Base64, no formato gerado pelo equipamento.</summary>
    [JsonPropertyName("template")]
    public string? Template { get; set; }

    /// <summary>0 = digital comum, 1 = digital de pânico.</summary>
    [JsonPropertyName("finger_type")]
    public int? FingerType { get; set; }

    /// <summary>Posição do dedo (campo reservado em boa parte dos firmwares).</summary>
    [JsonPropertyName("finger_position")]
    public int? FingerPosition { get; set; }
}
