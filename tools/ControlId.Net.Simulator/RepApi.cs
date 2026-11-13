using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ControlId.Simulator;

/// <summary>
/// Rotas <c>/{comando}.fcgi</c> do REP iDClass (HTTP puro). Mesmos formatos de erro e de sessão da linha de acesso
/// (<c>?session=</c>); <c>mode=671</c> escolhe CPF em vez de PIS e liga o fuso horário.
/// </summary>
public static class RepApi
{
    private static readonly string[] Commands =
    [
        "get_about", "get_info", "get_system_information", "get_system_date_time", "set_system_date_time", "count_users",
        "load_users", "add_users", "update_users", "remove_users", "get_afd", "change_login",
    ];

    /// <summary>Comandos atendidos, para o <c>/simulator/state</c> e a documentação.</summary>
    public static IEnumerable<string> SupportedCommands =>
        Commands.Concat(["login", "logout", "session_is_valid"]).Order(StringComparer.Ordinal);

    public static void MapRepApi(this IEndpointRouteBuilder app)
    {
        app.MapMethods("/{command}.fcgi", ["GET", "POST"], HandleAsync);

        var group = app.MapGroup("/simulator");

        group.MapGet("/state", (RepState state) =>
        {
            lock (state.Lock)
            {
                return Results.Json(new
                {
                    kind = SimulatorOptions.KindRep,
                    deviceId = state.Options.DeviceId,
                    serial = state.Options.Serial,
                    firmware = state.Options.Firmware,
                    time = AfdWriter.DateTimeText(state.Now),
                    users = state.Users.Count,
                    lastNsr = state.LastNsr,
                    commands = SupportedCommands,
                });
            }
        });

        // Uma pessoa bate o ponto no REP: grava a marcação (tipo 3 do AFD) com o próximo NSR.
        group.MapPost("/punch", (PunchRequest request, RepState state) =>
        {
            try
            {
                lock (state.Lock)
                {
                    var (nsr, time) = state.Punch(request.Cpf);
                    return Results.Json(new { nsr, cpf = request.Cpf, time = AfdWriter.DateTimeText(time) });
                }
            }
            catch (DeviceException ex)
            {
                return Error(ex.Message, StatusCodes.Status404NotFound);
            }
        });

        group.MapPost("/expire-sessions", (RepState state) =>
        {
            lock (state.Lock)
            {
                state.ExpireSessions();
            }

            return Results.Ok();
        });
    }

