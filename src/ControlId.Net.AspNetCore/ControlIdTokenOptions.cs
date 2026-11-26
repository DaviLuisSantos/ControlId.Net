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
    /// Token anterior, aceito junto com <see cref="Token"/> durante uma rotação: publique o novo em <see cref="Token"/>, mantenha
    /// o antigo aqui até todos os equipamentos receberem o novo e então remova-o. Vale nas mesmas condições de <see cref="Token"/>.
    /// </summary>
    public string? PreviousToken { get; set; }

    /// <summary>
    /// Token por equipamento, a partir do <c>deviceId</c>. Quando devolve um valor não vazio, só ele é aceito para esse
    /// equipamento. Quando devolve <see langword="null"/> (ou o callback não traz <c>deviceId</c>), a requisição é recusada,
    /// a não ser que <see cref="AllowTokenFallback"/> esteja ligado. Só é chamado quando há um cabeçalho <c>Bearer</c> na requisição.
    /// </summary>
    public Func<long, string?>? DeviceToken { get; set; }

    /// <summary>
    /// Com <see cref="DeviceToken"/> definido, aceita <see cref="Token"/>/<see cref="PreviousToken"/> para equipamentos sem token
    /// próprio (função devolvendo <see langword="null"/>) e para callbacks sem <c>deviceId</c>. Padrão <see langword="false"/>:
    /// um token compartilhado vazado autenticaria todo o parque. Sem <see cref="DeviceToken"/>, não tem efeito.
    /// </summary>
    public bool AllowTokenFallback { get; set; }
}
