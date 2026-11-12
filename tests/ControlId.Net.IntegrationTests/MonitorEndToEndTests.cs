using ControlId.Access;
using ControlId.Access.Monitoring;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ControlId.Net.IntegrationTests;

public sealed class MonitorEndToEndTests
{
    [Fact]
    public async Task Delivers_access_and_door_events_to_the_aspnetcore_routes()
    {
        var accessLogs = new TaskCompletionSource<AccessLog>(TaskCreationOptions.RunContinuationsAsynchronously);
        var doors = new TaskCompletionSource<DoorNotification>(TaskCreationOptions.RunContinuationsAsynchronously);

        // O "seu servidor": as rotas do ControlId.Net.AspNetCore, numa porta livre.
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Services.AddControlIdMonitor((notification, _) =>
        {
            switch (notification)
            {
                case DaoNotification dao when dao.GetInsertedAccessLogs() is [var log, ..]:
                    accessLogs.TrySetResult(log);
                    break;
                case DoorNotification door:
                    doors.TrySetResult(door);
                    break;
            }

            return Task.CompletedTask;
        });
        await using var server = builder.Build();
        server.MapControlIdMonitor();
        await server.StartAsync();
        var serverUri = new Uri(server.Services.GetRequiredService<IServer>().Features.GetRequiredFeature<IServerAddressesFeature>().Addresses.First());

        await using var sim = await RunningSimulator.StartAsync();
        using var device = sim.CreateClient();

        // No equipamento, uma vez: para onde mandar as notificações.
        await device.ConfigureMonitorAsync(new MonitorConfiguration { Hostname = serverUri.Host, Port = serverUri.Port, Path = "api/notifications" });

        await sim.IdentifyAsync(cardValue: 4194301001);   // a Ana passa o cartão

        var log = await accessLogs.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(AccessEventType.AccessGranted, log.EventType);
        Assert.Equal(1, log.UserId);
        Assert.Equal(4194301001, log.CardValue);

        var door = await doors.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(door.Door!.Open);

        await server.StopAsync();
    }
}
