using System.Globalization;
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

        group.MapGet("/state", (DeviceState state, MonitorDispatcher monitor, PushClient push) =>
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
                    online = state.GetConfiguration("general", "online") == "1",
                    push = new
                    {
                        address = state.GetConfiguration("push_server", "push_remote_address"),
                        commandsExecuted = push.CommandsExecuted,
                        failures = push.Failures,
                        lastError = push.LastError,
                    },
                    commands = AccessApi.SupportedCommands,
                });
            }
        });

        // Uma pessoa se identifica no equipamento: por usuário ou por cartão. Gera o log de acesso, abre a porta se
        // o acesso for liberado e manda as notificações do Monitor (dao e door), como o equipamento. Com o modo online
        // ligado, quem decide é o servidor; se ele não responde, vale a decisão local (contingência).
        group.MapPost("/identify", async (IdentifyRequest request, DeviceState state, MonitorDispatcher monitor, OnlineClient online, CancellationToken cancellationToken) =>
        {
            var now = DateTimeOffset.UtcNow;
            var portal = request.PortalId ?? 1;
            long? userId;
            string? userName;
            bool localGranted;
            bool askServer;
            bool cardOnly;

            lock (state.Lock)
            {
                userId = request.UserId;
                if (userId is null && request.CardValue is { } card)
                {
                    var match = state.Objects.Rows("cards").FirstOrDefault(c => ObjectStore.TryGetLong(c["value"], out var v) && v == card);
                    userId = match is not null && ObjectStore.TryGetLong(match["user_id"], out var owner) ? owner : null;
                }

                var user = userId is { } id
                    ? state.Objects.Rows("users").FirstOrDefault(u => ObjectStore.TryGetLong(u["id"], out var uid) && uid == id)
                    : null;
                localGranted = user is not null;
                userName = user?["name"]?.ToString();

                // Enterprise (local_identification=0): o equipamento só lê o cartão e o servidor identifica.
                cardOnly = state.GetConfiguration("general", "local_identification") == "0" && request.CardValue is not null;
                askServer = online.IsEnabled();
            }

            JsonObject? decision = null;
            if (askServer)
            {
                var form = new List<KeyValuePair<string, string>>
                {
                    new("device_id", state.Options.DeviceId.ToString(CultureInfo.InvariantCulture)),
                    new("time", now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
                    new("portal_id", portal.ToString(CultureInfo.InvariantCulture)),
                    new("identifier_id", request.CardValue is null ? "0" : "1"),
                    new("uuid", Guid.NewGuid().ToString()),
                };
                if (request.CardValue is { } cardValue)
                {
                    form.Add(new("card_value", cardValue.ToString(CultureInfo.InvariantCulture)));
                }

                if (!cardOnly)
                {
                    form.Add(new("event", localGranted ? "7" : "3"));
                    form.Add(new("user_id", localGranted ? userId?.ToString(CultureInfo.InvariantCulture) ?? "0" : "0"));
                    form.Add(new("user_name", localGranted ? userName ?? string.Empty : string.Empty));
                    form.Add(new("identification_rule_id", "0"));
                    form.Add(new("user_has_image", "0"));
                    form.Add(new("duress", "0"));
                }

                decision = await online.AuthorizeAsync(cardOnly ? "new_card" : "new_user_identified", form, cancellationToken);
            }

            JsonObject log;
            var doors = new List<int>();
            lock (state.Lock)
            {
                long eventType;
                long? loggedUser;
                if (decision is not null)
                {
                    eventType = ObjectStore.TryGetLong(decision["event"], out var e) ? e : 3;
                    loggedUser = ObjectStore.TryGetLong(decision["user_id"], out var u) && u > 0 ? u : eventType == 7 ? userId : null;
                    if (ObjectStore.TryGetLong(decision["portal_id"], out var p) && p > 0)
                    {
                        portal = p;
                    }

                    if (eventType == 7)
                    {
                        // As ações do servidor mandam; sem ações, abre o portal da identificação.
                        var actions = decision["actions"] as JsonArray;
                        foreach (var action in actions?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>())
                        {
                            if (action["action"]?.ToString() == "door")
                            {
                                var parameters = AccessApi.Parameters(action["parameters"]?.ToString());
                                doors.Add(parameters.TryGetValue("door", out var d) && int.TryParse(d, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : 1);
                            }
                        }

                        if (actions is null or { Count: 0 })
                        {
                            doors.Add((int)Math.Clamp(portal, 1, state.DoorCount));
                        }
                    }
                }
                else
                {
                    eventType = localGranted ? 7 : 3; // 7: acesso liberado; 3: não identificado
                    loggedUser = localGranted ? userId : null;
                    if (localGranted)
                    {
                        doors.Add((int)Math.Clamp(portal, 1, state.DoorCount));
                    }
                }

                doors.RemoveAll(d => d < 1 || d > state.DoorCount);

                log = new JsonObject
                {
                    ["time"] = now.ToUnixTimeSeconds(),
                    ["event"] = eventType,
                    ["device_id"] = state.Options.DeviceId,
                    ["identifier_id"] = request.CardValue is null ? 0 : 1,
                    ["user_id"] = loggedUser ?? 0,
                    ["portal_id"] = portal,
                    ["identification_rule_id"] = 0,
                    ["card_value"] = request.CardValue ?? 0,
                    ["log_type_id"] = -1,
                };
                state.Objects.Insert("access_logs", new[] { log });
                log = (JsonObject)state.Objects.Rows("access_logs")[^1].DeepClone();

                foreach (var door in doors)
                {
                    state.OpenDoor(door, now);
                }
            }

            monitor.PublishInserted("access_logs", new[] { log });
            foreach (var door in doors)
            {
                monitor.Publish("door", new JsonObject { ["door"] = new JsonObject { ["id"] = door, ["open"] = true } });
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
