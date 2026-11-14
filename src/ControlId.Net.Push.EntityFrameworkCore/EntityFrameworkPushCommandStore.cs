using System.Text.Json;
using ControlId.Access.Push;
using Microsoft.EntityFrameworkCore;

namespace ControlId.Push.EntityFrameworkCore;

/// <summary>
/// <see cref="IPushCommandStore"/> sobre o EF Core: cada operação usa um contexto novo da fábrica, então o store é
/// singleton e seguro para uso paralelo entre equipamentos. Os envios sobrevivem a um reinício do processo.
/// </summary>
internal sealed class EntityFrameworkPushCommandStore<TContext> : IPushCommandStore
    where TContext : DbContext
{
    private readonly IDbContextFactory<TContext> _factory;

    public EntityFrameworkPushCommandStore(IDbContextFactory<TContext> factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public async Task AddAsync(PushEnvelope envelope, CancellationToken cancellationToken)
    {
        if (envelope is null)
        {
            throw new ArgumentNullException(nameof(envelope));
        }

        await using var context = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var records = context.Set<PushCommandRecord>();

        // Acrescenta ao fim da fila: a ordem é por CreatedAt, então garante que ele seja maior que o do último envio.
        var ticks = envelope.CreatedAt.UtcTicks;
        var last = await records.Where(r => r.DeviceId == envelope.DeviceId)
            .MaxAsync(r => (long?)r.CreatedAtTicks, cancellationToken).ConfigureAwait(false);
        if (last is { } lastTicks && ticks <= lastTicks)
        {
            ticks = lastTicks + 1;
        }

        records.Add(new PushCommandRecord
        {
            Id = envelope.Id,
            DeviceId = envelope.DeviceId,
            Batch = envelope.Batch,
            Endpoints = JsonSerializer.Serialize(envelope.Endpoints),
            Payload = envelope.Payload,
            Status = PushEnvelopeStatus.Pending,
            Uuid = null,
            CreatedAtTicks = ticks,
            DeliveredAtTicks = null,
        });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<PushTake> TakeNextAsync(long deviceId, string? uuid, CancellationToken cancellationToken)
    {
        await using var context = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var records = context.Set<PushCommandRecord>();

        var inFlight = await records
            .Where(r => r.DeviceId == deviceId && r.Status == PushEnvelopeStatus.InFlight)
            .OrderBy(r => r.CreatedAtTicks)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var next = await records
            .Where(r => r.DeviceId == deviceId && r.Status == PushEnvelopeStatus.Pending)
            .OrderBy(r => r.CreatedAtTicks)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        var orphan = inFlight.Count > 0 ? ToEnvelope(inFlight[0]) : null;
        records.RemoveRange(inFlight);

        PushEnvelope? taken = null;
        if (next is not null)
        {
            next.Status = PushEnvelopeStatus.InFlight;
            next.Uuid = uuid;
            next.DeliveredAtTicks = DateTimeOffset.UtcNow.UtcTicks;
            taken = ToEnvelope(next);
        }

        if (inFlight.Count > 0 || next is not null)
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return new PushTake(orphan, taken);
    }

    public async Task<PushEnvelope?> CompleteAsync(long deviceId, string? uuid, CancellationToken cancellationToken)
    {
        await using var context = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var records = context.Set<PushCommandRecord>();

        var current = await records
            .Where(r => r.DeviceId == deviceId && r.Status == PushEnvelopeStatus.InFlight)
            .OrderBy(r => r.CreatedAtTicks)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (current is null || (uuid is not null && current.Uuid is not null && uuid != current.Uuid))
        {
            return null;
        }

        // A exclusão decide quem completou: se outro processo já removeu a linha, não há nada a devolver.
        var id = current.Id;
        var deleted = await records.Where(r => r.Id == id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        return deleted > 0 ? ToEnvelope(current) : null;
    }

    public async Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var context = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var deleted = await context.Set<PushCommandRecord>().Where(r => r.Id == id)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        return deleted > 0;
    }

    public async Task<int> CountAsync(long deviceId, CancellationToken cancellationToken)
    {
        await using var context = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.Set<PushCommandRecord>().CountAsync(r => r.DeviceId == deviceId, cancellationToken).ConfigureAwait(false);
    }

    private static PushEnvelope ToEnvelope(PushCommandRecord record) =>
        new(
            record.Id,
            record.DeviceId,
            record.Batch,
            JsonSerializer.Deserialize<string[]>(record.Endpoints) ?? Array.Empty<string>(),
            record.Payload,
            new DateTimeOffset(record.CreatedAtTicks, TimeSpan.Zero))
        {
            Status = record.Status,
            Uuid = record.Uuid,
            DeliveredAt = record.DeliveredAtTicks is { } delivered ? new DateTimeOffset(delivered, TimeSpan.Zero) : null,
        };
}
