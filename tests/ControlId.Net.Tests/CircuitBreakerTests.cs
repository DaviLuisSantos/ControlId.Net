using System.Diagnostics;
using System.Net;
using System.Text;
using ControlId.Access;
using ControlId.DependencyInjection;
using ControlId.Rep;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace ControlId.Tests;

public class CircuitBreakerTests
{
    private static readonly TimeSpan BreakDuration = TimeSpan.FromSeconds(30);

    private static ControlIdDeviceOptions Options(FakeClock clock, int threshold = 3, int readRetries = 0) => new()
    {
        BaseAddress = new Uri("http://10.0.0.9"),
        CircuitBreakerFailureThreshold = threshold,
        CircuitBreakerBreakDuration = BreakDuration,
        CircuitClock = clock.Read,
        ReadRetries = readRetries,
        RetryDelay = TimeSpan.Zero,
    };

    private static AccessClient Client(ControlIdDeviceOptions options, Device device) => new(options, new HttpClient(device));

    private static Task<HttpResponseMessage> Status(HttpStatusCode status, string body = "") => Task.FromResult(Json(body, status));

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    // Falha o suficiente para abrir o circuito (o equipamento responde 503).
    private static async Task TripAsync(AccessClient client, Device device, int failures)
    {
        device.OnCommand = _ => Status(HttpStatusCode.ServiceUnavailable);
        for (var i = 0; i < failures; i++)
        {
            var error = await Assert.ThrowsAnyAsync<ControlIdException>(() => client.GetUsersAsync());
            Assert.Equal(HttpStatusCode.ServiceUnavailable, error.StatusCode);
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 300 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    [Fact]
    public async Task Is_off_by_default()
    {
        var device = new Device { OnCommand = _ => Status(HttpStatusCode.ServiceUnavailable) };
        using var client = new AccessClient(new ControlIdDeviceOptions { BaseAddress = new Uri("http://10.0.0.9") }, new HttpClient(device));

        for (var i = 0; i < 10; i++)
        {
            await Assert.ThrowsAnyAsync<ControlIdException>(() => client.GetUsersAsync());
        }

        Assert.Equal(10, device.Commands);
        Assert.Equal(ControlIdCircuitState.Closed, client.CircuitState);
        Assert.Equal(0, new ControlIdDeviceOptions().CircuitBreakerFailureThreshold);
        Assert.Equal(TimeSpan.FromSeconds(30), new ControlIdDeviceOptions().CircuitBreakerBreakDuration);
    }

    [Fact]
    public async Task Opens_at_the_threshold_and_stops_touching_the_device()
    {
        var clock = new FakeClock();
        var device = new Device();
        using var client = Client(Options(clock), device);

        await TripAsync(client, device, 2);
        Assert.Equal(ControlIdCircuitState.Closed, client.CircuitState);

        await TripAsync(client, device, 1);
        Assert.Equal(ControlIdCircuitState.Open, client.CircuitState);

        var commandsBefore = device.Commands;
        var loginsBefore = device.Logins;
        await Assert.ThrowsAsync<ControlIdCircuitOpenException>(() => client.GetUsersAsync());
        await Assert.ThrowsAsync<ControlIdCircuitOpenException>(() => client.OpenDoorAsync(1));

        Assert.Equal(commandsBefore, device.Commands);
        Assert.Equal(loginsBefore, device.Logins);
    }

    [Fact]
    public async Task Reports_how_long_until_the_next_probe()
    {
        var clock = new FakeClock();
        var device = new Device();
        using var client = Client(Options(clock), device);
        await TripAsync(client, device, 3);

        var first = await Assert.ThrowsAsync<ControlIdCircuitOpenException>(() => client.GetUsersAsync());
        Assert.InRange(first.RetryAfter.TotalSeconds, 29.9, 30.0);

        clock.Advance(TimeSpan.FromSeconds(10));
        var later = await Assert.ThrowsAsync<ControlIdCircuitOpenException>(() => client.GetUsersAsync());
        Assert.InRange(later.RetryAfter.TotalSeconds, 19.9, 20.0);
        Assert.Equal(ControlIdCircuitState.Open, client.CircuitState);
    }

    [Fact]
    public async Task Client_errors_do_not_count()
    {
        var clock = new FakeClock();
        var device = new Device();
        using var client = Client(Options(clock), device);

        device.OnCommand = _ => Status(HttpStatusCode.BadRequest, """{"error":"Invalid door","code":7}""");
        for (var i = 0; i < 5; i++)
        {
            await Assert.ThrowsAnyAsync<ControlIdException>(() => client.GetUsersAsync());
        }

        device.OnCommand = _ => Status(HttpStatusCode.Unauthorized);
        for (var i = 0; i < 5; i++)
        {
            await Assert.ThrowsAnyAsync<ControlIdException>(() => client.GetUsersAsync());
        }

        Assert.Equal(ControlIdCircuitState.Closed, client.CircuitState);
        device.OnCommand = _ => Status(HttpStatusCode.OK, """{"users":[]}""");
        Assert.Empty(await client.GetUsersAsync());
    }

    [Fact]
    public async Task A_refused_login_does_not_count()
    {
        var clock = new FakeClock();
        var device = new Device { OnLogin = _ => Status(HttpStatusCode.Unauthorized, """{"error":"bad credentials"}""") };
        using var client = Client(Options(clock), device);

        for (var i = 0; i < 5; i++)
        {
            await Assert.ThrowsAsync<ControlIdAuthenticationException>(() => client.GetUsersAsync());
        }

        Assert.Equal(5, device.Logins);
        Assert.Equal(ControlIdCircuitState.Closed, client.CircuitState);
    }

    [Fact]
    public async Task A_cancellation_by_the_caller_does_not_count()
    {
        var clock = new FakeClock();
        var device = new Device { OnCommand = async ct => { await Task.Delay(Timeout.Infinite, ct); return Json(string.Empty); } };
        using var client = Client(Options(clock), device);

        for (var i = 1; i <= 5; i++)
        {
            using var cts = new CancellationTokenSource();
            var call = client.GetUsersAsync(cts.Token);
            var expected = i;
            await WaitUntilAsync(() => device.Commands == expected);
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
        }

        Assert.Equal(ControlIdCircuitState.Closed, client.CircuitState);
    }

    [Theory]
    [InlineData("network")]
    [InlineData("timeout")]
    public async Task Network_failures_and_client_timeouts_count(string kind)
    {
        var clock = new FakeClock();
        var device = new Device
        {
            OnCommand = _ => kind == "network"
                ? throw new HttpRequestException("connection refused")
                : throw new TaskCanceledException("timeout"),
        };
        using var client = Client(Options(clock, threshold: 2), device);

        await Assert.ThrowsAnyAsync<ControlIdException>(() => client.GetUsersAsync());
        await Assert.ThrowsAnyAsync<ControlIdException>(() => client.GetUsersAsync());

        Assert.Equal(ControlIdCircuitState.Open, client.CircuitState);
        await Assert.ThrowsAsync<ControlIdCircuitOpenException>(() => client.GetUsersAsync());
        Assert.Equal(2, device.Commands);
    }

    [Fact]
    public async Task A_success_resets_the_count()
    {
        var clock = new FakeClock();
        var device = new Device();
        using var client = Client(Options(clock), device);

        await TripAsync(client, device, 2);
        device.OnCommand = _ => Status(HttpStatusCode.OK, """{"users":[]}""");
        await client.GetUsersAsync();
        await TripAsync(client, device, 2);
        Assert.Equal(ControlIdCircuitState.Closed, client.CircuitState);

        await TripAsync(client, device, 1);
        Assert.Equal(ControlIdCircuitState.Open, client.CircuitState);
    }

    [Fact]
    public async Task Half_open_lets_a_single_concurrent_probe_through_and_closes_on_success()
    {
        var clock = new FakeClock();
        var device = new Device();
        using var client = Client(Options(clock), device);
        await TripAsync(client, device, 3);

        clock.Advance(BreakDuration);
        Assert.Equal(ControlIdCircuitState.HalfOpen, client.CircuitState);

        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var before = device.Commands;
        device.OnCommand = async _ =>
        {
            await gate.Task;
            return Json("""{"users":[]}""");
        };

        var probe = client.GetUsersAsync();
        await WaitUntilAsync(() => device.Commands == before + 1);

        var rejected = await Assert.ThrowsAsync<ControlIdCircuitOpenException>(() => client.GetUsersAsync());
        Assert.Equal(TimeSpan.Zero, rejected.RetryAfter);
        Assert.Equal(before + 1, device.Commands);

        gate.SetResult(true);
        await probe;

        Assert.Equal(ControlIdCircuitState.Closed, client.CircuitState);
        Assert.Empty(await client.GetUsersAsync());
    }

    [Fact]
    public async Task A_failed_probe_reopens_the_circuit()
    {
        var clock = new FakeClock();
        var device = new Device();
        using var client = Client(Options(clock), device);
        await TripAsync(client, device, 3);

        clock.Advance(BreakDuration);
        var before = device.Commands;
        var error = await Assert.ThrowsAnyAsync<ControlIdException>(() => client.GetUsersAsync());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, error.StatusCode);
        Assert.Equal(before + 1, device.Commands);
        Assert.Equal(ControlIdCircuitState.Open, client.CircuitState);

        var rejected = await Assert.ThrowsAsync<ControlIdCircuitOpenException>(() => client.GetUsersAsync());
        Assert.InRange(rejected.RetryAfter.TotalSeconds, 29.9, 30.0);

        clock.Advance(BreakDuration);
        Assert.Equal(ControlIdCircuitState.HalfOpen, client.CircuitState);
    }

    [Fact]
    public async Task A_canceled_probe_gives_the_turn_to_the_next_request()
    {
        var clock = new FakeClock();
        var device = new Device();
        using var client = Client(Options(clock), device);
        await TripAsync(client, device, 3);

        clock.Advance(BreakDuration);
        var before = device.Commands;
        device.OnCommand = async ct => { await Task.Delay(Timeout.Infinite, ct); return Json(string.Empty); };
        using var cts = new CancellationTokenSource();
        var probe = client.GetUsersAsync(cts.Token);
        await WaitUntilAsync(() => device.Commands == before + 1);

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => probe);
        Assert.Equal(ControlIdCircuitState.HalfOpen, client.CircuitState);

        device.OnCommand = _ => Status(HttpStatusCode.OK, """{"users":[]}""");
        Assert.Empty(await client.GetUsersAsync());
        Assert.Equal(ControlIdCircuitState.Closed, client.CircuitState);
    }

