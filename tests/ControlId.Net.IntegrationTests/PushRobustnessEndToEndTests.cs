using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using ControlId.Access;
using ControlId.Access.Push;
using ControlId.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ControlId.Net.IntegrationTests;

/// <summary>
/// Robustez do Push com o simulador de verdade (P1-2, P1-3 e P1-4 da auditoria): handler de resultado que lança não perde o
/// comando seguinte, comando vencido (<see cref="PushServerOptions.CommandTtl"/>) não chega ao equipamento e a varredura
/// do serviço de fundo falha um envio entregue cujo resultado nunca volta (<see cref="PushServerOptions.InFlightTimeout"/>).
/// </summary>
public sealed class PushRobustnessEndToEndTests
{
    private const long SimulatorDeviceId = 935107;

    // EventIds do PushServer (categoria ControlId.Net, src/ControlId.Net/Access/Push/PushLog.cs).
    private const int ResultHandlerFailed = 1101;
    private const int CommandExpired = 1102;
    private const int CommandOrphaned = 1103;
    private const int Purged = 1107;

    [Fact]
    public async Task A_result_handler_that_throws_does_not_lose_the_next_command()
    {
        var completions = new CompletionLog();
        var (server, address, hits, logs) = await StartServerAsync(s =>
        {
            s.AddSingleton(completions);
            s.AddControlIdPush();
            s.AddControlIdPushResultHandler<ThrowsOnFirstCompletionHandler>();
        });
        await using var _ = server;
        await using var sim = await RunningSimulator.StartAsync();
        using var device = sim.CreateClient();
        var push = server.Services.GetRequiredService<PushServer>();

        var first = await push.EnqueueAsync(SimulatorDeviceId, PushCommand.ShowMessage("Primeiro"));
        var second = await push.EnqueueAsync(SimulatorDeviceId, PushCommand.ShowMessage("Segundo"));
        await ConfigurePushAsync(device, address);

        await EndToEndWait.UntilAsync(() => completions.Count == 2, "o handler receber os dois resultados");

        var all = completions.Snapshot();
        Assert.Equal(new[] { first, second }, all.Select(c => c.CommandId));
        Assert.All(all, c =>
        {
            Assert.Null(c.Error);
            Assert.True(Assert.Single(c.Results).Success);
        });
        Assert.Equal(1, completions.Thrown);

        // A exceção do handler virou log 1101 e não chegou ao equipamento: os dois /result responderam 200.
        Assert.Equal(1, logs.Count(ResultHandlerFailed));
        var results = hits.Snapshot().Where(h => h.Path == "/result").ToList();
        Assert.Equal(2, results.Count);
        Assert.All(results, h => Assert.Equal(200, h.StatusCode));

        var state = await StateAsync(sim);
        Assert.Equal("Segundo", state.GetProperty("screenMessage").GetString());
        Assert.Equal(2, state.GetProperty("push").GetProperty("commandsExecuted").GetInt32());
        Assert.Equal(0, await push.GetPendingCountAsync(SimulatorDeviceId));
    }

