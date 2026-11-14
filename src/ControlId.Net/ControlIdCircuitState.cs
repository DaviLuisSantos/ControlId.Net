namespace ControlId;

/// <summary>
/// Estado do circuit breaker de um equipamento (<see cref="ControlIdDeviceOptions.CircuitBreakerFailureThreshold"/>).
/// </summary>
public enum ControlIdCircuitState
{
    /// <summary>Normal: as requisições seguem para o equipamento. É o estado permanente quando o circuit breaker está desligado.</summary>
    Closed = 0,

    /// <summary>Aberto: o equipamento falhou várias vezes seguidas e as requisições são recusadas sem sair da máquina.</summary>
    Open = 1,

    /// <summary>Meio aberto: passado o tempo de pausa, uma única requisição de sonda testa o equipamento; as demais são recusadas.</summary>
    HalfOpen = 2,
}