    private static async Task<IResult> HandleAsync(string command, HttpContext context, RepState state)
    {
        var body = await ReadBodyAsync(context.Request);
        var session = context.Request.Query["session"].FirstOrDefault();
        var cpfMode = context.Request.Query["mode"].FirstOrDefault() == "671";

        try
        {
            lock (state.Lock)
            {
                switch (command)
                {
                    case "login":
                        var json = ParseObject(body);
                        var created = state.TryLogin(json["login"]?.GetValue<string>(), json["password"]?.GetValue<string>());
                        return created is null
                            ? Error("Invalid login or password", StatusCodes.Status401Unauthorized)
                            : Results.Json(new { session = created });
                    case "session_is_valid":
                        return Results.Json(new { session_is_valid = state.IsValidSession(session) });
                    case "logout":
                        state.Logout(session);
                        return Results.Ok();
                }

                if (!Commands.Contains(command))
                {
                    return Error("Invalid command", StatusCodes.Status400BadRequest);
                }

                if (!state.IsValidSession(session))
                {
                    return Error("Session is not valid", StatusCodes.Status401Unauthorized);
                }

                return Execute(command, state, ParseObject(body), cpfMode);
            }
        }
        catch (DeviceException ex)
        {
            return Error(ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or ArgumentException)
        {
            return Error("Invalid request: " + ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    private static IResult Execute(string command, RepState state, JsonObject json, bool cpfMode)
    {
        switch (command)
        {
            case "get_about":
                return Json(new JsonObject
                {
                    ["mac"] = "00:14:6C:00:00:01",
                    ["nSerie"] = state.Options.Serial,
                    ["versionFW"] = 260, // revisão 104 da API, em decimal
                    ["versionMRP"] = 1,
                    ["isFacial"] = false,
                });
            case "get_info":
                return Json(new JsonObject
                {
                    ["num_serie"] = state.Options.Serial,
                    ["user_count"] = state.Users.Count,
                    ["administrator_count"] = state.Users.Count(IsAdmin),
                    ["template_count"] = state.Users.Sum(u => (u["templates"] as JsonArray)?.Count ?? 0),
                    ["password_count"] = state.Users.Count(u => u["password"] is not null),
                    ["bars_count"] = state.Users.Count(u => u["bars"] is not null),
                    ["rfid_count"] = state.Users.Count(u => u["rfid"] is not null),
                    ["uptime"] = (long)state.Uptime.TotalSeconds,
                    ["low_on_paper"] = false,
                    ["cuts"] = 0,
                    ["total_printed"] = 0,
                });
            case "get_system_information":
                return Json(new JsonObject
                {
                    ["uptime"] = (long)state.Uptime.TotalSeconds,
                    ["user_count"] = state.Users.Count,
                    ["template_count"] = state.Users.Sum(u => (u["templates"] as JsonArray)?.Count ?? 0),
                    ["number_of_faces"] = "0",
                    ["paper_ok"] = true,
                    ["low_paper"] = false,
                    ["memory"] = 0,
                    ["used_mrp"] = 1,
                    ["last_nsr"] = state.LastNsr,
                    ["ticks"] = (long)state.Uptime.TotalSeconds,
                    ["cuts"] = 0,
                    ["coil_paper"] = 100,
                    ["total_paper"] = 100,
                });
            case "get_system_date_time":
                return GetDateTime(state, cpfMode);
            case "set_system_date_time":
                return SetDateTime(state, json, cpfMode);
            case "count_users":
                return Json(new JsonObject { ["count"] = state.Users.Count });
            case "load_users":
                return LoadUsers(state, json, cpfMode);
            case "add_users":
                return AddUsers(state, json, cpfMode);
            case "update_users":
                return UpdateUsers(state, json, cpfMode);
            case "remove_users":
                return RemoveUsers(state, json, cpfMode);
            case "get_afd":
                return GetAfd(state, json);
            case "change_login":
                state.ChangeLogin(Required(json, "login"), Required(json, "password"));
                return Json(new JsonObject());
            default:
                return Error("Invalid command", StatusCodes.Status400BadRequest);
        }
    }

    private static IResult GetDateTime(RepState state, bool cpfMode)
    {
        var now = state.Now;
        var result = new JsonObject
        {
            ["day"] = now.Day,
            ["month"] = now.Month,
            ["year"] = now.Year,
            ["hour"] = now.Hour,
            ["minute"] = now.Minute,
            ["second"] = now.Second,
        };

        if (cpfMode)
        {
            result["timezone"] = FormatZone(now.Offset);
        }

        return Json(result);
    }

    private static IResult SetDateTime(RepState state, JsonObject json, bool cpfMode)
    {
        var local = new DateTime(Int(json, "year"), Int(json, "month"), Int(json, "day"), Int(json, "hour"), Int(json, "minute"), Int(json, "second"));
        TimeSpan? zone = null;
        if (cpfMode && json["timezone"]?.GetValue<string>() is { } text)
        {
            zone = ParseZone(text);
        }

        state.SetClock(local, zone);
        return Json(new JsonObject());
    }

    private static IResult LoadUsers(RepState state, JsonObject json, bool cpfMode)
    {
        IEnumerable<JsonObject> users = state.Users;
        var filterName = cpfMode ? "users_cpf" : "users_pis";
        if (json[filterName] is JsonArray filter)
        {
            var wanted = filter.Select(n => ObjectStore.TryGetLong(n, out var v) ? v : (long?)null).Where(v => v is not null).Select(v => v!.Value).ToHashSet();
            users = users.Where(u => RepState.KeyOf(u, cpfMode) is { } key && wanted.Contains(key));
        }

        var all = users.ToList();
        var limit = json["limit"] is { } l && ObjectStore.TryGetLong(l, out var limitValue) ? (int)Math.Clamp(limitValue, 1, 100) : 100;
        var offset = json["offset"] is { } o && ObjectStore.TryGetLong(o, out var offsetValue) ? (int)Math.Max(0, offsetValue) : 0;
        var withTemplates = json["templates"]?.GetValue<bool>() == true;

        var page = all.Skip(offset).Take(limit).Select(u =>
        {
            var copy = (JsonObject)u.DeepClone();
            if (!withTemplates)
            {
                copy.Remove("templates");
            }

            return (JsonNode)copy;
        });

        return Json(new JsonObject { ["count"] = all.Count, ["users"] = new JsonArray(page.ToArray()) });
    }

    private static IResult AddUsers(RepState state, JsonObject json, bool cpfMode)
    {
        var users = (json["users"] as JsonArray ?? throw new DeviceException("Missing 'users'"))
            .Select(n => n as JsonObject ?? throw new DeviceException("Invalid user")).ToList();

        // Valida tudo antes de gravar: o REP não grava metade do lote.
        var seen = new HashSet<long>();
        foreach (var user in users)
        {
            var key = RepState.KeyOf(user, cpfMode) ?? throw new DeviceException($"Missing '{(cpfMode ? "cpf" : "pis")}'");
            if (!seen.Add(key) || state.Find(key, cpfMode) is not null)
            {
                throw new DeviceException($"User already exists: {key}");
            }
        }

        foreach (var user in users)
        {
            state.AddUser((JsonObject)user.DeepClone(), cpfMode);
        }

        return Json(new JsonObject());
    }

    private static IResult UpdateUsers(RepState state, JsonObject json, bool cpfMode)
    {
        var users = (json["users"] as JsonArray ?? throw new DeviceException("Missing 'users'"))
            .Select(n => n as JsonObject ?? throw new DeviceException("Invalid user")).ToList();

        foreach (var user in users)
        {
            var key = RepState.KeyOf(user, cpfMode) ?? throw new DeviceException($"Missing '{(cpfMode ? "cpf" : "pis")}'");
            if (state.Find(key, cpfMode) is null)
            {
                throw new DeviceException($"User not found: {key}");
            }
        }

        foreach (var user in users)
        {
            state.UpdateUser(user, cpfMode);
        }

        return Json(new JsonObject());
    }

    private static IResult RemoveUsers(RepState state, JsonObject json, bool cpfMode)
    {
        var keys = (json["users"] as JsonArray ?? throw new DeviceException("Missing 'users'"))
            .Select(n => ObjectStore.TryGetLong(n, out var key) ? key : throw new DeviceException("Invalid user identifier"))
            .ToList();

        return Json(new JsonObject { ["changes"] = state.RemoveUsers(keys, cpfMode) });
    }

    private static IResult GetAfd(RepState state, JsonObject json)
    {
        long? initialNsr = ObjectStore.TryGetLong(json["initial_nsr"], out var nsr) ? nsr : null;
        DateTime? initialDate = null;
        if (json["initial_date"] is JsonObject date)
        {
            initialDate = new DateTime(Int(date, "year"), Int(date, "month"), Int(date, "day"));
        }

        if (initialNsr is not null && initialDate is not null)
        {
            throw new DeviceException("Use initial_nsr or initial_date, not both");
        }

        var headerTop = json["headerTop"]?.GetValue<bool>() == true;
        return Results.Bytes(state.BuildAfd(initialNsr, initialDate, headerTop), "text/plain; charset=iso-8859-1");
    }

    private static bool IsAdmin(JsonObject user) => user["admin"] is JsonValue v && v.TryGetValue(out bool admin) && admin;

    private static string FormatZone(TimeSpan offset) =>
        (offset < TimeSpan.Zero ? "-" : "+") + offset.Duration().ToString("hhmm", CultureInfo.InvariantCulture);

    private static TimeSpan ParseZone(string text)
    {
        var zone = text.Trim();
        if (zone.Length == 5 && zone[0] is '+' or '-'
            && int.TryParse(zone.AsSpan(1, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var hours)
            && int.TryParse(zone.AsSpan(3, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var minutes))
        {
            var offset = new TimeSpan(hours, minutes, 0);
            return zone[0] == '-' ? -offset : offset;
        }

        throw new DeviceException($"Invalid timezone: {text}");
    }

    private static string Required(JsonObject json, string name) =>
        json[name]?.GetValue<string>() ?? throw new DeviceException($"Missing '{name}'");

    private static int Int(JsonObject json, string name) =>
        ObjectStore.TryGetLong(json[name], out var value) ? (int)value : throw new DeviceException($"Missing '{name}'");

    private static IResult Json(JsonNode node) => Results.Text(node.ToJsonString(AccessApi.Wire), "application/json");

    private static IResult Error(string message, int status) =>
        Results.Text(new JsonObject { ["error"] = message, ["code"] = 1 }.ToJsonString(AccessApi.Wire), "application/json", statusCode: status);

    private static async Task<byte[]> ReadBodyAsync(HttpRequest request)
    {
        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    private static JsonObject ParseObject(byte[] body) =>
        body.Length == 0 ? new JsonObject() : JsonNode.Parse(body) as JsonObject ?? new JsonObject();

    private sealed record PunchRequest(long Cpf);
}