    [Fact]
    public async Task A_read_retry_does_not_repeat_once_the_circuit_is_open()
    {
        var clock = new FakeClock();
        var device = new Device { OnCommand = _ => Status(HttpStatusCode.ServiceUnavailable) };
        using var client = Client(Options(clock, threshold: 2, readRetries: 5), device);

        await Assert.ThrowsAsync<ControlIdCircuitOpenException>(() => client.GetUsersAsync());

        // A primeira tentativa e a primeira repetição abrem o circuito; as seguintes nem saem da máquina.
        Assert.Equal(2, device.Commands);
        Assert.Equal(ControlIdCircuitState.Open, client.CircuitState);
    }

    [Fact]
    public async Task Login_goes_through_the_gate()
    {
        var clock = new FakeClock();
        var device = new Device { OnLogin = _ => Status(HttpStatusCode.ServiceUnavailable) };
        using var client = Client(Options(clock), device);

        for (var i = 0; i < 3; i++)
        {
            await Assert.ThrowsAnyAsync<ControlIdException>(() => client.GetUsersAsync());
        }

        Assert.Equal(ControlIdCircuitState.Open, client.CircuitState);
        await Assert.ThrowsAsync<ControlIdCircuitOpenException>(() => client.GetUsersAsync());
        await Assert.ThrowsAsync<ControlIdCircuitOpenException>(() => client.LoginAsync());

        Assert.Equal(3, device.Logins);
        Assert.Equal(0, device.Commands);
    }

