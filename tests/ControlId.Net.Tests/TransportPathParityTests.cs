using System.Diagnostics;
using System.Net;
using System.Security.Authentication;
using System.Text;
using ControlId.Internal;
using Microsoft.Extensions.Logging;

namespace ControlId.Tests;

/// <summary>
/// Os dois caminhos de envio do transporte (resposta lida inteira e resposta em stream) passam pelo mesmo núcleo: para a
/// mesma leitura, reagem igual a 401, 5xx, 408/429, timeout, erro de rede e de TLS, cancelamento e circuito aberto — mesma
/// exceção, mesmas requisições, mesmo estado do circuito, mesmos logs, spans e métricas, e nenhuma vaga presa ao fim.
/// </summary>
public class TransportPathParityTests
{
    // Leitura (ReadRetry.IsReadOnly) usada pelo REP também em stream (OpenAfdStreamAsync).
    private const string Endpoint = "get_afd.fcgi";
    private const string Afd = "0000000011\r\n";
    private static readonly TimeSpan BreakDuration = TimeSpan.FromSeconds(30);
    private static int _hosts;

    public enum SendPath
    {
        Buffered,
        Stream,
    }

    public static TheoryData<string> Scenarios() => new()
    {
        "401-then-success",
        "401-twice",
        "503-without-retries",
        "503-retried-then-success",
        "503-retries-exhausted",
        "408-retried",
        "429-retried",
        "429-without-retries",
        "timeout",
        "timeout-retried",
        "network",
        "network-retried",
        "tls",
        "caller-cancel",
        "circuit-open",
        "retry-stops-at-open-circuit",
        "half-open-probe-success",
        "half-open-probe-failure",
        "half-open-probe-network",
        "half-open-probe-cancelled",
    };

    [Theory]
    [MemberData(nameof(Scenarios))]
    public async Task Both_paths_react_the_same(string scenario)
    {
        var buffered = await RunAsync(scenario, SendPath.Buffered);
        var stream = await RunAsync(scenario, SendPath.Stream);

        Assert.Equal(buffered.ToString(), stream.ToString());
        Assert.Equal(0, buffered.ActiveRequests);
        Assert.Equal(0, stream.ActiveRequests);
        Expect(scenario, buffered);
    }

    [Fact]
    public async Task The_stream_path_holds_the_slot_until_the_stream_is_closed()
    {
        using var rig = new Rig(readRetries: 0, threshold: 0);
        rig.Fake.OnJson(Endpoint, Afd);

        var (stream, _) = await rig.Transport.PostForStreamAsync(Endpoint, null, CancellationToken.None);
        Assert.Equal(1, rig.ActiveRequests);

        stream.Dispose();
        Assert.Equal(0, rig.ActiveRequests);
        stream.Dispose(); // fechar de novo não libera a vaga duas vezes
        Assert.Equal(0, rig.ActiveRequests);

        Assert.Equal(Afd, await rig.Transport.PostRawAsync(Endpoint, null, CancellationToken.None));
        Assert.Equal(0, rig.ActiveRequests);
    }

