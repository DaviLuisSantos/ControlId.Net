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

    /// <summary>ID do formato Wiegand do cartão (<see cref="WiegandMode.Id"/>). Apenas V7.12.0 ou superior.</summary>
    [JsonPropertyName("wiegand_format_id")]
    public int? WiegandFormatId { get; set; }

    /// <summary>Tipo de credencial: 0 = cartão, 1 = credencial facial vinculada a cartão, 2 = QR Code. Apenas V7.12.0 ou superior.</summary>
    [JsonPropertyName("credential_type")]
    public int? CredentialType { get; set; }
}
