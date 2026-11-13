using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ControlId.Simulator;

/// <summary>
/// Rotas <c>/{comando}.fcgi</c> da linha de acesso. Cada comando lê o corpo JSON (ou binário), exige
/// <c>?session=</c> válida (menos <c>login</c> e <c>session_is_valid</c>) e responde como o firmware.
/// </summary>
public static class AccessApi
{
    private delegate Task<IResult> Command(Request request);

    // O firmware não escapa caracteres como ">" (ex.: "config->general" nas mensagens de erro); o padrão do
    // System.Text.Json escaparia para ">".
    internal static readonly JsonSerializerOptions Wire = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static readonly Dictionary<string, Command> Commands = new(StringComparer.Ordinal)
    {
        ["load_objects"] = LoadObjects,
        ["create_objects"] = CreateObjects,
        ["modify_objects"] = ModifyObjects,
        ["destroy_objects"] = DestroyObjects,
        ["create_or_modify_objects"] = CreateOrModifyObjects,
        ["execute_actions"] = ExecuteActions,
        ["doors_state"] = DoorsState,
        ["system_information"] = SystemInformation,
        ["get_configuration"] = GetConfiguration,
        ["set_configuration"] = SetConfiguration,
        ["user_set_image"] = UserSetImage,
        ["user_get_image"] = UserGetImage,
        ["user_list_images"] = UserListImages,
        ["user_destroy_image"] = UserDestroyImage,
        ["message_to_screen"] = MessageToScreen,
        ["buzzer_buzz"] = Ok,
        ["change_login"] = ChangeLogin,
        ["reboot"] = Reboot,
    };

    /// <summary>Comandos atendidos, para o <c>/simulator/state</c> e a documentação.</summary>
    public static IEnumerable<string> SupportedCommands =>
        Commands.Keys.Concat(new[] { "login", "logout", "session_is_valid" }).Order(StringComparer.Ordinal);

    public static void MapAccessApi(this IEndpointRouteBuilder app)
    {
        app.MapMethods("/{command}.fcgi", new[] { "GET", "POST" }, HandleAsync);
    }

