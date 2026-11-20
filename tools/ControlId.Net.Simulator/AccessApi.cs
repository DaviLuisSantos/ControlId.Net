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
        ["face_template_match"] = FaceTemplateMatch,
        ["face_template_count_distinct"] = FaceTemplateCount,
        ["template_match"] = TemplateMatch,
        ["io_state"] = IoState,
        ["is_identifying"] = _ => Json(new JsonObject { ["is_identifying"] = false }),
        ["get_catra_info"] = _ => Json(new JsonObject { ["left_turns"] = 0, ["right_turns"] = 0, ["entrance_turns"] = 0, ["exit_turns"] = 0, ["total_turns"] = 0 }),
        ["get_last_snapshot"] = _ => Json(new JsonObject { ["image_base64"] = Convert.ToBase64String(new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 }) }),
        ["device_rfid_module"] = _ => Json(new JsonObject { ["module"] = "MIFARE" }),
        ["get_hid_last_card"] = _ => Json(new JsonObject { ["csn"] = "0A1B2C3D", ["pac"] = "00112233", ["type"] = "iCLASS", ["bits"] = 26, ["custom"] = "0" }),
        ["get_wiegand_card_read"] = _ => Json(new JsonObject
        {
            ["card_read_value"] = "12345678",
            ["card_read_num_bits"] = 26,
            ["facility_code"] = "10",
            ["card_number"] = "5678",
            ["parities"] = 3,
            ["last_read_time"] = "2026-01-01 00:00:00",
        }),
        ["object_metadata"] = ObjectMetadata,
        ["object_add"] = ObjectAdd,
        ["object_remove"] = ObjectRemove,
        ["object_add_field"] = ObjectAddField,
        ["object_update_field"] = ObjectUpdateField,
        ["object_remove_fields"] = ObjectRemoveFields,
        ["export_object"] = ExportObject,
        ["export_custom_tables_metadata"] = ExportCustomTablesMetadata,
        ["get_ntp_server"] = GetNtpServer,
        ["set_ntp_server"] = SetNtpServer,
        ["get_ntp_server_status"] = _ => Json(new JsonObject { ["server_status"] = true }),
        ["check_port_availability"] = _ => Json(new JsonObject { ["status"] = "OK" }),
        ["get_vpn_ip"] = _ => Json(new JsonObject { ["ip"] = "10.8.0.2" }),
        ["get_ssl_certificate"] = _ => Text("-----BEGIN CERTIFICATE-----\nSIMULADOR\n-----END CERTIFICATE-----\n"),
        ["get_802_1X_status"] = _ => Json(new JsonObject { ["status"] = 11 }),
        ["get_ac_log"] = _ => Text("ac: simulador iniciado\n"),
        ["get_openvpn_log"] = _ => Text("openvpn: inativo\n"),
        ["get_wpa_log"] = _ => Text("wpa: inativo\n"),
        ["get_wpasupplicant_log"] = _ => Text("wpa_supplicant: inativo\n"),
        ["master_password"] = MasterPassword,
        ["recovery_password"] = RecoveryPassword,
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

    private static Task<IResult> FaceTemplateMatch(Request r)
    {
        _ = RequiredString(r.Json, "face_template");
        long? userId = r.Json["user_id"] is { } node && ObjectStore.TryGetLong(node, out var id) ? id : null;
        lock (r.State.Lock)
        {
            if (userId is { } wanted && r.State.Objects.Rows("users").Any(u => ObjectStore.TryGetLong(u["id"], out var each) && each == wanted))
            {
                return Json(new JsonObject
                {
                    ["match"] = true,
                    ["info"] = new JsonObject { ["match_user_id"] = wanted, ["match_score"] = 900, ["match_similarity"] = 95 },
                });
            }
        }

        return Json(new JsonObject { ["match"] = false });
    }

    private static Task<IResult> FaceTemplateCount(Request r)
    {
        int count;
        lock (r.State.Lock)
        {
            count = r.State.UsersWithImage.Count();
        }

        return Json(new JsonObject { ["count"] = count });
    }

    private static Task<IResult> TemplateMatch(Request r)
    {
        if (r.Body.Length == 0)
        {
            throw new DeviceException("Empty template");
        }

        return Json(new JsonObject { ["template"] = Convert.ToBase64String(r.Body) });
    }

    private static Task<IResult> IoState(Request r) =>
        Json(new JsonObject
        {
            ["gpios"] = new JsonArray(new JsonObject
            {
                ["index"] = 0,
                ["role"] = "door_sensor",
                ["enabled"] = true,
                ["available"] = true,
                ["in"] = true,
                ["value"] = 0,
                ["idle"] = 0,
                ["active"] = false,
            }),
            ["relays"] = new JsonArray(new JsonObject
            {
                ["id"] = 1,
                ["gpio"] = 0,
                ["enabled"] = true,
                ["timeout_ms"] = 3000,
                ["auto_close"] = true,
                ["output_mode"] = 0,
                ["available"] = true,
                ["active"] = false,
            }),
            ["sec_boxes"] = new JsonArray(),
        });

    private static Task<IResult> ObjectMetadata(Request r)
    {
        var filter = r.Json["filter"]?.GetValue<string>();
        var result = new JsonObject();
        lock (r.State.Lock)
        {
            if (filter != "custom")
            {
                result["users"] = new JsonObject
                {
                    ["fields"] = new JsonObject
                    {
                        ["id"] = new JsonObject { ["type"] = "BIGINT", ["primary key"] = true, ["not null"] = true },
                        ["name"] = new JsonObject { ["type"] = "TEXT" },
                    },
                    ["joins"] = new JsonObject(),
                };
            }

            if (filter != "default")
            {
                foreach (var table in r.State.CustomTables)
                {
                    var fields = new JsonObject { ["id"] = new JsonObject { ["type"] = "BIGINT", ["primary key"] = true, ["not null"] = true } };
                    foreach (var field in ((JsonArray)table["fields"]!).OfType<JsonObject>())
                    {
                        fields[field["column_name"]!.GetValue<string>()] = new JsonObject
                        {
                            ["type"] = field["type"]?.GetValue<string>() ?? "TEXT",
                            ["not null"] = field["constraint"]?.GetValue<string>() == "NOT_NULL",
                            ["unique"] = field["unique"]?.GetValue<bool>() ?? false,
                        };
                    }

                    result[table["object"]!.GetValue<string>()] = new JsonObject { ["fields"] = fields, ["joins"] = new JsonObject() };
                }
            }
        }

        return Json(result);
    }

    private static Task<IResult> ObjectAdd(Request r)
    {
        var json = r.Json;
        var fields = (json["fields"] as JsonArray ?? throw new DeviceException("Missing 'fields'"))
            .Select(f => f as JsonObject ?? throw new DeviceException("Invalid field"))
            .Select(f => (JsonObject)f.DeepClone())
            .ToList();
        long? given = json["id"] is { } n && ObjectStore.TryGetLong(n, out var value) ? value : null;
        long id;
        lock (r.State.Lock)
        {
            id = r.State.AddCustomTable(RequiredString(json, "object"), RequiredString(json, "name"), given, fields);
        }

        return Json(new JsonObject { ["ids"] = new JsonArray((JsonNode)id) });
    }

    private static Task<IResult> ObjectRemove(Request r)
    {
        lock (r.State.Lock)
        {
            r.State.RemoveCustomTables(Ids(r.Json));
        }

        return Json(new JsonObject());
    }

    private static Task<IResult> ObjectAddField(Request r)
    {
        var json = r.Json;
        var table = RequiredString(json, "object");
        var field = (JsonObject)json.DeepClone();
        field.Remove("object");
        long id;
        lock (r.State.Lock)
        {
            id = r.State.AddCustomField(table, field);
        }

        return Json(new JsonObject { ["ids"] = new JsonArray((JsonNode)id) });
    }

    private static Task<IResult> ObjectUpdateField(Request r)
    {
        var json = r.Json;
        var id = json["id"] is { } n && ObjectStore.TryGetLong(n, out var value) ? value : throw new DeviceException("Missing 'id'");
        lock (r.State.Lock)
        {
            r.State.UpdateCustomField(id, json);
        }

        return Json(new JsonObject());
    }

    private static Task<IResult> ObjectRemoveFields(Request r)
    {
        List<long> removed;
        lock (r.State.Lock)
        {
            removed = r.State.RemoveCustomFields(Ids(r.Json));
        }

        return Json(new JsonObject { ["ids"] = new JsonArray(removed.Select(i => (JsonNode)i).ToArray()) });
    }

    private static Task<IResult> ExportObject(Request r)
    {
        var table = RequiredString(r.Json, "object");
        var requested = r.Json["columns"] is JsonArray c ? c.Select(n => n!.GetValue<string>()).ToList() : null;
        List<JsonObject> rows;
        lock (r.State.Lock)
        {
            rows = r.State.Objects.Rows(table).Select(x => (JsonObject)x.DeepClone()).ToList();
        }

        var columns = requested ?? rows.SelectMany(x => x.Select(p => p.Key)).Distinct().ToList();
        var csv = new System.Text.StringBuilder().AppendLine(string.Join(",", columns));
        foreach (var row in rows)
        {
            csv.AppendLine(string.Join(",", columns.Select(col => Csv(NodeComparer.AsString(row[col])))));
        }

        return Task.FromResult(Results.Bytes(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv"));
    }

    private static Task<IResult> ExportCustomTablesMetadata(Request r)
    {
        var csv = new System.Text.StringBuilder().AppendLine("object,name,column_name,type");
        lock (r.State.Lock)
        {
            foreach (var table in r.State.CustomTables)
            {
                foreach (var field in ((JsonArray)table["fields"]!).OfType<JsonObject>())
                {
                    csv.AppendLine(string.Join(",", Csv(table["object"]!.GetValue<string>()), Csv(table["name"]!.GetValue<string>()), Csv(field["column_name"]?.GetValue<string>()), Csv(field["type"]?.GetValue<string>())));
                }
            }
        }

        return Task.FromResult(Results.Bytes(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv"));
    }

    private static Task<IResult> GetNtpServer(Request r)
    {
        lock (r.State.Lock)
        {
            return Json(new JsonObject
            {
                ["server1"] = r.State.GetConfiguration("ntp", "server1") ?? "pool.ntp.org",
                ["server2"] = r.State.GetConfiguration("ntp", "server2") ?? string.Empty,
            });
        }
    }

    private static Task<IResult> SetNtpServer(Request r)
    {
        var server1 = RequiredString(r.Json, "server1");
        lock (r.State.Lock)
        {
            r.State.SetConfiguration("ntp", "server1", server1);
            r.State.SetConfiguration("ntp", "server2", r.Json["server2"]?.GetValue<string>() ?? string.Empty);
        }

        return Json(new JsonObject());
    }

    // Sem "password": consulta hash e sal; com "password": grava (o simulador guarda só um marcador).
    private static Task<IResult> MasterPassword(Request r)
    {
        lock (r.State.Lock)
        {
            if (r.Json["password"]?.GetValue<string>() is { Length: > 0 })
            {
                r.State.SetConfiguration("master_password", "set", "1");
                return Json(new JsonObject());
            }

            return Json(new JsonObject { ["hash"] = "00000000000000000000000000000000", ["salt"] = "simulador" });
        }
    }

    private static Task<IResult> RecoveryPassword(Request r)
    {
        lock (r.State.Lock)
        {
            if (r.Json["password"]?.GetValue<string>() is { Length: > 0 })
            {
                r.State.SetConfiguration("recovery", "enabled", "1");
                return Json(new JsonObject());
            }

            if (r.Json["enabled"] is { } flag)
            {
                r.State.SetConfiguration("recovery", "enabled", flag.GetValue<bool>() ? "1" : "0");
                return Json(new JsonObject());
            }

            return Json(new JsonObject { ["enabled"] = r.State.GetConfiguration("recovery", "enabled") == "1" });
        }
    }

    private static List<long> Ids(JsonObject json) =>
        (json["ids"] as JsonArray ?? throw new DeviceException("Missing 'ids'"))
            .Select(n => ObjectStore.TryGetLong(n, out var id) ? id : throw new DeviceException("Invalid id"))
            .ToList();

    private static string Csv(string? value) =>
        value is not null && value.IndexOfAny(new[] { ',', '"', '\n' }) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value ?? string.Empty;

    private static Task<IResult> Text(string text) =>
        Task.FromResult(Results.Text(text, "text/plain"));

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