    [Fact]
    public async Task The_afd_stream_goes_through_the_gate()
    {
        var clock = new FakeClock();
        var device = new Device { OnCommand = _ => Status(HttpStatusCode.ServiceUnavailable) };
        using var client = new RepClient(Options(clock, threshold: 2), RepMode.Portaria671, new HttpClient(device));

        await Assert.ThrowsAnyAsync<ControlIdException>(() => client.OpenAfdStreamAsync());
        await Assert.ThrowsAnyAsync<ControlIdException>(() => client.OpenAfdStreamAsync());
        Assert.Equal(ControlIdCircuitState.Open, client.CircuitState);

        await Assert.ThrowsAsync<ControlIdCircuitOpenException>(() => client.OpenAfdStreamAsync());
        await Assert.ThrowsAsync<ControlIdCircuitOpenException>(() => client.GetAboutAsync());
        Assert.Equal(2, device.Commands);

        // Passada a pausa, a sonda do stream fecha o circuito.
        clock.Advance(BreakDuration);
        device.OnCommand = _ => Status(HttpStatusCode.OK, "000000001");
        using var stream = await client.OpenAfdStreamAsync();
        Assert.Equal(ControlIdCircuitState.Closed, client.CircuitState);
    }

    [Fact]
    public async Task Records_transitions_rejections_and_the_span_error()
    {
        using var telemetry = new TelemetryTests.TelemetryCapture("10.77.9.1");
        var clock = new FakeClock();
        var options = telemetry.Options();
        options.CircuitBreakerFailureThreshold = 2;
        options.CircuitBreakerBreakDuration = BreakDuration;
        options.CircuitClock = clock.Read;
        var device = new Device { OnCommand = _ => Status(HttpStatusCode.ServiceUnavailable) };
        using var client = Client(options, device);

        await Assert.ThrowsAnyAsync<ControlIdException>(() => client.GetUsersAsync());
        await Assert.ThrowsAnyAsync<ControlIdException>(() => client.GetUsersAsync());
        await Assert.ThrowsAsync<ControlIdCircuitOpenException>(() => client.GetUsersAsync());

        clock.Advance(BreakDuration);
        device.OnCommand = _ => Status(HttpStatusCode.OK, """{"users":[]}""");
        await client.GetUsersAsync();

        double Transitions(string state) => telemetry.Measurements
            .Where(m => m.Instrument == "controlid.client.circuit.transitions" && Equals(m.Tags["controlid.circuit.state"], state))
            .Sum(m => m.Value);

        Assert.Equal(1, Transitions("open"));
        Assert.Equal(1, Transitions("half_open"));
        Assert.Equal(1, Transitions("closed"));
        Assert.Equal(1, telemetry.Sum("controlid.client.circuit.rejected"));

        var rejected = Assert.Single(telemetry.Activities, a => Equals(a.GetTagItem("error.type"), "circuit_open"));
        Assert.Equal("load_objects", rejected.DisplayName);
        Assert.Equal(ActivityStatusCode.Error, rejected.Status);
    }

