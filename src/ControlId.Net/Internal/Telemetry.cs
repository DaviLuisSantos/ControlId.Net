using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ControlId.Internal;

/// <summary>Instrumentos de traces e métricas (OpenTelemetry). Veja <see cref="ControlIdTelemetry"/>.</summary>
internal static class Telemetry
{
    private static readonly string Version =
        typeof(Telemetry).Assembly.GetName().Version?.ToString() ?? "0.0.0";

    public static readonly ActivitySource Source = new(ControlIdTelemetry.ActivitySourceName, Version);

    private static readonly Meter Meter = new(ControlIdTelemetry.MeterName, Version);

    public static readonly Histogram<double> OperationDuration = Meter.CreateHistogram<double>(
        "controlid.client.operation.duration", "s", "Duração de um comando ao equipamento, com fila, re-login e HTTP.");

    public static readonly Counter<long> Relogins = Meter.CreateCounter<long>(
        "controlid.client.relogins", "{relogin}", "Logins refeitos porque a sessão expirou (ex.: o equipamento reiniciou).");

    public static readonly UpDownCounter<long> QueuedRequests = Meter.CreateUpDownCounter<long>(
        "controlid.client.requests.queued", "{request}", "Requisições esperando vaga no limite por equipamento (MaxConcurrentRequests).");

    public static readonly UpDownCounter<long> ActiveRequests = Meter.CreateUpDownCounter<long>(
        "controlid.client.requests.active", "{request}", "Requisições em andamento no equipamento.");

    /// <summary>Nome do comando para spans e métricas: sem <c>.fcgi</c> nem query string (baixa cardinalidade).</summary>
    public static string CommandName(string endpoint)
    {
        var name = endpoint;
        var query = name.IndexOf('?');
        if (query >= 0)
        {
            name = name.Substring(0, query);
        }

        name = name.Trim('/');
        return name.EndsWith(".fcgi", StringComparison.Ordinal) ? name.Substring(0, name.Length - 5) : name;
    }

    /// <summary>Atributos que identificam o equipamento, comuns a spans e métricas.</summary>
    public static KeyValuePair<string, object?>[] DeviceTags(Uri baseAddress) => new[]
    {
        new KeyValuePair<string, object?>("server.address", baseAddress.Host),
        new KeyValuePair<string, object?>("server.port", baseAddress.Port),
    };
}

/// <summary>
/// Um comando ao equipamento em andamento: o span e o tempo da métrica de duração. Descartar encerra os dois.
/// </summary>
internal sealed class CommandScope : IDisposable
{
    private readonly KeyValuePair<string, object?>[] _deviceTags;
    private readonly string _command;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private string? _errorType;

    public CommandScope(string endpoint, KeyValuePair<string, object?>[] deviceTags, string? displayName = null)
    {
        _deviceTags = deviceTags;
        _command = Telemetry.CommandName(endpoint);

        Activity = Telemetry.Source.StartActivity(displayName ?? _command, ActivityKind.Client);
        if (Activity is { IsAllDataRequested: true })
        {
            Activity.SetTag("controlid.command", _command);
            foreach (var tag in deviceTags)
            {
                Activity.SetTag(tag.Key, tag.Value);
            }
        }
    }

    public Activity? Activity { get; }

    /// <summary>A sessão expirou e o login foi refeito durante este comando.</summary>
    public void Relogin()
    {
        Telemetry.Relogins.Add(1, _deviceTags);
        Activity?.AddEvent(new ActivityEvent("controlid.relogin"));
    }

    public void Fail(Exception exception)
    {
        _errorType = exception switch
        {
            ControlIdException { StatusCode: { } status } => ((int)status).ToString(System.Globalization.CultureInfo.InvariantCulture),
            OperationCanceledException => "canceled",
            _ => exception.GetType().FullName,
        };

        if (Activity is null)
        {
            return;
        }

        Activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        Activity.SetTag("error.type", _errorType);
        if (exception is ControlIdException { StatusCode: { } code } controlId)
        {
            Activity.SetTag("http.response.status_code", (int)code);
            if (controlId.DeviceErrorCode is { } deviceCode)
            {
                Activity.SetTag("controlid.device.error_code", deviceCode);
            }
        }
    }

    public void Dispose()
    {
        var tags = new TagList
        {
            { "controlid.command", _command },
        };
        foreach (var tag in _deviceTags)
        {
            tags.Add(tag.Key, tag.Value);
        }

        if (_errorType is not null)
        {
            tags.Add("error.type", _errorType);
        }

        Telemetry.OperationDuration.Record(_stopwatch.Elapsed.TotalSeconds, tags);
        Activity?.Dispose();
    }
}
