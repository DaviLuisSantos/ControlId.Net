using System.Text.Json;
using ControlId.Access.Push;
using Microsoft.EntityFrameworkCore;

namespace ControlId.Push.EntityFrameworkCore;

/// <summary>
/// <see cref="IPushCommandStore"/> sobre o EF Core: cada operação usa um contexto novo da fábrica, então o store é
/// singleton e seguro para uso paralelo entre equipamentos. Os envios sobrevivem a um reinício do processo.
/// </summary>
/// <remarks>
/// Várias instâncias (processos) podem dividir o mesmo banco: cada mudança de estado é um <c>UPDATE</c>/<c>DELETE</c>
/// condicional ao estado lido, e quem não alterou nenhuma linha perdeu a disputa. Assim um envio nunca é entregue,
/// completado ou reportado como órfão/vencido por duas instâncias. A ordem da fila é <c>(CreatedAtTicks, Id)</c>.
/// </remarks>
internal sealed class EntityFrameworkPushCommandStore<TContext> : IPushCommandStore, IPushCommandStoreMaintenance
    where TContext : DbContext
{
    /// <summary>Tentativas de pegar um pendente quando outra instância leva o candidato antes.</summary>
    internal const int MaxClaimAttempts = 5;

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
        // Duas instâncias acrescentando ao mesmo tempo podem chegar ao mesmo valor; o desempate por Id mantém a ordem determinística.
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

        // Órfãos: normalmente um só; mais de um quando instâncias entregaram ao mesmo tempo. Todos saem, e o mais antigo
        // que esta chamada apagou é o reportado (outra instância que apagou um deles é quem o reporta).
        var inFlight = await records.AsNoTracking()
            .Where(r => r.DeviceId == deviceId && r.Status == PushEnvelopeStatus.InFlight)
            .OrderBy(r => r.CreatedAtTicks).ThenBy(r => r.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        PushEnvelope? orphan = null;
        foreach (var record in inFlight)
        {
            var id = record.Id;
            var deleted = await records
                .Where(r => r.Id == id && r.Status == PushEnvelopeStatus.InFlight)
                .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            if (deleted > 0)
            {
                orphan ??= ToEnvelope(record);
            }
        }

        // Próximo: o pendente mais antigo, marcado com um UPDATE condicional. Zero linhas = outra instância o levou
        // entre a leitura e a marcação; tenta o seguinte.
        PushEnvelope? next = null;
        for (var attempt = 0; attempt < MaxClaimAttempts && next is null; attempt++)
        {
            var candidate = await records.AsNoTracking()
                .Where(r => r.DeviceId == deviceId && r.Status == PushEnvelopeStatus.Pending)
                .OrderBy(r => r.CreatedAtTicks).ThenBy(r => r.Id)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (candidate is null)
            {
                break;
            }

            var id = candidate.Id;
            var delivered = DateTimeOffset.UtcNow.UtcTicks;
            var claimed = await records
                .Where(r => r.Id == id && r.Status == PushEnvelopeStatus.Pending)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(r => r.Status, PushEnvelopeStatus.InFlight)
                        .SetProperty(r => r.Uuid, uuid)
                        .SetProperty(r => r.DeliveredAtTicks, (long?)delivered),
                    cancellationToken)
                .ConfigureAwait(false);
            if (claimed > 0)
            {
                candidate.Status = PushEnvelopeStatus.InFlight;
                candidate.Uuid = uuid;
                candidate.DeliveredAtTicks = delivered;
                next = ToEnvelope(candidate);
            }
        }

        return new PushTake(orphan, next);
    }

    public async Task<PushEnvelope?> CompleteAsync(long deviceId, string? uuid, CancellationToken cancellationToken)
    {
        await using var context = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var records = context.Set<PushCommandRecord>();

        // uuid nulo de qualquer lado casa. Com mais de um em andamento (entregas simultâneas), o uuid escolhe o certo.
        var current = await records.AsNoTracking()
            .Where(r => r.DeviceId == deviceId && r.Status == PushEnvelopeStatus.InFlight
                && (uuid == null || r.Uuid == null || r.Uuid == uuid))
            .OrderBy(r => r.CreatedAtTicks).ThenBy(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            return null;
        }

        // A exclusão decide quem completou: se outro processo já removeu a linha, não há nada a devolver.
        var id = current.Id;
        var deleted = await records.Where(r => r.Id == id && r.Status == PushEnvelopeStatus.InFlight)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
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

    public async Task<IReadOnlyList<PushEnvelope>> RemoveExpiredAsync(
        DateTimeOffset pendingCreatedBefore, DateTimeOffset inFlightDeliveredBefore, CancellationToken cancellationToken)
    {
        await using var context = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var records = context.Set<PushCommandRecord>();

        // Lê os vencidos e apaga cada um com a condição do estado lido: só devolve o que esta chamada apagou. Um envio
        // entregue, completado ou removido por outra instância entre a leitura e a exclusão não é reportado duas vezes.
        var candidates = new List<PushCommandRecord>();
        if (pendingCreatedBefore != DateTimeOffset.MinValue)
        {
            var limit = pendingCreatedBefore.UtcTicks;
            candidates.AddRange(await records.AsNoTracking()
                .Where(r => r.Status == PushEnvelopeStatus.Pending && r.CreatedAtTicks < limit)
                .ToListAsync(cancellationToken).ConfigureAwait(false));
        }

        if (inFlightDeliveredBefore != DateTimeOffset.MinValue)
        {
            var limit = inFlightDeliveredBefore.UtcTicks;
            candidates.AddRange(await records.AsNoTracking()
                .Where(r => r.Status == PushEnvelopeStatus.InFlight && (r.DeliveredAtTicks ?? r.CreatedAtTicks) < limit)
                .ToListAsync(cancellationToken).ConfigureAwait(false));
        }

        var removed = new List<PushEnvelope>(candidates.Count);
        foreach (var record in candidates)
        {
            var id = record.Id;
            var status = record.Status;
            var deleted = await records.Where(r => r.Id == id && r.Status == status)
                .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            if (deleted > 0)
            {
                removed.Add(ToEnvelope(record));
            }
        }

        return removed;
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
