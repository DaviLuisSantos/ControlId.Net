using System.Net;
using ControlId.Access;
using ControlId.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Http;

namespace ControlId.Tests;

public class DeviceHealthCheckTests
{
    private const string Info = """{"serial":"SN-1","version":"6.22.3","device_id":"42","online":true}""";

    private static FakeDeviceHandler Healthy() => new FakeDeviceHandler()
        .OnJson("login.fcgi", """{"session":"s"}""")
        .OnJson("system_information.fcgi", Info)
        .OnJson("get_about.fcgi", """{"nSerie":"REP-9","versionFW":260}""");

    private sealed class DelayHandler : DelegatingHandler
    {
        private readonly TimeSpan _delay;

        public DelayHandler(HttpMessageHandler inner, TimeSpan delay)
            : base(inner) => _delay = delay;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(_delay, cancellationToken);
            return await base.SendAsync(request, cancellationToken);
        }
    }

    private static ServiceProvider Build(
        HttpMessageHandler handler,
        Action<IServiceCollection> register,
        Action<IHealthChecksBuilder>? checks = null,
        DeviceContactTracker? tracker = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        if (tracker is not null)
        {
            services.AddSingleton(tracker);
        }

        // O health check é registrado antes dos equipamentos: a ordem não importa.
        var builder = services.AddHealthChecks();
        (checks ?? (b => b.AddControlIdDeviceChecks()))(builder);
        register(services);
        services.ConfigureAll<HttpClientFactoryOptions>(o => o.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = handler));
        return services.BuildServiceProvider();
    }

    private static Task<HealthReport> RunAsync(ServiceProvider provider) =>
        provider.GetRequiredService<HealthCheckService>().CheckHealthAsync();

    private static void Access(IServiceCollection s, string name = "portaria", Action<ControlIdDeviceOptions>? more = null) =>
        s.AddControlIdAccess(name, o =>
        {
            o.BaseAddress = new Uri("http://10.0.0.5");
            o.Password = "secret";
            more?.Invoke(o);
        });

    [Fact]
    public async Task Registers_one_check_per_device_with_the_type_in_the_name()
    {
        await using var provider = Build(Healthy(), s =>
        {
            Access(s, "portaria");
            Access(s, "garagem");
            s.AddControlIdAccess(o =>
            {
                o.BaseAddress = new Uri("http://10.0.0.6");
                o.Password = "secret";
            });
            s.AddControlIdRep("rh", o =>
            {
                o.BaseAddress = new Uri("http://10.0.0.7");
                o.Password = "secret";
            });
        });

        var report = await RunAsync(provider);

        Assert.Equal(
            new[] { "controlid:access:default", "controlid:access:garagem", "controlid:access:portaria", "controlid:rep:rh" },
            report.Entries.Keys.OrderBy(k => k, StringComparer.Ordinal));
    }

    [Fact]
    public async Task Reports_the_full_data_for_an_access_device_with_contact()
    {
        var tracker = new DeviceContactTracker();
        tracker.Record(42, DeviceContactSource.Push);
        await using var provider = Build(Healthy(), s => Access(s), tracker: tracker);

        var entry = (await RunAsync(provider)).Entries["controlid:access:portaria"];

        Assert.Equal(HealthStatus.Healthy, entry.Status);
        Assert.Equal("SN-1", entry.Data["serial"]);
        Assert.Equal("6.22.3", entry.Data["firmware"]);
        Assert.Equal("42", entry.Data["deviceId"]);
        Assert.Equal("Closed", entry.Data["circuit"]);
        Assert.IsType<long>(entry.Data["latencyMs"]);
        Assert.Equal("Push", entry.Data["lastContactSource"]);
        Assert.True(DateTimeOffset.TryParse((string)entry.Data["lastContact"], out _));
    }

    [Fact]
    public async Task Reports_serial_and_firmware_for_a_rep_without_last_contact()
    {
        await using var provider = Build(Healthy(), s => s.AddControlIdRep("rh", o =>
        {
            o.BaseAddress = new Uri("http://10.0.0.7");
            o.Password = "secret";
        }));

        var entry = (await RunAsync(provider)).Entries["controlid:rep:rh"];

        Assert.Equal(HealthStatus.Healthy, entry.Status);
        Assert.Equal("REP-9", entry.Data["serial"]);
        Assert.Equal("260", entry.Data["firmware"]);
        Assert.False(entry.Data.ContainsKey("lastContact"));
    }

    [Fact]
    public async Task Is_degraded_when_the_device_is_slow()
    {
        await using var provider = Build(
            new DelayHandler(Healthy(), TimeSpan.FromMilliseconds(80)),
            s => Access(s),
            b => b.AddControlIdDeviceChecks(o => o.SlowThreshold = TimeSpan.FromMilliseconds(10)));

        Assert.Equal(HealthStatus.Degraded, (await RunAsync(provider)).Entries["controlid:access:portaria"].Status);
    }

    [Fact]
    public async Task Uses_the_failure_status_when_the_probe_fails()
    {
        var device = new FakeDeviceHandler()
            .OnJson("login.fcgi", """{"session":"s"}""")
            .On("system_information.fcgi", HttpStatusCode.InternalServerError);
        await using var provider = Build(device, s => Access(s), b => b.AddControlIdDeviceChecks(failureStatus: HealthStatus.Degraded));

        var entry = (await RunAsync(provider)).Entries["controlid:access:portaria"];

        Assert.Equal(HealthStatus.Degraded, entry.Status);
        Assert.NotNull(entry.Exception);
        Assert.True(entry.Data.ContainsKey("latencyMs"));
    }

    [Fact]
    public async Task Does_not_send_a_request_while_the_circuit_is_open()
    {
        var device = new FakeDeviceHandler()
            .OnJson("login.fcgi", """{"session":"s"}""")
            .On("system_information.fcgi", HttpStatusCode.InternalServerError);
        await using var provider = Build(device, s => Access(s, more: o =>
        {
            o.CircuitBreakerFailureThreshold = 1;
            o.CircuitBreakerBreakDuration = TimeSpan.FromMinutes(5);
        }));

        var first = (await RunAsync(provider)).Entries["controlid:access:portaria"];
        Assert.Equal(HealthStatus.Unhealthy, first.Status);
        var requests = device.Requests.Count;

        var second = (await RunAsync(provider)).Entries["controlid:access:portaria"];

        Assert.Equal(HealthStatus.Unhealthy, second.Status);
        Assert.Equal("Open", second.Data["circuit"]);
        Assert.Equal(requests, device.Requests.Count);
    }

    [Fact]
    public async Task Is_degraded_when_the_last_contact_is_older_than_the_limit()
    {
        var tracker = new DeviceContactTracker();
        tracker.Record(42, DeviceContactSource.Monitor);
        await Task.Delay(50);
        await using var provider = Build(
            Healthy(),
            s => Access(s),
            b => b.AddControlIdDeviceChecks(o => o.MaxContactAge = TimeSpan.FromMilliseconds(10)),
            tracker);

        var entry = (await RunAsync(provider)).Entries["controlid:access:portaria"];

        Assert.Equal(HealthStatus.Degraded, entry.Status);
        Assert.Equal("Monitor", entry.Data["lastContactSource"]);
    }

    [Fact]
    public async Task Tolerates_a_missing_tracker()
    {
        await using var provider = Build(
            Healthy(),
            s => Access(s),
            b => b.AddControlIdDeviceChecks(o => o.MaxContactAge = TimeSpan.FromMilliseconds(1)));

        var entry = (await RunAsync(provider)).Entries["controlid:access:portaria"];

        Assert.Equal(HealthStatus.Healthy, entry.Status);
        Assert.False(entry.Data.ContainsKey("lastContact"));
    }

    [Fact]
    public async Task Leaves_the_aggregate_check_unchanged_and_applies_prefix_and_tags()
    {
        await using var provider = Build(
            Healthy(),
            s => Access(s),
            b => b.AddControlIdDevices().AddControlIdDeviceChecks(namePrefix: "dev", tags: new[] { "ready" }));

        var report = await RunAsync(provider);

        Assert.Equal(new[] { "controlid", "dev:access:portaria" }, report.Entries.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal("1 de 1 equipamentos respondendo.", report.Entries["controlid"].Description);
        Assert.Contains("ready", report.Entries["dev:access:portaria"].Tags);
    }
}
