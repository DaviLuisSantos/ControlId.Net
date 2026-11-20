using ControlId.Access.Push;
using ControlId.Push.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlId.Tests;

/// <summary>
/// Contrato do <see cref="IPushCommandStore"/> sobre o EF Core + SQLite. Usa um arquivo temporário (uma conexão por
/// operação, como num banco real) em vez de memória, para os equipamentos em paralelo não dividirem uma conexão.
/// </summary>
public sealed class EfPushStoreTests : PushStoreContractTests, IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"controlid-push-{Guid.NewGuid():N}.db");
    private ServiceProvider? _provider;

    protected override IPushCommandStore CreateStore() => NewProvider().GetRequiredService<IPushCommandStore>();

    /// <summary>O store EF implementa a varredura: os testes de manutenção do contrato valem para ele.</summary>
    protected override bool RequiresMaintenance => true;

    /// <summary>Provedor novo sobre o mesmo arquivo: simula o reinício do processo.</summary>
    private ServiceProvider NewProvider()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<TestContext>(options => options.UseSqlite($"Data Source={_path};Pooling=False"));
        services.AddControlIdPushEntityFrameworkStore<TestContext>();
        var provider = services.BuildServiceProvider();

        using (var context = provider.GetRequiredService<IDbContextFactory<TestContext>>().CreateDbContext())
        {
            context.Database.EnsureCreated();
        }

        _provider?.Dispose();
        _provider = provider;
        return provider;
    }

    public void Dispose()
    {
        _provider?.Dispose();
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
    public async Task Pending_envelope_survives_a_restart_and_the_result_reaches_the_handler()
    {
        var handler = new RecordingHandler();
        var before = new PushServer(NewProvider().GetRequiredService<IPushCommandStore>());
        var id = await before.EnqueueAsync(7, PushCommand.Reboot());
        Assert.Equal(1, await before.GetPendingCountAsync(7));

        var after = new PushServer(NewProvider().GetRequiredService<IPushCommandStore>(), handler);

        Assert.Equal(1, await after.GetPendingCountAsync(7));
        Assert.Contains("\"endpoint\":\"reboot\"", await after.HandlePushAsync(7, "u"));
        Assert.True(await after.HandleResultAsync(7, "u", """{"response":{}}"""));
        var completion = Assert.Single(handler.Completions);
        Assert.Equal(id, completion.CommandId);
        Assert.Equal(7, completion.DeviceId);
        Assert.Null(completion.Error);
        Assert.Equal(0, await after.GetPendingCountAsync(7));
    }

    [Fact]
    public async Task In_flight_envelope_lost_in_a_restart_is_reported_as_orphan_and_not_resent()
    {
        var handler = new RecordingHandler();
        var before = new PushServer(NewProvider().GetRequiredService<IPushCommandStore>());
        var id = await before.EnqueueAsync(7, PushCommand.Reboot());
        Assert.Contains("\"endpoint\":\"reboot\"", await before.HandlePushAsync(7, "a"));

        var after = new PushServer(NewProvider().GetRequiredService<IPushCommandStore>(), handler);

        Assert.Equal(string.Empty, await after.HandlePushAsync(7, "b"));
        var completion = Assert.Single(handler.Completions);
        Assert.Equal(id, completion.CommandId);
        Assert.NotNull(completion.Error);
        Assert.Equal(0, await after.GetPendingCountAsync(7));
    }

    [Fact]
    public async Task Created_at_is_kept_in_order_even_when_the_clock_does_not_advance()
    {
        var store = CreateStore();
        var moment = DateTimeOffset.UtcNow;
        var first = NewEnvelope(1, "first", createdAt: moment);
        var second = NewEnvelope(1, "second", createdAt: moment);
        var third = NewEnvelope(1, "third", createdAt: moment.AddTicks(-5));
        await store.AddAsync(first, CancellationToken.None);
        await store.AddAsync(second, CancellationToken.None);
        await store.AddAsync(third, CancellationToken.None);

        var order = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            order.Add((await store.TakeNextAsync(1, $"u{i}", CancellationToken.None)).Next!.Id);
        }

        Assert.Equal(new[] { first.Id, second.Id, third.Id }, order);
    }

    [Fact]
    public void Resolving_the_store_without_a_context_factory_explains_what_to_register()
    {
        var services = new ServiceCollection();
        services.AddControlIdPushEntityFrameworkStore<TestContext>();
        using var provider = services.BuildServiceProvider();

        var ex = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IPushCommandStore>());

        Assert.Contains("IDbContextFactory<TestContext>", ex.Message);
        Assert.Contains("AddDbContextFactory<TestContext>", ex.Message);
    }

    [Fact]
    public async Task Server_purges_expired_envelopes_through_the_store()
    {
        var handler = new RecordingHandler();
        var store = CreateStore();
        var server = new PushServer(store, new PushServerOptions { CommandTtl = TimeSpan.FromHours(1), InFlightTimeout = null }, handler);
        var old = NewEnvelope(7, "old", createdAt: DateTimeOffset.UtcNow.AddHours(-2));
        await store.AddAsync(old, CancellationToken.None);
        await server.EnqueueAsync(7, PushCommand.Reboot());

        Assert.Equal(1, await server.PurgeExpiredAsync());

        var completion = Assert.Single(handler.Completions);
        Assert.Equal(old.Id, completion.CommandId);
        Assert.NotNull(completion.Error);
        Assert.Equal(1, await server.GetPendingCountAsync(7));
    }

    [Fact]
    public void ApplyControlIdPush_uses_the_requested_table_and_schema()
    {
        var builder = new ModelBuilder();

        builder.ApplyControlIdPush("fila_push", "app");

        var entity = Assert.Single(builder.Model.GetEntityTypes());
        Assert.Equal("fila_push", entity.GetTableName());
        Assert.Equal("app", entity.GetSchema());
    }

    private sealed class RecordingHandler : IPushResultHandler
    {
        public List<PushCompletion> Completions { get; } = new();

        public Task HandleAsync(PushCompletion completion, CancellationToken cancellationToken)
        {
            Completions.Add(completion);
            return Task.CompletedTask;
        }
    }

    private sealed class TestContext : DbContext
    {
        public TestContext(DbContextOptions<TestContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.ApplyControlIdPush();
    }
}
