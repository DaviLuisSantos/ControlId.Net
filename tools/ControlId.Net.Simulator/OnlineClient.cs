using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ControlId.Simulator;

/// <summary>
/// O lado do equipamento no modo online (<c>general.online=1</c>): pergunta ao servidor cadastrado em <c>devices</c>
/// (<c>online_client.server_id</c>) se libera cada identificação, e avisa que está vivo a cada <c>alive_interval</c>.
/// Se o servidor não responde depois de <c>max_request_attempts</c> tentativas, quem chama decide com os dados locais
/// (contingência).
/// </summary>
public sealed class OnlineClient(DeviceState state, IHttpClientFactory httpClientFactory, ILogger<OnlineClient> logger) : BackgroundService
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DefaultAlive = TimeSpan.FromSeconds(5);

    /// <summary>Se o modo online está ligado e há um servidor cadastrado.</summary>
    public bool IsEnabled()
    {
        lock (state.Lock)
        {
            return ReadSettings().BaseUrl is not null;
        }
    }

    /// <summary>
    /// Envia a identificação ao servidor (<c>POST {servidor}/{endpoint}.fcgi</c>, form-urlencoded) e devolve o
    /// <c>result</c> da resposta, ou <c>null</c> se o servidor não respondeu (contingência).
    /// </summary>
    /// <param name="endpoint"><c>new_user_identified</c> ou <c>new_card</c>.</param>
    /// <param name="form">Campos do formulário.</param>
    /// <param name="cancellationToken">Cancelamento.</param>
    public async Task<JsonObject?> AuthorizeAsync(string endpoint, IEnumerable<KeyValuePair<string, string>> form, CancellationToken cancellationToken)
    {
        OnlineSettings settings;
        lock (state.Lock)
        {
            settings = ReadSettings();
        }

        if (settings.BaseUrl is null)
        {
            return null;
        }

        var http = httpClientFactory.CreateClient(nameof(OnlineClient));
        var fields = form.ToList();
        for (var attempt = 1; attempt <= settings.MaxAttempts; attempt++)
        {
            try
            {
                using var content = new FormUrlEncodedContent(fields);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(settings.Timeout);
                using var response = await http.PostAsync($"{settings.BaseUrl}/{endpoint}.fcgi", content, timeout.Token);
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadAsStringAsync(timeout.Token);
                if (JsonNode.Parse(body) is JsonObject { } root && root["result"] is JsonObject result)
                {
                    return result;
                }

                logger.LogWarning("Online: resposta sem 'result' de {Server} (tentativa {Attempt})", settings.BaseUrl, attempt);
            }
            catch (Exception ex) when ((ex is HttpRequestException or TaskCanceledException or JsonException) && !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("Online: falha ao consultar {Server} (tentativa {Attempt}): {Error}", settings.BaseUrl, attempt, ex.Message);
            }
        }

        return null;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var http = httpClientFactory.CreateClient(nameof(OnlineClient));
        while (!stoppingToken.IsCancellationRequested)
        {
            OnlineSettings settings;
            lock (state.Lock)
            {
                settings = ReadSettings();
            }

            if (settings.BaseUrl is null)
            {
                await SimulatorTiming.WaitAsync(TimeSpan.FromMilliseconds(200), () => false, stoppingToken);
                continue;
            }

            try
            {
                using var content = new FormUrlEncodedContent([new KeyValuePair<string, string>("device_id", settings.DeviceId.ToString(CultureInfo.InvariantCulture))]);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                timeout.CancelAfter(settings.Timeout);
                using var response = await http.PostAsync($"{settings.BaseUrl}/device_is_alive.fcgi", content, timeout.Token);
            }
            catch (Exception ex) when ((ex is HttpRequestException or TaskCanceledException) && !stoppingToken.IsCancellationRequested)
            {
                logger.LogDebug("Online: device_is_alive não chegou a {Server}: {Error}", settings.BaseUrl, ex.Message);
            }

            await SimulatorTiming.WaitAsync(settings.Alive, () => IsDifferent(settings), stoppingToken);
        }
    }

    private bool IsDifferent(OnlineSettings previous)
    {
        lock (state.Lock)
        {
            return ReadSettings().BaseUrl != previous.BaseUrl;
        }
    }

    // Chamar com o lock do estado.
    private OnlineSettings ReadSettings()
    {
        string? baseUrl = null;
        if (state.GetConfiguration("general", "online") == "1"
            && long.TryParse(state.GetConfiguration("online_client", "server_id"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var serverId))
        {
            var server = state.Objects.Rows("devices").FirstOrDefault(d => ObjectStore.TryGetLong(d["id"], out var id) && id == serverId);
            if (server?["ip"] is JsonValue ip && ip.TryGetValue(out string? address))
            {
                baseUrl = SimulatorTiming.BaseUrl(address);
            }
        }

        return new OnlineSettings(
            baseUrl,
            int.TryParse(state.GetConfiguration("online_client", "max_request_attempts"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var attempts) && attempts >= 1 ? attempts : 3,
            Milliseconds("request_timeout", DefaultTimeout),
            Milliseconds("alive_interval", DefaultAlive),
            state.Options.DeviceId);
    }

    private TimeSpan Milliseconds(string field, TimeSpan fallback) =>
        double.TryParse(state.GetConfiguration("online_client", field), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0
            ? TimeSpan.FromMilliseconds(value)
            : fallback;

    private sealed record OnlineSettings(string? BaseUrl, int MaxAttempts, TimeSpan Timeout, TimeSpan Alive, long DeviceId);
}
