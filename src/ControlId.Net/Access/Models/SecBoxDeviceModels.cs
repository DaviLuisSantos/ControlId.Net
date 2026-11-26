using System.Text.Json.Serialization;

namespace ControlId.Access;

/// <summary>
/// Resposta de <c>update_secbox_firmware_status.fcgi</c>: progresso da atualização de firmware do módulo SecBox
/// (linha iDAccess).
/// </summary>
public sealed class SecBoxFirmwareUpdateStatus
{
    /// <summary>Campo <c>isFinished</c>: <c>true</c> quando o processo de atualização de firmware foi concluído.</summary>
    [JsonPropertyName("isFinished")]
    public bool IsFinished { get; set; }

    /// <summary>Campo <c>bitsWritten</c>: número de bits gravados no SecBox durante a atualização.</summary>
    [JsonPropertyName("bitsWritten")]
    public int BitsWritten { get; set; }
}

/// <summary>
/// Resposta de <c>change_crypto_key_default.fcgi</c>, <c>change_crypto_key_random.fcgi</c> e
/// <c>reset_crypto_key.fcgi</c>: código de resultado da operação sobre a chave de segurança do SecBox.
/// </summary>
public sealed class SecBoxKeyChangeResult
{
    /// <summary>Código devolvido pelo equipamento no campo <c>Status Key</c> em caso de sucesso.</summary>
    public const string SuccessCode = "00";

    /// <summary>Campo <c>Status Key</c>: código de resultado da operação; <c>"00"</c> em caso de sucesso.</summary>
    [JsonPropertyName("Status Key")]
    public string StatusKey { get; set; } = string.Empty;

    /// <summary><c>true</c> quando <see cref="StatusKey"/> é <c>"00"</c>.</summary>
    [JsonIgnore]
    public bool Succeeded => string.Equals(StatusKey, SuccessCode, System.StringComparison.Ordinal);
}

/// <summary>
/// Resposta de <c>get_dual_sided_status.fcgi</c>: estado do dual sided do SecBox (linha iDAccess). O segredo
/// compartilhado nunca é devolvido pelo equipamento.
/// </summary>
public sealed class DualSidedStatus
{
    /// <summary>Campo <c>secret_configured</c>: indica se há um segredo compartilhado gravado no equipamento.</summary>
    [JsonPropertyName("secret_configured")]
    public bool SecretConfigured { get; set; }

    /// <summary>Campo <c>door1_peer_address_valid</c>: indica se <c>door1_peer_address</c> (módulo <c>dual_sided</c>) contém um endereço válido.</summary>
    [JsonPropertyName("door1_peer_address_valid")]
    public bool Door1PeerAddressValid { get; set; }
}
