using System.Text.Json.Serialization;

namespace ControlId.Access;

/// <summary>Objeto <c>cards</c> (cartões de proximidade) da linha de acesso.</summary>
[ControlIdObject("cards")]
public sealed class AccessCard
{
    /// <summary>ID no equipamento.</summary>
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    /// <summary>Valor do cartão.</summary>
    [JsonPropertyName("value")]
    public long Value { get; set; }

    /// <summary>ID do usuário dono do cartão.</summary>
    [JsonPropertyName("user_id")]
    public long UserId { get; set; }
}
