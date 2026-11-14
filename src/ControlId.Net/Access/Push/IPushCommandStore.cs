using System;
using System.Threading;
using System.Threading.Tasks;

namespace ControlId.Access.Push;

/// <summary>
/// Fila de envios do <see cref="PushServer"/>. A implementação padrão é <see cref="InMemoryPushCommandStore"/>;
/// um store durável (banco de dados) permite que comandos pendentes sobrevivam a um reinício.
/// <para>
/// O <see cref="PushServer"/> chama o store em série por equipamento e em paralelo entre equipamentos.
/// Uma instância de <see cref="PushServer"/> por store.
/// </para>
/// </summary>
public interface IPushCommandStore
{
    /// <summary>Acrescenta o envio ao fim da fila do equipamento, como <see cref="PushEnvelopeStatus.Pending"/>.</summary>
    Task AddAsync(PushEnvelope envelope, CancellationToken cancellationToken);

    /// <summary>
    /// Remove o envio em andamento (<see cref="PushEnvelopeStatus.InFlight"/>), se houver, devolvendo-o em
    /// <see cref="PushTake.Orphan"/>; depois marca o <see cref="PushEnvelopeStatus.Pending"/> mais antigo como
    /// <see cref="PushEnvelopeStatus.InFlight"/> com o <paramref name="uuid"/> e o devolve em <see cref="PushTake.Next"/>.
    /// </summary>
    Task<PushTake> TakeNextAsync(long deviceId, string? uuid, CancellationToken cancellationToken);

    /// <summary>
    /// Remove e devolve o envio em andamento, ou <c>null</c> se não há nenhum ou se o <paramref name="uuid"/> não confere.
    /// Um <paramref name="uuid"/> nulo, de qualquer lado, conta como coincidência.
    /// </summary>
    Task<PushEnvelope?> CompleteAsync(long deviceId, string? uuid, CancellationToken cancellationToken);

    /// <summary>Remove o envio (pendente ou em andamento). <c>true</c> se ele existia.</summary>
    Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Quantidade de envios do equipamento, pendentes mais o em andamento.</summary>
    Task<int> CountAsync(long deviceId, CancellationToken cancellationToken);
}

/// <summary>Resultado de <see cref="IPushCommandStore.TakeNextAsync"/>.</summary>
public readonly struct PushTake
{
    /// <summary>Cria o resultado.</summary>
    public PushTake(PushEnvelope? orphan, PushEnvelope? next)
    {
        Orphan = orphan;
        Next = next;
    }

    /// <summary>Envio que estava em andamento e não recebeu resultado (o equipamento consultou de novo); nunca é reenviado.</summary>
    public PushEnvelope? Orphan { get; }

    /// <summary>Envio a entregar agora, ou <c>null</c> se a fila está vazia.</summary>
    public PushEnvelope? Next { get; }
}
