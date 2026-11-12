using System.Text.Json.Nodes;

namespace ControlId.Simulator;

/// <summary>
/// Rotas <c>/simulator/*</c>, que não existem no equipamento: provocam o que no mundo real vem de uma pessoa diante dele
/// (uma identificação) ou de um evento (reinício), e mostram o estado para quem está testando.
/// </summary>
public static class SimulatorControlApi
{
    public static void MapSimulatorControl(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/simulator");

        group.MapGet("/state", (DeviceState state, MonitorDispatcher monitor) =>
        {
            var now = DateTimeOffset.UtcNow;
            lock (state.Lock)
            {
                return Results.Json(new
                {
                    deviceId = state.Options.DeviceId,
                    firmware = state.Options.Firmware,
                    tables = state.Objects.TableNames.Order(StringComparer.Ordinal).ToDictionary(t => t, t => state.Objects.Rows(t).Count),
                    doors = Enumerable.Range(1, state.DoorCount).Select(d => new { id = d, open = state.IsDoorOpen(d, now) }),
                    usersWithImage = state.UsersWithImage.ToList(),
                    screenMessage = state.ScreenMessage,
                    monitor = new
                    {
                        hostname = state.GetConfiguration("monitor", "hostname"),
                        port = state.GetConfiguration("monitor", "port"),
                        path = state.GetConfiguration("monitor", "path"),
                        deliveries = monitor.History,
                    },
                    commands = AccessApi.SupportedCommands,
                });
            }
        });

        // Uma pessoa se identifica no equipamento: por usuário ou por cartão. Gera o log de acesso, abre a porta se o
        // usuário existir e manda as notificações do Monitor (dao e door), como o equipamento.
        group.MapPost("/identify", (IdentifyRequest request, DeviceState state, MonitorDispatcher monitor) =>
        {
            var now = DateTimeOffset.UtcNow;
            var portal = request.PortalId ?? 1;
            JsonObject log;
            bool granted;

            lock (state.Lock)
            {
                var userId = request.UserId;
                if (userId is null && request.CardValue is { } card)
                {
                    var match = state.Objects.Rows("cards").FirstOrDefault(c => ObjectStore.TryGetLong(c["value"], out var v) && v == card);
                    userId = match is not null && ObjectStore.TryGetLong(match["user_id"], out var owner) ? owner : null;
                }

                granted = userId is { } id && state.Objects.Rows("users").Any(u => ObjectStore.TryGetLong(u["id"], out var uid) && uid == id);

                log = new JsonObject
                {
                    ["time"] = now.ToUnixTimeSeconds(),
                    ["event"] = granted ? 7 : 3, // 7: acesso liberado; 3: não identificado
                    ["device_id"] = state.Options.DeviceId,
                    ["identifier_id"] = request.CardValue is null ? 0 : 1,
                    ["user_id"] = granted ? userId : 0,
                    ["portal_id"] = portal,
                    ["identification_rule_id"] = 0,
                    ["card_value"] = request.CardValue ?? 0,
                    ["log_type_id"] = -1,
                };
                state.Objects.Insert("access_logs", new[] { log });
                log = (JsonObject)state.Objects.Rows("access_logs")[^1].DeepClone();

                if (granted)
                {
                    state.OpenDoor((int)Math.Clamp(portal, 1, state.DoorCount), now);
                }
            }

            monitor.PublishInserted("access_logs", new[] { log });
            if (granted)
            {
                monitor.Publish("door", new JsonObject { ["door"] = new JsonObject { ["id"] = portal, ["open"] = true } });
            }

            return Results.Text(log.ToJsonString(AccessApi.Wire), "application/json");
        });

        // Como um reinício do equipamento: as sessões abertas deixam de valer (o cliente deve refazer o login).
        group.MapPost("/expire-sessions", (DeviceState state) =>
        {
            lock (state.Lock)
            {
                state.ExpireSessions();
            }

            return Results.NoContent();
        });

        group.MapPost("/reset", (DeviceState state) =>
        {
            lock (state.Lock)
            {
                state.Reset();
            }

            return Results.NoContent();
        });
    }

    /// <summary>Identificação simulada: informe <c>userId</c> ou <c>cardValue</c>.</summary>
    public sealed record IdentifyRequest(long? UserId, long? CardValue, long? PortalId);
}
