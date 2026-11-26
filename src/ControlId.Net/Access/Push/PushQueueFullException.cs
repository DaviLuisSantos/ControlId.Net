namespace ControlId.Access.Push;

/// <summary>
/// A fila do equipamento atingiu <see cref="PushServerOptions.MaxPendingPerDevice"/>: o envio não foi enfileirado.
/// Normalmente o equipamento está desligado ou sem rede; espere a fila andar ou cancele envios antigos.
/// </summary>
public sealed class PushQueueFullException : ControlIdException
{
    /// <summary>Cria a exceção.</summary>
    /// <param name="deviceId">Equipamento cuja fila está cheia.</param>
    /// <param name="limit">Limite configurado em <see cref="PushServerOptions.MaxPendingPerDevice"/>.</param>
    public PushQueueFullException(long deviceId, int limit)
        : base($"A fila de Push do equipamento {deviceId} está cheia ({limit} envios pendentes ou em andamento).")
    {
        DeviceId = deviceId;
        Limit = limit;
    }

    /// <summary>Equipamento cuja fila está cheia.</summary>
    public long DeviceId { get; }

    /// <summary>Limite de envios por equipamento (<see cref="PushServerOptions.MaxPendingPerDevice"/>).</summary>
    public int Limit { get; }
}
