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
        _doorOpenUntil = new DateTimeOffset?[_doorOpenUntil.Length];
        _login = _options.Login;
        _password = _options.Password;
        ScreenMessage = null;

        SetConfiguration("general", "language", "pt_BR");
        SetConfiguration("general", "beep_enabled", "1");
        SetConfiguration("monitor", "hostname", string.Empty);
        SetConfiguration("monitor", "port", string.Empty);
        SetConfiguration("monitor", "path", "api/notifications");
        SetConfiguration("monitor", "request_timeout", "5000");

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
