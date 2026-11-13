using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ControlId.Net.IntegrationTests;

public sealed class HealthCheckEndToEndTests
{
    // Porta fechada: a conexão é recusada na hora.
    private static readonly Uri Offline = new("http://127.0.0.1:1");

    [Fact]
    public async Task Is_healthy_when_every_device_answers()
    {
        await using var portaria = await RunningSimulator.StartAsync();
        await using var garagem = await RunningSimulator.StartAsync();

        var report = await CheckAsync(s =>
        {
            s.AddControlIdAccess("portaria", o => o.BaseAddress = portaria.BaseAddress);
            s.AddControlIdAccess("garagem", o => o.BaseAddress = garagem.BaseAddress);
        });

        var entry = report.Entries["controlid"];
        Assert.Equal(HealthStatus.Healthy, entry.Status);
        Assert.Equal("2 de 2 equipamentos respondendo.", entry.Description);
        Assert.StartsWith("ok: ", (string)entry.Data["access:portaria"]);
        Assert.StartsWith("ok: ", (string)entry.Data["access:garagem"]);
    }

    [Fact]
    public async Task Is_degraded_when_some_device_is_offline()
    {
        await using var portaria = await RunningSimulator.StartAsync();

        var report = await CheckAsync(s =>
        {
            s.AddControlIdAccess("portaria", o => o.BaseAddress = portaria.BaseAddress);
            s.AddControlIdAccess("garagem", o => o.BaseAddress = Offline);
        });

        var entry = report.Entries["controlid"];
        Assert.Equal(HealthStatus.Degraded, entry.Status);
        Assert.Equal("1 de 2 equipamentos respondendo.", entry.Description);
        Assert.StartsWith("falhou em ", (string)entry.Data["access:garagem"]);
    }

    [Fact]
    public async Task Uses_the_failure_status_when_no_device_answers()
    {
        var report = await CheckAsync(
            s => s.AddControlIdAccess(o => o.BaseAddress = Offline),
            failureStatus: HealthStatus.Degraded);

        var entry = report.Entries["controlid"];
        Assert.Equal(HealthStatus.Degraded, entry.Status);
        Assert.Equal("0 de 1 equipamentos respondendo.", entry.Description);
        Assert.True(entry.Data.ContainsKey("access:default"));
    }

    [Fact]
    public async Task Reports_slow_devices_as_degraded()
    {
        await using var portaria = await RunningSimulator.StartAsync();

        var report = await CheckAsync(
            s => s.AddControlIdAccess("portaria", o => o.BaseAddress = portaria.BaseAddress),
            o => o.SlowThreshold = TimeSpan.Zero);

        var entry = report.Entries["controlid"];
        Assert.Equal(HealthStatus.Degraded, entry.Status);
        Assert.StartsWith("lento: ", (string)entry.Data["access:portaria"]);
    }

    [Fact]
    public async Task Times_out_a_device_that_never_answers_without_holding_the_others()
    {
        await using var portaria = await RunningSimulator.StartAsync();

        // Aceita a conexão e nunca responde, como um equipamento travado.
        using var silent = new TcpListener(IPAddress.Loopback, 0);
        silent.Start();
        var silentUri = new Uri($"http://127.0.0.1:{((IPEndPoint)silent.LocalEndpoint).Port}");

        var started = DateTime.UtcNow;
        var report = await CheckAsync(
            s =>
            {
                s.AddControlIdAccess("portaria", o => o.BaseAddress = portaria.BaseAddress);
                s.AddControlIdAccess("travado", o => o.BaseAddress = silentUri);
            },
            o => o.Timeout = TimeSpan.FromMilliseconds(500));

        var entry = report.Entries["controlid"];
        Assert.Equal(HealthStatus.Degraded, entry.Status);
        Assert.Contains("Sem resposta em 0.5 s", (string)entry.Data["access:travado"], StringComparison.Ordinal);
        Assert.StartsWith("ok: ", (string)entry.Data["access:portaria"]);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(5));
    }

    private static async Task<HealthReport> CheckAsync(
        Action<IServiceCollection> registerDevices,
        Action<DependencyInjection.ControlIdHealthCheckOptions>? configure = null,
        HealthStatus? failureStatus = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        registerDevices(services);
        services.AddHealthChecks().AddControlIdDevices(configure, failureStatus: failureStatus);

        await using var provider = services.BuildServiceProvider();
        return await provider.GetRequiredService<HealthCheckService>().CheckHealthAsync();
    }
}
