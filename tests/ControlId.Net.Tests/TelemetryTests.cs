using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using ControlId.Access;

namespace ControlId.Tests;

public class TelemetryTests
{
    [Fact]
    public async Task Traces_each_command_with_login_as_a_child_span()
    {
        using var telemetry = new TelemetryCapture("10.77.0.1");
        var device = new FakeDeviceHandler()
            .OnJson("login.fcgi", """{"session":"s3cr3t"}""")
            .OnJson("load_objects.fcgi", """{"users":[]}""");
        using var client = new AccessClient(telemetry.Options(), device.CreateClient());

        await client.GetUsersAsync();

        var load = Assert.Single(telemetry.Activities, a => a.DisplayName == "load_objects");
        var login = Assert.Single(telemetry.Activities, a => a.DisplayName == "login");
        Assert.Equal(ActivityKind.Client, load.Kind);
        Assert.Equal(load.SpanId, login.ParentSpanId);
        Assert.Equal("load_objects", load.GetTagItem("controlid.command"));
        Assert.Equal("10.77.0.1", load.GetTagItem("server.address"));
        Assert.Equal(80, load.GetTagItem("server.port"));
        Assert.Equal(ActivityStatusCode.Unset, load.Status);

        // A sessão e a senha nunca vão para a telemetria.
        Assert.DoesNotContain(telemetry.Activities.SelectMany(a => a.TagObjects), t => t.Value?.ToString()?.Contains("s3cr3t") == true);
        Assert.DoesNotContain(telemetry.Activities.SelectMany(a => a.TagObjects), t => t.Value?.ToString()?.Contains("secret") == true);
    }

    [Fact]
    public async Task Records_the_relogin_when_the_session_expires()
    {
        using var telemetry = new TelemetryCapture("10.77.0.2");
        var device = new FakeDeviceHandler()
            .OnJson("login.fcgi", """{"session":"old"}""")
            .OnJson("login.fcgi", """{"session":"new"}""")
            .On("load_objects.fcgi", HttpStatusCode.Unauthorized)
            .OnJson("load_objects.fcgi", """{"users":[]}""");
        using var client = new AccessClient(telemetry.Options(), device.CreateClient());

        await client.GetUsersAsync();

        var load = Assert.Single(telemetry.Activities, a => a.DisplayName == "load_objects");
        Assert.Contains(load.Events, e => e.Name == "controlid.relogin");
        Assert.Equal(2, telemetry.Activities.Count(a => a.DisplayName == "login"));
        Assert.Equal(1, telemetry.Sum("controlid.client.relogins"));
    }

    [Fact]
    public async Task Marks_the_span_and_the_duration_as_failed_on_a_device_error()
    {
        using var telemetry = new TelemetryCapture("10.77.0.3");
        var device = new FakeDeviceHandler()
            .OnJson("login.fcgi", """{"session":"s"}""")
            .On("execute_actions.fcgi", HttpStatusCode.BadRequest, """{"error":"Invalid door","code":7}""");
        using var client = new AccessClient(telemetry.Options(), device.CreateClient());

        await Assert.ThrowsAsync<ControlIdException>(() => client.OpenDoorAsync(9));

        var span = Assert.Single(telemetry.Activities, a => a.DisplayName == "execute_actions");
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal("400", span.GetTagItem("error.type"));
        Assert.Equal(400, span.GetTagItem("http.response.status_code"));
        Assert.Equal(7, span.GetTagItem("controlid.device.error_code"));

        var duration = Assert.Single(telemetry.Measurements, m => m.Instrument == "controlid.client.operation.duration" && Equals(m.Tags["controlid.command"], "execute_actions"));
        Assert.Equal("400", duration.Tags["error.type"]);
        Assert.True(duration.Value >= 0);
    }

    [Fact]
    public async Task Counts_requests_waiting_for_the_per_device_limit()
    {
        using var telemetry = new TelemetryCapture("10.77.0.4");
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http = new HttpClient(new GatedHandler(gate.Task));
        using var client = new AccessClient(telemetry.Options(maxConcurrentRequests: 1), http);

        // Uma leitura ocupa a única vaga (presa no equipamento); as outras duas esperam na fila.
        var calls = Enumerable.Range(0, 3).Select(_ => client.GetUsersAsync()).ToList();
        await WaitUntilAsync(() => telemetry.Current("controlid.client.requests.queued") == 2);
        Assert.Equal(1, telemetry.Current("controlid.client.requests.active"));

        gate.SetResult(true);
        await Task.WhenAll(calls);

        Assert.Equal(0, telemetry.Current("controlid.client.requests.queued"));
        Assert.Equal(0, telemetry.Current("controlid.client.requests.active"));
        Assert.Equal(2, telemetry.Max("controlid.client.requests.queued"));
    }

