using System.Collections.Concurrent;
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
/// Callbacks fechados por padrão (ADR dos callbacks, P1-1 da auditoria): uma rota mapeada sem <c>RequireControlIdToken</c>
/// nem <c>AllowAnonymousControlIdCallbacks()</c> responde 401 ao simulador, mesmo que ele mande um token; com o token
/// configurado nos dois lados (<c>ConfigureCallbackSecurityAsync</c> no equipamento e <c>RequireControlIdToken</c> na rota),
/// o comando chega e a notificação é tratada.
/// </summary>
public sealed class CallbackDefaultsEndToEndTests
{
    private const long SimulatorDeviceId = 935107;
    private const string Token = "segredo-dos-callbacks-fechados";

    [Fact]
    public async Task Push_route_is_closed_by_default_and_delivers_only_with_the_token_on_both_sides()
    {
        var (server, address, hits) = await StartServerAsync(
            s => s.AddControlIdPush(),
            app =>
            {
                // Mesma fila atrás de duas rotas: uma no padrão (fechada) e outra protegida pelo token.
                app.MapControlIdPush("/fechado");
                app.MapControlIdPush("/protegido").RequireControlIdToken(Token);
            });
        await using var _ = server;
        await using var sim = await RunningSimulator.StartAsync();
        using var device = sim.CreateClient();
        var push = server.Services.GetRequiredService<PushServer>();

        var pending = push.SendAsync(SimulatorDeviceId, PushCommand.ShowMessage("Callback com token"));
        await device.ConfigurePushAsync(new PushConfiguration { RemoteAddress = Root(address) + "/fechado", RequestPeriod = TimeSpan.FromSeconds(1) });

        // Sem token no equipamento: 401 na rota fechada, e o simulador vê a recusa.
        await EndToEndWait.UntilAsync(
            () => hits.Any(h => h.Path == "/fechado/push" && !h.HasAuthorization && h.StatusCode == 401),
            "GET /fechado/push sem token respondido com 401");
        await EndToEndWait.UntilAsync(
            async () => (await PushStateAsync(sim)).GetProperty("lastError").GetString()?.Contains("401", StringComparison.Ordinal) == true,
            "o simulador registrar a falha 401 do /push");
        await AssertNotDeliveredAsync(sim, push, pending);

        // Token só no equipamento: a rota continua fechada (o servidor não declarou token nem liberou a rota).
        await device.ConfigureCallbackSecurityAsync(Token);
        await EndToEndWait.UntilAsync(
            () => hits.Any(h => h.Path == "/fechado/push" && h.HasAuthorization && h.StatusCode == 401),
            "GET /fechado/push com token respondido com 401");
        await AssertNotDeliveredAsync(sim, push, pending);

        // Token nos dois lados: o comando que esperava na fila é entregue, executado e o resultado volta.
        await device.ConfigurePushAsync(new PushConfiguration { RemoteAddress = Root(address) + "/protegido", RequestPeriod = TimeSpan.FromSeconds(1) });
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(6));

