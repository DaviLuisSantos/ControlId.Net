using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;

namespace ControlId.Simulator;

/// <summary>
/// Envia as notificações do Monitor como o equipamento: <c>POST http://{hostname}:{port}/{path}/{endpoint}</c>, com o
/// destino lido da seção <c>monitor</c> da configuração no momento do envio. Sem <c>hostname</c>, nada é enviado.
/// </summary>
public sealed class MonitorDispatcher(DeviceState state, IHttpClientFactory httpClientFactory, ILogger<MonitorDispatcher> logger)
    : BackgroundService
{
    private readonly Channel<(string Endpoint, JsonObject Body)> _queue =
        Channel.CreateBounded<(string, JsonObject)>(new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest });

    private readonly Queue<MonitorDelivery> _history = new();

    /// <summary>Últimas entregas (até 50), para inspeção em <c>/simulator/state</c>.</summary>
    public IReadOnlyList<MonitorDelivery> History
    {
        get
        {
            lock (_history)
            {
                return _history.ToList();
            }
        }
    }

    /// <summary>Enfileira uma notificação. Campos <c>device_id</c> e <c>time</c> são preenchidos aqui.</summary>
    public void Publish(string endpoint, JsonObject body)
    {
        body["device_id"] ??= state.Options.DeviceId;
        body["time"] ??= DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        _queue.Writer.TryWrite((endpoint, body));
    }

    /// <summary>Notificação <c>dao</c> de objetos inseridos, com os valores em texto, como o firmware manda.</summary>
    public void PublishInserted(string table, IEnumerable<JsonObject> rows)
    {
        var changes = new JsonArray();
        foreach (var row in rows)
        {
            var values = new JsonObject();
            foreach (var (key, value) in row)
            {
                values[key] = value is JsonValue v && v.TryGetValue(out string? s) ? s : value?.ToJsonString();
            }

            changes.Add(new JsonObject { ["object"] = table, ["type"] = "inserted", ["values"] = values });
        }

        if (changes.Count > 0)
        {
            Publish("dao", new JsonObject { ["object_changes"] = changes });
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var http = httpClientFactory.CreateClient(nameof(MonitorDispatcher));
        await foreach (var (endpoint, body) in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            Uri? target;
            lock (state.Lock)
            {
                target = Target(endpoint);
            }

            if (target is null)
            {
                continue;
            }

            int? status = null;
            string? error = null;
            try
            {
                using var response = await http.PostAsJsonAsync(target, body, stoppingToken);
                status = (int)response.StatusCode;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !stoppingToken.IsCancellationRequested)
            {
                error = ex.Message;
                logger.LogWarning("Monitor: falha ao enviar {Endpoint} para {Target}: {Error}", endpoint, target, ex.Message);
            }

            lock (_history)
            {
                _history.Enqueue(new MonitorDelivery(DateTimeOffset.UtcNow, endpoint, target.ToString(), status, error));
                while (_history.Count > 50)
                {
                    _history.Dequeue();
                }
            }
        }
    }

    private Uri? Target(string endpoint)
    {
        var host = state.GetConfiguration("monitor", "hostname");
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        var port = state.GetConfiguration("monitor", "port");
        var path = (state.GetConfiguration("monitor", "path") ?? string.Empty).Trim('/');
        var authority = string.IsNullOrWhiteSpace(port) ? host : string.Create(CultureInfo.InvariantCulture, $"{host}:{port}");
        var url = path.Length > 0 ? $"http://{authority}/{path}/{endpoint}" : $"http://{authority}/{endpoint}";
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri : null;
    }
}

/// <summary>Uma tentativa de entrega do Monitor.</summary>
public sealed record MonitorDelivery(DateTimeOffset At, string Endpoint, string Target, int? StatusCode, string? Error);