    [Fact]
    public async Task A_canceled_call_does_not_take_a_slot_nor_reach_the_device()
    {
        using var telemetry = new TelemetryCapture("10.77.0.5");
        var device = new FakeDeviceHandler().OnJson("login.fcgi", """{"session":"s"}""").OnJson("load_objects.fcgi", """{"users":[]}""");
        using var client = new AccessClient(telemetry.Options(), device.CreateClient());
        await client.GetUsersAsync();

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetUsersAsync(cts.Token));

        Assert.Single(device.RequestsTo("load_objects.fcgi"));
        Assert.Equal(0, telemetry.Current("controlid.client.requests.active"));
        var canceled = Assert.Single(telemetry.Activities, a => Equals(a.GetTagItem("error.type"), "canceled"));
        Assert.Equal("load_objects", canceled.DisplayName);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    private sealed class GatedHandler(Task gate) : HttpMessageHandler
    {
        // O login responde na hora; as leituras ficam presas até o portão abrir.
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("login.fcgi", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"session":"s"}""") };
            }

            await gate;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"users":[]}""") };
        }
    }

    /// <summary>
    /// Escuta os spans e as métricas do ControlId.Net de um único equipamento (pelo host), porque os listeners são globais e
    /// outros testes rodam em paralelo.
    /// </summary>
    private sealed class TelemetryCapture : IDisposable
    {
        private readonly string _host;
        private readonly ActivityListener _activities;
        private readonly MeterListener _meters = new();
        private readonly ConcurrentQueue<Activity> _captured = new();
        private readonly ConcurrentQueue<Measurement> _measurements = new();

        public TelemetryCapture(string host)
        {
            _host = host;
            _activities = new ActivityListener
            {
                ShouldListenTo = s => s.Name == ControlIdTelemetry.ActivitySourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = a =>
                {
                    if (Equals(a.GetTagItem("server.address"), _host))
                    {
                        _captured.Enqueue(a);
                    }
                },
            };
            ActivitySource.AddActivityListener(_activities);

            _meters.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == ControlIdTelemetry.MeterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _meters.SetMeasurementEventCallback<double>((i, v, tags, _) => Record(i, v, tags));
            _meters.SetMeasurementEventCallback<long>((i, v, tags, _) => Record(i, v, tags));
            _meters.Start();
        }

        public IReadOnlyList<Activity> Activities => _captured.ToList();

        public IReadOnlyList<Measurement> Measurements => _measurements.ToList();

        public ControlIdDeviceOptions Options(int maxConcurrentRequests = 4) => new()
        {
            BaseAddress = new Uri("http://" + _host),
            Login = "admin",
            Password = "secret",
            MaxConcurrentRequests = maxConcurrentRequests,
        };

        public double Sum(string instrument) => Measurements.Where(m => m.Instrument == instrument).Sum(m => m.Value);

        /// <summary>Valor atual de um UpDownCounter: a soma dos incrementos.</summary>
        public double Current(string instrument) => Sum(instrument);

        public double Max(string instrument)
        {
            double current = 0, max = 0;
            foreach (var m in Measurements.Where(m => m.Instrument == instrument))
            {
                current += m.Value;
                max = Math.Max(max, current);
            }

            return max;
        }

        private void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var dictionary = new Dictionary<string, object?>();
            foreach (var tag in tags)
            {
                dictionary[tag.Key] = tag.Value;
            }

            if (dictionary.TryGetValue("server.address", out var address) && Equals(address, _host))
            {
                _measurements.Enqueue(new Measurement(instrument.Name, value, dictionary));
            }
        }

        public void Dispose()
        {
            _activities.Dispose();
            _meters.Dispose();
        }
    }

    private sealed class Measurement(string instrument, double value, Dictionary<string, object?> tags)
    {
        public string Instrument { get; } = instrument;

        public double Value { get; } = value;

        public Dictionary<string, object?> Tags { get; } = tags;
    }
}
