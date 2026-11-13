using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace ControlId.Simulator;

/// <summary>Estado do REP simulado: sessões, usuários, relógio e o AFD. Todo acesso passa pelo <see cref="Lock"/>.</summary>
public sealed class RepState
{
    private readonly SimulatorOptions _options;
    private readonly HashSet<string> _sessions = new(StringComparer.Ordinal);
    private readonly List<JsonObject> _users = new();
    private readonly List<AfdEntry> _afd = new();
    private readonly RepIdentity _identity;
    private readonly DateTimeOffset _bootTime = DateTimeOffset.UtcNow;
    private TimeSpan _clockDelta = TimeSpan.Zero;
    private TimeSpan _zone = TimeSpan.FromHours(-3);
    private string _login;
    private string _password;
    private long _nsr;

    public RepState(IOptions<SimulatorOptions> options)
    {
        _options = options.Value;
        _login = _options.Login;
        _password = _options.Password;
        _identity = new RepIdentity(
            EmployerId: "12345678000195",
            EmployerName: "Empresa Simulada Ltda",
            Location: "Simulador ControlId.Net",
            RepIdentifier: _options.Serial.PadLeft(17, '0'),
            ManufacturerId: "73010454000110",
            Model: "iDClass Simulador");

        var now = Now;
        Append(2, now, AfdWriter.Company(NextNsr(), now, _identity));
        if (_options.SeedSampleData)
        {
            Seed("Ana Souza", 11144477735, "1001");
            Seed("Bruno Lima", 52998224725, "1002");
            Seed("Carla Dias", 39053344705, "1003");
        }
    }

    /// <summary>Lock de todo o estado.</summary>
    public object Lock { get; } = new();

    public SimulatorOptions Options => _options;

    /// <summary>Horário local do REP (relógio do sistema mais o ajuste feito em <c>set_system_date_time</c>) com o fuso configurado.</summary>
    public DateTimeOffset Now => (DateTimeOffset.UtcNow + _clockDelta).ToOffset(_zone);

    public TimeSpan Zone => _zone;

    public long LastNsr => _nsr;

    public IReadOnlyList<JsonObject> Users => _users;

    public TimeSpan Uptime => DateTimeOffset.UtcNow - _bootTime;

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

    public void ChangeLogin(string login, string password)
    {
        _login = login;
        _password = password;
    }

    public void ExpireSessions() => _sessions.Clear();

    /// <summary>Identificador do usuário: CPF (modo 671) ou PIS (legado).</summary>
    public static long? KeyOf(JsonObject user, bool cpfMode) => ObjectStore.TryGetLong(user[cpfMode ? "cpf" : "pis"], out var key) ? key : null;

    public JsonObject? Find(long key, bool cpfMode) => _users.FirstOrDefault(u => KeyOf(u, cpfMode) == key);

    public void AddUser(JsonObject user, bool cpfMode)
    {
        var key = KeyOf(user, cpfMode) ?? throw new DeviceException($"Missing '{(cpfMode ? "cpf" : "pis")}'");
        if (Find(key, cpfMode) is not null)
        {
            throw new DeviceException($"User already exists: {key}");
        }

        _users.Add(user);
        AppendEmployee('I', user);
    }

    public void UpdateUser(JsonObject change, bool cpfMode)
    {
        var key = KeyOf(change, cpfMode) ?? throw new DeviceException($"Missing '{(cpfMode ? "cpf" : "pis")}'");
        var user = Find(key, cpfMode) ?? throw new DeviceException($"User not found: {key}");

        foreach (var (name, value) in change)
        {
            switch (name)
            {
                case "pis" or "cpf" or "new_pis" or "new_cpf" or "remove_templates" or "remove_faces":
                    break;
                default:
                    user[name] = value?.DeepClone();
                    break;
            }
        }

        if (ObjectStore.TryGetLong(change[cpfMode ? "new_cpf" : "new_pis"], out var newKey))
        {
            user[cpfMode ? "cpf" : "pis"] = newKey;
        }

        if (change["remove_templates"]?.GetValue<bool>() == true)
        {
            user.Remove("templates");
        }

        if (change["remove_faces"]?.GetValue<bool>() == true)
        {
            user.Remove("face");
        }

        AppendEmployee('A', user);
    }