    private static async Task<IResult> HandleAsync(string command, HttpContext context, DeviceState state, MonitorDispatcher monitor)
    {
        var body = await ReadBodyAsync(context.Request);
        var session = context.Request.Query["session"].FirstOrDefault();

        try
        {
            switch (command)
            {
                case "login":
                    return Login(state, body);
                case "session_is_valid":
                    lock (state.Lock)
                    {
                        return Results.Json(new { session_is_valid = state.IsValidSession(session) });
                    }

                case "logout":
                    lock (state.Lock)
                    {
                        state.Logout(session);
                    }

                    return Results.Ok();
            }

            if (!Commands.TryGetValue(command, out var handler))
            {
                return Error("Invalid command", StatusCodes.Status400BadRequest);
            }

            lock (state.Lock)
            {
                if (!state.IsValidSession(session))
                {
                    return Error("Session is not valid", StatusCodes.Status401Unauthorized);
                }
            }

            return await handler(new Request(context, state, monitor, body));
        }
        catch (DeviceException ex)
        {
            return Error(ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return Error("Invalid request: " + ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>Resposta de um comando executado fora de uma requisição HTTP do cliente (pelo Push).</summary>
    internal sealed record CommandOutcome(int Status, byte[] Body, string? ContentType)
    {
        public bool Success => Status is >= 200 and < 300;
    }

    /// <summary>
    /// Executa um comando como o equipamento faz com o que recebe do Push: pelos mesmos handlers da API HTTP, mas sem
    /// sessão (o Push já é autenticado pelo próprio equipamento). <c>login</c>, <c>logout</c> e <c>session_is_valid</c>
    /// não existem por esse caminho.
    /// </summary>
    internal static async Task<CommandOutcome> ExecuteAsync(string command, byte[] body, string? queryString, IServiceProvider services)
    {
        var state = services.GetRequiredService<DeviceState>();
        var monitor = services.GetRequiredService<MonitorDispatcher>();
        var context = new DefaultHttpContext { RequestServices = services };
        if (!string.IsNullOrEmpty(queryString))
        {
            context.Request.QueryString = new QueryString("?" + queryString.TrimStart('?'));
        }

        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        IResult result;
        try
        {
            result = Commands.TryGetValue(command, out var handler)
                ? await handler(new Request(context, state, monitor, body))
                : Error("Invalid command", StatusCodes.Status400BadRequest);
        }
        catch (DeviceException ex)
        {
            result = Error(ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            result = Error("Invalid request: " + ex.Message, StatusCodes.Status400BadRequest);
        }

        await result.ExecuteAsync(context);
        return new CommandOutcome(context.Response.StatusCode, buffer.ToArray(), context.Response.ContentType);
    }

    private static IResult Login(DeviceState state, byte[] body)
    {
        var json = ParseObject(body);
        string? session;
        lock (state.Lock)
        {
            session = state.TryLogin(json["login"]?.GetValue<string>(), json["password"]?.GetValue<string>());
        }

        return session is null
            ? Error("Invalid login or password", StatusCodes.Status401Unauthorized)
            : Results.Json(new { session });
    }

    private static Task<IResult> LoadObjects(Request r)
    {
        var json = r.Json;
        var table = RequiredString(json, "object");
        var order = json["order"] is JsonArray o ? o.Select(n => n!.GetValue<string>()).ToList() : null;
        var fields = json["fields"] is JsonArray f ? f.Select(n => n!.GetValue<string>()).ToList() : null;

        List<JsonObject> rows;
        lock (r.State.Lock)
        {
            rows = r.State.Objects.Query(table, json["where"], order, Int(json["limit"]), Int(json["offset"]), fields);
        }

        return Json(new JsonObject { [table] = new JsonArray(rows.Cast<JsonNode>().ToArray()) });
    }

    private static Task<IResult> CreateObjects(Request r)
    {
        var json = r.Json;
        var table = RequiredString(json, "object");
        var values = json["values"] as JsonArray ?? throw new DeviceException("Missing 'values'");
        var rows = values.Select(v => v as JsonObject ?? throw new DeviceException("Invalid value")).ToList();

        List<long> ids;
        List<JsonObject> inserted;
        lock (r.State.Lock)
        {
            ids = r.State.Objects.Insert(table, rows);
            inserted = r.State.Objects.Rows(table).TakeLast(rows.Count).Select(x => (JsonObject)x.DeepClone()).ToList();
        }

        if (table is "access_logs" or "alarm_logs")
        {
            r.Monitor.PublishInserted(table, inserted);
        }

        return Json(new JsonObject { ["ids"] = new JsonArray(ids.Select(id => (JsonNode)id).ToArray()) });
    }

    private static Task<IResult> ModifyObjects(Request r)
    {
        var json = r.Json;
        var table = RequiredString(json, "object");
        var values = json["values"] as JsonObject ?? throw new DeviceException("Missing 'values'");

        int changes;
        lock (r.State.Lock)
        {
            changes = r.State.Objects.Modify(table, values, json["where"]).Count;
        }

        return Json(new JsonObject { ["changes"] = changes });
    }

    private static Task<IResult> DestroyObjects(Request r)
    {
        var json = r.Json;
        var table = RequiredString(json, "object");

        int changes;
        lock (r.State.Lock)
        {
            var removed = r.State.Objects.Delete(table, json["where"], Int(json["limit"]));
            changes = removed.Count;
            if (table == "users")
            {
                foreach (var user in removed)
                {
                    if (ObjectStore.TryGetLong(user["id"], out var id))
                    {
                        r.State.DeleteImage(id); // a foto vai junto com o usuário
                    }
                }
            }
        }

        return Json(new JsonObject { ["changes"] = changes });
    }

    private static Task<IResult> CreateOrModifyObjects(Request r)
    {
        var json = r.Json;
        var table = RequiredString(json, "object");
        var values = json["values"] as JsonArray ?? throw new DeviceException("Missing 'values'");

        var changes = 0;
        lock (r.State.Lock)
        {
            foreach (var value in values.OfType<JsonObject>())
            {
                if (value["id"] is { } id && r.State.Objects.Modify(table, value, new JsonObject { [table] = new JsonObject { ["id"] = id.DeepClone() } }).Count > 0)
                {
                    changes++;
                    continue;
                }

                r.State.Objects.Insert(table, new[] { value });
                changes++;
            }
        }

        return Json(new JsonObject { ["changes"] = changes });
    }

    private static Task<IResult> ExecuteActions(Request r)
    {
        var actions = r.Json["actions"] as JsonArray ?? throw new DeviceException("Missing 'actions'");
        var now = DateTimeOffset.UtcNow;
        var opened = new List<int>();

        lock (r.State.Lock)
        {
            foreach (var action in actions.OfType<JsonObject>())
            {
                var name = action["action"]?.GetValue<string>();
                var parameters = Parameters(action["parameters"]?.GetValue<string>());
                switch (name)
                {
                    case "door":
                        var door = parameters.TryGetValue("door", out var d) ? int.Parse(d, CultureInfo.InvariantCulture) : 1;
                        r.State.OpenDoor(door, now);
                        opened.Add(door);
                        break;
                    case "sec_box":
                    case "catra":
                        break; // aceitos; o simulador não modela SecBox nem catraca
                    default:
                        throw new DeviceException($"Invalid action: {name}");
                }
            }
        }

        foreach (var door in opened)
        {
            r.Monitor.Publish("door", new JsonObject { ["door"] = new JsonObject { ["id"] = door, ["open"] = true } });
        }

        return Json(new JsonObject());
    }

    private static Task<IResult> DoorsState(Request r)
    {
        var now = DateTimeOffset.UtcNow;
        var doors = new JsonArray();
        lock (r.State.Lock)
        {
            for (var door = 1; door <= r.State.DoorCount; door++)
            {
                doors.Add(new JsonObject { ["id"] = door, ["open"] = r.State.IsDoorOpen(door, now) });
            }
        }

        return Json(new JsonObject { ["doors"] = doors, ["sec_boxes"] = new JsonArray() });
    }

    private static Task<IResult> SystemInformation(Request r)
    {
        var now = DateTimeOffset.UtcNow;
        var options = r.State.Options;
        TimeSpan uptime;
        bool online;
        lock (r.State.Lock)
        {
            uptime = r.State.Uptime(now);
            online = r.State.GetConfiguration("general", "online") == "1";
        }

        var local = r.Context.Connection.LocalIpAddress?.MapToIPv4().ToString() ?? "127.0.0.1";
        return Json(new JsonObject
        {
            ["serial"] = options.Serial,
            ["version"] = options.Firmware,
            ["device_id"] = options.DeviceId.ToString(CultureInfo.InvariantCulture), // texto, como no firmware
            ["time"] = now.ToUnixTimeSeconds(),
            ["online"] = online,
            ["uptime"] = new JsonObject { ["days"] = uptime.Days, ["hours"] = uptime.Hours, ["minutes"] = uptime.Minutes, ["seconds"] = uptime.Seconds },
            ["network"] = new JsonObject
            {
                ["mac"] = "02:00:00:00:00:01",
                ["ip"] = local,
                ["netmask"] = "255.255.255.0",
                ["gateway"] = "0.0.0.0",
                ["web_server_port"] = r.Context.Connection.LocalPort,
                ["ssl_enabled"] = false,
                ["dhcp_enabled"] = false,
            },
        });
    }

    // Como o firmware: um parâmetro inexistente recusa a leitura inteira, com o caminho dele na mensagem (ADR 0007).
    private static Task<IResult> GetConfiguration(Request r)
    {
        var response = new JsonObject();
        lock (r.State.Lock)
        {
            foreach (var (section, fields) in r.Json)
            {
                var values = new JsonObject();
                foreach (var field in (fields as JsonArray ?? new JsonArray()).Select(n => n!.GetValue<string>()))
                {
                    values[field] = r.State.GetConfiguration(section, field)
                        ?? throw new DeviceException($"Node or attribute not found. Node path: config->{section}->param[name={field}]");
                }

                response[section] = values;
            }
        }

        return Json(response);
    }

    private static Task<IResult> SetConfiguration(Request r)
    {
        lock (r.State.Lock)
        {
            foreach (var (section, fields) in r.Json)
            {
                foreach (var (field, value) in fields as JsonObject ?? new JsonObject())
                {
                    r.State.SetConfiguration(section, field, NodeComparer.AsString(value) ?? string.Empty);
                }
            }
        }

        return Json(new JsonObject());
    }

    private static Task<IResult> UserSetImage(Request r)
    {
        var userId = QueryLong(r.Context, "user_id") ?? throw new DeviceException("Missing 'user_id'");
        var timestamp = QueryLong(r.Context, "timestamp") ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (r.Body.Length == 0)
        {
            throw new DeviceException("Empty image");
        }

        lock (r.State.Lock)
        {
            if (!r.State.Objects.Rows("users").Any(u => ObjectStore.TryGetLong(u["id"], out var id) && id == userId))
            {
                throw new DeviceException("User not found");
            }

            r.State.SetImage(userId, r.Body, timestamp);
        }

        return Json(new JsonObject { ["user_id"] = userId, ["success"] = true, ["scores"] = new JsonObject { ["bounds_width"] = 400, ["center_pose_quality"] = 100 } });
    }

    private static Task<IResult> UserGetImage(Request r)
    {
        var userId = QueryLong(r.Context, "user_id") ?? throw new DeviceException("Missing 'user_id'");
        (byte[] Image, long Timestamp)? entry;
        lock (r.State.Lock)
        {
            entry = r.State.GetImage(userId);
        }

        if (entry is not { } image)
        {
            throw new DeviceException("Image not found");
        }

        if (r.Context.Request.Query["get_timestamp"] == "1")
        {
            return Json(new JsonObject { ["timestamp"] = image.Timestamp, ["image"] = Convert.ToBase64String(image.Image) });
        }

        return Task.FromResult(Results.Bytes(image.Image, "image/jpeg"));
    }

    private static Task<IResult> UserListImages(Request r)
    {
        List<(long Id, long Timestamp)> list;
        lock (r.State.Lock)
        {
            list = r.State.UsersWithImage.Select(id => (id, r.State.GetImage(id)!.Value.Timestamp)).ToList();
        }

        if (r.Context.Request.Query["get_timestamp"] == "1")
        {
            return Json(new JsonObject
            {
                ["image_info"] = new JsonArray(list.Select(i => (JsonNode)new JsonObject { ["user_id"] = i.Id, ["timestamp"] = i.Timestamp }).ToArray()),
            });
        }

        return Json(new JsonObject { ["user_ids"] = new JsonArray(list.Select(i => (JsonNode)i.Id).ToArray()) });
    }

    private static Task<IResult> UserDestroyImage(Request r)
    {
        var json = r.Json;
        lock (r.State.Lock)
        {
            if (json["all"]?.GetValue<bool>() == true)
            {
                r.State.DeleteAllImages();
            }
            else if (json["user_id"] is { } one && ObjectStore.TryGetLong(one, out var id))
            {
                r.State.DeleteImage(id);
            }
            else if (json["user_ids"] is JsonArray many)
            {
                foreach (var node in many)
                {
                    if (ObjectStore.TryGetLong(node, out var each))
                    {
                        r.State.DeleteImage(each);
                    }
                }
            }
        }

        return Json(new JsonObject());
    }

    private static Task<IResult> MessageToScreen(Request r)
    {
        lock (r.State.Lock)
        {
            var message = r.Json["message"]?.GetValue<string>();
            r.State.ScreenMessage = string.IsNullOrEmpty(message) ? null : message;
        }

        return Json(new JsonObject());
    }

    private static Task<IResult> ChangeLogin(Request r)
    {
        var json = r.Json;
        lock (r.State.Lock)
        {
            r.State.ChangeLogin(RequiredString(json, "login"), RequiredString(json, "password"));
        }

        return Json(new JsonObject());
    }

    private static Task<IResult> Reboot(Request r)
    {
        lock (r.State.Lock)
        {
            r.State.ExpireSessions();
        }

        return Json(new JsonObject());
    }

    private static Task<IResult> Ok(Request r) => Json(new JsonObject());

    private static Task<IResult> Json(JsonNode node) =>
        Task.FromResult(Results.Text(node.ToJsonString(Wire), "application/json"));

    private static IResult Error(string message, int status) =>
        Results.Text(new JsonObject { ["error"] = message, ["code"] = 1 }.ToJsonString(Wire), "application/json", statusCode: status);

    private static async Task<byte[]> ReadBodyAsync(HttpRequest request)
    {
        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    private static JsonObject ParseObject(byte[] body) =>
        body.Length == 0 ? new JsonObject() : JsonNode.Parse(body) as JsonObject ?? new JsonObject();

    private static string RequiredString(JsonObject json, string name) =>
        json[name]?.GetValue<string>() ?? throw new DeviceException($"Missing '{name}'");

    private static int? Int(JsonNode? node) => ObjectStore.TryGetLong(node, out var value) ? (int)value : null;

    private static long? QueryLong(HttpContext context, string name) =>
        long.TryParse(context.Request.Query[name].FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

    internal static Dictionary<string, string> Parameters(string? text) =>
        (text ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.Split('=', 2))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0], p => p[1], StringComparer.Ordinal);

    private sealed record Request(HttpContext Context, DeviceState State, MonitorDispatcher Monitor, byte[] Body)
    {
        private JsonObject? _json;

        public JsonObject Json => _json ??= ParseObject(Body);
    }
}
