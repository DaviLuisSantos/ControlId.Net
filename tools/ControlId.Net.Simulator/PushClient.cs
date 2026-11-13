using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ControlId.Simulator;

/// <summary>
/// O lado do equipamento no modo Push: com <c>push_server.push_remote_address</c> configurado, consulta
/// <c>GET http://{endereço}/push?deviceId=&amp;uuid=</c> a cada <c>push_request_period</c> segundos, executa o comando
/// recebido (simples ou em <c>transactions</c>) pelos handlers da API e devolve o resultado em <c>POST /result</c>.
/// Sem endereço configurado, nada é consultado.
/// </summary>
public sealed class PushClient(DeviceState state, IServiceProvider services, IHttpClientFactory httpClientFactory, ILogger<PushClient> logger)
    : BackgroundService
{
    private static readonly TimeSpan DefaultPeriod = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    private int _executed;

    /// <summary>Comandos executados por Push desde o início (para inspeção em <c>/simulator/state</c>).</summary>
    public int CommandsExecuted => Volatile.Read(ref _executed);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var http = httpClientFactory.CreateClient(nameof(PushClient));
        while (!stoppingToken.IsCancellationRequested)
        {
            var settings = ReadSettings();
            if (settings.BaseUrl is null)
            {
                await SimulatorTiming.WaitAsync(TimeSpan.FromMilliseconds(200), () => false, stoppingToken);
                continue;
            }

            var executed = false;
            try
            {
                executed = await PollAsync(http, settings, stoppingToken);
            }
            catch (Exception ex) when ((ex is HttpRequestException or TaskCanceledException or JsonException) && !stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning("Push: falha ao falar com {Server}: {Error}", settings.BaseUrl, ex.Message);
            }

            // Depois de um comando o equipamento volta a consultar na hora; sem comando, espera o período.
            if (!executed)
            {
                await SimulatorTiming.WaitAsync(settings.Period, () => ReadSettings().BaseUrl != settings.BaseUrl, stoppingToken);
            }
        }
    }

    private async Task<bool> PollAsync(HttpClient http, PushSettings settings, CancellationToken stoppingToken)
    {
        var uuid = Guid.NewGuid().ToString();
        var query = string.Create(CultureInfo.InvariantCulture, $"?deviceId={settings.DeviceId}&uuid={uuid}");

        string text;
        using (var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken))
        {
            cts.CancelAfter(settings.Timeout);
            text = await http.GetStringAsync(settings.BaseUrl + "/push" + query, cts.Token);
        }

        if (string.IsNullOrWhiteSpace(text) || JsonNode.Parse(text) is not JsonObject root)
        {
            return false;
        }

        JsonObject result;
        if (root["transactions"] is JsonArray transactions)
        {
            var results = new JsonArray();
            foreach (var item in transactions.OfType<JsonObject>())
            {
                var outcome = await ExecuteAsync(item);
                var (response, error) = Describe(outcome);
                results.Add(new JsonObject
                {
                    ["transactionid"] = long.TryParse(item["transactionid"]?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : 0,
                    ["success"] = error is null,
                    ["response"] = response,
                });
            }

            result = new JsonObject { ["transactions_results"] = results };
        }
        else
        {
            var outcome = await ExecuteAsync(root);
            var (response, error) = Describe(outcome);
            result = error is null ? new JsonObject { ["response"] = response } : new JsonObject { ["error"] = error };
        }

        using var content = new StringContent(result.ToJsonString(AccessApi.Wire), Encoding.UTF8, "application/json");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(settings.Timeout);
        using var posted = await http.PostAsync(settings.BaseUrl + "/result" + query, content, timeout.Token);
        return true;
    }

    private async Task<AccessApi.CommandOutcome> ExecuteAsync(JsonObject command)
    {
        Interlocked.Increment(ref _executed);
        var endpoint = command["endpoint"]?.GetValue<string>() ?? string.Empty;
        if (endpoint.EndsWith(".fcgi", StringComparison.Ordinal))
        {
            endpoint = endpoint[..^5];
        }

        var contentType = command["contentType"]?.GetValue<string>() ?? "application/json";
        var body = command["body"];
        byte[] bytes;
        if (body is null)
        {
            bytes = [];
        }
        else if (contentType.StartsWith("application/octet-stream", StringComparison.OrdinalIgnoreCase)
            && body is JsonValue encoded && encoded.TryGetValue(out string? base64))
        {
            bytes = Convert.FromBase64String(base64);
        }
        else if (body is JsonValue text && text.TryGetValue(out string? raw))
        {
            bytes = Encoding.UTF8.GetBytes(raw);
        }
        else
        {
            bytes = Encoding.UTF8.GetBytes(body.ToJsonString(AccessApi.Wire));
        }

        return await AccessApi.ExecuteAsync(endpoint, bytes, command["queryString"]?.GetValue<string>(), services);
    }

    // O resultado do comando: JSON quando o corpo é JSON, Base64 quando é binário (foto).
    private static (JsonNode? Response, string? Error) Describe(AccessApi.CommandOutcome outcome)
    {
        JsonNode? parsed = null;
        if (outcome.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
        {
            try
            {
                parsed = outcome.Body.Length == 0 ? new JsonObject() : JsonNode.Parse(outcome.Body);
            }
            catch (JsonException)
            {
                parsed = null;
            }
        }

        if (!outcome.Success)
        {
            return (parsed, parsed?["error"]?.ToString() ?? $"HTTP {outcome.Status}");
        }

        return (parsed ?? JsonValue.Create(Convert.ToBase64String(outcome.Body)), null);
    }

    private PushSettings ReadSettings()
    {
        string? address;
        string? period;
        string? timeout;
        long deviceId;
        lock (state.Lock)
        {
            address = state.GetConfiguration("push_server", "push_remote_address");
            period = state.GetConfiguration("push_server", "push_request_period");
            timeout = state.GetConfiguration("push_server", "push_request_timeout");
            deviceId = state.Options.DeviceId;
        }

        return new PushSettings(
            SimulatorTiming.BaseUrl(address),
            double.TryParse(period, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0 ? TimeSpan.FromSeconds(seconds) : DefaultPeriod,
            double.TryParse(timeout, NumberStyles.Float, CultureInfo.InvariantCulture, out var millis) && millis > 0 ? TimeSpan.FromMilliseconds(millis) : DefaultTimeout,
            deviceId);
    }

    private sealed record PushSettings(string? BaseUrl, TimeSpan Period, TimeSpan Timeout, long DeviceId);
}

/// <summary>Utilidades dos clientes de fundo do simulador (Push e modo online).</summary>
internal static class SimulatorTiming
{
    /// <summary>Endereço configurado no equipamento (com ou sem <c>http://</c>) como URL base, sem a barra final.</summary>
    public static string? BaseUrl(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return null;
        }

        var trimmed = address.Trim().TrimEnd('/');
        return trimmed.Contains("://", StringComparison.Ordinal) ? trimmed : "http://" + trimmed;
    }

    /// <summary>
    /// Espera <paramref name="total"/> em fatias curtas, para uma mudança de configuração (<paramref name="changed"/>)
    /// valer logo, sem esperar o período inteiro.
    /// </summary>
    public static async Task WaitAsync(TimeSpan total, Func<bool> changed, CancellationToken cancellationToken)
    {
        var watch = Stopwatch.StartNew();
        var slice = TimeSpan.FromMilliseconds(100);
        try
        {
            while (watch.Elapsed < total && !cancellationToken.IsCancellationRequested)
            {
                var remaining = total - watch.Elapsed;
                await Task.Delay(remaining < slice ? remaining : slice, cancellationToken);
                if (changed())
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // encerrando o host
        }
    }
}
