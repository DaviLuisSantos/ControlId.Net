using ControlId.Access;
using Microsoft.Extensions.Options;

namespace ControlId.Dashboard;

/// <summary>Um acesso mostrado na lista.</summary>
public sealed record AccessEntry(DateTimeOffset Time, long? UserId, string UserName, AccessEventType Event, long? PortalId)
{
    /// <summary>Acesso liberado.</summary>
    public bool Granted => Event == AccessEventType.AccessGranted;
}

/// <summary>Estado de uma porta.</summary>
public sealed record DoorView(int Id, bool Open, DateTimeOffset Since);

/// <summary>Foto do estado para a página desenhar (imutável, sem lock).</summary>
public sealed record DashboardSnapshot(
    bool Online,
    DateTimeOffset? LastSeen,
    string? DeviceInfo,
    string? Problem,
    IReadOnlyList<DoorView> Doors,
    IReadOnlyList<AccessEntry> Events,
    IReadOnlyList<KeyValuePair<long, string>> Users,
    int NotificationCount);

/// <summary>
/// Estado em memória do dashboard: últimos acessos, portas, usuários e se o equipamento responde. Quem muda o estado
/// dispara <see cref="Changed"/>, e as páginas abertas se redesenham.
/// </summary>
public sealed class DashboardState(IOptions<DashboardOptions> options)
{
    private readonly object _lock = new();
    private readonly List<AccessEntry> _events = new();
    private readonly Dictionary<int, DoorView> _doors = new() { [1] = new DoorView(1, false, DateTimeOffset.UtcNow) };
    private readonly Dictionary<long, string> _users = new();
    private bool _online;
    private DateTimeOffset? _lastSeen;
    private string? _deviceInfo;
    private string? _problem;
    private int _notifications;

    /// <summary>Disparado depois de qualquer mudança (em qualquer thread).</summary>
    public event Action? Changed;

    /// <summary>Copia o estado atual.</summary>
    public DashboardSnapshot Snapshot()
    {
        lock (_lock)
        {
            return new DashboardSnapshot(
                _online,
                _lastSeen,
                _deviceInfo,
                _problem,
                _doors.Values.OrderBy(d => d.Id).ToList(),
                _events.ToList(),
                _users.OrderBy(u => u.Value, StringComparer.CurrentCultureIgnoreCase).ToList(),
                _notifications);
        }
    }

    /// <summary>Nome do usuário, ou <c>null</c> se ainda não foi carregado.</summary>
    public string? FindUserName(long userId)
    {
        lock (_lock)
        {
            return _users.GetValueOrDefault(userId);
        }
    }

    /// <summary>Substitui a lista de usuários conhecidos.</summary>
    public void SetUsers(IEnumerable<KeyValuePair<long, string>> users)
    {
        lock (_lock)
        {
            _users.Clear();
            foreach (var (id, name) in users)
            {
                _users[id] = name;
            }
        }

        Changed?.Invoke();
    }

    /// <summary>Acrescenta um acesso no topo da lista.</summary>
    public void AddEvent(AccessEntry entry)
    {
        lock (_lock)
        {
            _events.Insert(0, entry);
            var max = Math.Max(1, options.Value.MaxEvents);
            if (_events.Count > max)
            {
                _events.RemoveRange(max, _events.Count - max);
            }
        }

        Changed?.Invoke();
    }

    /// <summary>Marca a notificação recebida (o equipamento está falando com o dashboard).</summary>
    public void NotificationReceived()
    {
        lock (_lock)
        {
            _notifications++;
            _lastSeen = DateTimeOffset.UtcNow;
            _online = true;
            _problem = null;
        }

        Changed?.Invoke();
    }

    /// <summary>O equipamento respondeu à consulta.</summary>
    public void SetOnline(string deviceInfo)
    {
        lock (_lock)
        {
            _online = true;
            _lastSeen = DateTimeOffset.UtcNow;
            _deviceInfo = deviceInfo;
            _problem = null;
        }

        Changed?.Invoke();
    }

    /// <summary>O equipamento não respondeu.</summary>
    public void SetOffline(string problem)
    {
        lock (_lock)
        {
            _online = false;
            _problem = problem;
        }

        Changed?.Invoke();
    }

    /// <summary>Atualiza a porta. Se abriu, volta a "fechada" sozinha depois de <see cref="DashboardOptions.DoorFallbackSeconds"/>.</summary>
    public void SetDoor(int id, bool open)
    {
        DateTimeOffset since;
        lock (_lock)
        {
            since = DateTimeOffset.UtcNow;
            _doors[id] = new DoorView(id, open, since);
        }

        Changed?.Invoke();

        if (open)
        {
            _ = CloseLaterAsync(id, since);
        }
    }

    private async Task CloseLaterAsync(int id, DateTimeOffset since)
    {
        await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, options.Value.DoorFallbackSeconds)));
        lock (_lock)
        {
            // Outra notificação chegou nesse meio-tempo: ela manda.
            if (_doors.TryGetValue(id, out var current) && current.Since != since)
            {
                return;
            }

            _doors[id] = new DoorView(id, false, DateTimeOffset.UtcNow);
        }

        Changed?.Invoke();
    }
}