        Assert.True(result.Success);
        var state = await StateAsync(sim);
        Assert.Equal("Callback com token", state.GetProperty("screenMessage").GetString());
        Assert.Equal(1, state.GetProperty("push").GetProperty("commandsExecuted").GetInt32());
        Assert.NotNull(push.GetLastSeen(SimulatorDeviceId));
        Assert.Equal(0, await push.GetPendingCountAsync(SimulatorDeviceId));
        Assert.Contains(hits.Snapshot(), h => h.Path == "/protegido/push" && h.HasAuthorization && h.StatusCode == 200);
        Assert.Contains(hits.Snapshot(), h => h.Path == "/protegido/result" && h.HasAuthorization && h.StatusCode == 200);
        Assert.DoesNotContain(hits.Snapshot(), h => h.Path.StartsWith("/fechado/", StringComparison.Ordinal) && h.StatusCode != 401);
    }

    [Fact]
    public async Task Monitor_route_is_closed_by_default_and_reaches_the_handler_only_with_the_token_on_both_sides()
    {
        var notifications = new ConcurrentQueue<MonitorNotification>();
        var (server, address, hits) = await StartServerAsync(
            s => s.AddControlIdMonitor((notification, _) =>
            {
                notifications.Enqueue(notification);
                return Task.CompletedTask;
            }),
            app =>
            {
                app.MapControlIdMonitor("fechado/api/notifications");
                app.MapControlIdMonitor("api/notifications").RequireControlIdToken(Token);
            });
        await using var _ = server;
        await using var sim = await RunningSimulator.StartAsync();
        using var device = sim.CreateClient();

        // Sem token no equipamento: o Monitor recebe 401 e o handler não é chamado.
        await device.ConfigureMonitorAsync(new MonitorConfiguration { Hostname = address.Host, Port = address.Port, Path = "fechado/api/notifications" });
        await sim.IdentifyAsync(cardValue: 4194301001);
        await EndToEndWait.UntilAsync(
            () => hits.Any(h => h.Path == "/fechado/api/notifications/dao" && !h.HasAuthorization && h.StatusCode == 401),
            "POST /fechado/api/notifications/dao sem token respondido com 401");
        await EndToEndWait.UntilAsync(
            async () => MonitorDeliveries(await StateAsync(sim)).Any(d => d.Endpoint == "dao" && d.StatusCode == 401),
            "o simulador registrar a entrega do dao com 401");
        Assert.Empty(notifications);

        // Token só no equipamento: a rota padrão continua fechada.
        await device.ConfigureCallbackSecurityAsync(Token);
        await sim.IdentifyAsync(cardValue: 4194301001);
        await EndToEndWait.UntilAsync(
            () => hits.Any(h => h.Path == "/fechado/api/notifications/dao" && h.HasAuthorization && h.StatusCode == 401),
            "POST /fechado/api/notifications/dao com token respondido com 401");
        Assert.Empty(notifications);

        // Token nos dois lados: a notificação chega ao handler.
        await device.ConfigureMonitorAsync(new MonitorConfiguration { Hostname = address.Host, Port = address.Port, Path = "api/notifications" });
        await sim.IdentifyAsync(cardValue: 4194301001);
        await EndToEndWait.UntilAsync(
            () => notifications.Any(n => n is DaoNotification dao && dao.GetInsertedAccessLogs().Count > 0),
            "o handler receber o dao do log de acesso");

        var log = notifications.OfType<DaoNotification>().SelectMany(n => n.GetInsertedAccessLogs()).First();
        Assert.Equal(AccessEventType.AccessGranted, log.EventType);
        Assert.Equal((long?)SimulatorDeviceId, log.DeviceId);
        Assert.Contains(hits.Snapshot(), h => h.Path == "/api/notifications/dao" && h.HasAuthorization && h.StatusCode == 200);
        Assert.DoesNotContain(hits.Snapshot(), h => h.Path.StartsWith("/fechado/", StringComparison.Ordinal) && h.StatusCode != 401);
    }

    private static async Task AssertNotDeliveredAsync(RunningSimulator sim, PushServer push, Task<PushResult> pending)
    {
        Assert.False(pending.IsCompleted);
        Assert.Equal(0, (await PushStateAsync(sim)).GetProperty("commandsExecuted").GetInt32());

        // A recusa acontece antes do PushServer: a consulta não é registrada e o comando continua na fila.
        Assert.Null(push.GetLastSeen(SimulatorDeviceId));
        Assert.Equal(1, await push.GetPendingCountAsync(SimulatorDeviceId));
    }

    private static string Root(Uri address) => address.ToString().TrimEnd('/');

    private static async Task<JsonElement> StateAsync(RunningSimulator sim) =>
        await sim.Control.GetFromJsonAsync<JsonElement>("/simulator/state");

    private static async Task<JsonElement> PushStateAsync(RunningSimulator sim) =>
        (await StateAsync(sim)).GetProperty("push");

    private static IEnumerable<(string? Endpoint, int? StatusCode)> MonitorDeliveries(JsonElement state) =>
        state.GetProperty("monitor").GetProperty("deliveries").EnumerateArray().Select(d => (
            d.GetProperty("endpoint").GetString(),
            d.GetProperty("statusCode").ValueKind == JsonValueKind.Number ? d.GetProperty("statusCode").GetInt32() : (int?)null));

    private static async Task<(WebApplication Server, Uri Address, CallbackRecorder Hits)> StartServerAsync(
        Action<IServiceCollection> services, Action<WebApplication> map)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.SetMinimumLevel(LogLevel.Critical);
        services(builder.Services);
        var server = builder.Build();

        // Registra cada callback recebido (caminho, se veio Authorization, status da resposta) para o teste esperar por ele.
        var hits = new CallbackRecorder();
        server.Use(async (context, next) =>
        {
            await next(context);
            hits.Add(new CallbackHit(context.Request.Path.Value ?? string.Empty, context.Request.Headers.Authorization.Count > 0, context.Response.StatusCode));
        });

        map(server);
        await server.StartAsync();
        var address = server.Services.GetRequiredService<IServer>().Features.GetRequiredFeature<IServerAddressesFeature>().Addresses.First();
        return (server, new Uri(address), hits);
    }

    private sealed record CallbackHit(string Path, bool HasAuthorization, int StatusCode);

    private sealed class CallbackRecorder
    {
        private readonly ConcurrentQueue<CallbackHit> _hits = new();

        public void Add(CallbackHit hit) => _hits.Enqueue(hit);

        public bool Any(Func<CallbackHit, bool> predicate) => _hits.Any(predicate);

        public IReadOnlyList<CallbackHit> Snapshot() => _hits.ToArray();
    }
}
