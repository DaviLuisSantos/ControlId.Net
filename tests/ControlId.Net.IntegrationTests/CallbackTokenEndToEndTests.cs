using System.Net.Http.Json;
using System.Text.Json;
using ControlId.Access;
using ControlId.Access.Monitoring;
using ControlId.Access.Push;
using ControlId.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ControlId.Net.IntegrationTests;

/// <summary>
/// O token Bearer dos callbacks: o equipamento recebe o token com <c>ConfigureCallbackSecurityAsync</c> e o manda no
/// <c>Authorization</c>; as rotas protegidas com <c>RequireControlIdToken</c> aceitam o certo e recusam o resto.
/// </summary>
public sealed class CallbackTokenEndToEndTests
{
    private const long SimulatorDeviceId = 935107;
    private const string Token = "segredo-do-callback";

    private static async Task<(WebApplication Server, Uri Address)> StartServerAsync(Action<IServiceCollection> services, Action<WebApplication> map)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.SetMinimumLevel(LogLevel.Critical);
        services(builder.Services);
        var server = builder.Build();
        map(server);
        await server.StartAsync();
        var address = server.Services.GetRequiredService<IServer>().Features.GetRequiredFeature<IServerAddressesFeature>().Addresses.First();
        return (server, new Uri(address));
    }

    [Fact]
    public async Task Push_route_accepts_the_configured_token_and_refuses_a_wrong_or_missing_one()
    {
        var (server, address) = await StartServerAsync(s => s.AddControlIdPush(), app => app.MapControlIdPush().RequireControlIdToken(Token));
        await using var _ = server;
        await using var sim = await RunningSimulator.StartAsync();
        using var device = sim.CreateClient();
        var push = server.Services.GetRequiredService<PushServer>();

        await device.ConfigurePushAsync(new PushConfiguration { RemoteAddress = address.ToString().TrimEnd('/'), RequestPeriod = TimeSpan.FromSeconds(1) });

        // Sem token no equipamento: o servidor responde 401 e o comando fica na fila.
        var pending = push.SendAsync(SimulatorDeviceId, PushCommand.ShowMessage("Com token"));
        await Assert.ThrowsAsync<TimeoutException>(() => pending.WaitAsync(TimeSpan.FromSeconds(4)));

        // Token errado: continua recusado.
        await device.ConfigureCallbackSecurityAsync("token-errado");
        await Assert.ThrowsAsync<TimeoutException>(() => pending.WaitAsync(TimeSpan.FromSeconds(4)));
        Assert.False(pending.IsCompleted);

        // Token certo: o comando que esperava na fila é entregue e executado.
        await device.ConfigureCallbackSecurityAsync(Token);
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.True(result.Success);
        var state = await sim.Control.GetFromJsonAsync<JsonElement>("/simulator/state");
        Assert.Equal("Com token", state.GetProperty("screenMessage").GetString());
    }

    [Fact]
    public async Task Monitor_route_accepts_the_configured_token_and_refuses_a_wrong_one()
    {
        var received = new TaskCompletionSource<AccessLog>(TaskCreationOptions.RunContinuationsAsynchronously);
        var (server, address) = await StartServerAsync(
            s => s.AddControlIdMonitor((notification, _) =>
            {
                if (notification is DaoNotification dao && dao.GetInsertedAccessLogs() is [var log, ..])
                {
                    received.TrySetResult(log);
                }

                return Task.CompletedTask;
            }),
            app => app.MapControlIdMonitor().RequireControlIdToken(Token));
        await using var _ = server;
        await using var sim = await RunningSimulator.StartAsync();
        using var device = sim.CreateClient();

        await device.ConfigureMonitorAsync(new MonitorConfiguration { Hostname = address.Host, Port = address.Port, Path = "api/notifications" });
        await device.ConfigureCallbackSecurityAsync("token-errado");

        await sim.IdentifyAsync(cardValue: 4194301001);
        await Assert.ThrowsAsync<TimeoutException>(() => received.Task.WaitAsync(TimeSpan.FromSeconds(3)));

        await device.ConfigureCallbackSecurityAsync(Token);
        await sim.IdentifyAsync(cardValue: 4194301001);

        var log = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(AccessEventType.AccessGranted, log.EventType);
    }
}
