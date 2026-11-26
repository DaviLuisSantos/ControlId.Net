using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using ControlId.Access.Push;
using ControlId.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ControlId.Tests;

/// <summary>
/// <c>AddControlIdPush</c>: opções do <see cref="PushServer"/>, despacho dos resultados sem dono para todos os
/// <see cref="IPushResultHandler"/> (por escopo) e a varredura periódica (<see cref="ControlIdPushMaintenanceService"/>).
/// </summary>
public sealed class AspNetCorePushOptionsTests
{
    private const string ResultBody = """{"response":{}}""";

    // ---------- opções ----------

    [Fact]
    public async Task Configure_reaches_the_push_server()
    {
        await using var provider = Build(services => services.AddControlIdPush(o =>
        {
            o.MaxPendingPerDevice = 1;
            o.RequireUuid = true;
        }));
        var push = provider.GetRequiredService<PushServer>();

        await push.EnqueueAsync(1, new PushCommand("reboot"));
        var full = await Assert.ThrowsAsync<PushQueueFullException>(() => push.EnqueueAsync(1, new PushCommand("reboot")));
        Assert.Equal(1, full.Limit);

        // RequireUuid: /push sem uuid não tira nada da fila.
        Assert.Equal(string.Empty, await push.HandlePushAsync(1, uuid: null));
        Assert.Equal(1, await push.GetPendingCountAsync(1));
    }

    [Fact]
    public async Task Repeated_calls_apply_every_configuration_in_order_without_duplicating_services()
    {
        var services = new ServiceCollection();
        services.AddControlIdPush(o => o.MaxPendingPerDevice = 5);
        services.AddControlIdPush();
        services.AddControlIdPush(o => o.MaxPendingPerDevice = 2);
        services.Configure<PushServerOptions>(o => o.RequireUuid = true);
        services.AddControlIdPush(o => o.CommandTtl = TimeSpan.FromHours(1));

        Assert.Single(services, d => d.ServiceType == typeof(PushServer));
        Assert.Single(services, d => d.ServiceType == typeof(IHostedService));
        Assert.Single(services, d => d.ServiceType == typeof(IPostConfigureOptions<PushServerOptions>));

        await using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<PushServerOptions>>().Value;
        Assert.Equal(2, options.MaxPendingPerDevice);
        Assert.True(options.RequireUuid);
        Assert.Equal(TimeSpan.FromHours(1), options.CommandTtl);
        Assert.Single(provider.GetServices<IHostedService>().OfType<ControlIdPushMaintenanceService>());

        var push = provider.GetRequiredService<PushServer>();
        await push.EnqueueAsync(1, new PushCommand("reboot"));
        await push.EnqueueAsync(1, new PushCommand("reboot"));
        await Assert.ThrowsAsync<PushQueueFullException>(() => push.EnqueueAsync(1, new PushCommand("reboot")));
    }

    [Fact]
    public async Task Logger_factory_comes_from_the_container_unless_set_in_the_options()
    {
        var container = new PushLogCapture();
        await using (var provider = Build(services =>
        {
            services.AddSingleton<ILoggerFactory>(container);
            services.AddControlIdPush();
        }))
        {
            Assert.Same(container, provider.GetRequiredService<IOptions<PushServerOptions>>().Value.LoggerFactory);
        }

        var own = new PushLogCapture();
        await using (var provider = Build(services =>
        {
            services.AddSingleton<ILoggerFactory>(container);
            services.AddControlIdPush(o => o.LoggerFactory = own);
        }))
        {
            Assert.Same(own, provider.GetRequiredService<IOptions<PushServerOptions>>().Value.LoggerFactory);
        }

        // Sem logging no container, o servidor funciona sem logs.
        await using (var provider = Build(services => services.AddControlIdPush()))
        {
            Assert.Null(provider.GetRequiredService<IOptions<PushServerOptions>>().Value.LoggerFactory);
            Assert.NotNull(provider.GetRequiredService<PushServer>());
        }
    }

