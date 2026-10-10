using System.Text.Json.Serialization;

namespace ControlId.Access;

/// <summary>Resposta de <c>system_information.fcgi</c> (campos principais).</summary>
public sealed class AccessSystemInformation
{
    /// <summary>Número de série.</summary>
    [JsonPropertyName("serial")]
    public string? Serial { get; set; }

    /// <summary>Versão do firmware.</summary>
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    /// <summary>ID do equipamento.</summary>
    [JsonPropertyName("device_id")]
    public string? DeviceId { get; set; }

    /// <summary>Data/hora do equipamento (Unix timestamp).</summary>
    [JsonPropertyName("time")]
    public long Time { get; set; }

    /// <summary>Equipamento rodando em modo online.</summary>
    [JsonPropertyName("online")]
    public bool Online { get; set; }

    /// <summary>Tempo ligado.</summary>
    [JsonPropertyName("uptime")]
    public AccessUptime? Uptime { get; set; }

    /// <summary>Configuração de rede.</summary>
    [JsonPropertyName("network")]
    public AccessNetworkInfo? Network { get; set; }
}

/// <summary>Tempo ligado do equipamento.</summary>
public sealed class AccessUptime
{
    /// <summary>Dias.</summary>
    [JsonPropertyName("days")] public int Days { get; set; }

    /// <summary>Horas.</summary>
    [JsonPropertyName("hours")] public int Hours { get; set; }

    /// <summary>Minutos.</summary>
    [JsonPropertyName("minutes")] public int Minutes { get; set; }

    /// <summary>Segundos.</summary>
    [JsonPropertyName("seconds")] public int Seconds { get; set; }
}

/// <summary>Rede do equipamento.</summary>
public sealed class AccessNetworkInfo
{
    /// <summary>MAC.</summary>
    [JsonPropertyName("mac")] public string? Mac { get; set; }

    /// <summary>IP.</summary>
    [JsonPropertyName("ip")] public string? Ip { get; set; }

    /// <summary>Máscara.</summary>
    [JsonPropertyName("netmask")] public string? Netmask { get; set; }

    /// <summary>Gateway.</summary>
    [JsonPropertyName("gateway")] public string? Gateway { get; set; }

    /// <summary>Porta do servidor web.</summary>
    [JsonPropertyName("web_server_port")] public int WebServerPort { get; set; }

    /// <summary>HTTPS habilitado.</summary>
    [JsonPropertyName("ssl_enabled")] public bool SslEnabled { get; set; }

    /// <summary>DHCP habilitado.</summary>
    [JsonPropertyName("dhcp_enabled")] public bool DhcpEnabled { get; set; }
}
