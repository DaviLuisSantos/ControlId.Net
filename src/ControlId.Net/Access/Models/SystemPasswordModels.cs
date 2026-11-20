using System.Text.Json.Serialization;

namespace ControlId.Access;

/// <summary>
/// Resposta de <c>master_password.fcgi</c> sem <c>password</c>: hash e salt da senha mestra atual.
/// O <see cref="ToString"/> não mostra o hash nem o salt.
/// </summary>
public sealed class MasterPasswordInfo
{
    /// <summary>Hash da senha mestra atual.</summary>
    [JsonPropertyName("hash")]
    public string Hash { get; set; } = string.Empty;

    /// <summary>Salt usado para gerar o hash.</summary>
    [JsonPropertyName("salt")]
    public string Salt { get; set; } = string.Empty;

    /// <inheritdoc/>
    public override string ToString() => "MasterPasswordInfo { Hash = ***, Salt = *** }";
}