    [Fact]
    public async Task Logs_each_transition()
    {
        var logs = new EventLog();
        var clock = new FakeClock();
        var options = Options(clock, threshold: 1);
        options.LoggerFactory = logs;
        var device = new Device { OnCommand = _ => Status(HttpStatusCode.ServiceUnavailable) };
        using var client = Client(options, device);

        await Assert.ThrowsAnyAsync<ControlIdException>(() => client.GetUsersAsync());
        clock.Advance(BreakDuration);
        device.OnCommand = _ => Status(HttpStatusCode.OK, """{"users":[]}""");
        await client.GetUsersAsync();

        Assert.Contains((1007, LogLevel.Warning), logs.Entries);
        Assert.Contains((1008, LogLevel.Debug), logs.Entries);
        Assert.Contains((1009, LogLevel.Information), logs.Entries);
    }

    [Theory]
    [InlineData(10_000, 20_000)]
    [InlineData(100, 1_000)] // mínimo de 1 s
    [InlineData(400, 1_000)]
    [InlineData(-1, 300_000)] // Timeout.InfiniteTimeSpan: teto de 5 min
    [InlineData(0, 300_000)]
    public void Probe_deadline_is_twice_the_timeout(int timeoutMs, int expectedMs)
    {
        Assert.Equal(TimeSpan.FromMilliseconds(expectedMs), Internal.CircuitBreaker.ProbeDeadlineFor(TimeSpan.FromMilliseconds(timeoutMs)));
        Assert.Equal(TimeSpan.MaxValue, Internal.CircuitBreaker.ProbeDeadlineFor(TimeSpan.MaxValue));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(-1)] // timeout infinito: a sonda presa não prende o HalfOpen para sempre
    public async Task An_abandoned_probe_counts_as_a_failure_and_reopens(int timeoutSeconds)
    {
        var clock = new FakeClock();
        var device = new Device();
        var options = Options(clock);
        options.Timeout = timeoutSeconds < 0 ? Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(timeoutSeconds);
        var deadline = timeoutSeconds < 0 ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(timeoutSeconds * 2);
        using var client = Client(options, device);
        await TripAsync(client, device, 3);

        clock.Advance(BreakDuration);
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var before = device.Commands;
        device.OnCommand = async _ =>
        {
            await gate.Task;
            return Json("""{"users":[]}""");
        };
        var probe = client.GetUsersAsync();
        await WaitUntilAsync(() => device.Commands == before + 1);

        clock.Advance(deadline - TimeSpan.FromSeconds(1));
        Assert.Equal(ControlIdCircuitState.HalfOpen, client.CircuitState);
        Assert.Equal(TimeSpan.Zero, (await Assert.ThrowsAsync<ControlIdCircuitOpenException>(() => client.GetUsersAsync())).RetryAfter);

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(ControlIdCircuitState.Open, client.CircuitState);
        var rejected = await Assert.ThrowsAsync<ControlIdCircuitOpenException>(() => client.GetUsersAsync());
        Assert.InRange(rejected.RetryAfter.TotalSeconds, 29.9, 30.0);

        // O sucesso tardio da sonda abandonada não fecha o circuito.
        gate.SetResult(true);
        Assert.Empty(await probe);
        Assert.Equal(ControlIdCircuitState.Open, client.CircuitState);
        Assert.Equal(before + 1, device.Commands);

        // Passada a nova pausa, uma sonda nova fecha.
        clock.Advance(BreakDuration);
        Assert.Empty(await client.GetUsersAsync());
        Assert.Equal(ControlIdCircuitState.Closed, client.CircuitState);
    }

