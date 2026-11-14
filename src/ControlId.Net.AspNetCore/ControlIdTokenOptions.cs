using System;

namespace ControlId.AspNetCore;

/// <summary>
/// Token Bearer exigido nos callbacks (Push, Monitor, Online) do equipamento. O equipamento envia o valor
/// configurado em <c>curl_bearer_token</c> no cabeçalho <c>Authorization: Bearer</c>.
/// </summary>
public sealed class ControlIdTokenOptions
{
    /// <summary>Token único aceito para todos os equipamentos.</summary>
    public string? Token { get; set; }

    /// <summary>
    /// Token por equipamento, a partir do <c>deviceId</c>. Quando devolve um valor não nulo, ele vence <see cref="Token"/>;
    /// quando devolve <see langword="null"/>, vale <see cref="Token"/>.
    /// </summary>
    public Func<long, string?>? DeviceToken { get; set; }
}
