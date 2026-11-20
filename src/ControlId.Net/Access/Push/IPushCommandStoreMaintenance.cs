using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ControlId.Access.Push;

/// <summary>
/// Manutenção opcional de um <see cref="IPushCommandStore"/>: remoção em massa de envios vencidos, usada por
/// <see cref="PushServer.PurgeExpiredAsync"/>. Um store que não a implementa simplesmente não é varrido (os envios
/// pendentes vencidos ainda são descartados na consulta do equipamento, por <see cref="PushServerOptions.CommandTtl"/>).
/// Implementada por <see cref="InMemoryPushCommandStore"/>.
/// </summary>
public interface IPushCommandStoreMaintenance
{
    /// <summary>
    /// Remove, de todos os equipamentos, e devolve os envios vencidos:
    /// <list type="bullet">
    /// <item><see cref="PushEnvelopeStatus.Pending"/> com <see cref="PushEnvelope.CreatedAt"/> anterior a <paramref name="pendingCreatedBefore"/>;</item>
    /// <item><see cref="PushEnvelopeStatus.InFlight"/> com <see cref="PushEnvelope.DeliveredAt"/> (ou, se nulo, <see cref="PushEnvelope.CreatedAt"/>)
    /// anterior a <paramref name="inFlightDeliveredBefore"/>.</item>
    /// </list>
    /// As comparações são estritas (um envio exatamente no limite fica); <see cref="DateTimeOffset.MinValue"/> desliga o critério.
    /// Os envios devolvidos trazem a situação que tinham ao serem removidos; a ordem não é garantida.
    /// </summary>
    /// <param name="pendingCreatedBefore">Limite de criação dos envios pendentes.</param>
    /// <param name="inFlightDeliveredBefore">Limite de entrega dos envios em andamento.</param>
    /// <param name="cancellationToken">Cancela a operação.</param>
    Task<IReadOnlyList<PushEnvelope>> RemoveExpiredAsync(DateTimeOffset pendingCreatedBefore, DateTimeOffset inFlightDeliveredBefore, CancellationToken cancellationToken);
}