    [Fact]
    public async Task An_expired_command_never_reaches_the_device_and_fails_through_the_handler()
    {
        var ttl = TimeSpan.FromSeconds(2);
        var completions = new CompletionLog();
        var (server, address, _, logs) = await StartServerAsync(s =>
        {
            s.AddSingleton(completions);

            // Sem varredura: o vencido só pode sair na consulta do equipamento (/push).
            s.AddControlIdPush(o =>
            {
                o.CommandTtl = ttl;
                o.PurgeInterval = null;
            });
            s.AddControlIdPushResultHandler<RecordingHandler>();
        });
        await using var _ = server;
        await using var sim = await RunningSimulator.StartAsync();
        using var device = sim.CreateClient();
        var push = server.Services.GetRequiredService<PushServer>();

        // Enfileirado com o equipamento sem Push configurado (como desligado). O relógio do PushServer é interno e este
        // projeto não tem InternalsVisibleTo, então o TTL passa de verdade: aqui a espera é o próprio tempo, não um efeito.
        var stale = await push.EnqueueAsync(SimulatorDeviceId, PushCommand.ShowMessage("Vencido"));
        await Task.Delay(ttl + TimeSpan.FromMilliseconds(250));
        var fresh = await push.EnqueueAsync(SimulatorDeviceId, PushCommand.ShowMessage("Dentro do prazo"));

        await ConfigurePushAsync(device, address);
        await EndToEndWait.UntilAsync(() => completions.Count == 2, "o handler receber a expiração e o resultado do comando novo");

        var expired = completions.Single(stale);
        Assert.Empty(expired.Results);
        Assert.Contains("CommandTtl", expired.Error, StringComparison.Ordinal);

        var delivered = completions.Single(fresh);
        Assert.Null(delivered.Error);
        Assert.True(Assert.Single(delivered.Results).Success);

        // O simulador executou só o comando dentro do prazo.
        var state = await StateAsync(sim);
        Assert.Equal(1, state.GetProperty("push").GetProperty("commandsExecuted").GetInt32());
        Assert.Equal("Dentro do prazo", state.GetProperty("screenMessage").GetString());
        Assert.Equal(1, logs.Count(CommandExpired));
        Assert.Equal(0, await push.GetPendingCountAsync(SimulatorDeviceId));
    }

    [Fact]
    public async Task The_maintenance_sweep_fails_an_in_flight_command_whose_result_never_arrives()
    {
        var completions = new CompletionLog();

        // O resultado se perde na rede: o servidor nunca vê o /result, e a partir daí o equipamento não alcança mais o
        // /push (senão a nova consulta reportaria o órfão antes da varredura).
        var resultLost = new StrongBox<bool>();
        var (server, address, hits, logs) = await StartServerAsync(
            s =>
            {
                s.AddSingleton(completions);
                s.AddControlIdPush(o =>
                {
                    o.InFlightTimeout = TimeSpan.FromMilliseconds(500);
                    o.PurgeInterval = TimeSpan.FromMilliseconds(200);
                });
                s.AddControlIdPushResultHandler<RecordingHandler>();
            },
            intercept: context =>
            {
                if (context.Request.Path == "/result")
                {
                    Volatile.Write(ref resultLost.Value, true);
                }
                else if (context.Request.Path != "/push" || !Volatile.Read(ref resultLost.Value))
                {
                    return false;
                }

                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return true;
            });
        await using var _ = server;
        await using var sim = await RunningSimulator.StartAsync();
        using var device = sim.CreateClient();
        var push = server.Services.GetRequiredService<PushServer>();

        var id = await push.EnqueueAsync(SimulatorDeviceId, PushCommand.ShowMessage("Sem resultado"));
        await ConfigurePushAsync(device, address);

        await EndToEndWait.UntilAsync(() => completions.Count == 1, "a varredura falhar o envio abandonado");

        var orphan = completions.Single(id);
        Assert.Empty(orphan.Results);
        Assert.Contains("InFlightTimeout", orphan.Error, StringComparison.Ordinal);
        Assert.Equal(1, logs.Count(CommandOrphaned));
        Assert.Equal(0, await push.GetPendingCountAsync(SimulatorDeviceId));

        // O log 1107 (Information) sai depois de avisar o handler.
        await EndToEndWait.UntilAsync(() => logs.Count(Purged) == 1, "a varredura registrar o log 1107");

        // O equipamento recebeu e executou o comando; só o resultado se perdeu. Nenhuma consulta depois disso chegou ao PushServer.
        var state = await StateAsync(sim);
        Assert.Equal(1, state.GetProperty("push").GetProperty("commandsExecuted").GetInt32());
        Assert.Equal("Sem resultado", state.GetProperty("screenMessage").GetString());
        var snapshot = hits.Snapshot();
        Assert.Equal(1, snapshot.Count(h => h.Path == "/push" && h.StatusCode == 200));
        Assert.Contains(snapshot, h => h.Path == "/result" && h.StatusCode == 503);
    }

