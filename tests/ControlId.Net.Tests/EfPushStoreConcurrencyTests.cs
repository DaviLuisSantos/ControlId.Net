using System.Collections.Concurrent;
using ControlId.Access.Push;
using ControlId.Push.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlId.Tests;

/// <summary>
/// Várias instâncias da aplicação sobre o mesmo banco: cada store tem o seu provedor e a sua fábrica de contextos, e
/// todos apontam para o mesmo arquivo SQLite. As disputas são reais (conexões distintas); o SQLite serializa as
/// escritas e o <c>Default Timeout</c> faz quem encontra o banco ocupado esperar em vez de falhar.
/// </summary>
public sealed class EfPushStoreConcurrencyTests : IDisposable
{
    private const long Device = 42;
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly TimeSpan Guard = TimeSpan.FromMinutes(2);

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"controlid-push-race-{Guid.NewGuid():N}.db");
    private readonly List<ServiceProvider> _providers = new();

    public EfPushStoreConcurrencyTests()
    {
        using var context = new RaceContext(new DbContextOptionsBuilder<RaceContext>().UseSqlite(ConnectionString).Options);
        context.Database.EnsureCreated();
    }

    private string ConnectionString => $"Data Source={_path};Pooling=False;Default Timeout=60";

    public void Dispose()
    {
        foreach (var provider in _providers)
        {
            provider.Dispose();
        }

        SqliteConnection.ClearAllPools();
        foreach (var file in new[] { _path, _path + "-journal", _path + "-wal", _path + "-shm" })
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
                // Arquivo temporário: a limpeza é de melhor esforço.
            }
        }
    }

    [Fact]
    public async Task Two_servers_on_the_same_database_deliver_each_command_once_and_in_order()
    {
        const int total = 120;
        var storeA = new RecordingStore(NewStore());
        var storeB = new RecordingStore(NewStore());
        var handlerA = new RecordingHandler();
        var handlerB = new RecordingHandler();
        var serverA = new PushServer(storeA, handlerA);
        var serverB = new PushServer(storeB, handlerB);

        var index = new Dictionary<Guid, int>();
        for (var i = 0; i < total; i++)
        {
            index[await (i % 2 == 0 ? serverA : serverB).EnqueueAsync(Device, PushCommand.ShowMessage($"m{i:D4}"))] = i;
        }

        // Cada servidor recebe várias consultas simultâneas do mesmo equipamento (4 por servidor), até tudo sair.
        var payloads = new ConcurrentBag<string>();
        var delivered = 0;
        async Task Worker(PushServer server, int number)
        {
            for (var round = 0; Volatile.Read(ref delivered) < total; round++)
            {
                Assert.True(round < total * 20, "As consultas não esvaziaram a fila.");
                var payload = await server.HandlePushAsync(Device, $"u{number}-{round}");
                if (payload.Length > 0)
                {
                    payloads.Add(payload);
                    Interlocked.Increment(ref delivered);
                }
            }
        }

        await Task.WhenAll(Enumerable.Range(0, 8).Select(n => Task.Run(() => Worker(n % 2 == 0 ? serverA : serverB, n)))).WaitAsync(Guard);

        // Nenhum envio saiu duas vezes, e todos saíram.
        Assert.Equal(total, payloads.Count);
        Assert.Equal(total, payloads.Distinct().Count());
        var taken = storeA.Taken.Concat(storeB.Taken).ToList();
        Assert.Equal(total, taken.Count);
        Assert.Equal(index.Keys.OrderBy(id => id), taken.OrderBy(id => id));

        // Quem entregou entregou na ordem da fila (as consultas de um servidor passam em série pelo store). Não se exige que
        // os dois servidores entreguem algo: numa máquina rápida, um deles pode esvaziar a fila enquanto o outro espera o
        // banco, e isso não é defeito (a CI do Linux mostrou esse caso).
        AssertIncreasing(storeA.Taken.Select(id => index[id]));
        AssertIncreasing(storeB.Taken.Select(id => index[id]));

        // Esvazia o que ficou em andamento: nada pendente, e cada órfão foi reportado por uma só instância, uma vez.
        Assert.Equal(string.Empty, await serverA.HandlePushAsync(Device, "fim"));
        Assert.Equal(0, await serverA.GetPendingCountAsync(Device));
        var orphans = handlerA.Completions.Concat(handlerB.Completions).ToList();
        Assert.All(orphans, completion => Assert.NotNull(completion.Error));
        Assert.Equal(orphans.Count, orphans.Select(c => c.CommandId).Distinct().Count());
        Assert.All(orphans, completion => Assert.Contains(completion.CommandId, index.Keys));
        Assert.Equal(orphans.Count, storeA.Orphans.Count + storeB.Orphans.Count);
    }

    [Fact]
    public async Task Parallel_takers_on_two_stores_never_take_the_same_envelope()
    {
        const int total = 150;
        var stores = new[] { NewStore(), NewStore() };
        var start = DateTimeOffset.UtcNow.AddMinutes(-10);
        for (var i = 0; i < total; i++)
        {
            await stores[i % 2].AddAsync(Envelope(i, start.AddMilliseconds(i)), Ct);
        }

        // 6 tomadores por store, cada um em série; os tomadores não se coordenam (sem o gate do PushServer).
        var taken = new ConcurrentBag<int>();
        var orphans = new ConcurrentBag<int>();
        var delivered = 0;
        async Task<List<int>> Taker(IPushCommandStore store, int number)
        {
            var own = new List<int>();
            for (var round = 0; Volatile.Read(ref delivered) < total; round++)
            {
                Assert.True(round < total * 20, "Os tomadores não esvaziaram a fila.");
                var take = await store.TakeNextAsync(Device, $"t{number}-{round}", Ct);
                if (take.Orphan is { } orphan)
                {
                    orphans.Add(IndexOf(orphan));
                }

                if (take.Next is { } next)
                {
                    Assert.Equal(PushEnvelopeStatus.InFlight, next.Status);
                    Assert.Equal($"t{number}-{round}", next.Uuid);
                    own.Add(IndexOf(next));
                    taken.Add(IndexOf(next));
                    Interlocked.Increment(ref delivered);
                }
            }

            return own;
        }

        var perTaker = await Task.WhenAll(Enumerable.Range(0, 12).Select(n => Task.Run(() => Taker(stores[n % 2], n)))).WaitAsync(Guard);

        Assert.Equal(Enumerable.Range(0, total), taken.OrderBy(i => i));
        foreach (var own in perTaker)
        {
            AssertIncreasing(own);
        }

        var last = await stores[0].TakeNextAsync(Device, "fim", Ct);
        if (last.Orphan is { } lastOrphan)
        {
            orphans.Add(IndexOf(lastOrphan));
        }

        Assert.Null(last.Next);
        Assert.Equal(orphans.Count, orphans.Distinct().Count());
        Assert.Equal(0, await stores[1].CountAsync(Device, Ct));
    }

    [Fact]
    public async Task Orphan_delivered_by_another_instance_is_reported_once()
    {
        var handlerA = new RecordingHandler();
        var handlerB = new RecordingHandler();
        var serverA = new PushServer(NewStore(), handlerA);
        var serverB = new PushServer(NewStore(), handlerB);
        var id = await serverA.EnqueueAsync(Device, PushCommand.Reboot());

        Assert.Contains("reboot", await serverA.HandlePushAsync(Device, "a"));
        Assert.Equal(string.Empty, await serverB.HandlePushAsync(Device, "b"));
        Assert.Equal(string.Empty, await serverA.HandlePushAsync(Device, "c"));
        Assert.Equal(string.Empty, await serverB.HandlePushAsync(Device, "d"));

        Assert.Empty(handlerA.Completions);
        var orphan = Assert.Single(handlerB.Completions);
        Assert.Equal(id, orphan.CommandId);
        Assert.NotNull(orphan.Error);
        Assert.False(await serverA.HandleResultAsync(Device, "a", """{"response":{}}"""));
        Assert.Equal(0, await serverB.GetPendingCountAsync(Device));
    }

    [Fact]
    public async Task Result_reaches_the_other_instance_handler_when_it_completes_the_delivery()
    {
        var handlerB = new RecordingHandler();
        var serverA = new PushServer(NewStore());
        var serverB = new PushServer(NewStore(), handlerB);
        var id = await serverA.EnqueueAsync(Device, PushCommand.Reboot());

        Assert.Contains("reboot", await serverA.HandlePushAsync(Device, "a"));
        Assert.True(await serverB.HandleResultAsync(Device, "a", """{"response":{}}"""));
        Assert.False(await serverA.HandleResultAsync(Device, "a", """{"response":{}}"""));

        var completion = Assert.Single(handlerB.Completions);
        Assert.Equal(id, completion.CommandId);
        Assert.Null(completion.Error);
    }

    [Fact]
    public async Task Several_in_flight_envelopes_are_all_removed_and_the_oldest_is_reported()
    {
        var store = NewStore();
        var start = DateTimeOffset.UtcNow.AddMinutes(-1);
        var first = Envelope(0, start);
        var second = Envelope(1, start.AddSeconds(1));
        var third = Envelope(2, start.AddSeconds(2));
        await store.AddAsync(first, Ct);
        await store.AddAsync(second, Ct);
        await store.AddAsync(third, Ct);
        await MarkInFlightAsync(0, 1); // Duas instâncias entregaram ao mesmo tempo.

        var take = await store.TakeNextAsync(Device, "u", Ct);

        Assert.Equal(first.Id, take.Orphan!.Id);
        Assert.Equal(PushEnvelopeStatus.InFlight, take.Orphan.Status);
        Assert.Equal(third.Id, take.Next!.Id);
        Assert.Equal(1, await store.CountAsync(Device, Ct));
        Assert.False(await store.RemoveAsync(second.Id, Ct));
    }

    [Fact]
    public async Task Complete_picks_the_in_flight_envelope_with_the_matching_uuid()
    {
        var store = NewStore();
        var start = DateTimeOffset.UtcNow.AddMinutes(-1);
        var first = Envelope(0, start);
        var second = Envelope(1, start.AddSeconds(1));
        await store.AddAsync(first, Ct);
        await store.AddAsync(second, Ct);
        await MarkInFlightAsync(0, 1);

        Assert.Equal(second.Id, (await store.CompleteAsync(Device, "u1", Ct))!.Id);
        Assert.Null(await store.CompleteAsync(Device, "u1", Ct));
        Assert.Equal(first.Id, (await store.CompleteAsync(Device, null, Ct))!.Id);
        Assert.Equal(0, await store.CountAsync(Device, Ct));
    }

    [Fact]
    public async Task Parallel_RemoveExpired_on_two_stores_reports_each_envelope_once()
    {
        const int total = 80;
        var stores = new[] { NewStore(), NewStore() };
        var old = DateTimeOffset.UtcNow.AddDays(-1);
        for (var i = 0; i < total; i++)
        {
            await stores[0].AddAsync(Envelope(i, old.AddSeconds(i), device: 100 + (i % 4)), Ct);
        }

        var limit = DateTimeOffset.UtcNow.AddHours(-1);
        var removed = await Task.WhenAll(Enumerable.Range(0, 6).Select(n =>
            Task.Run(() => ((IPushCommandStoreMaintenance)stores[n % 2]).RemoveExpiredAsync(limit, DateTimeOffset.MinValue, Ct)))).WaitAsync(Guard);

        var all = removed.SelectMany(list => list).Select(IndexOf).ToList();
        Assert.Equal(Enumerable.Range(0, total), all.OrderBy(i => i));
        for (var device = 100; device < 104; device++)
        {
            Assert.Equal(0, await stores[1].CountAsync(device, Ct));
        }
    }

    private IPushCommandStore NewStore()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<RaceContext>(options => options.UseSqlite(ConnectionString));
        services.AddControlIdPushEntityFrameworkStore<RaceContext>();
        var provider = services.BuildServiceProvider();
        _providers.Add(provider);
        return provider.GetRequiredService<IPushCommandStore>();
    }

    /// <summary>Simula entregas simultâneas: marca os envios indicados como em andamento, com uuid <c>u{índice}</c>.</summary>
    private async Task MarkInFlightAsync(params int[] indexes)
    {
        await using var context = new RaceContext(new DbContextOptionsBuilder<RaceContext>().UseSqlite(ConnectionString).Options);
        foreach (var i in indexes)
        {
            var endpoint = $"e{i:D4}";
            var uuid = $"u{i}";
            var changed = await context.Database.ExecuteSqlAsync(
                $"UPDATE controlid_push_commands SET Status = 1, Uuid = {uuid}, DeliveredAtTicks = CreatedAtTicks WHERE json_extract(Payload, '$.endpoint') = {endpoint}");
            Assert.Equal(1, changed);
        }
    }

    private static PushEnvelope Envelope(int index, DateTimeOffset createdAt, long device = Device) =>
        new(Guid.NewGuid(), device, false, new[] { $"e{index:D4}" }, $$"""{"endpoint":"e{{index:D4}}"}""", createdAt);

    private static int IndexOf(PushEnvelope envelope) => int.Parse(envelope.Endpoints[0].Substring(1), System.Globalization.CultureInfo.InvariantCulture);

    private static void AssertIncreasing(IEnumerable<int> sequence)
    {
        var previous = -1;
        foreach (var current in sequence)
        {
            Assert.True(current > previous, $"Fora de ordem: {current} depois de {previous}.");
            previous = current;
        }
    }

    /// <summary>Anota, na ordem em que o store devolveu, o que cada <c>TakeNextAsync</c> entregou e reportou.</summary>
    private sealed class RecordingStore : IPushCommandStore
    {
        private readonly IPushCommandStore _inner;

        public RecordingStore(IPushCommandStore inner) => _inner = inner;

        public List<Guid> Taken { get; } = new();

        public List<Guid> Orphans { get; } = new();

        public Task AddAsync(PushEnvelope envelope, CancellationToken cancellationToken) => _inner.AddAsync(envelope, cancellationToken);

        public async Task<PushTake> TakeNextAsync(long deviceId, string? uuid, CancellationToken cancellationToken)
        {
            var take = await _inner.TakeNextAsync(deviceId, uuid, cancellationToken);
            lock (Taken)
            {
                if (take.Next is { } next)
                {
                    Taken.Add(next.Id);
                }

                if (take.Orphan is { } orphan)
                {
                    Orphans.Add(orphan.Id);
                }
            }

            return take;
        }

        public Task<PushEnvelope?> CompleteAsync(long deviceId, string? uuid, CancellationToken cancellationToken) =>
            _inner.CompleteAsync(deviceId, uuid, cancellationToken);

        public Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken) => _inner.RemoveAsync(id, cancellationToken);

        public Task<int> CountAsync(long deviceId, CancellationToken cancellationToken) => _inner.CountAsync(deviceId, cancellationToken);
    }

    private sealed class RecordingHandler : IPushResultHandler
    {
        private readonly ConcurrentQueue<PushCompletion> _completions = new();

        public IReadOnlyCollection<PushCompletion> Completions => _completions;

        public Task HandleAsync(PushCompletion completion, CancellationToken cancellationToken)
        {
            _completions.Enqueue(completion);
            return Task.CompletedTask;
        }
    }

    private sealed class RaceContext : DbContext
    {
        public RaceContext(DbContextOptions<RaceContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.ApplyControlIdPush();
    }
}
