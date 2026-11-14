using ControlId.Access.Push;

namespace ControlId.Tests;

/// <summary>
/// Contrato de <see cref="IPushCommandStore"/>: qualquer implementação (memória, EF Core) herda estes testes.
/// </summary>
public abstract class PushStoreContractTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    /// <summary>Cria um store vazio e isolado.</summary>
    protected abstract IPushCommandStore CreateStore();

    protected static PushEnvelope NewEnvelope(long deviceId, string endpoint, bool batch = false, DateTimeOffset? createdAt = null) =>
        new(Guid.NewGuid(), deviceId, batch, new[] { endpoint }, $$"""{"endpoint":"{{endpoint}}"}""", createdAt ?? DateTimeOffset.UtcNow);

    [Fact]
    public async Task Empty_store_has_nothing_to_take_complete_or_count()
    {
        var store = CreateStore();

        var take = await store.TakeNextAsync(1, "u", Ct);

        Assert.Null(take.Orphan);
        Assert.Null(take.Next);
        Assert.Null(await store.CompleteAsync(1, "u", Ct));
        Assert.Equal(0, await store.CountAsync(1, Ct));
        Assert.False(await store.RemoveAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task Take_returns_oldest_first_and_marks_in_flight()
    {
        var store = CreateStore();
        var start = DateTimeOffset.UtcNow;
        var first = NewEnvelope(1, "first", createdAt: start);
        var second = NewEnvelope(1, "second", createdAt: start.AddSeconds(1));
        await store.AddAsync(first, Ct);
        await store.AddAsync(second, Ct);

        var take = await store.TakeNextAsync(1, "u1", Ct);

        Assert.Null(take.Orphan);
        Assert.NotNull(take.Next);
        Assert.Equal(first.Id, take.Next!.Id);
        Assert.Equal(PushEnvelopeStatus.InFlight, take.Next.Status);
        Assert.Equal("u1", take.Next.Uuid);
        Assert.NotNull(take.Next.DeliveredAt);
        Assert.Equal(first.Payload, take.Next.Payload);
        Assert.Equal(first.Endpoints, take.Next.Endpoints);
        Assert.Equal(2, await store.CountAsync(1, Ct));
    }

    [Fact]
    public async Task Complete_returns_the_in_flight_envelope_and_frees_the_slot()
    {
        var store = CreateStore();
        var first = NewEnvelope(1, "first");
        var second = NewEnvelope(1, "second");
        await store.AddAsync(first, Ct);
        await store.AddAsync(second, Ct);
        await store.TakeNextAsync(1, "u1", Ct);

        var completed = await store.CompleteAsync(1, "u1", Ct);

        Assert.NotNull(completed);
        Assert.Equal(first.Id, completed!.Id);
        Assert.Equal(1, await store.CountAsync(1, Ct));
        Assert.Null(await store.CompleteAsync(1, "u1", Ct));

        var take = await store.TakeNextAsync(1, "u2", Ct);
        Assert.Null(take.Orphan);
        Assert.Equal(second.Id, take.Next!.Id);
    }

    [Fact]
    public async Task Take_without_complete_reports_the_in_flight_envelope_as_orphan()
    {
        var store = CreateStore();
        var first = NewEnvelope(1, "first");
        var second = NewEnvelope(1, "second");
        await store.AddAsync(first, Ct);
        await store.AddAsync(second, Ct);
        await store.TakeNextAsync(1, "a", Ct);

        var take = await store.TakeNextAsync(1, "b", Ct);

        Assert.Equal(first.Id, take.Orphan!.Id);
        Assert.Equal(second.Id, take.Next!.Id);
        Assert.Equal("b", take.Next.Uuid);
        Assert.Equal(1, await store.CountAsync(1, Ct));
    }

    [Fact]
    public async Task Orphan_is_never_delivered_again()
    {
        var store = CreateStore();
        var only = NewEnvelope(1, "only");
        await store.AddAsync(only, Ct);
        await store.TakeNextAsync(1, "a", Ct);

        var second = await store.TakeNextAsync(1, "b", Ct);
        var third = await store.TakeNextAsync(1, "c", Ct);

        Assert.Equal(only.Id, second.Orphan!.Id);
        Assert.Null(second.Next);
        Assert.Null(third.Orphan);
        Assert.Null(third.Next);
        Assert.Equal(0, await store.CountAsync(1, Ct));
    }

    [Fact]
    public async Task Complete_with_a_different_uuid_leaves_the_envelope_in_flight()
    {
        var store = CreateStore();
        await store.AddAsync(NewEnvelope(1, "x"), Ct);
        await store.TakeNextAsync(1, "right", Ct);

        Assert.Null(await store.CompleteAsync(1, "wrong", Ct));
        Assert.Equal(1, await store.CountAsync(1, Ct));
        Assert.NotNull(await store.CompleteAsync(1, "right", Ct));
    }

    [Fact]
    public async Task Null_uuid_on_either_side_matches()
    {
        var store = CreateStore();
        await store.AddAsync(NewEnvelope(1, "a"), Ct);
        await store.AddAsync(NewEnvelope(1, "b"), Ct);

        await store.TakeNextAsync(1, "u", Ct);
        Assert.NotNull(await store.CompleteAsync(1, null, Ct));

        await store.TakeNextAsync(1, null, Ct);
        Assert.NotNull(await store.CompleteAsync(1, "anything", Ct));
    }

    [Fact]
    public async Task Remove_drops_a_pending_envelope()
    {
        var store = CreateStore();
        var first = NewEnvelope(1, "first");
        var second = NewEnvelope(1, "second");
        await store.AddAsync(first, Ct);
        await store.AddAsync(second, Ct);

        Assert.True(await store.RemoveAsync(first.Id, Ct));
        Assert.False(await store.RemoveAsync(first.Id, Ct));
        Assert.Equal(1, await store.CountAsync(1, Ct));
        Assert.Equal(second.Id, (await store.TakeNextAsync(1, "u", Ct)).Next!.Id);
    }

    [Fact]
    public async Task Remove_drops_the_in_flight_envelope()
    {
        var store = CreateStore();
        var only = NewEnvelope(1, "only");
        await store.AddAsync(only, Ct);
        await store.TakeNextAsync(1, "u", Ct);

        Assert.True(await store.RemoveAsync(only.Id, Ct));

        Assert.Equal(0, await store.CountAsync(1, Ct));
        Assert.Null(await store.CompleteAsync(1, "u", Ct));
        var take = await store.TakeNextAsync(1, "v", Ct);
        Assert.Null(take.Orphan);
        Assert.Null(take.Next);
    }

    [Fact]
    public async Task Devices_have_independent_queues()
    {
        var store = CreateStore();
        var one = NewEnvelope(1, "one");
        var two = NewEnvelope(2, "two");
        await store.AddAsync(one, Ct);
        await store.AddAsync(two, Ct);

        Assert.Equal(two.Id, (await store.TakeNextAsync(2, "u", Ct)).Next!.Id);
        Assert.Equal(1, await store.CountAsync(1, Ct));
        Assert.Equal(1, await store.CountAsync(2, Ct));
        Assert.Null(await store.CompleteAsync(1, "u", Ct));
        Assert.Equal(two.Id, (await store.CompleteAsync(2, "u", Ct))!.Id);
        Assert.Equal(one.Id, (await store.TakeNextAsync(1, "u", Ct)).Next!.Id);
    }

    [Fact]
    public async Task Batch_flag_and_endpoints_round_trip()
    {
        var store = CreateStore();
        var envelope = new PushEnvelope(Guid.NewGuid(), 1, batch: true, new[] { "a", "b", "c" }, """{"transactions":[]}""", DateTimeOffset.UtcNow);
        await store.AddAsync(envelope, Ct);

        var next = (await store.TakeNextAsync(1, "u", Ct)).Next!;

        Assert.True(next.Batch);
        Assert.Equal(new[] { "a", "b", "c" }, next.Endpoints);
        Assert.Equal(1, next.DeviceId);
        Assert.Equal(envelope.Payload, next.Payload);
    }

    [Fact]
    public async Task Concurrent_devices_do_not_interfere()
    {
        var store = CreateStore();
        var ids = Enumerable.Range(1, 8).ToArray();

        await Task.WhenAll(ids.Select(async device =>
        {
            for (var i = 0; i < 5; i++)
            {
                await store.AddAsync(NewEnvelope(device, $"d{device}-{i}"), Ct);
            }
        }));

        await Task.WhenAll(ids.Select(async device =>
        {
            for (var i = 0; i < 5; i++)
            {
                var take = await store.TakeNextAsync(device, $"u{i}", Ct);
                Assert.Equal($"d{device}-{i}", Assert.Single(take.Next!.Endpoints));
                Assert.NotNull(await store.CompleteAsync(device, $"u{i}", Ct));
            }
        }));

        foreach (var device in ids)
        {
            Assert.Equal(0, await store.CountAsync(device, Ct));
        }
    }
}
