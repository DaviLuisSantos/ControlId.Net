using ControlId.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Hosting;

namespace ControlId.Tests;

public class ConfigurationGuardTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    private static FakeDeviceHandler Device() => new FakeDeviceHandler()
        .OnJson("login.fcgi", """{"session":"s"}""")
        .OnJson("set_configuration.fcgi", "{}")
        .OnJson("set_system_time.fcgi", "{}");

    private static string Info(long time) => $$"""{"serial":"SN-1","version":"6.22.3","device_id":"1","time":{{time}},"online":true}""";

    private static long NowEpoch(int offsetSeconds = 0) => DateTimeOffset.UtcNow.ToUnixTimeSeconds() + offsetSeconds;

    // Um equipamento que não responde: o cliente recebe a falha de conexão.
    private sealed class OfflineHostHandler : HttpMessageHandler
    {
        private readonly string _offlineHost;
        private readonly HttpMessageHandler _inner;

        public OfflineHostHandler(string offlineHost, HttpMessageHandler inner)
        {
            _offlineHost = offlineHost;
            _inner = inner;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.Host == _offlineHost)
            {
                throw new HttpRequestException("Equipamento fora do ar.");
            }

            return new HttpMessageInvoker(_inner).SendAsync(request, cancellationToken);
        }
    }

    private static ServiceProvider Build(
        HttpMessageHandler handler,
        Action<ControlIdConfigurationGuardOptions> guard,
        params string[] hosts)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHealthChecks().AddControlIdConfigurationGuard();
        foreach (var host in hosts)
        {
            services.AddControlIdAccess(host, o =>
            {
                o.BaseAddress = new Uri("http://" + host);
                o.Password = "secret";
            });
        }

        services.AddControlIdConfigurationGuard(o =>
        {
            o.Interval = TimeSpan.FromMilliseconds(30);
            o.TimeZone = TimeZoneInfo.Utc;
            guard(o);
        });
        services.ConfigureAll<HttpClientFactoryOptions>(o => o.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = handler));
        return services.BuildServiceProvider();
    }

    private static IHostedService Guard(ServiceProvider provider) =>
        provider.GetServices<IHostedService>().Single(s => s.GetType().Name == "ControlIdConfigurationGuardService");

    private static Task<HealthReport> Report(ServiceProvider provider) =>
        provider.GetRequiredService<HealthCheckService>().CheckHealthAsync();

    // Espera o guardião fechar pelo menos "cycles" ciclos; o health check é a única janela pública para o estado dele.
    private static async Task<HealthReportEntry> WaitForCyclesAsync(ServiceProvider provider, long cycles)
    {
        var deadline = DateTime.UtcNow + Wait;
        while (true)
        {
            var entry = (await Report(provider)).Entries["controlid-guard"];
            if ((long)entry.Data["cycles"] >= cycles)
            {
                return entry;
            }

            Assert.True(DateTime.UtcNow < deadline, "O guardião não concluiu os ciclos a tempo.");
            await Task.Delay(20);
        }
    }

    private static DesiredDeviceConfiguration Online(bool online) => new() { Online = online };

    [Fact]
    public async Task Restores_general_online_changed_outside_on_the_next_cycle()
    {
        var device = Device()
            .OnJson("get_configuration.fcgi", """{"general":{"online":"1"}}""")
            .OnJson("get_configuration.fcgi", """{"general":{"online":"0"}}""");
        await using var provider = Build(device, o =>
        {
            o.ClockTolerance = null;
            o.Devices["10.0.0.5"] = Online(true);
        }, "10.0.0.5");
        var guard = Guard(provider);

        await guard.StartAsync(default);
        try
        {
            await WaitForCyclesAsync(provider, 3);
        }
        finally
        {
            await guard.StopAsync(default);
        }

        var requests = device.Requests.ToList();
        var firstSet = requests.FindIndex(r => r.Uri.AbsolutePath.EndsWith("/set_configuration.fcgi", StringComparison.Ordinal));
        Assert.True(firstSet >= 0, "Nenhuma correção foi enviada.");
        Assert.True(
            requests.Take(firstSet).Count(r => r.Uri.AbsolutePath.EndsWith("/get_configuration.fcgi", StringComparison.Ordinal)) >= 2,
            "O primeiro ciclo, com a configuração igual, não deveria gravar nada.");
        Assert.Contains("\"general\":{\"online\":\"1\"}", requests[firstSet].Body);
    }

    [Fact]
    public async Task Does_not_write_when_the_configuration_already_matches()
    {
        var device = Device().OnJson("get_configuration.fcgi", """{"general":{"online":"1"}}""");
        await using var provider = Build(device, o =>
        {
            o.ClockTolerance = null;
            o.Devices["10.0.0.5"] = Online(true);
        }, "10.0.0.5");
        var guard = Guard(provider);

        await guard.StartAsync(default);
        HealthReportEntry entry;
        try
        {
            entry = await WaitForCyclesAsync(provider, 3);
        }
        finally
        {
            await guard.StopAsync(default);
        }

        Assert.Equal(HealthStatus.Healthy, entry.Status);
        Assert.Equal(0L, entry.Data["corrections"]);
        Assert.Empty(device.RequestsTo("set_configuration.fcgi"));
    }

    [Fact]
    public async Task Writes_only_the_fields_that_differ_with_typed_and_extra_settings()
    {
        var device = Device().OnJson(
            "get_configuration.fcgi",
            """{"general":{"online":"1","antipassback":"0"},"online_client":{"server_id":"3"},"monitor":{"hostname":"antigo","port":"5000","path":"api","request_timeout":"1000"}}""");
        await using var provider = Build(device, o =>
        {
            o.ClockTolerance = null;
            var desired = new DesiredDeviceConfiguration
            {
                Online = true,
                OnlineServerId = 7,
                Monitor = new MonitorTarget { Hostname = "srv", Port = 5000, Path = "/api/", RequestTimeout = TimeSpan.FromSeconds(5) },
            };
            desired.Settings["general"] = new Dictionary<string, string> { ["antipassback"] = "1" };
            o.Devices["10.0.0.5"] = desired;
        }, "10.0.0.5");
        var guard = Guard(provider);

        await guard.StartAsync(default);
        try
        {
            await WaitForCyclesAsync(provider, 1);
        }
        finally
        {
            await guard.StopAsync(default);
        }

        var body = device.RequestsTo("set_configuration.fcgi").First().Body;
        Assert.Contains("\"antipassback\":\"1\"", body);
        Assert.Contains("\"server_id\":\"7\"", body);
        Assert.Contains("\"hostname\":\"srv\"", body);
        Assert.Contains("\"request_timeout\":\"5000\"", body);
        Assert.DoesNotContain("\"online\"", body);
        Assert.DoesNotContain("\"port\"", body);
        Assert.DoesNotContain("\"path\"", body);
    }

    [Fact]
    public async Task A_device_that_is_down_does_not_stall_the_others()
    {
        var device = Device().OnJson("get_configuration.fcgi", """{"general":{"online":"0"}}""");
        await using var provider = Build(new OfflineHostHandler("10.0.0.2", device), o =>
        {
            o.ClockTolerance = null;
            o.Devices["10.0.0.1"] = Online(true);
            o.Devices["10.0.0.2"] = Online(true);
        }, "10.0.0.1", "10.0.0.2");
        var guard = Guard(provider);

        await guard.StartAsync(default);
        HealthReportEntry entry;
        try
        {
            entry = await WaitForCyclesAsync(provider, 1);
        }
        finally
        {
            await guard.StopAsync(default);
        }

        Assert.NotEmpty(device.RequestsTo("set_configuration.fcgi"));
        Assert.Equal(HealthStatus.Degraded, entry.Status);
        Assert.StartsWith("corrigido", (string)entry.Data["device:10.0.0.1"]);
        Assert.StartsWith("falhou", (string)entry.Data["device:10.0.0.2"]);
    }

    [Fact]
    public async Task Fixes_the_clock_when_it_drifts_beyond_the_tolerance()
    {
        var device = Device().OnJson("system_information.fcgi", Info(NowEpoch(-3600)));
        await using var provider = Build(device, o => o.Devices["10.0.0.5"] = new DesiredDeviceConfiguration(), "10.0.0.5");
        var guard = Guard(provider);

        await guard.StartAsync(default);
        HealthReportEntry entry;
        try
        {
            entry = await WaitForCyclesAsync(provider, 1);
        }
        finally
        {
            await guard.StopAsync(default);
        }

        var request = device.RequestsTo("set_system_time.fcgi").First();
        Assert.Contains("\"year\":" + DateTime.UtcNow.Year, request.Body);
        Assert.Contains("clock", (string)entry.Data["device:10.0.0.5"]);
        Assert.Empty(device.RequestsTo("get_configuration.fcgi"));
    }

    [Fact]
    public async Task Leaves_the_clock_alone_within_the_tolerance()
    {
        var device = Device().OnJson("system_information.fcgi", Info(NowEpoch(-5)));
        await using var provider = Build(device, o => o.Devices["10.0.0.5"] = new DesiredDeviceConfiguration(), "10.0.0.5");
        var guard = Guard(provider);

        await guard.StartAsync(default);
        try
        {
            await WaitForCyclesAsync(provider, 2);
        }
        finally
        {
            await guard.StopAsync(default);
        }

        Assert.NotEmpty(device.RequestsTo("system_information.fcgi"));
        Assert.Empty(device.RequestsTo("set_system_time.fcgi"));
    }

    [Fact]
    public async Task Health_check_is_degraded_before_the_first_cycle()
    {
        await using var provider = Build(Device(), o => o.Devices["10.0.0.5"] = Online(true), "10.0.0.5");

        var entry = (await Report(provider)).Entries["controlid-guard"];

        Assert.Equal(HealthStatus.Degraded, entry.Status);
        Assert.Equal(0L, entry.Data["cycles"]);
    }

    [Fact]
    public void Rejects_invalid_options_and_double_registration()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentOutOfRangeException>(() => services.AddControlIdConfigurationGuard(o => o.Interval = TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => services.AddControlIdConfigurationGuard(o => o.ClockTolerance = TimeSpan.FromSeconds(-1)));
        Assert.Throws<ArgumentNullException>(() => services.AddControlIdConfigurationGuard(null!));

        services.AddControlIdConfigurationGuard(_ => { });
        Assert.Throws<InvalidOperationException>(() => services.AddControlIdConfigurationGuard(_ => { }));
    }
}
