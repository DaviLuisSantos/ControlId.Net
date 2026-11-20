using System.Diagnostics;
using System.Net;
using ControlId.Access;
using ControlId.Rep;
using Microsoft.Extensions.Logging;

namespace ControlId.Tests;

public class ClientDiagnosticsTests
{
    private static ControlIdDeviceOptions Options(string scheme, Func<long> clock, ILoggerFactory? logs = null) => new()
    {
        BaseAddress = new Uri(scheme + "://10.0.0.7"),
        CircuitBreakerFailureThreshold = 2,
        CircuitBreakerBreakDuration = TimeSpan.FromSeconds(30),
        CircuitClock = clock,
        LoggerFactory = logs,
    };

    [Fact]
    public void Both_clients_implement_the_interface_and_the_published_interfaces_do_not_change()
    {
        Assert.True(typeof(IControlIdClientDiagnostics).IsAssignableFrom(typeof(AccessClient)));
        Assert.True(typeof(IControlIdClientDiagnostics).IsAssignableFrom(typeof(RepClient)));
        Assert.False(typeof(IControlIdClientDiagnostics).IsAssignableFrom(typeof(IAccessClient)));
        Assert.False(typeof(IControlIdClientDiagnostics).IsAssignableFrom(typeof(IRepClient)));
    }

    [Fact]
    public async Task Session_through_the_interface()
    {
        var device = new FakeDeviceHandler().OnJson("login.fcgi", """{"session":"abc"}""").OnJson("get_about.fcgi", "{}");
        using var rep = new RepClient(new ControlIdDeviceOptions { BaseAddress = new Uri("https://10.0.0.7") }, RepMode.Portaria671, device.CreateClient());
        IRepClient asInterface = rep;

        var diagnostics = Assert.IsAssignableFrom<IControlIdClientDiagnostics>(asInterface);
        Assert.Null(diagnostics.Session);
        Assert.Equal(ControlIdCircuitState.Closed, diagnostics.CircuitState);

        await rep.GetAboutAsync();
        Assert.Equal("abc", diagnostics.Session);

        await rep.LogoutAsync();
        Assert.Null(diagnostics.Session);
    }

    [Fact]
    public async Task Reset_closes_an_open_access_circuit_and_logs_1011()
    {
        var clock = new FakeClock();
        var logs = new ConfigurationCacheTests.MemoryLogs();
        var device = new FakeDeviceHandler()
            .OnJson("login.fcgi", """{"session":"s"}""")
            .On("load_objects.fcgi", HttpStatusCode.ServiceUnavailable)
            .On("load_objects.fcgi", HttpStatusCode.ServiceUnavailable)
            .OnJson("load_objects.fcgi", """{"users":[]}""");
        using var client = new AccessClient(Options("http", clock.Read, logs), device.CreateClient());
        IControlIdClientDiagnostics diagnostics = client;

        await Assert.ThrowsAsync<ControlIdException>(() => client.GetUsersAsync());
        await Assert.ThrowsAsync<ControlIdException>(() => client.GetUsersAsync());
        Assert.Equal(ControlIdCircuitState.Open, diagnostics.CircuitState);
        await Assert.ThrowsAsync<ControlIdCircuitOpenException>(() => client.GetUsersAsync());

        diagnostics.ResetCircuit();

        Assert.Equal(ControlIdCircuitState.Closed, diagnostics.CircuitState);
        Assert.Empty(await client.GetUsersAsync()); // sem esperar a pausa
        var entry = Assert.Single(logs.Entries, e => e.EventId == 1011);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("10.0.0.7", entry.Message);
    }

    [Fact]
    public async Task Reset_zeroes_the_failure_count()
    {
        var clock = new FakeClock();
        var device = new FakeDeviceHandler()
            .OnJson("login.fcgi", """{"session":"s"}""")
            .On("get_about.fcgi", HttpStatusCode.ServiceUnavailable);
        using var rep = new RepClient(Options("https", clock.Read), RepMode.Portaria671, device.CreateClient());

        await Assert.ThrowsAsync<ControlIdException>(() => rep.GetAboutAsync());
        rep.ResetCircuit();
        await Assert.ThrowsAsync<ControlIdException>(() => rep.GetAboutAsync());

        // Uma falha antes do reset e uma depois: o limite (2) não foi atingido.
        Assert.Equal(ControlIdCircuitState.Closed, rep.CircuitState);
        await Assert.ThrowsAsync<ControlIdException>(() => rep.GetAboutAsync());
        Assert.Equal(ControlIdCircuitState.Open, rep.CircuitState);
    }

    [Fact]
    public async Task Reset_during_a_probe_ignores_its_late_verdict()
    {
        var clock = new FakeClock();
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new GatedHandler(gate.Task);
        using var rep = new RepClient(Options("https", clock.Read), RepMode.Portaria671, new HttpClient(handler));

        handler.Status = HttpStatusCode.ServiceUnavailable;
        handler.Gated = false;
        await Assert.ThrowsAsync<ControlIdException>(() => rep.GetAboutAsync());
        await Assert.ThrowsAsync<ControlIdException>(() => rep.GetAboutAsync());
        Assert.Equal(ControlIdCircuitState.Open, rep.CircuitState);

        clock.Advance(TimeSpan.FromSeconds(30));
        handler.Gated = true;
        var probe = rep.GetAboutAsync(); // sonda presa no equipamento
        await handler.Entered.Task;

        rep.ResetCircuit();
        Assert.Equal(ControlIdCircuitState.Closed, rep.CircuitState);

        // A sonda antiga falha depois do reset: não reabre o circuito nem conta como falha de sonda.
        gate.SetResult(true);
        await Assert.ThrowsAsync<ControlIdException>(() => probe);
        Assert.Equal(ControlIdCircuitState.Closed, rep.CircuitState);
    }

    [Fact]
    public void Reset_with_the_circuit_disabled_does_nothing()
    {
        var logs = new ConfigurationCacheTests.MemoryLogs();
        using var client = new AccessClient(new ControlIdDeviceOptions { BaseAddress = new Uri("http://10.0.0.7"), LoggerFactory = logs });

        client.ResetCircuit();

        Assert.Equal(ControlIdCircuitState.Closed, client.CircuitState);
        Assert.DoesNotContain(logs.Entries, e => e.EventId == 1011);
    }

    private sealed class FakeClock
    {
        private long _now = 1_000;

        public long Read() => Interlocked.Read(ref _now);

        public void Advance(TimeSpan time) => Interlocked.Add(ref _now, (long)(time.TotalSeconds * Stopwatch.Frequency));
    }

    // Login sempre responde; os comandos respondem Status, esperando o portão quando Gated.
    private sealed class GatedHandler : HttpMessageHandler
    {
        private readonly Task _gate;

        public GatedHandler(Task gate) => _gate = gate;

        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

        public bool Gated { get; set; }

        public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("login.fcgi", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"session":"s"}""") };
            }

            if (Gated)
            {
                Entered.TrySetResult(true);
                await _gate;
            }

            return new HttpResponseMessage(Status) { Content = new StringContent("{}") };
        }
    }
}
