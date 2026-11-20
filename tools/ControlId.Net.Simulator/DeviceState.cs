using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace ControlId.Simulator;

/// <summary>Estado do equipamento simulado. Todo acesso passa pelo <see cref="Lock"/>.</summary>
public sealed class DeviceState
{
    private readonly SimulatorOptions _options;
    private readonly HashSet<string> _sessions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, string>> _configuration = new(StringComparer.Ordinal);
    private readonly Dictionary<long, (byte[] Image, long Timestamp)> _images = new();
    private readonly DateTimeOffset _bootTime = DateTimeOffset.UtcNow;
    private DateTimeOffset?[] _doorOpenUntil;
    private string _login;
    private string _password;

    public DeviceState(IOptions<SimulatorOptions> options)
    {
        _options = options.Value;
        _login = _options.Login;
        _password = _options.Password;
        _doorOpenUntil = new DateTimeOffset?[Math.Clamp(_options.Doors, 1, 4)];
        Reset();
    }

    private readonly Dictionary<string, long> _requests = new(StringComparer.Ordinal);

    /// <summary>Requisições recebidas por comando (sem o <c>.fcgi</c>) desde o início ou o último <see cref="Reset"/>.</summary>
    public IReadOnlyDictionary<string, long> Requests => _requests;

    /// <summary>Conta uma requisição ao comando. Chamar com o <see cref="Lock"/>.</summary>
    public void CountRequest(string command) => _requests[command] = _requests.GetValueOrDefault(command) + 1;

    /// <summary>Lock de todo o estado.</summary>
    public object Lock { get; } = new();

    public ObjectStore Objects { get; } = new();

    public SimulatorOptions Options => _options;

    /// <summary>Última mensagem enviada à tela (<c>message_to_screen</c>).</summary>
    public string? ScreenMessage { get; set; }

