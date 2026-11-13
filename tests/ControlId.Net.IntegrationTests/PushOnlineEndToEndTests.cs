using System.Net.Http.Json;
using System.Text.Json;
using ControlId.Access;
using ControlId.Access.Online;
using ControlId.Access.Push;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ControlId.Net.IntegrationTests;

/// <summary>
/// O simulador no modo Push (executa o que o <c>PushServer</c> manda) e no modo online (pergunta ao
/// <c>IOnlineAccessHandler</c> se libera), com as rotas reais do ControlId.Net.AspNetCore.
/// </summary>
public sealed class PushOnlineEndToEndTests
{
    private const long SimulatorDeviceId = 935107;

    private static async Task<(WebApplication Server, Uri Address)> StartServerAsync(Action<IServiceCollection> services, Action<WebApplication> map)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        services(builder.Services);
        var server = builder.Build();
        map(server);
        await server.StartAsync();
        var address = server.Services.GetRequiredService<IServer>().Features.GetRequiredFeature<IServerAddressesFeature>().Addresses.First();
        return (server, new Uri(address));
    }

    private static async Task<JsonElement> StateAsync(RunningSimulator sim) =>
        await sim.Control.GetFromJsonAsync<JsonElement>("/simulator/state");

    private static async Task<JsonElement> IdentifyAsync(RunningSimulator sim, long? userId = null, long? cardValue = null)
    {
        using var response = await sim.Control.PostAsJsonAsync("/simulator/identify", new { userId, cardValue });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<bool> DoorIsOpenAsync(RunningSimulator sim) =>
        (await StateAsync(sim)).GetProperty("doors")[0].GetProperty("open").GetBoolean();

    [Fact]
    public async Task Executes_a_single_command_sent_through_the_push_server()
    {
        var (server, address) = await StartServerAsync(s => s.AddControlIdPush(), app => app.MapControlIdPush());
        await using var _ = server;
        await using var sim = await RunningSimulator.StartAsync();
        using var device = sim.CreateClient();

        await device.ConfigurePushAsync(new PushConfiguration { RemoteAddress = address.ToString().TrimEnd('/'), RequestPeriod = TimeSpan.FromSeconds(1) });
        var push = server.Services.GetRequiredService<PushServer>();

        var users = await push.SendAsync(SimulatorDeviceId, PushCommand.LoadObjects(new LoadObjectsRequest("users")))
            .WaitAsync(TimeSpan.FromSeconds(15));
        Assert.True(users.Success);
        Assert.Equal(3, users.Response!.Value.GetProperty("users").GetArrayLength());

        var open = await push.SendAsync(SimulatorDeviceId, PushCommand.ExecuteActions(DeviceAction.OpenDoor(1))).WaitAsync(TimeSpan.FromSeconds(15));
        Assert.True(open.Success);
        Assert.True(await DoorIsOpenAsync(sim));

        await push.SendAsync(SimulatorDeviceId, PushCommand.ShowMessage("Via Push")).WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal("Via Push", (await StateAsync(sim)).GetProperty("screenMessage").GetString());

        var refused = await push.SendAsync(SimulatorDeviceId, new PushCommand("comando_inexistente")).WaitAsync(TimeSpan.FromSeconds(15));
        Assert.False(refused.Success);
        Assert.Contains("Invalid command", refused.Error);

        Assert.True((await StateAsync(sim)).GetProperty("push").GetProperty("commandsExecuted").GetInt32() >= 4);
    }

    [Fact]
    public async Task Executes_a_batch_of_commands_and_reports_each_result()
    {
        var (server, address) = await StartServerAsync(s => s.AddControlIdPush(), app => app.MapControlIdPush());
        await using var _ = server;
        await using var sim = await RunningSimulator.StartAsync(o => o.SeedSampleData = false);
        using var device = sim.CreateClient();

        await device.ConfigurePushAsync(new PushConfiguration { RemoteAddress = address.ToString().TrimEnd('/'), RequestPeriod = TimeSpan.FromSeconds(1) });
        var push = server.Services.GetRequiredService<PushServer>();

        var results = await push.SendBatchAsync(
            SimulatorDeviceId,
            [
                PushCommand.CreateObjects("users", [new { id = 50, name = "Dora" }, new { id = 51, name = "Edu" }]),
                PushCommand.ShowMessage("Lote"),
                new PushCommand("comando_inexistente"),
                PushCommand.LoadObjects(new LoadObjectsRequest("users")),
            ]).WaitAsync(TimeSpan.FromSeconds(15));

        Assert.Equal(4, results.Count);
        Assert.True(results[0].Success);
        Assert.True(results[1].Success);
        Assert.False(results[2].Success);
        Assert.True(results[3].Success);
        Assert.Equal(2, results[3].Response!.Value.GetProperty("users").GetArrayLength());

        var state = await StateAsync(sim);
        Assert.Equal("Lote", state.GetProperty("screenMessage").GetString());
        Assert.Equal(2, state.GetProperty("tables").GetProperty("users").GetInt32());
    }

    [Fact]
    public async Task Online_mode_lets_the_server_grant_or_deny_and_logs_the_decision()
    {
        var seen = new List<OnlineIdentificationEvent>();
        var (server, address) = await StartServerAsync(
            s => s.AddControlIdOnline((identification, _) =>
            {
                lock (seen)
                {
                    seen.Add(identification);
                }

                // O servidor libera a Ana (1) e nega o Bruno (2), mesmo ele existindo no equipamento.
                var user = (identification as UserIdentifiedEvent)?.UserId;
                return Task.FromResult(user == 1
                    ? OnlineAuthorization.Grant(1, "Ana Souza", DeviceAction.OpenDoor(1))
                    : OnlineAuthorization.Deny(user, null, "Bloqueado pelo servidor"));
            }),
            app => app.MapControlIdOnline("/api"));
        await using var _ = server;
        await using var sim = await RunningSimulator.StartAsync();
        using var device = sim.CreateClient();

        var serverId = await device.ConfigureOnlineModeAsync(new OnlineModeConfiguration { ServerAddress = new Uri(address, "/api").ToString() });
        Assert.True(serverId > 0);
        Assert.True((await StateAsync(sim)).GetProperty("online").GetBoolean());

        var denied = await IdentifyAsync(sim, userId: 2);
        Assert.Equal((int)AccessEventType.AccessDenied, denied.GetProperty("event").GetInt32());
        Assert.False(await DoorIsOpenAsync(sim));

        var granted = await IdentifyAsync(sim, userId: 1);
        Assert.Equal((int)AccessEventType.AccessGranted, granted.GetProperty("event").GetInt32());
        Assert.Equal(1, granted.GetProperty("user_id").GetInt32());
        Assert.True(await DoorIsOpenAsync(sim));

        lock (seen)
        {
            Assert.Equal(2, seen.Count);
            var first = Assert.IsType<UserIdentifiedEvent>(seen[0]);
            Assert.Equal(2L, first.UserId);
            Assert.Equal((long?)SimulatorDeviceId, first.DeviceId);
        }

        // Desligado o modo online, o equipamento volta a decidir sozinho (o Bruno é liberado) e o servidor não é consultado.
        await device.DisableOnlineModeAsync();
        var local = await IdentifyAsync(sim, userId: 2);
        Assert.Equal((int)AccessEventType.AccessGranted, local.GetProperty("event").GetInt32());
        lock (seen)
        {
            Assert.Equal(2, seen.Count);
        }
    }

    [Fact]
    public async Task Online_mode_in_enterprise_sends_the_card_to_the_server()
    {
        OnlineIdentificationEvent? received = null;
        var (server, address) = await StartServerAsync(
            s => s.AddControlIdOnline((identification, _) =>
            {
                received = identification;
                return Task.FromResult(OnlineAuthorization.Grant(2, "Bruno Lima", DeviceAction.OpenDoor(1)));
            }),
            app => app.MapControlIdOnline("/api"));
        await using var _ = server;
        await using var sim = await RunningSimulator.StartAsync();
        using var device = sim.CreateClient();

        await device.ConfigureOnlineModeAsync(new OnlineModeConfiguration { ServerAddress = new Uri(address, "/api").ToString(), Mode = OnlineMode.Enterprise });

        // Um cartão que o equipamento não conhece: no Enterprise quem identifica é o servidor.
        var log = await IdentifyAsync(sim, cardValue: 999);
        Assert.Equal((int)AccessEventType.AccessGranted, log.GetProperty("event").GetInt32());
        Assert.Equal(2, log.GetProperty("user_id").GetInt32());
        var card = Assert.IsType<CardEvent>(received);
        Assert.Equal(999L, card.CardValue);
        Assert.True(await DoorIsOpenAsync(sim));
    }

    [Fact]
    public async Task Falls_back_to_the_local_decision_when_the_online_server_is_down()
    {
        var calls = 0;
        var (server, address) = await StartServerAsync(
            s => s.AddControlIdOnline((_, _) =>
            {
                Interlocked.Increment(ref calls);
                return Task.FromResult(OnlineAuthorization.Deny(null, null, "Fora do horário"));
            }),
            app => app.MapControlIdOnline("/api"));
        await using var sim = await RunningSimulator.StartAsync();
        using var device = sim.CreateClient();

        await device.ConfigureOnlineModeAsync(new OnlineModeConfiguration
        {
            ServerAddress = new Uri(address, "/api").ToString(),
            MaxRequestAttempts = 2,
            RequestTimeout = TimeSpan.FromSeconds(1),
        });

        var online = await IdentifyAsync(sim, userId: 1);
        Assert.Equal((int)AccessEventType.AccessDenied, online.GetProperty("event").GetInt32());
        Assert.Equal(1, calls);

        // O servidor cai: o equipamento tenta de novo (2 vezes), desiste e libera quem conhece, como em contingência.
        await server.StopAsync();
        await server.DisposeAsync();

        var contingency = await IdentifyAsync(sim, userId: 1);
        Assert.Equal((int)AccessEventType.AccessGranted, contingency.GetProperty("event").GetInt32());
        Assert.True(await DoorIsOpenAsync(sim));

        var stranger = await IdentifyAsync(sim, userId: 99);
        Assert.Equal((int)AccessEventType.NotIdentified, stranger.GetProperty("event").GetInt32());
        Assert.Equal(1, calls);
    }
}
