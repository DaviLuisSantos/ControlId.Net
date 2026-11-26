using ControlId.Access;
using ControlId.Access.Push;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace ControlId.Net.IntegrationTests;

/// <summary>Um health check por equipamento (<c>AddControlIdDeviceChecks</c>) com o último contato vindo do Push.</summary>
public sealed class DeviceHealthEndToEndTests
{
    private const long SimulatorDeviceId = 935107;

    [Fact]
    public async Task Reports_the_last_contact_of_a_device_after_a_push_poll()
    {
        await using var sim = await RunningSimulator.StartAsync();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.SetMinimumLevel(LogLevel.Critical);
        builder.Services.AddControlIdPush();
        builder.Services.AddControlIdAccess("portaria", o => o.BaseAddress = sim.BaseAddress);
        // Este teste cobre o último contato, não lentidão: folga no limite para a primeira sonda (login + JIT).
        builder.Services.AddHealthChecks().AddControlIdDeviceChecks(o => o.SlowThreshold = TimeSpan.FromMinutes(1));
        await using var server = builder.Build();
        server.MapControlIdPush();
        await server.StartAsync();
        var address = new Uri(server.Services.GetRequiredService<IServer>().Features.GetRequiredFeature<IServerAddressesFeature>().Addresses.First());
        var health = server.Services.GetRequiredService<HealthCheckService>();

        // Antes de qualquer Push: o equipamento responde, mas não há último contato.
        var before = await health.CheckHealthAsync();
        var entryBefore = before.Entries["controlid:access:portaria"];
        Assert.Equal(HealthStatus.Healthy, entryBefore.Status);
        Assert.False(entryBefore.Data.ContainsKey("lastContact"));
        Assert.Equal("Closed", entryBefore.Data["circuit"].ToString());

        using var device = sim.CreateClient();
        await device.ConfigurePushAsync(new PushConfiguration { RemoteAddress = address.ToString().TrimEnd('/'), RequestPeriod = TimeSpan.FromSeconds(1) });
        var push = server.Services.GetRequiredService<PushServer>();
        var result = await push.SendAsync(SimulatorDeviceId, PushCommand.ShowMessage("Contato")).WaitAsync(TimeSpan.FromSeconds(15));
        Assert.True(result.Success);

        var after = await health.CheckHealthAsync();
        var entry = after.Entries["controlid:access:portaria"];
        Assert.Equal(HealthStatus.Healthy, entry.Status);
        Assert.Equal("Push", entry.Data["lastContactSource"].ToString());
        Assert.True(DateTimeOffset.TryParse(entry.Data["lastContact"].ToString(), out var at));
        Assert.True(DateTimeOffset.UtcNow - at < TimeSpan.FromMinutes(1));
    }
}
