using System.Collections.Concurrent;
using System.Net;
using System.Text;
using ControlId.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Http;

namespace ControlId.Tests;

/// <summary>RequireDevices, CacheDuration, TotalTimeout e validação das opções dos health checks.</summary>
public class HealthCheckOptionsTests
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    /// <summary>Equipamento simulado seguro para chamadas em paralelo; conta as requisições por host e endpoint.</summary>
    private sealed class CountingDevice : HttpMessageHandler
    {
        private readonly ConcurrentDictionary<string, int> _counts = new(StringComparer.Ordinal);

        public HashSet<string> HangingHosts { get; } = new(StringComparer.Ordinal);

        public HttpStatusCode ProbeStatus { get; set; } = HttpStatusCode.OK;

        public int Count(string endpoint) => _counts.Where(c => c.Key.EndsWith("/" + endpoint, StringComparison.Ordinal)).Sum(c => c.Value);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            _counts.AddOrUpdate(uri.Host + uri.AbsolutePath, 1, (_, n) => n + 1);
            if (uri.AbsolutePath.EndsWith("/login.fcgi", StringComparison.Ordinal))
            {
                return Json("""{"session":"s"}""");
            }

            if (HangingHosts.Contains(uri.Host))
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            if (ProbeStatus != HttpStatusCode.OK)
            {
                return new HttpResponseMessage(ProbeStatus);
            }

            return uri.AbsolutePath.EndsWith("/get_about.fcgi", StringComparison.Ordinal)
                ? Json("""{"nSerie":"REP-9","versionFW":260}""")
                : Json("""{"serial":"SN-1","version":"6.22.3","device_id":"42","online":true}""");
        }

        private static HttpResponseMessage Json(string body) =>
            new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }

    private sealed class FakeClock
    {
        public TimeSpan Now { get; set; } = TimeSpan.FromHours(1);
    }

    private static ServiceProvider Build(
        HttpMessageHandler handler,
        Action<IServiceCollection> register,
        Action<IHealthChecksBuilder> checks,
        FakeClock? clock = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        if (clock is not null)
        {
            // Registrado antes dos checks: o TryAdd deles mantém este.
            services.AddSingleton(new ControlIdProbeCache(() => clock.Now));
        }

        checks(services.AddHealthChecks());
        register(services);
        services.ConfigureAll<HttpClientFactoryOptions>(o => o.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = handler));
        return services.BuildServiceProvider();
    }

    private static Task<HealthReport> RunAsync(ServiceProvider provider) =>
        provider.GetRequiredService<HealthCheckService>().CheckHealthAsync();

    private static void Access(IServiceCollection services, string name, string host) =>
        services.AddControlIdAccess(name, o => o.BaseAddress = new Uri("http://" + host));

    [Fact]
    public void Defaults()
    {
        var options = new ControlIdHealthCheckOptions();

        Assert.True(options.RequireDevices);
        Assert.Equal(TimeSpan.Zero, options.CacheDuration);
        Assert.Equal(TimeSpan.FromSeconds(30), options.TotalTimeout);
    }

    public static TheoryData<string> InvalidOptions() => new() { "SlowThreshold", "CacheDuration", "TotalTimeout", "TotalTimeoutZero" };

    [Theory]
    [MemberData(nameof(InvalidOptions))]
    public void Rejects_out_of_range_options_in_both_registrations(string option)
    {
        void Configure(ControlIdHealthCheckOptions o)
        {
            switch (option)
            {
                case "SlowThreshold": o.SlowThreshold = TimeSpan.FromMilliseconds(-1); break;
                case "CacheDuration": o.CacheDuration = TimeSpan.FromSeconds(-1); break;
                case "TotalTimeout": o.TotalTimeout = TimeSpan.FromSeconds(-1); break;
                default: o.TotalTimeout = TimeSpan.Zero; break;
            }
        }

        var services = new ServiceCollection();
        Assert.Throws<ArgumentOutOfRangeException>(() => services.AddHealthChecks().AddControlIdDevices(Configure));
        Assert.Throws<ArgumentOutOfRangeException>(() => services.AddHealthChecks().AddControlIdDeviceChecks(Configure));
    }

    [Fact]
    public void Accepts_a_zero_slow_threshold_and_cache()
    {
        var services = new ServiceCollection();
        services.AddHealthChecks().AddControlIdDevices(o =>
        {
            o.SlowThreshold = TimeSpan.Zero;
            o.CacheDuration = TimeSpan.Zero;
        });
    }

    [Fact]
    public async Task Aggregate_without_devices_uses_the_failure_status()
    {
        await using var provider = Build(new CountingDevice(), _ => { }, b => b.AddControlIdDevices());

        var entry = (await RunAsync(provider)).Entries["controlid"];

        Assert.Equal(HealthStatus.Unhealthy, entry.Status);
        Assert.Contains("Nenhum equipamento Control iD registrado", entry.Description);
        Assert.Contains("RequireDevices", entry.Description);
    }

    [Fact]
    public async Task Aggregate_without_devices_honors_a_custom_failure_status_and_the_opt_out()
    {
        await using var degraded = Build(new CountingDevice(), _ => { }, b => b.AddControlIdDevices(failureStatus: HealthStatus.Degraded));
        await using var optOut = Build(new CountingDevice(), _ => { }, b => b.AddControlIdDevices(o => o.RequireDevices = false));

        Assert.Equal(HealthStatus.Degraded, (await RunAsync(degraded)).Entries["controlid"].Status);
        Assert.Equal(HealthStatus.Healthy, (await RunAsync(optOut)).Entries["controlid"].Status);
    }

    [Fact]
    public async Task Device_checks_without_devices_register_a_failing_none_check()
    {
        await using var required = Build(new CountingDevice(), _ => { }, b => b.AddControlIdDeviceChecks(namePrefix: "dev", tags: new[] { "ready" }));
        await using var optOut = Build(new CountingDevice(), _ => { }, b => b.AddControlIdDeviceChecks(o => o.RequireDevices = false));

        var report = await RunAsync(required);
        var entry = Assert.Single(report.Entries);
        Assert.Equal("dev:none", entry.Key);
        Assert.Equal(HealthStatus.Unhealthy, entry.Value.Status);
        Assert.Contains("ready", entry.Value.Tags);
        Assert.Empty((await RunAsync(optOut)).Entries);
    }

    [Fact]
    public async Task Without_cache_each_check_probes_the_device()
    {
        var device = new CountingDevice();
        await using var provider = Build(device, s => Access(s, "portaria", "10.0.0.1"), b => b.AddControlIdDevices().AddControlIdDeviceChecks());

        await RunAsync(provider);

        Assert.Equal(2, device.Count("system_information.fcgi"));
    }

    [Fact]
    public async Task Cache_shares_one_probe_per_device_between_the_aggregate_and_the_device_checks()
    {
        var device = new CountingDevice();
        var clock = new FakeClock();
        await using var provider = Build(
            device,
            s =>
            {
                Access(s, "portaria", "10.0.0.1");
                Access(s, "garagem", "10.0.0.2");
                s.AddControlIdRep("rh", o => o.BaseAddress = new Uri("http://10.0.0.3"));
            },
            b => b.AddControlIdDevices(o => o.CacheDuration = Window).AddControlIdDeviceChecks(o => o.CacheDuration = Window),
            clock);

        var first = await RunAsync(provider);

        Assert.Equal(2, device.Count("system_information.fcgi"));
        Assert.Equal(1, device.Count("get_about.fcgi"));
        Assert.Equal(HealthStatus.Healthy, first.Status);
        Assert.Equal("3 de 3 equipamentos respondendo.", first.Entries["controlid"].Description);
        Assert.Equal("SN-1", first.Entries["controlid:access:portaria"].Data["serial"]);
        Assert.Equal("REP-9", first.Entries["controlid:rep:rh"].Data["serial"]);

        // Dentro da janela: nenhuma requisição nova; os dois checks contam como reaproveitados.
        clock.Now += Window - TimeSpan.FromSeconds(1);
        var second = await RunAsync(provider);

        Assert.Equal(2, device.Count("system_information.fcgi"));
        Assert.Equal(1, device.Count("get_about.fcgi"));
        Assert.EndsWith("(cache)", (string)second.Entries["controlid"].Data["access:portaria"], StringComparison.Ordinal);
        Assert.Equal(true, second.Entries["controlid:access:portaria"].Data["cached"]);
        Assert.Equal("SN-1", second.Entries["controlid:access:portaria"].Data["serial"]);

        // Janela vencida: uma nova sonda por equipamento, ainda compartilhada entre os checks.
        clock.Now += TimeSpan.FromSeconds(2);
        await RunAsync(provider);

        Assert.Equal(4, device.Count("system_information.fcgi"));
        Assert.Equal(2, device.Count("get_about.fcgi"));
    }

    [Fact]
    public async Task Cache_keeps_failures_for_the_window()
    {
        var device = new CountingDevice { ProbeStatus = HttpStatusCode.InternalServerError };
        var clock = new FakeClock();
        await using var provider = Build(
            device,
            s => Access(s, "portaria", "10.0.0.1"),
            b => b.AddControlIdDevices(o => o.CacheDuration = Window).AddControlIdDeviceChecks(o => o.CacheDuration = Window),
            clock);

        var first = await RunAsync(provider);
        var second = await RunAsync(provider);

        Assert.Equal(HealthStatus.Unhealthy, first.Entries["controlid"].Status);
        Assert.Equal(HealthStatus.Unhealthy, first.Entries["controlid:access:portaria"].Status);
        Assert.Equal(HealthStatus.Unhealthy, second.Entries["controlid:access:portaria"].Status);
        Assert.Equal(1, device.Count("system_information.fcgi"));
    }

    [Fact]
    public async Task Total_timeout_cuts_the_aggregate_and_keeps_what_answered()
    {
        var device = new CountingDevice();
        device.HangingHosts.Add("10.0.0.2");
        await using var provider = Build(
            device,
            s =>
            {
                Access(s, "portaria", "10.0.0.1");
                Access(s, "travado", "10.0.0.2");
            },
            b => b.AddControlIdDevices(o =>
            {
                o.Timeout = TimeSpan.FromMinutes(5);
                o.TotalTimeout = TimeSpan.FromMilliseconds(300);
            }));

        var entry = (await RunAsync(provider)).Entries["controlid"];

        Assert.Equal(HealthStatus.Degraded, entry.Status);
        Assert.Equal("1 de 2 equipamentos respondendo.", entry.Description);
        Assert.StartsWith("ok: ", (string)entry.Data["access:portaria"], StringComparison.Ordinal);
        Assert.Contains("Prazo total de 0.3 s", (string)entry.Data["access:travado"], StringComparison.Ordinal);
        Assert.True(entry.Duration < TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Total_timeout_also_cuts_devices_waiting_for_a_parallelism_slot()
    {
        var device = new CountingDevice();
        device.HangingHosts.Add("10.0.0.1");
        await using var provider = Build(
            device,
            s =>
            {
                Access(s, "a", "10.0.0.1");
                Access(s, "b", "10.0.0.1");
            },
            b => b.AddControlIdDevices(o =>
            {
                o.MaxParallelism = 1;
                o.Timeout = TimeSpan.FromMinutes(5);
                o.TotalTimeout = TimeSpan.FromMilliseconds(200);
            }));

        var entry = (await RunAsync(provider)).Entries["controlid"];

        Assert.Equal(HealthStatus.Unhealthy, entry.Status);
        Assert.Contains("Prazo total", (string)entry.Data["access:a"], StringComparison.Ordinal);
        Assert.Contains("Prazo total", (string)entry.Data["access:b"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Slow_threshold_is_compared_as_a_time_span_below_one_millisecond()
    {
        // Antes, o check por equipamento truncava para milissegundos: 1 tick virava 0 ms e uma resposta em menos de 1 ms passava.
        await using var provider = Build(
            new CountingDevice(),
            s => Access(s, "portaria", "10.0.0.1"),
            b => b.AddControlIdDevices(o => o.SlowThreshold = TimeSpan.FromTicks(1))
                .AddControlIdDeviceChecks(o => o.SlowThreshold = TimeSpan.FromTicks(1)));

        var report = await RunAsync(provider);

        Assert.Equal(HealthStatus.Degraded, report.Entries["controlid"].Status);
        Assert.Equal(HealthStatus.Degraded, report.Entries["controlid:access:portaria"].Status);
    }

    [Fact]
    public async Task Probe_cache_shares_an_in_flight_probe_and_survives_a_caller_giving_up()
    {
        var clock = new FakeClock();
        var cache = new ControlIdProbeCache(() => clock.Now);
        var gate = new TaskCompletionSource<ControlIdProbeResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        Task<ControlIdProbeResult> Probe(CancellationToken _)
        {
            Interlocked.Increment(ref calls);
            return gate.Task;
        }

        using var giveUp = new CancellationTokenSource();
        var first = cache.GetAsync("access:x", Window, Probe, giveUp.Token);
        var second = cache.GetAsync("access:x", Window, Probe, CancellationToken.None);
        giveUp.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        gate.SetResult(new ControlIdProbeResult(TimeSpan.FromMilliseconds(5), null));
        var (result, shared) = await second;

        Assert.True(result.Succeeded);
        Assert.True(shared);
        Assert.Equal(1, calls);

        // Outra chave e cache desligado sondam de novo.
        await cache.GetAsync("rep:x", Window, Probe, CancellationToken.None);
        await cache.GetAsync("access:x", TimeSpan.Zero, Probe, CancellationToken.None);
        Assert.Equal(3, calls);

        clock.Now += Window;
        var (_, sharedAfterExpiry) = await cache.GetAsync("access:x", Window, Probe, CancellationToken.None);
        Assert.False(sharedAfterExpiry);
        Assert.Equal(4, calls);
    }
}