    /// <summary>Remove os usuários (os que não existem são ignorados, como no equipamento) e devolve quantos saíram.</summary>
    public int RemoveUsers(IEnumerable<long> keys, bool cpfMode)
    {
        var removed = 0;
        foreach (var key in keys)
        {
            if (Find(key, cpfMode) is { } user)
            {
                _users.Remove(user);
                AppendEmployee('E', user);
                removed++;
            }
        }

        return removed;
    }

    /// <summary>Ajusta o relógio. Sem <paramref name="zone"/>, o fuso atual é mantido. Grava o registro tipo 4.</summary>
    public void SetClock(DateTime local, TimeSpan? zone)
    {
        var before = Now;
        var newZone = zone ?? _zone;
        _clockDelta = new DateTimeOffset(local, newZone).ToUniversalTime() - DateTimeOffset.UtcNow;
        _zone = newZone;
        var after = Now;
        Append(4, after, AfdWriter.ClockAdjustment(NextNsr(), before, after));
    }

    /// <summary>Grava uma marcação (tipo 3) para um CPF cadastrado.</summary>
    public (long Nsr, DateTimeOffset Time) Punch(long cpf)
    {
        if (Find(cpf, cpfMode: true) is null)
        {
            throw new DeviceException($"User not found: {cpf}");
        }

        var at = Now;
        var nsr = NextNsr();
        Append(3, at, AfdWriter.Punch(nsr, at, cpf));
        return (nsr, at);
    }

    /// <summary>
    /// AFD a partir de um NSR ou de uma data. Com <paramref name="headerTop"/> o cabeçalho vem primeiro; sem ele, a ordem é
    /// a do firmware antigo (corpo, trailer, assinatura e cabeçalho; a linha com o nome do arquivo não é gerada).
    /// </summary>
    public byte[] BuildAfd(long? initialNsr, DateTime? initialDate, bool headerTop)
    {
        var entries = _afd
            .Where(e => (initialNsr is null || e.Nsr >= initialNsr) && (initialDate is null || e.Time.Date >= initialDate.Value.Date))
            .ToList();

        var now = Now;
        var start = entries.Count > 0 ? entries.Min(e => e.Time.Date) : now.Date;
        var end = entries.Count > 0 ? entries.Max(e => e.Time.Date) : now.Date;
        var header = AfdWriter.Header(_identity, start, end, now);
        var trailer = AfdWriter.Trailer(
            entries.Count(e => e.Type == 2), entries.Count(e => e.Type == 3), entries.Count(e => e.Type == 4), entries.Count(e => e.Type == 5));

        var body = entries.Select(e => e.Line).ToList();
        var signature = "SIM" + Convert.ToBase64String(SHA256.HashData(Encoding.Latin1.GetBytes(string.Join('\n', body))));

        var lines = headerTop
            ? new[] { header }.Concat(body).Append(trailer).Append(signature)
            : body.Append(trailer).Append(signature).Append(header);

        return Encoding.Latin1.GetBytes(string.Join("\r\n", lines) + "\r\n");
    }

    private void Seed(string name, long cpf, string registration)
    {
        var user = new JsonObject { ["cpf"] = cpf, ["name"] = name, ["registration"] = registration, ["admin"] = false };
        _users.Add(user);
        AppendEmployee('I', user);
    }

    private void AppendEmployee(char operation, JsonObject user)
    {
        var id = ObjectStore.TryGetLong(user["cpf"], out var cpf) ? cpf : ObjectStore.TryGetLong(user["pis"], out var pis) ? pis : 0;
        var at = Now;
        Append(5, at, AfdWriter.Employee(NextNsr(), at, operation, id, user["name"]?.GetValue<string>() ?? string.Empty));
    }

    private long NextNsr() => ++_nsr;

    // O NSR do registro é o que acabou de sair de NextNsr (o argumento da linha é avaliado antes de Append).
    private void Append(int type, DateTimeOffset at, string line) => _afd.Add(new AfdEntry(_nsr, type, at, line));

    private sealed record AfdEntry(long Nsr, int Type, DateTimeOffset Time, string Line);
}
