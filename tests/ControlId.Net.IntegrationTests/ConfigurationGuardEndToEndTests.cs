using ControlId.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ControlId.Net.IntegrationTests;

/// <summary>O guardião de configuração, num host de verdade, restaurando o que alguém mexeu no equipamento.</summary>
public sealed class ConfigurationGuardEndToEndTests
{
    private static IHost BuildHost(RunningSimulator sim)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddControlIdAccess("sim", o =>
        {
            o.BaseAddress = sim.BaseAddress;
            o.Login = "admin";
            o.Password = "admin";
            o.Timeout = TimeSpan.FromSeconds(10);
        });
        builder.Services.AddControlIdConfigurationGuard(g =>
        {
            g.Interval = TimeSpan.FromMilliseconds(100);
            g.ClockTolerance = null;
            g.Devices["sim"] = new DesiredDeviceConfiguration { Online = true };
        });
        return builder.Build();
    }

    [Fact]
    public async Task Guard_turns_online_on_and_restores_it_after_someone_turns_it_off()
    {
        await using var sim = await RunningSimulator.StartAsync();
        Assert.False(await sim.IsOnlineAsync());
        using var host = BuildHost(sim);
        await host.StartAsync();
        try
        {
            await EndToEndWait.UntilAsync(sim.IsOnlineAsync, "o guardião ligar o modo online");

            using var device = sim.CreateClient();
            await device.SetConfigurationAsync(new { general = new { online = "0" } });

            await EndToEndWait.UntilAsync(sim.IsOnlineAsync, "o guardião restaurar general.online");
        }
        finally
        {
            await host.StopAsync();
        }
    }
}
