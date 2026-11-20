using System;
using System.Text.Json.Serialization;

namespace ControlId.Access;

/// <summary>Servidores NTP configurados (<c>get_ntp_server.fcgi</c>).</summary>
public sealed class NtpServers
{
    /// <summary>Endereço do servidor NTP primário.</summary>
    [JsonPropertyName("server1")]
    public string? Server1 { get; set; }

    /// <summary>Endereço do servidor NTP secundário; <c>null</c> se não houver um segundo servidor configurado.</summary>
    [JsonPropertyName("server2")]
    public string? Server2 { get; set; }
}

/// <summary>Resultado de <c>check_port_availability.fcgi</c>.</summary>
public sealed class PortAvailabilityResult
{
    /// <summary>
    /// <c>"OK"</c> se não houver conflito; senão o serviço em conflito
    /// (<c>"WEB"</c>, <c>"RTSP"</c>, <c>"SIP"</c>, <c>"SIP Outbound"</c> ou <c>"ONVIF"</c>).
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>O intervalo de portas não conflita com nenhum serviço do equipamento.</summary>
    [JsonIgnore]
    public bool IsAvailable => string.Equals(Status, "OK", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Interface de rede a que se refere <c>get_802_1X_status.fcgi</c>.</summary>
public enum NetworkInterfaceKind
{
    /// <summary>Interface cabeada (padrão do equipamento).</summary>
    Wired = 0,

    /// <summary>Interface sem fio.</summary>
    Wireless = 1,
}

/// <summary>Estado da autenticação 802.1X (<c>get_802_1X_status.fcgi</c>).</summary>
public enum Ieee8021XStatus
{
    /// <summary>Código que esta versão da biblioteca não conhece (ex.: de um firmware mais novo).</summary>
    Unknown = -1,

    /// <summary>0: INITIALIZE.</summary>
    Initialize = 0,

    /// <summary>1: DISCONNECTED.</summary>
    Disconnected = 1,

    /// <summary>2: CONNECTING.</summary>
    Connecting = 2,

    /// <summary>3: AUTHENTICATING.</summary>
    Authenticating = 3,

    /// <summary>4: AUTHENTICATED.</summary>
    Authenticated = 4,

    /// <summary>5: ABORTING.</summary>
    Aborting = 5,

    /// <summary>6: HELD.</summary>
    Held = 6,

    /// <summary>7: FORCE_AUTH.</summary>
    ForceAuth = 7,

    /// <summary>8: FORCE_UNAUTH.</summary>
    ForceUnauth = 8,

    /// <summary>9: RESTART.</summary>
    Restart = 9,

    /// <summary>10: UNKNOWN reportado pelo próprio equipamento.</summary>
    DeviceUnknown = 10,

    /// <summary>11: DISABLED.</summary>
    Disabled = 11,
}

/// <summary>Log de texto do equipamento (<c>GetDeviceLogAsync</c>).</summary>
public enum DeviceLog
{
    /// <summary><c>get_ac_log.fcgi</c>: log da aplicação (principal e erros).</summary>
    AccessControl,

    /// <summary><c>get_openvpn_log.fcgi</c>: log do cliente OpenVPN.</summary>
    OpenVpn,

    /// <summary><c>get_wpa_log.fcgi</c>: log de autenticação WPA. Apenas firmware V7.</summary>
    Wpa,

    /// <summary><c>get_wpasupplicant_log.fcgi</c>: log do WPA supplicant. Linha iDAccess e firmware V6/V8.</summary>
    WpaSupplicant,
}