    public string? TryLogin(string? login, string? password)
    {
        if (!string.Equals(login, _login, StringComparison.Ordinal) || !string.Equals(password, _password, StringComparison.Ordinal))
        {
            return null;
        }

        var session = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(12));
        _sessions.Add(session);
        return session;
    }

    public bool IsValidSession(string? session) => session is not null && _sessions.Contains(session);

    public void Logout(string? session)
    {
        if (session is not null)
        {
            _sessions.Remove(session);
        }
    }

    /// <summary>Troca as credenciais. As sessões abertas continuam valendo, como no equipamento.</summary>
    public void ChangeLogin(string login, string password)
    {
        _login = login;
        _password = password;
    }

    /// <summary>Encerra todas as sessões (como um reinício do equipamento).</summary>
    public void ExpireSessions() => _sessions.Clear();

    public string? GetConfiguration(string section, string field) =>
        _configuration.TryGetValue(section, out var fields) && fields.TryGetValue(field, out var value) ? value : null;

    public void SetConfiguration(string section, string field, string value)
    {
        if (!_configuration.TryGetValue(section, out var fields))
        {
            fields = new Dictionary<string, string>(StringComparer.Ordinal);
            _configuration[section] = fields;
        }

        fields[field] = value;
    }

    public void SetImage(long userId, byte[] image, long timestamp) => _images[userId] = (image, timestamp);

    public (byte[] Image, long Timestamp)? GetImage(long userId) => _images.TryGetValue(userId, out var entry) ? entry : null;

    public IEnumerable<long> UsersWithImage => _images.Keys.Order();

    public bool DeleteImage(long userId) => _images.Remove(userId);

    public void DeleteAllImages() => _images.Clear();

    private readonly List<JsonObject> _customTables = new();
    private long _nextCustomId = 1;

    /// <summary>Tabelas customizadas (<c>object_add</c>), com seus campos; cada uma é <c>{id, object, name, fields[]}</c>.</summary>
    public IReadOnlyList<JsonObject> CustomTables => _customTables;

    public long AddCustomTable(string table, string name, long? id, IEnumerable<JsonObject> fields)
    {
        if (_customTables.Any(t => t["object"]!.GetValue<string>() == table))
        {
            throw new DeviceException($"Object already exists: {table}");
        }

        var tableId = id ?? _nextCustomId++;
        var list = new JsonArray();
        foreach (var field in fields)
        {
            field["id"] = _nextCustomId++;
            list.Add(field);
        }

        _customTables.Add(new JsonObject { ["id"] = tableId, ["object"] = table, ["name"] = name, ["fields"] = list });
        return tableId;
    }

    public void RemoveCustomTables(IEnumerable<long> ids)
    {
        var set = ids.ToHashSet();
        _customTables.RemoveAll(t => set.Contains(t["id"]!.GetValue<long>()));
    }

    public long AddCustomField(string table, JsonObject field)
    {
        var owner = _customTables.FirstOrDefault(t => t["object"]!.GetValue<string>() == table)
            ?? throw new DeviceException($"Object not found: {table}");
        field["id"] = _nextCustomId;
        ((JsonArray)owner["fields"]!).Add(field);
        return _nextCustomId++;
    }

    public void UpdateCustomField(long id, JsonObject values)
    {
        foreach (var table in _customTables)
        {
            foreach (var field in ((JsonArray)table["fields"]!).OfType<JsonObject>())
            {
                if (field["id"]!.GetValue<long>() == id)
                {
                    foreach (var (key, value) in values)
                    {
                        if (key != "id")
                        {
                            field[key] = value?.DeepClone();
                        }
                    }

                    return;
                }
            }
        }

        throw new DeviceException($"Field not found: {id}");
    }

    public List<long> RemoveCustomFields(IEnumerable<long> ids)
    {
        var removed = new List<long>();
        foreach (var id in ids)
        {
            foreach (var table in _customTables)
            {
                var fields = (JsonArray)table["fields"]!;
                var hit = fields.OfType<JsonObject>().FirstOrDefault(f => f["id"]!.GetValue<long>() == id);
                if (hit is not null)
                {
                    fields.Remove(hit);
                    removed.Add(id);
                    break;
                }
            }
        }

        return removed;
    }

    /// <summary>Imagens do slideshow da tela (<c>send_slideshow_images</c>), por nome de arquivo.</summary>
    public Dictionary<string, byte[]> Slideshow { get; } = new(StringComparer.Ordinal);

    /// <summary>Ícones do quiosque (<c>set_kiosk_icon</c>), por nome de arquivo.</summary>
    public Dictionary<string, byte[]> KioskIcons { get; } = new(StringComparer.Ordinal);

    /// <summary>Ícone do telefone (<c>set_phone_icon</c>); <c>null</c> sem ícone.</summary>
    public byte[]? PhoneIcon { get; set; }

    /// <summary>Logotipo do streaming (<c>set_streaming_logo</c>); <c>null</c> sem logotipo.</summary>
    public byte[]? StreamingLogo { get; set; }

    public int DoorCount => _doorOpenUntil.Length;

    public bool IsDoorOpen(int door, DateTimeOffset now) =>
        door >= 1 && door <= _doorOpenUntil.Length && _doorOpenUntil[door - 1] is { } until && until > now;

    public void OpenDoor(int door, DateTimeOffset now)
    {
        if (door < 1 || door > _doorOpenUntil.Length)
        {
            throw new DeviceException($"Invalid door: {door}");
        }

        _doorOpenUntil[door - 1] = now + _options.DoorOpenTime;
    }

    public TimeSpan Uptime(DateTimeOffset now) => now - _bootTime;

    /// <summary>Volta ao estado de fábrica (mais os dados de exemplo, se configurados).</summary>
    public void Reset()
    {
        Objects.Clear();
        _sessions.Clear();
        _configuration.Clear();
        _images.Clear();
        _customTables.Clear();
        _requests.Clear();
        _nextCustomId = 1;
        _doorOpenUntil = new DateTimeOffset?[_doorOpenUntil.Length];
        _login = _options.Login;
        _password = _options.Password;
        ScreenMessage = null;
        Slideshow.Clear();
        KioskIcons.Clear();
        PhoneIcon = null;
        StreamingLogo = null;

        SetConfiguration("general", "language", "pt_BR");
        SetConfiguration("general", "beep_enabled", "1");
        SetConfiguration("monitor", "hostname", string.Empty);
        SetConfiguration("monitor", "port", string.Empty);
        SetConfiguration("monitor", "path", "api/notifications");
        SetConfiguration("monitor", "request_timeout", "5000");
        SetConfiguration("general", "online", "0");
        SetConfiguration("general", "local_identification", "1");
        SetConfiguration("push_server", "push_remote_address", string.Empty);
        SetConfiguration("push_server", "push_request_timeout", "5000");
        SetConfiguration("push_server", "push_request_period", "5");
        SetConfiguration("online_client", "server_id", "-1");
        SetConfiguration("online_client", "extract_template", "0");
        SetConfiguration("online_client", "contingency_enabled", "1");
        SetConfiguration("online_client", "max_request_attempts", "3");
        SetConfiguration("online_client", "request_timeout", "5000");
        SetConfiguration("online_client", "alive_interval", "5000");

        for (var door = 1; door <= _doorOpenUntil.Length; door++)
        {
            Objects.Insert("portals", new[] { new JsonObject { ["id"] = door, ["name"] = $"Porta {door}" } });
        }

        if (_options.SeedSampleData)
        {
            Objects.Insert("users", new[]
            {
                new JsonObject { ["id"] = 1, ["name"] = "Ana Souza", ["registration"] = "1001" },
                new JsonObject { ["id"] = 2, ["name"] = "Bruno Lima", ["registration"] = "1002" },
                new JsonObject { ["id"] = 3, ["name"] = "Carla Dias", ["registration"] = "1003" },
            });
            Objects.Insert("cards", new[]
            {
                new JsonObject { ["user_id"] = 1, ["value"] = 4194301001L },
                new JsonObject { ["user_id"] = 2, ["value"] = 4194301002L },
            });
        }
    }
}