    [Fact]
    public async Task Server_logs_go_to_the_container_logger_factory()
    {
        var logs = new PushLogCapture();
        await using var provider = Build(services =>
        {
            services.AddSingleton<ILoggerFactory>(logs);
            services.AddSingleton(new HandlerCalls());
            services.AddControlIdPush();
            services.AddControlIdPushResultHandler<FailingHandler>();
        });
        var push = provider.GetRequiredService<PushServer>();

        await CompleteUnclaimedAsync(push, 1);

        Assert.Single(logs.WithId(1101));
    }

    [Fact]
    public async Task Invalid_options_prevent_the_host_from_starting()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControlIdPush(o => o.MaxTrackedDevices = 0);
        await using var app = builder.Build();

        var error = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => app.StartAsync());

        Assert.Equal(nameof(PushServerOptions.MaxTrackedDevices), error.ParamName);
    }

    // ---------- handlers de resultado ----------

    [Fact]
    public async Task Two_different_handlers_both_run_and_the_same_handler_twice_runs_once()
    {
        var calls = new HandlerCalls();
        await using var provider = Build(services =>
        {
            services.AddSingleton(calls);
            services.AddControlIdPush();
            services.AddControlIdPushResultHandler<FirstHandler>();
            services.AddControlIdPushResultHandler<SecondHandler>();
            services.AddControlIdPushResultHandler<FirstHandler>();
        });
        var push = provider.GetRequiredService<PushServer>();

        var id = await CompleteUnclaimedAsync(push, 7);

        Assert.Equal(new[] { nameof(FirstHandler), nameof(SecondHandler) }, calls.Names);
        Assert.All(calls.Completions, c => Assert.Equal(id, c.CommandId));

        // Um escopo por resultado, compartilhado pelos handlers desse resultado.
        Assert.Single(calls.Scopes.Distinct());
        await CompleteUnclaimedAsync(push, 7);
        Assert.Equal(2, calls.Scopes.Distinct().Count());
    }

    [Fact]
    public async Task A_failing_handler_does_not_stop_the_others_and_is_logged_with_its_type()
    {
        var calls = new HandlerCalls();
        var logs = new PushLogCapture();
        await using var provider = Build(services =>
        {
            services.AddSingleton(calls);
            services.AddSingleton<ILoggerFactory>(logs);
            services.AddControlIdPush();
            services.AddControlIdPushResultHandler<FailingHandler>();
            services.AddControlIdPushResultHandler<SecondHandler>();
        });
        var push = provider.GetRequiredService<PushServer>();

        await CompleteUnclaimedAsync(push, 3);

        Assert.Equal(new[] { nameof(FailingHandler), nameof(SecondHandler) }, calls.Names);
        var failure = Assert.Single(logs.WithId(2011));
        Assert.Equal(LogLevel.Error, failure.Level);
        Assert.Equal(ControlIdPushMaintenanceService.LoggerCategory, failure.Category);
        Assert.Contains(typeof(FailingHandler).FullName!, failure.Message, StringComparison.Ordinal);
        Assert.IsType<InvalidOperationException>(failure.Exception);

        // Uma falha só: o PushServer loga 1101 com a própria exceção.
        var serverLog = Assert.Single(logs.WithId(1101));
        Assert.IsType<InvalidOperationException>(serverLog.Exception);
    }

    [Fact]
    public async Task Several_failing_handlers_become_one_aggregate_exception_in_the_server_log()
    {
        var calls = new HandlerCalls();
        var logs = new PushLogCapture();
        await using var provider = Build(services =>
        {
            services.AddSingleton(calls);
            services.AddSingleton<ILoggerFactory>(logs);
            services.AddControlIdPush();
            services.AddControlIdPushResultHandler<FailingHandler>();
            services.AddControlIdPushResultHandler<SyncThrowingHandler>();
            services.AddControlIdPushResultHandler<SecondHandler>();
        });
        var push = provider.GetRequiredService<PushServer>();

        Assert.True(await CompleteUnclaimedResultAsync(push, 4));

        Assert.Equal(new[] { nameof(FailingHandler), nameof(SyncThrowingHandler), nameof(SecondHandler) }, calls.Names);
        Assert.Equal(2, logs.WithId(2011).Count);
        var aggregate = Assert.IsType<AggregateException>(Assert.Single(logs.WithId(1101)).Exception);
        Assert.Equal(2, aggregate.InnerExceptions.Count);
    }

    [Fact]
    public async Task A_scoped_handler_registered_directly_is_resolved_in_a_scope_not_captured_by_the_singleton()
    {
        var calls = new HandlerCalls();
        await using var provider = Build(
            services =>
            {
                services.AddSingleton(calls);
                services.AddScoped<IPushResultHandler, FirstHandler>();
                services.AddControlIdPush();
            },
            validateScopes: true);

        // Com a resolução no provider raiz (antes), ValidateScopes lançaria aqui.
        var push = provider.GetRequiredService<PushServer>();
        await CompleteUnclaimedAsync(push, 1);
        await CompleteUnclaimedAsync(push, 1);

        Assert.Equal(new[] { nameof(FirstHandler), nameof(FirstHandler) }, calls.Names);
        Assert.Equal(2, calls.Scopes.Distinct().Count());
    }

    [Fact]
    public async Task A_singleton_handler_registered_directly_also_runs()
    {
        var handler = new PushRecordingHandler();
        await using var provider = Build(
            services =>
            {
                services.AddSingleton<IPushResultHandler>(handler);
                services.AddControlIdPush();
            },
            validateScopes: true);
        var push = provider.GetRequiredService<PushServer>();

        var id = await CompleteUnclaimedAsync(push, 2);

        Assert.Equal(id, Assert.Single(handler.Completions).CommandId);
    }

    [Fact]
    public async Task Without_handlers_the_server_gets_no_dispatcher_and_discards_unclaimed_results()
    {
        await using var provider = Build(services => services.AddControlIdPush(), validateScopes: true);
        var push = provider.GetRequiredService<PushServer>();

        Assert.True(await CompleteUnclaimedResultAsync(push, 5));
        Assert.Null(UnclaimedHandlerOf(push));
    }

    [Fact]
    public async Task The_dispatcher_is_not_registered_as_a_handler()
    {
        var calls = new HandlerCalls();
        await using var provider = Build(services =>
        {
            services.AddSingleton(calls);
            services.AddControlIdPush();
            services.AddControlIdPushResultHandler<FirstHandler>();
        });
        var push = provider.GetRequiredService<PushServer>();
        var dispatcher = UnclaimedHandlerOf(push);

        using var scope = provider.CreateScope();
        var registered = scope.ServiceProvider.GetServices<IPushResultHandler>().ToList();

        Assert.NotNull(dispatcher);
        Assert.IsType<FirstHandler>(Assert.Single(registered));
        Assert.DoesNotContain(registered, h => h.GetType() == dispatcher!.GetType());
    }

    [Fact]
    public async Task Handlers_still_run_through_the_push_and_result_routes()
    {
        var calls = new HandlerCalls();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Host.UseDefaultServiceProvider(o => o.ValidateScopes = true);
        builder.Services.AddSingleton(calls);
        builder.Services.AddControlIdPush(o => o.RequireUuid = true);
        builder.Services.AddScoped<IPushResultHandler, FirstHandler>();
        builder.Services.AddControlIdPushResultHandler<SecondHandler>();
        await using var app = builder.Build();
        app.MapControlIdPush().AllowAnonymousControlIdCallbacks();
        await app.StartAsync();
        using var http = app.GetTestClient();
        var id = await app.Services.GetRequiredService<PushServer>().EnqueueAsync(9, new PushCommand("reboot"));

        await http.GetAsync("/push?deviceId=9&uuid=a");
        var response = await http.PostAsync("/result?deviceId=9&uuid=a", new StringContent(ResultBody, System.Text.Encoding.UTF8, "application/json"));

        response.EnsureSuccessStatusCode();
        Assert.Equal(new[] { nameof(FirstHandler), nameof(SecondHandler) }, calls.Names);
        Assert.All(calls.Completions, c => Assert.Equal(id, c.CommandId));
    }

    // ---------- varredura ----------

    [Fact]
    public async Task Maintenance_purges_on_every_tick_and_survives_a_store_failure()
    {
        var store = new SweepStore { FailNext = true };
        var timer = new ManualTimer();
        var logs = new PushLogCapture();
        TimeSpan? period = null;
        using var service = new ControlIdPushMaintenanceService(
            new PushServer(store, new PushServerOptions()),
            TimeSpan.FromMinutes(1),
            logs.CreateLogger(ControlIdPushMaintenanceService.LoggerCategory),
            p =>
            {
                period = p;
                return timer;
            });

        // O Microsoft.Extensions.Hosting 10 roda o ExecuteAsync inteiro em segundo plano: nada é síncrono no StartAsync.
        await service.StartAsync(CancellationToken.None);
        Assert.Equal(0, store.Calls);

        await timer.TickAsync(store, expectedCalls: 1);
        Assert.Equal(TimeSpan.FromMinutes(1), period);

        // A segunda varredura só acontece depois de o laço tratar a falha da primeira.
        await timer.TickAsync(store, expectedCalls: 2);
        var failure = Assert.Single(logs.WithId(2012));
        Assert.Equal(LogLevel.Error, failure.Level);
        Assert.IsType<InvalidOperationException>(failure.Exception);

        await timer.TickAsync(store, expectedCalls: 3);
        Assert.Single(logs.WithId(2012));
        Assert.False(service.ExecuteTask!.IsCompleted);

        await service.StopAsync(CancellationToken.None);
        Assert.True(service.ExecuteTask.IsCompletedSuccessfully);
        Assert.True(timer.Disposed);
    }

    [Fact]
    public async Task Maintenance_fails_expired_commands_through_the_server()
    {
        var clock = new PushFakeClock();
        var store = new SweepStore();
        var handled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new PushRecordingHandler
        {
            OnHandle = (_, _) =>
            {
                handled.TrySetResult(true);
                return Task.CompletedTask;
            },
        };
        var push = new PushServer(store, new PushServerOptions { CommandTtl = TimeSpan.FromMinutes(5), Clock = clock.Now }, handler);
        var timer = new ManualTimer();
        using var service = new ControlIdPushMaintenanceService(push, TimeSpan.FromMinutes(1), logger: null, _ => timer);
        var id = await push.EnqueueAsync(1, new PushCommand("reboot"));
        await service.StartAsync(CancellationToken.None);

        clock.Advance(TimeSpan.FromMinutes(6));
        await timer.TickAsync(store, expectedCalls: 1);
        await handled.Task.WaitAsync(TimeSpan.FromSeconds(30));

        var completion = Assert.Single(handler.Completions);
        Assert.Equal(id, completion.CommandId);
        Assert.NotNull(completion.Error);
        Assert.Equal(0, await push.GetPendingCountAsync(1));
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Maintenance_with_null_interval_ends_at_start_without_purging()
    {
        var store = new SweepStore();
        var logs = new PushLogCapture();
        var timerCreated = false;
        using var service = new ControlIdPushMaintenanceService(
            new PushServer(store, new PushServerOptions { PurgeInterval = null }),
            interval: null,
            logs.CreateLogger(ControlIdPushMaintenanceService.LoggerCategory),
            _ =>
            {
                timerCreated = true;
                return new ManualTimer();
            });

        await service.StartAsync(CancellationToken.None);
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.True(service.ExecuteTask.IsCompletedSuccessfully);
        Assert.False(timerCreated);
        Assert.Equal(0, store.Calls);
        Assert.Single(logs.WithId(2013));
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task AddControlIdPush_with_null_purge_interval_registers_a_service_that_never_sweeps()
    {
        var store = new SweepStore();
        await using var provider = Build(services =>
        {
            services.AddSingleton<IPushCommandStore>(store);
            services.AddControlIdPush(o => o.PurgeInterval = null);
        });
        var service = Assert.IsType<ControlIdPushMaintenanceService>(Assert.Single(provider.GetServices<IHostedService>()));

        await service.StartAsync(CancellationToken.None);
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.True(service.ExecuteTask.IsCompletedSuccessfully);
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task The_hosted_service_sweeps_the_registered_store_at_the_configured_interval()
    {
        var store = new SweepStore();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IPushCommandStore>(store);
        builder.Services.AddControlIdPush(o => o.PurgeInterval = TimeSpan.FromMilliseconds(10));
        await using var app = builder.Build();

        await app.StartAsync();

        // PeriodicTimer real (a fiação do DI); o prazo só protege contra travar o teste.
        await store.WaitForCallsAsync(2).WaitAsync(TimeSpan.FromSeconds(30));
        await app.StopAsync();
    }

    // ---------- apoio ----------

    private static ServiceProvider Build(Action<IServiceCollection> configure, bool validateScopes = false)
    {
        var services = new ServiceCollection();
        configure(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = validateScopes, ValidateOnBuild = validateScopes });
    }

    private static async Task<Guid> CompleteUnclaimedAsync(PushServer push, long deviceId)
    {
        var id = await push.EnqueueAsync(deviceId, new PushCommand("reboot"));
        Assert.True(await DeliverAndCompleteAsync(push, deviceId));
        return id;
    }

    private static async Task<bool> CompleteUnclaimedResultAsync(PushServer push, long deviceId)
    {
        await push.EnqueueAsync(deviceId, new PushCommand("reboot"));
        return await DeliverAndCompleteAsync(push, deviceId);
    }

    private static async Task<bool> DeliverAndCompleteAsync(PushServer push, long deviceId)
    {
        var uuid = Guid.NewGuid().ToString("N");
        Assert.NotEqual(string.Empty, await push.HandlePushAsync(deviceId, uuid));
        return await push.HandleResultAsync(deviceId, uuid, ResultBody);
    }

    private static IPushResultHandler? UnclaimedHandlerOf(PushServer push) =>
        (IPushResultHandler?)typeof(PushServer)
            .GetField("_unclaimedResultHandler", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(push);

    private sealed class HandlerCalls
    {
        private readonly ConcurrentQueue<(string Name, Guid Scope, PushCompletion Completion)> _calls = new();

        public IReadOnlyList<string> Names => _calls.Select(c => c.Name).ToList();

        public IReadOnlyList<Guid> Scopes => _calls.Select(c => c.Scope).ToList();

        public IReadOnlyList<PushCompletion> Completions => _calls.Select(c => c.Completion).ToList();

        public void Add(string name, Guid scope, PushCompletion completion) => _calls.Enqueue((name, scope, completion));
    }

    /// <summary>Marca o escopo: um por instância do serviço scoped.</summary>
    private sealed class ScopeMarker
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    private abstract class RecordingHandlerBase : IPushResultHandler
    {
        private readonly HandlerCalls _calls;
        private readonly Guid _scope;

        protected RecordingHandlerBase(HandlerCalls calls, IServiceProvider scope)
        {
            _calls = calls;

            // O IServiceProvider injetado num serviço scoped é o do escopo: o mesmo para todos os handlers do resultado.
            _scope = ScopeIds.GetValue(scope, static _ => new ScopeMarker()).Id;
        }

        private static ConditionalWeakTable<IServiceProvider, ScopeMarker> ScopeIds { get; } = new();

        public virtual Task HandleAsync(PushCompletion completion, CancellationToken cancellationToken)
        {
            _calls.Add(GetType().Name, _scope, completion);
            return Task.CompletedTask;
        }
    }

    private sealed class FirstHandler(HandlerCalls calls, IServiceProvider scope) : RecordingHandlerBase(calls, scope);

    private sealed class SecondHandler(HandlerCalls calls, IServiceProvider scope) : RecordingHandlerBase(calls, scope);

    private sealed class FailingHandler(HandlerCalls calls, IServiceProvider scope) : RecordingHandlerBase(calls, scope)
    {
        public override async Task HandleAsync(PushCompletion completion, CancellationToken cancellationToken)
        {
            await base.HandleAsync(completion, cancellationToken);
            await Task.Yield();
            throw new InvalidOperationException("falha do handler");
        }
    }

    private sealed class SyncThrowingHandler(HandlerCalls calls, IServiceProvider scope) : RecordingHandlerBase(calls, scope)
    {
        public override Task HandleAsync(PushCompletion completion, CancellationToken cancellationToken)
        {
            base.HandleAsync(completion, cancellationToken);
            throw new NotSupportedException("falha síncrona");
        }
    }

    /// <summary>Store em memória com manutenção: conta as varreduras e pode falhar a próxima.</summary>
    private sealed class SweepStore : IPushCommandStore, IPushCommandStoreMaintenance
    {
        private readonly InMemoryPushCommandStore _inner = new();
        private TaskCompletionSource<bool> _signal = NewSignal();
        private int _calls;

        public bool FailNext { get; set; }

        public int Calls => Volatile.Read(ref _calls);

        /// <summary>Completa quando a varredura tiver sido chamada pelo menos <paramref name="count"/> vezes.</summary>
        public async Task WaitForCallsAsync(int count)
        {
            while (true)
            {
                // Pega o sinal antes de conferir a contagem: uma chamada entre os dois passos completa este sinal.
                var signal = Volatile.Read(ref _signal);
                if (Calls >= count)
                {
                    return;
                }

                await signal.Task;
            }
        }

        private static TaskCompletionSource<bool> NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<IReadOnlyList<PushEnvelope>> RemoveExpiredAsync(DateTimeOffset pendingCreatedBefore, DateTimeOffset inFlightDeliveredBefore, CancellationToken cancellationToken)
        {
            try
            {
                if (FailNext)
                {
                    FailNext = false;
                    throw new InvalidOperationException("store fora do ar");
                }

                return await _inner.RemoveExpiredAsync(pendingCreatedBefore, inFlightDeliveredBefore, cancellationToken);
            }
            finally
            {
                Interlocked.Increment(ref _calls);
                Interlocked.Exchange(ref _signal, NewSignal()).TrySetResult(true);
            }
        }

        public Task AddAsync(PushEnvelope envelope, CancellationToken cancellationToken) => _inner.AddAsync(envelope, cancellationToken);

        public Task<PushTake> TakeNextAsync(long deviceId, string? uuid, CancellationToken cancellationToken) => _inner.TakeNextAsync(deviceId, uuid, cancellationToken);

        public Task<PushEnvelope?> CompleteAsync(long deviceId, string? uuid, CancellationToken cancellationToken) => _inner.CompleteAsync(deviceId, uuid, cancellationToken);

        public Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken) => _inner.RemoveAsync(id, cancellationToken);

        public Task<int> CountAsync(long deviceId, CancellationToken cancellationToken) => _inner.CountAsync(deviceId, cancellationToken);
    }

    /// <summary>Temporizador manual: cada <see cref="TickAsync"/> libera uma volta do laço da varredura.</summary>
    private sealed class ManualTimer : ControlIdPushMaintenanceService.IPurgeTimer
    {
        private readonly Channel<bool> _ticks = Channel.CreateUnbounded<bool>();

        public bool Disposed { get; private set; }

        public async Task TickAsync(SweepStore store, int expectedCalls)
        {
            var called = store.WaitForCallsAsync(expectedCalls);
            await _ticks.Writer.WriteAsync(true);
            await called.WaitAsync(TimeSpan.FromSeconds(30));
        }

        public async ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken) =>
            await _ticks.Reader.ReadAsync(cancellationToken);

        public void Dispose()
        {
            Disposed = true;
            _ticks.Writer.TryComplete();
        }
    }
}
