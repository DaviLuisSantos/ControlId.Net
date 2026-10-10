using System;
using System.Collections.Generic;
using System.Globalization;

namespace ControlId.Access.Monitoring;

/// <summary>
/// Seção <c>monitor</c> do <c>set_configuration.fcgi</c>: para onde o equipamento envia as notificações.
/// Campos <c>null</c> não são alterados.
/// </summary>
public sealed class MonitorConfiguration
{
    /// <summary>IP ou nome do servidor que recebe as notificações.</summary>
    public string? Hostname { get; set; }

    /// <summary>Porta do servidor.</summary>
    public int? Port { get; set; }

    /// <summary>Caminho base. O padrão do equipamento é <c>api/notifications</c>.</summary>
    public string? Path { get; set; }

    /// <summary>Timeout de cada notificação.</summary>
    public TimeSpan? RequestTimeout { get; set; }

    /// <summary>Intervalo entre os <c>device_is_alive</c> (padrão do equipamento: 30 s).</summary>
    public TimeSpan? AliveInterval { get; set; }

    /// <summary>Envia a foto tirada na identificação (<c>access_photo</c>).</summary>
    public bool? EnablePhotoUpload { get; set; }

    /// <summary>Inclui <c>access_event_id</c> nas notificações de porta, SecBox e catraca.</summary>
    public bool? InformAccessEventId { get; set; }

    internal Dictionary<string, object> ToConfiguration()
    {
        var values = new Dictionary<string, string>();
        Add(values, "hostname", Hostname);
        Add(values, "port", Port?.ToString(CultureInfo.InvariantCulture));
        Add(values, "path", Path?.Trim('/'));
        Add(values, "request_timeout", Milliseconds(RequestTimeout));
        Add(values, "alive_interval", Milliseconds(AliveInterval));
        Add(values, "enable_photo_upload", Flag(EnablePhotoUpload));
        Add(values, "inform_access_event_id", Flag(InformAccessEventId));
        return new Dictionary<string, object> { ["monitor"] = values };
    }

    internal static void Add(Dictionary<string, string> values, string key, string? value)
    {
        if (value is not null)
        {
            values[key] = value;
        }
    }

    internal static string? Milliseconds(TimeSpan? value) =>
        value is { } time ? ((long)time.TotalMilliseconds).ToString(CultureInfo.InvariantCulture) : null;

    private static string? Flag(bool? value) => value is { } flag ? (flag ? "1" : "0") : null;
}
