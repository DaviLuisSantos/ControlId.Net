using System;
using System.Text.Json.Serialization;

namespace ControlId.Access.Online;

/// <summary><c>new_rex_log.fcgi</c>: acesso por botoeira no modo online (corpo JSON).</summary>
public sealed class ExitButtonEvent
{
    /// <summary>Equipamento que enviou.</summary>
    [JsonPropertyName("device_id")]
    public long? DeviceId { get; set; }

    /// <summary>Dados do acesso.</summary>
    [JsonPropertyName("rex_log")]
    public ExitButtonLog? Log { get; set; }
}

/// <summary>Campo <c>rex_log</c> de <see cref="ExitButtonEvent"/>.</summary>
public sealed class ExitButtonLog
{
    /// <summary>Momento (Unix timestamp).</summary>
    [JsonPropertyName("time")]
    public long Time { get; set; }

    /// <summary>Evento (11 = acesso por botoeira).</summary>
    [JsonPropertyName("event")]
    public int Event { get; set; }

    /// <summary>Usuário, quando houver.</summary>
    [JsonPropertyName("user_id")]
    public long? UserId { get; set; }

    /// <summary>Portal aberto.</summary>
    [JsonPropertyName("portal_id")]
    public long? PortalId { get; set; }

    /// <summary><see cref="Time"/> como data, em UTC.</summary>
    [JsonIgnore]
    public DateTimeOffset TimeUtc => DateTimeOffset.FromUnixTimeSeconds(Time);
}
