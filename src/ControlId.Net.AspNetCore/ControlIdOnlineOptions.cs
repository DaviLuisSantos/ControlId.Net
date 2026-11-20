using System;
using System.Threading;
using ControlId.Access.Online;

namespace ControlId.AspNetCore;

/// <summary>
/// Comportamento das rotas do modo online (<c>MapControlIdOnline</c>) quando o <see cref="IOnlineAccessHandler"/> não decide.
/// Configure com <c>services.Configure&lt;ControlIdOnlineOptions&gt;(o =&gt; ...)</c>; sem configuração valem os padrões.
/// </summary>
public sealed class ControlIdOnlineOptions
{
    /// <summary>Padrão de <see cref="DecisionTimeout"/>: 3 segundos (o equipamento espera até 5 s).</summary>
    public static readonly TimeSpan DefaultDecisionTimeout = TimeSpan.FromSeconds(3);

    private OnlineAuthorization _fallbackAuthorization = OnlineAuthorization.Deny(message: "Sistema indisponível");
    private TimeSpan _decisionTimeout = DefaultDecisionTimeout;

    /// <summary>
    /// Resposta (HTTP 200) enviada ao equipamento quando o handler lança exceção, devolve <see langword="null"/>, estoura o
    /// <see cref="DecisionTimeout"/> ou quando a requisição não pode ser interpretada. Evita o 500, que o equipamento conta como
    /// falha de comunicação e que o leva à contingência. O padrão nega o acesso com a mensagem "Sistema indisponível".
    /// </summary>
    /// <exception cref="ArgumentNullException">Valor nulo.</exception>
    public OnlineAuthorization FallbackAuthorization
    {
        get => _fallbackAuthorization;
        set => _fallbackAuthorization = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Tempo máximo que o <see cref="IOnlineAccessHandler.AuthorizeAsync"/> tem para decidir (padrão 3 s). Vencido o prazo, o
    /// <see cref="CancellationToken"/> do handler é cancelado e o equipamento recebe o <see cref="FallbackAuthorization"/>.
    /// <see cref="Timeout.InfiniteTimeSpan"/> desliga o prazo.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Valor menor ou igual a zero que não seja <see cref="Timeout.InfiniteTimeSpan"/>.</exception>
    public TimeSpan DecisionTimeout
    {
        get => _decisionTimeout;
        set => _decisionTimeout = value > TimeSpan.Zero || value == Timeout.InfiniteTimeSpan
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "O prazo de decisão do modo online deve ser maior que zero (ou Timeout.InfiniteTimeSpan).");
    }
}
