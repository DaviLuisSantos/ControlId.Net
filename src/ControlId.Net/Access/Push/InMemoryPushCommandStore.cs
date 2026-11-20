using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ControlId.Access.Push;

/// <summary>
/// <see cref="IPushCommandStore"/> em memória (padrão do <see cref="PushServer"/>): rápido, mas os envios pendentes
/// se perdem quando o processo reinicia. Só guarda equipamentos com envios: a fila que esvazia é descartada, então
/// consultas de <c>deviceId</c> desconhecidos não ocupam memória. Implementa <see cref="IPushCommandStoreMaintenance"/>.
/// </summary>
public sealed class InMemoryPushCommandStore : IPushCommandStore, IPushCommandStoreMaintenance
{
    private readonly ConcurrentDictionary<long, DeviceQueue> _devices = new();
    private readonly Func<DateTimeOffset> _clock;

    /// <summary>Cria o store vazio.</summary>
    public InMemoryPushCommandStore()
        : this(static () => DateTimeOffset.UtcNow)
    {
    }

    /// <summary>Cria o store com o relógio de <see cref="PushEnvelope.DeliveredAt"/> (testes).</summary>
    internal InMemoryPushCommandStore(Func<DateTimeOffset> clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>Equipamentos com fila guardada (testes).</summary>
    internal int DeviceCount => _devices.Count;

    /// <inheritdoc />
    public Task AddAsync(PushEnvelope envelope, CancellationToken cancellationToken)
    {
        if (envelope is null)
        {
            throw new ArgumentNullException(nameof(envelope));
        }

        var copy = Copy(envelope);
        copy.Status = PushEnvelopeStatus.Pending;
        copy.Uuid = null;
        copy.DeliveredAt = null;

        while (true)
        {
            var queue = _devices.GetOrAdd(envelope.DeviceId, static _ => new DeviceQueue());
            lock (queue)
            {
                // A fila pode ter sido descartada (esvaziou) entre o GetOrAdd e o lock: tenta de novo com uma nova.
                if (queue.Discarded)
                {
                    continue;
                }

                queue.Pending.AddLast(copy);
                return Task.CompletedTask;
            }
        }
    }

    /// <inheritdoc />
    public Task<PushTake> TakeNextAsync(long deviceId, string? uuid, CancellationToken cancellationToken)
    {
        if (!_devices.TryGetValue(deviceId, out var queue))
        {
            return Task.FromResult(default(PushTake));
        }

        PushEnvelope? orphan;
        PushEnvelope? next = null;
        lock (queue)
        {
            orphan = queue.InFlight;
            queue.InFlight = null;

            if (queue.Pending.First is { } first)
            {
                queue.Pending.RemoveFirst();
                next = first.Value;
                next.Status = PushEnvelopeStatus.InFlight;
                next.Uuid = uuid;
                next.DeliveredAt = _clock();
                queue.InFlight = next;
            }

            DiscardIfEmpty(deviceId, queue);
        }

        return Task.FromResult(new PushTake(orphan is null ? null : Copy(orphan), next is null ? null : Copy(next)));
    }

    /// <inheritdoc />
    public Task<PushEnvelope?> CompleteAsync(long deviceId, string? uuid, CancellationToken cancellationToken)
    {
        if (!_devices.TryGetValue(deviceId, out var queue))
        {
            return Task.FromResult<PushEnvelope?>(null);
        }

        lock (queue)
        {
            var current = queue.InFlight;
            if (current is null || (uuid is not null && current.Uuid is not null && uuid != current.Uuid))
            {
                return Task.FromResult<PushEnvelope?>(null);
            }

            queue.InFlight = null;
            DiscardIfEmpty(deviceId, queue);
            return Task.FromResult<PushEnvelope?>(Copy(current));
        }
    }

    /// <inheritdoc />
    public Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken)
    {
        foreach (var pair in _devices)
        {
            var queue = pair.Value;
            lock (queue)
            {
                if (queue.InFlight is { } inFlight && inFlight.Id == id)
                {
                    queue.InFlight = null;
                    DiscardIfEmpty(pair.Key, queue);
                    return Task.FromResult(true);
                }

                for (var node = queue.Pending.First; node is not null; node = node.Next)
                {
                    if (node.Value.Id == id)
                    {
                        queue.Pending.Remove(node);
                        DiscardIfEmpty(pair.Key, queue);
                        return Task.FromResult(true);
                    }
                }
            }
        }

        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task<int> CountAsync(long deviceId, CancellationToken cancellationToken)
    {
        if (!_devices.TryGetValue(deviceId, out var queue))
        {
            return Task.FromResult(0);
        }

        lock (queue)
        {
            return Task.FromResult(queue.Pending.Count + (queue.InFlight is null ? 0 : 1));
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PushEnvelope>> RemoveExpiredAsync(DateTimeOffset pendingCreatedBefore, DateTimeOffset inFlightDeliveredBefore, CancellationToken cancellationToken)
    {
        var removed = new List<PushEnvelope>();
        foreach (var pair in _devices)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var queue = pair.Value;
            lock (queue)
            {
                if (queue.InFlight is { } inFlight && (inFlight.DeliveredAt ?? inFlight.CreatedAt) < inFlightDeliveredBefore)
                {
                    queue.InFlight = null;
                    removed.Add(Copy(inFlight));
                }

                var node = queue.Pending.First;
                while (node is not null)
                {
                    var following = node.Next;
                    if (node.Value.CreatedAt < pendingCreatedBefore)
                    {
                        queue.Pending.Remove(node);
                        removed.Add(Copy(node.Value));
                    }

                    node = following;
                }

                DiscardIfEmpty(pair.Key, queue);
            }
        }

        return Task.FromResult<IReadOnlyList<PushEnvelope>>(removed);
    }

    // Chamado com o lock da fila: tira do dicionário a fila sem nenhum envio. Só remove esta instância (outra pode ter
    // entrado no lugar), e a marca impede que um AddAsync que já a pegou grave numa fila fora do dicionário.
    private void DiscardIfEmpty(long deviceId, DeviceQueue queue)
    {
        if (queue.Pending.Count == 0 && queue.InFlight is null && !queue.Discarded)
        {
            queue.Discarded = true;
            ((ICollection<KeyValuePair<long, DeviceQueue>>)_devices).Remove(new KeyValuePair<long, DeviceQueue>(deviceId, queue));
        }
    }

    // O store guarda a própria cópia: o chamador não altera o estado interno por referência.
    private static PushEnvelope Copy(PushEnvelope source) =>
        new(source.Id, source.DeviceId, source.Batch, source.Endpoints, source.Payload, source.CreatedAt)
        {
            Status = source.Status,
            Uuid = source.Uuid,
            DeliveredAt = source.DeliveredAt,
        };

    private sealed class DeviceQueue
    {
        public LinkedList<PushEnvelope> Pending { get; } = new();

        public PushEnvelope? InFlight { get; set; }

        /// <summary>Fila tirada do dicionário: não recebe mais envios.</summary>
        public bool Discarded { get; set; }
    }
}