    [Fact]
    public async Task A_stream_that_fails_to_open_frees_the_slot_and_the_response()
    {
        using var rig = new Rig(readRetries: 0, threshold: 0);
        var content = new UnreadableContent();
        rig.Device.Faults.Enqueue(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content }));

        await Assert.ThrowsAsync<IOException>(() => rig.Transport.PostForStreamAsync(Endpoint, null, CancellationToken.None));

        Assert.True(content.Disposed);
        Assert.Equal(0, rig.ActiveRequests);
        var span = Assert.Single(rig.Telemetry.Activities, a => a.DisplayName == "get_afd");
        Assert.Equal(typeof(IOException).FullName, span.GetTagItem("error.type"));
    }

    private static void Expect(string scenario, Outcome outcome)
    {
        switch (scenario)
        {
            case "401-then-success":
                Assert.Equal(Afd, outcome.Result);
                Assert.Equal((2, 2), (outcome.Logins, outcome.Commands));
                break;
            case "401-twice":
                AssertDeviceError(outcome, HttpStatusCode.Unauthorized, commands: 2);
                Assert.Equal(2, outcome.Logins);
                break;
            case "503-without-retries":
                AssertDeviceError(outcome, HttpStatusCode.ServiceUnavailable, commands: 1);
                Assert.Equal(ControlIdCircuitState.Closed, outcome.Circuit);
                break;
            case "503-retried-then-success":
                Assert.Equal(Afd, outcome.Result);
                Assert.Equal(3, outcome.Commands);
                break;
            case "503-retries-exhausted":
                AssertDeviceError(outcome, HttpStatusCode.ServiceUnavailable, commands: 3);
                Assert.Equal(ControlIdCircuitState.Open, outcome.Circuit); // as três tentativas contaram
                break;
            case "408-retried":
            case "429-retried":
                Assert.Equal(Afd, outcome.Result);
                Assert.Equal(2, outcome.Commands);
                break;
            case "429-without-retries":
                AssertDeviceError(outcome, (HttpStatusCode)429, commands: 1);
                break;
            case "timeout":
                AssertCommunicationFailure(outcome, nameof(TaskCanceledException), "Timeout ao chamar");
                break;
            case "network":
                AssertCommunicationFailure(outcome, nameof(HttpRequestException), "Falha de comunicação");
                Assert.DoesNotContain("certificado TLS", outcome.Message, StringComparison.Ordinal);
                break;
            case "tls":
                AssertCommunicationFailure(outcome, nameof(HttpRequestException), "certificado TLS do equipamento foi recusado");
                break;
            case "timeout-retried":
            case "network-retried":
                Assert.Equal(Afd, outcome.Result);
                Assert.Equal(ControlIdCircuitState.Closed, outcome.Circuit);
                break;
            case "caller-cancel":
                Assert.IsAssignableFrom<OperationCanceledException>(outcome.Exception);
                Assert.Equal(1, outcome.Commands);
                Assert.Equal(ControlIdCircuitState.Closed, outcome.Circuit); // limite 1: o cancelamento não contou
                break;
            case "circuit-open":
                Assert.IsType<ControlIdCircuitOpenException>(outcome.Exception);
                Assert.Equal((0, 0), (outcome.Logins, outcome.Commands));
                Assert.Equal(BreakDuration.TotalSeconds, outcome.RetryAfter!.Value, 3);
                break;
            case "retry-stops-at-open-circuit":
                Assert.IsType<ControlIdCircuitOpenException>(outcome.Exception);
                Assert.Equal(2, outcome.Commands);
                Assert.Equal(ControlIdCircuitState.Open, outcome.Circuit);
                break;
            case "half-open-probe-success":
                Assert.Equal(Afd, outcome.Result);
                Assert.Equal(ControlIdCircuitState.Closed, outcome.Circuit);
                break;
            case "half-open-probe-failure":
                AssertDeviceError(outcome, HttpStatusCode.ServiceUnavailable, commands: 1);
                Assert.Equal(ControlIdCircuitState.Open, outcome.Circuit);
                break;
            case "half-open-probe-network":
                AssertCommunicationFailure(outcome, nameof(HttpRequestException), "Falha de comunicação");
                break;
            case "half-open-probe-cancelled":
                Assert.IsAssignableFrom<OperationCanceledException>(outcome.Exception);
                Assert.Equal(ControlIdCircuitState.HalfOpen, outcome.Circuit); // a sonda devolveu a vez
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
        }
    }

    private static void AssertDeviceError(Outcome outcome, HttpStatusCode status, int commands)
    {
        var error = Assert.IsAssignableFrom<ControlIdException>(outcome.Exception);
        Assert.Equal(status, error.StatusCode);
        Assert.Equal(commands, outcome.Commands);
    }

    private static void AssertCommunicationFailure(Outcome outcome, string inner, string message)
    {
        var error = Assert.IsAssignableFrom<ControlIdException>(outcome.Exception);
        Assert.Null(error.StatusCode);
        Assert.Equal(inner, error.InnerException?.GetType().Name);
        Assert.Contains(message, error.Message, StringComparison.Ordinal);
        Assert.Equal(1, outcome.Commands);
        Assert.Equal(ControlIdCircuitState.Open, outcome.Circuit); // limite 1: a falha contou
    }

    // Monta o equipamento do cenário, faz a mesma leitura pelo caminho pedido e descreve tudo o que se pode observar.
    private static async Task<Outcome> RunAsync(string scenario, SendPath path)
    {
        var (readRetries, threshold) = scenario switch
        {
            "503-retried-then-success" or "503-retries-exhausted" or "network-retried" => (2, 3),
            "408-retried" or "429-retried" or "timeout-retried" => (1, 3),
            "timeout" or "network" or "tls" or "caller-cancel" => (0, 1),
            "retry-stops-at-open-circuit" => (5, 2),
            "circuit-open" or "half-open-probe-success" or "half-open-probe-failure" or "half-open-probe-network" or "half-open-probe-cancelled" => (0, 2),
            _ => (0, 3),
        };

        using var rig = new Rig(readRetries, threshold);
        await ArrangeAsync(scenario, rig);

        var baseline = rig.Mark();
        string? result = null;
        Exception? exception = null;
        try
        {
            result = path == SendPath.Buffered
                ? await rig.Transport.PostRawAsync(Endpoint, null, rig.Caller.Token)
                : await ReadStreamAsync(rig);
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        return rig.Describe(baseline, result, exception);
    }

    private static async Task<string> ReadStreamAsync(Rig rig)
    {
        var (stream, _) = await rig.Transport.PostForStreamAsync(Endpoint, null, rig.Caller.Token);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    private static async Task ArrangeAsync(string scenario, Rig rig)
    {
        var fake = rig.Fake;
        var faults = rig.Device.Faults;
        switch (scenario)
        {
            case "401-then-success":
                fake.On(Endpoint, HttpStatusCode.Unauthorized).OnJson(Endpoint, Afd);
                break;
            case "401-twice":
                fake.On(Endpoint, HttpStatusCode.Unauthorized, """{"error":"Invalid session"}""");
                break;
            case "503-without-retries" or "503-retries-exhausted" or "retry-stops-at-open-circuit":
                fake.On(Endpoint, HttpStatusCode.ServiceUnavailable, """{"error":"busy","code":3}""");
                break;
            case "503-retried-then-success":
                fake.On(Endpoint, HttpStatusCode.ServiceUnavailable).On(Endpoint, HttpStatusCode.BadGateway).OnJson(Endpoint, Afd);
                break;
            case "408-retried":
                fake.On(Endpoint, HttpStatusCode.RequestTimeout).OnJson(Endpoint, Afd);
                break;
            case "429-retried":
                fake.On(Endpoint, (HttpStatusCode)429).OnJson(Endpoint, Afd);
                break;
            case "429-without-retries":
                fake.On(Endpoint, (HttpStatusCode)429, "too many");
                break;
            case "timeout":
                faults.Enqueue(Timeout);
                break;
            case "timeout-retried":
                faults.Enqueue(Timeout);
                fake.OnJson(Endpoint, Afd);
                break;
            case "network":
                faults.Enqueue(_ => throw new HttpRequestException("connection refused"));
                break;
            case "network-retried":
                faults.Enqueue(_ => throw new HttpRequestException("connection refused"));
                faults.Enqueue(_ => throw new HttpRequestException("connection reset"));
                fake.OnJson(Endpoint, Afd);
                break;
            case "tls":
                faults.Enqueue(_ => throw new HttpRequestException("ssl", new AuthenticationException("remote certificate is invalid")));
                break;
            case "caller-cancel":
                faults.Enqueue(CancelByCaller(rig));
                break;
            case "circuit-open":
                await TripAsync(rig);
                fake.OnJson(Endpoint, Afd);
                break;
            case "half-open-probe-success":
                await TripAsync(rig);
                rig.Clock.Advance(BreakDuration);
                fake.OnJson(Endpoint, Afd);
                break;
            case "half-open-probe-failure":
                await TripAsync(rig);
                rig.Clock.Advance(BreakDuration);
                fake.On(Endpoint, HttpStatusCode.ServiceUnavailable);
                break;
            case "half-open-probe-network":
                await TripAsync(rig);
                rig.Clock.Advance(BreakDuration);
                faults.Enqueue(_ => throw new HttpRequestException("connection refused"));
                break;
            case "half-open-probe-cancelled":
                await TripAsync(rig);
                rig.Clock.Advance(BreakDuration);
                faults.Enqueue(CancelByCaller(rig));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
        }

        static Task<HttpResponseMessage> Timeout(CancellationToken cancellationToken) => throw new TaskCanceledException("timeout");
    }

    // Abre o circuito (limite 2) com outro comando, pelo caminho comum.
    private static async Task TripAsync(Rig rig)
    {
        rig.Fake.On("load_objects.fcgi", HttpStatusCode.ServiceUnavailable);
        for (var i = 0; i < 2; i++)
        {
            await Assert.ThrowsAnyAsync<ControlIdException>(() => rig.Transport.PostRawAsync("load_objects.fcgi", null, CancellationToken.None));
        }

        Assert.Equal(ControlIdCircuitState.Open, rig.Transport.CircuitState);
    }

    // O chamador cancela enquanto a requisição está no equipamento (sem esperar tempo).
    private static Func<CancellationToken, Task<HttpResponseMessage>> CancelByCaller(Rig rig) => cancellationToken =>
    {
        rig.Caller.Cancel();
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException("o token do HttpClient não seguiu o do chamador");
    };

    private sealed class Rig : IDisposable
    {
        public Rig(int readRetries, int threshold)
        {
            var n = Interlocked.Increment(ref _hosts);
            Host = $"10.98.{n / 250}.{(n % 250) + 1}";
            Telemetry = new TelemetryTests.TelemetryCapture(Host);
            Fake = new FakeDeviceHandler().OnJson("login.fcgi", """{"session":"s"}""");
            Device = new CountingDevice(Fake);
            var options = Telemetry.Options();
            options.ReadRetries = readRetries;
            options.RetryDelay = TimeSpan.Zero;
            options.CircuitBreakerFailureThreshold = threshold;
            options.CircuitBreakerBreakDuration = BreakDuration;
            options.CircuitClock = Clock.Read;
            options.LoggerFactory = Logs;
            Transport = new ControlIdTransport(options, new HttpClient(Device));
        }

        public string Host { get; }

        public FakeDeviceHandler Fake { get; }

        public CountingDevice Device { get; }

        public FakeClock Clock { get; } = new();

        public EventLog Logs { get; } = new();

        public TelemetryTests.TelemetryCapture Telemetry { get; }

        public CancellationTokenSource Caller { get; } = new();

        public ControlIdTransport Transport { get; }

        public double ActiveRequests => Telemetry.Current("controlid.client.requests.active");

        public (int Logins, int Commands, int Logs, int Activities, int Measurements) Mark() =>
            (Device.Logins, Device.Commands, Logs.EventIds.Count, Telemetry.Activities.Count, Telemetry.Measurements.Count);

        public Outcome Describe((int Logins, int Commands, int Logs, int Activities, int Measurements) baseline, string? result, Exception? exception)
        {
            var spans = Telemetry.Activities.Skip(baseline.Activities).Select(a =>
                $"{a.DisplayName}[{a.GetTagItem("error.type")}|{a.Status}|{string.Join(",", a.Events.Select(e => e.Name))}]");

            // Contadores somados, histogramas contados; tags do equipamento fora (o host muda a cada execução).
            var metrics = Telemetry.Measurements.Skip(baseline.Measurements)
                .Select(m => (m.Instrument, Tags: string.Join(",", m.Tags.Where(t => !t.Key.StartsWith("server.", StringComparison.Ordinal))
                    .OrderBy(t => t.Key, StringComparer.Ordinal).Select(t => $"{t.Key}={t.Value}")), m.Value))
                .GroupBy(m => $"{m.Instrument}{{{m.Tags}}}")
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => g.Key.Contains(".duration{") ? $"{g.Key}#{g.Count()}" : $"{g.Key}={g.Sum(m => m.Value)}");

            return new Outcome
            {
                Result = result,
                Exception = exception,
                Message = exception?.Message.Replace(Host, "{host}"),
                RetryAfter = (exception as ControlIdCircuitOpenException)?.RetryAfter.TotalSeconds,
                Logins = Device.Logins - baseline.Logins,
                Commands = Device.Commands - baseline.Commands,
                Circuit = Transport.CircuitState,
                ActiveRequests = ActiveRequests,
                Logs = string.Join(",", Logs.EventIds.Skip(baseline.Logs)),
                Spans = string.Join(" ", spans),
                Metrics = string.Join(" ", metrics),
            };
        }

        public void Dispose()
        {
            Transport.Dispose();
            Telemetry.Dispose();
            Caller.Dispose();
        }
    }

    private sealed class Outcome
    {
        public string? Result { get; set; }

        public Exception? Exception { get; set; }

        public string? Message { get; set; }

        public double? RetryAfter { get; set; }

        public int Logins { get; set; }

        public int Commands { get; set; }

        public ControlIdCircuitState Circuit { get; set; }

        public double ActiveRequests { get; set; }

        public string Logs { get; set; } = string.Empty;

        public string Spans { get; set; } = string.Empty;

        public string Metrics { get; set; } = string.Empty;

        public override string ToString()
        {
            var error = Exception is null
                ? "-"
                : $"{Exception.GetType().Name}({(Exception as ControlIdException)?.StatusCode}; {Exception.InnerException?.GetType().Name}; {Message})";
            return $"resultado={Result ?? "-"}\nerro={error}\nretryAfter={RetryAfter}\nlogins={Logins} comandos={Commands}" +
                $"\ncircuito={Circuit} vagasEmUso={ActiveRequests}\nlogs={Logs}\nspans={Spans}\nmétricas={Metrics}";
        }
    }

    /// <summary>Conta logins e comandos; falhas enfileiradas em <see cref="Faults"/> valem para os próximos comandos.</summary>
    private sealed class CountingDevice(FakeDeviceHandler inner) : DelegatingHandler(inner)
    {
        private int _logins;
        private int _commands;

        public Queue<Func<CancellationToken, Task<HttpResponseMessage>>> Faults { get; } = new();

        public int Logins => Volatile.Read(ref _logins);

        public int Commands => Volatile.Read(ref _commands);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("login.fcgi", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _logins);
                return base.SendAsync(request, cancellationToken);
            }

            Interlocked.Increment(ref _commands);
            return Faults.Count > 0 ? Faults.Dequeue()(cancellationToken) : base.SendAsync(request, cancellationToken);
        }
    }

    private sealed class FakeClock
    {
        private long _now = 1_000;

        public long Read() => Interlocked.Read(ref _now);

        public void Advance(TimeSpan time) => Interlocked.Add(ref _now, (long)(time.TotalSeconds * Stopwatch.Frequency));
    }

    private sealed class EventLog : ILoggerFactory, ILogger
    {
        private readonly object _gate = new();
        private readonly List<int> _eventIds = new();

        public IReadOnlyList<int> EventIds
        {
            get
            {
                lock (_gate)
                {
                    return _eventIds.ToList();
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
                _eventIds.Add(eventId.Id);
            }
        }

        public void Dispose()
        {
        }
    }

    // Corpo que não se deixa abrir: a resposta chegou, mas o stream falha (e registra o descarte).
    private sealed class UnreadableContent : HttpContent
    {
        public bool Disposed { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => throw new IOException("corpo ilegível");

        protected override Task<Stream> CreateContentReadStreamAsync() => throw new IOException("corpo ilegível");

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