    [Fact]
    public async Task The_late_failure_of_an_abandoned_probe_does_not_affect_the_next_probe()
    {
        var clock = new FakeClock();
        var device = new Device();
        var options = Options(clock);
        options.Timeout = TimeSpan.FromSeconds(1);
        options.MaxConcurrentRequests = 2; // a sonda abandonada segura uma vaga até o equipamento responder
        using var client = Client(options, device);
        await TripAsync(client, device, 3);

        clock.Advance(BreakDuration);
        var first = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        device.OnCommand = async _ =>
        {
            await first.Task;
            return Json(string.Empty, HttpStatusCode.ServiceUnavailable);
        };
        var abandoned = client.GetUsersAsync();
        await WaitUntilAsync(() => device.Commands == 4);

        clock.Advance(TimeSpan.FromSeconds(2)); // prazo da sonda vencido: reabre
        Assert.Equal(ControlIdCircuitState.Open, client.CircuitState);
        clock.Advance(BreakDuration); // nova sonda liberada

        var second = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        device.OnCommand = async _ =>
        {
            await second.Task;
            return Json("""{"users":[]}""");
        };
        var probe = client.GetUsersAsync();
        await WaitUntilAsync(() => device.Commands == 5);

        first.SetResult(true);
        await Assert.ThrowsAnyAsync<ControlIdException>(() => abandoned);
        Assert.Equal(ControlIdCircuitState.HalfOpen, client.CircuitState); // a falha tardia não reabriu

        second.SetResult(true);
        await probe;
        Assert.Equal(ControlIdCircuitState.Closed, client.CircuitState);
    }

    [Fact]
    public async Task Transitions_are_logged_outside_the_lock()
    {
        var logs = new ReentrantLog();
        var clock = new FakeClock();
        var options = Options(clock, threshold: 1);
        options.LoggerFactory = logs;
        var device = new Device { OnCommand = _ => Status(HttpStatusCode.ServiceUnavailable) };
        using var client = Client(options, device);
        logs.Probe = () => client.CircuitState;

        await Assert.ThrowsAnyAsync<ControlIdException>(() => client.GetUsersAsync());
        clock.Advance(BreakDuration);
        device.OnCommand = _ => Status(HttpStatusCode.OK, """{"users":[]}""");
        await client.GetUsersAsync();
        client.ResetCircuit();

        // Abertura, meio aberto, fechamento e reset: em cada log, outra thread conseguiu ler o estado (o lock estava livre).
        Assert.Equal(new[] { 1007, 1008, 1009, 1011 }, logs.Checked.Select(c => c.EventId).ToArray());
        Assert.All(logs.Checked, c => Assert.True(c.LockWasFree, $"evento {c.EventId} registrado sob o lock"));
    }

