namespace ControlId;

/// <summary>
/// Diagnóstico de um cliente de equipamento: sessão e circuit breaker. Implementada por
/// <see cref="Access.AccessClient"/> e <see cref="Rep.RepClient"/>; use com <c>client as IControlIdClientDiagnostics</c>
/// quando só tiver a interface do cliente (<see cref="Access.IAccessClient"/>, <see cref="Rep.IRepClient"/>), como num
/// health check.
/// </summary>
public interface IControlIdClientDiagnostics
{
    /// <summary>Sessão atual, ou <c>null</c> se ainda não logou (ou depois do logout).</summary>
    string? Session { get; }

    /// <summary>
    /// Estado do circuit breaker (<see cref="ControlIdDeviceOptions.CircuitBreakerFailureThreshold"/>). Sempre
    /// <see cref="ControlIdCircuitState.Closed"/> com o circuit breaker desligado.
    /// </summary>
    ControlIdCircuitState CircuitState { get; }

    /// <summary>
    /// Fecha o circuit breaker na hora, sem esperar a pausa nem a sonda: zera a contagem de falhas e descarta a sonda em
    /// andamento (o resultado dela passa a ser ignorado). Útil depois de consertar o equipamento ou a rede. Registra o
    /// log 1011. Sem efeito com o circuit breaker desligado.
    /// </summary>
    void ResetCircuit();
}
