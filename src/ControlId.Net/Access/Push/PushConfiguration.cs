using System;
using System.Collections.Generic;
using System.Globalization;
using ControlId.Access.Monitoring;

namespace ControlId.Access.Push;

/// <summary>
/// Seção <c>push_server</c> do <c>set_configuration.fcgi</c>: servidor que o equipamento consulta em busca de comandos.
/// Campos <c>null</c> não são alterados.
/// </summary>
public sealed class PushConfiguration
{
    /// <summary>Endereço do servidor no formato <c>ip:porta</c>, ex.: <c>192.168.0.20:8080</c>.</summary>
    public string? RemoteAddress { get; set; }

    /// <summary>Intervalo entre as consultas a <c>/push</c> (resolução de segundos).</summary>
    public TimeSpan? RequestPeriod { get; set; }

    /// <summary>Timeout de cada requisição do equipamento ao servidor.</summary>
    public TimeSpan? RequestTimeout { get; set; }

    internal Dictionary<string, object> ToConfiguration()
    {
        var values = new Dictionary<string, string>();
        MonitorConfiguration.Add(values, "push_remote_address", RemoteAddress);
        MonitorConfiguration.Add(values, "push_request_timeout", MonitorConfiguration.Milliseconds(RequestTimeout));
        MonitorConfiguration.Add(
            values,
            "push_request_period",
            RequestPeriod is { } period ? ((long)period.TotalSeconds).ToString(CultureInfo.InvariantCulture) : null);
        return new Dictionary<string, object> { ["push_server"] = values };
    }
}