    [Theory]
    [InlineData(-1, 30, false)]
    [InlineData(0, 30, true)]
    [InlineData(100, 30, true)]
    [InlineData(101, 30, false)]
    [InlineData(3, 0, false)]
    [InlineData(3, -5, false)]
    public void Validate_covers_both_options(int threshold, int seconds, bool valid)
    {
        var options = new ControlIdDeviceOptions
        {
            BaseAddress = new Uri("http://10.0.0.9"),
            CircuitBreakerFailureThreshold = threshold,
            CircuitBreakerBreakDuration = TimeSpan.FromSeconds(seconds),
        };

        Assert.Equal(100, ControlIdDeviceOptions.MaxCircuitBreakerFailureThreshold);
        if (valid)
        {
            using var client = new AccessClient(options);
        }
        else
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AccessClient(options));
        }
    }

    [Fact]
    public async Task Rep_options_bind_from_configuration_and_reach_the_client()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ControlId:Rep:rh:BaseAddress"] = "https://10.0.0.3",
            ["ControlId:Rep:rh:CircuitBreakerFailureThreshold"] = "2",
            ["ControlId:Rep:rh:CircuitBreakerBreakDuration"] = "00:00:10",
        }).Build();
        var device = new FakeDeviceHandler()
            .OnJson("login.fcgi", """{"session":"s"}""")
            .On("get_about.fcgi", HttpStatusCode.ServiceUnavailable);

        var services = new ServiceCollection();
        services.AddControlIdRepDevices(configuration.GetSection("ControlId:Rep"));
        services.ConfigureAll<HttpClientFactoryOptions>(o => o.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = device));
        await using var provider = services.BuildServiceProvider();
        var rep = Assert.IsType<RepClient>(provider.GetRequiredService<IControlIdClientFactory>().GetRepClient("rh"));

        await Assert.ThrowsAnyAsync<ControlIdException>(() => rep.GetAboutAsync());
        await Assert.ThrowsAnyAsync<ControlIdException>(() => rep.GetAboutAsync());
        var rejected = await Assert.ThrowsAsync<ControlIdCircuitOpenException>(() => rep.GetAboutAsync());

        Assert.Equal(ControlIdCircuitState.Open, rep.CircuitState);
        Assert.InRange(rejected.RetryAfter.TotalSeconds, 9, 10);
        Assert.Equal(2, device.RequestsTo("get_about.fcgi").Count());
    }

    private sealed class FakeClock
    {
        private long _now = 1_000;

        public long Read() => Interlocked.Read(ref _now);

        public void Advance(TimeSpan time) => Interlocked.Add(ref _now, (long)(time.TotalSeconds * Stopwatch.Frequency));
    }

    private sealed class Device : HttpMessageHandler
    {
        private int _logins;
        private int _commands;

        public Func<CancellationToken, Task<HttpResponseMessage>> OnLogin { get; set; } = _ => Task.FromResult(Json("""{"session":"s"}"""));

        public Func<CancellationToken, Task<HttpResponseMessage>> OnCommand { get; set; } = _ => Task.FromResult(Json("""{"users":[]}"""));

        public int Logins => Volatile.Read(ref _logins);

        public int Commands => Volatile.Read(ref _commands);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("login.fcgi", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _logins);
                return OnLogin(cancellationToken);
            }

            Interlocked.Increment(ref _commands);
            return OnCommand(cancellationToken);
        }
    }

    // A cada log do circuito, lê o estado em outra thread: se o log saísse sob o lock, a leitura ficaria presa.
    private sealed class ReentrantLog : ILoggerFactory, ILogger
    {
        private static readonly int[] CircuitEvents = { 1007, 1008, 1009, 1011 };
        private readonly object _gate = new();
        private readonly List<(int EventId, bool LockWasFree)> _checked = new();

        public Func<ControlIdCircuitState>? Probe { get; set; }

        public IReadOnlyList<(int EventId, bool LockWasFree)> Checked
        {
            get
            {
                lock (_gate)
                {
                    return _checked.ToList();
                }
            }
        }

        public ILogger CreateLogger(string categoryName) => this;

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (Probe is not { } probe || !CircuitEvents.Contains(eventId.Id))
            {
                return;
            }

            var read = Task.Run(probe);
            var free = read.Wait(TimeSpan.FromSeconds(5));
            lock (_gate)
            {
                _checked.Add((eventId.Id, free));
            }
        }

        public void Dispose()
        {
        }
    }

    private sealed class EventLog : ILoggerFactory, ILogger
    {
        private readonly object _gate = new();
        private readonly List<(int EventId, LogLevel Level)> _entries = new();

        public IReadOnlyList<(int EventId, LogLevel Level)> Entries
        {
            get
            {
                lock (_gate)
                {
                    return _entries.ToList();
                }
            }
        }

        public ILogger CreateLogger(string categoryName) => this;

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (_gate)
            {
                _entries.Add((eventId.Id, logLevel));
            }
        }

        public void Dispose()
        {
        }
    }
}