    private static Task ConfigurePushAsync(AccessClient device, Uri address) =>
        device.ConfigurePushAsync(new PushConfiguration { RemoteAddress = address.ToString().TrimEnd('/'), RequestPeriod = TimeSpan.FromSeconds(1) });

    private static async Task<JsonElement> StateAsync(RunningSimulator sim) =>
        await sim.Control.GetFromJsonAsync<JsonElement>("/simulator/state");

    private static async Task<(WebApplication Server, Uri Address, CallbackRecorder Hits, LogCapture Logs)> StartServerAsync(
        Action<IServiceCollection> services, Func<HttpContext, bool>? intercept = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var logs = new LogCapture();
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Information);
        builder.Logging.AddProvider(logs);
        services(builder.Services);
        var server = builder.Build();

        var hits = new CallbackRecorder();
        server.Use(async (context, next) =>
        {
            if (intercept is null || !intercept(context))
            {
                await next(context);
            }

            hits.Add(new CallbackHit(context.Request.Path.Value ?? string.Empty, context.Response.StatusCode));
        });

        server.MapControlIdPush().AllowAnonymousControlIdCallbacks();
        await server.StartAsync();
        var address = server.Services.GetRequiredService<IServer>().Features.GetRequiredFeature<IServerAddressesFeature>().Addresses.First();
        return (server, new Uri(address), hits, logs);
    }

    /// <summary>Resultados recebidos pelos handlers (singleton: os handlers são scoped, um por resultado).</summary>
    private sealed class CompletionLog
    {
        private readonly ConcurrentQueue<PushCompletion> _completions = new();
        private int _thrown;

        public int Count => _completions.Count;

        public int Thrown => Volatile.Read(ref _thrown);

        public void Add(PushCompletion completion) => _completions.Enqueue(completion);

        public bool MarkThrown() => Interlocked.CompareExchange(ref _thrown, 1, 0) == 0;

        public IReadOnlyList<PushCompletion> Snapshot() => _completions.ToArray();

        public PushCompletion Single(Guid commandId) => Assert.Single(_completions, c => c.CommandId == commandId);
    }

    private sealed class RecordingHandler(CompletionLog log) : IPushResultHandler
    {
        public Task HandleAsync(PushCompletion completion, CancellationToken cancellationToken)
        {
            log.Add(completion);
            return Task.CompletedTask;
        }
    }

    /// <summary>Registra o resultado e lança no primeiro, como um handler com o banco fora do ar.</summary>
    private sealed class ThrowsOnFirstCompletionHandler(CompletionLog log) : IPushResultHandler
    {
        public Task HandleAsync(PushCompletion completion, CancellationToken cancellationToken)
        {
            log.Add(completion);
            if (log.MarkThrown())
            {
                throw new InvalidOperationException("Falha simulada no handler de resultado.");
            }

            return Task.CompletedTask;
        }
    }

    private sealed record CallbackHit(string Path, int StatusCode);

    private sealed class CallbackRecorder
    {
        private readonly ConcurrentQueue<CallbackHit> _hits = new();

        public void Add(CallbackHit hit) => _hits.Enqueue(hit);

        public IReadOnlyList<CallbackHit> Snapshot() => _hits.ToArray();
    }

    /// <summary>Guarda o EventId de cada log da categoria do núcleo (nunca a mensagem).</summary>
    private sealed class LogCapture : ILoggerProvider
    {
        private readonly ConcurrentQueue<int> _events = new();

        public int Count(int eventId) => _events.Count(e => e == eventId);

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this, categoryName == ControlIdTelemetry.LoggerCategory);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(LogCapture owner, bool core) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => core;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (core)
                {
                    owner._events.Enqueue(eventId.Id);
                }
            }
        }
    }
}
