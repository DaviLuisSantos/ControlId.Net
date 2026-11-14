using System;

namespace ControlId;

/// <summary>
/// A requisição foi recusada porque o circuit breaker do equipamento está aberto
/// (<see cref="ControlIdDeviceOptions.CircuitBreakerFailureThreshold"/>): nada foi enviado ao equipamento.
/// </summary>
public sealed class ControlIdCircuitOpenException : ControlIdException
{
    /// <summary>Cria a exceção.</summary>
    public ControlIdCircuitOpenException(string message, TimeSpan retryAfter)
        : base(message)
    {
        RetryAfter = retryAfter;
    }

    /// <summary>
    /// Quanto falta para o circuito aceitar uma requisição de sonda. É <see cref="TimeSpan.Zero"/> quando a pausa já passou e
    /// uma sonda está em andamento: o resultado dela decide se o circuito fecha ou reabre.
    /// </summary>
    public TimeSpan RetryAfter { get; }
}
