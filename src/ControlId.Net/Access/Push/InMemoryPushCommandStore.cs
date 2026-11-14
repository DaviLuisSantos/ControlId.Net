using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ControlId.Access.Push;

/// <summary>
/// <see cref="IPushCommandStore"/> em memória (padrão do <see cref="PushServer"/>): rápido, mas os envios pendentes
/// se perdem quando o processo reinicia.
/// </summary>
public sealed class InMemoryPushCommandStore : IPushCommandStore
{
    private readonly ConcurrentDictionary<long, DeviceQueue> _devices = new();

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

        var queue = _devices.GetOrAdd(envelope.DeviceId, _ => new DeviceQueue());
        lock (queue)
        {
            queue.Pending.AddLast(copy);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<PushTake> TakeNextAsync(long deviceId, string? uuid, CancellationToken cancellationToken)
    {
        var queue = _devices.GetOrAdd(deviceId, _ => new DeviceQueue());
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
                next.DeliveredAt = DateTimeOffset.UtcNow;
                queue.InFlight = next;
            }
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
            return Task.FromResult<PushEnvelope?>(Copy(current));
        }
    }

    /// <inheritdoc />
    public Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken)
    {
        foreach (var queue in _devices.Values)
        {
            lock (queue)
            {
                if (queue.InFlight is { } inFlight && inFlight.Id == id)
                {
                    queue.InFlight = null;
                    return Task.FromResult(true);
                }

                for (var node = queue.Pending.First; node is not null; node = node.Next)
                {
                    if (node.Value.Id == id)
                    {
                        queue.Pending.Remove(node);
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
    }
}
