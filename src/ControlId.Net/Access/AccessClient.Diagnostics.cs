using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ControlId.Internal;

namespace ControlId.Access;

public sealed partial class AccessClient
{
    /// <inheritdoc/>
    public Task<NtpServers> GetNtpServerAsync(CancellationToken cancellationToken = default) =>
        GetJsonAsync<NtpServers>("get_ntp_server.fcgi", cancellationToken);

    /// <inheritdoc/>
    public Task SetNtpServerAsync(string server1, string? server2 = null, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object> { ["server1"] = Required(server1, nameof(server1)) };
        if (!string.IsNullOrWhiteSpace(server2))
        {
            body["server2"] = server2!;
        }

        return _transport.PostRawAsync("set_ntp_server.fcgi", body, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> GetNtpServerStatusAsync(CancellationToken cancellationToken = default) =>
        (await GetJsonAsync<NtpServerStatusResponse>("get_ntp_server_status.fcgi", cancellationToken).ConfigureAwait(false)).ServerStatus;

    /// <inheritdoc/>
    public Task<PortAvailabilityResult> CheckPortAvailabilityAsync(int initialPort, int finalPort, CancellationToken cancellationToken = default)
    {
        ValidatePort(initialPort, nameof(initialPort));
        ValidatePort(finalPort, nameof(finalPort));
        if (finalPort < initialPort)
        {
            throw new ArgumentException("A porta final não pode ser menor que a inicial.", nameof(finalPort));
        }

        return _transport.PostAsync<PortAvailabilityResult>(
            "check_port_availability.fcgi",
            new Dictionary<string, object> { ["initial_port"] = initialPort, ["final_port"] = finalPort },
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<string> GetVpnIpAsync(CancellationToken cancellationToken = default) =>
        (await GetJsonAsync<VpnIpResponse>("get_vpn_ip.fcgi", cancellationToken).ConfigureAwait(false)).Ip ?? string.Empty;

    /// <inheritdoc/>
    public Task<string> GetSslCertificateAsync(CancellationToken cancellationToken = default) =>
        GetTextAsync("get_ssl_certificate.fcgi", null, cancellationToken);

    /// <inheritdoc/>
    public async Task<Ieee8021XStatus> Get8021XStatusAsync(
        NetworkInterfaceKind networkInterface = NetworkInterfaceKind.Wired, CancellationToken cancellationToken = default)
    {
        var query = networkInterface switch
        {
            NetworkInterfaceKind.Wired => "interface=wired",
            NetworkInterfaceKind.Wireless => "interface=wireless",
            _ => throw new ArgumentOutOfRangeException(nameof(networkInterface), networkInterface, "Interface de rede desconhecida."),
        };
        var text = await GetTextAsync("get_802_1X_status.fcgi", query, cancellationToken).ConfigureAwait(false);
        return EnumValues.ToDefined<Ieee8021XStatus>(ControlIdJson.Deserialize<Ieee8021XStatusResponse>(text).Status);
    }

    /// <inheritdoc/>
    public Task<string> GetDeviceLogAsync(DeviceLog log, CancellationToken cancellationToken = default)
    {
        var endpoint = log switch
        {
            DeviceLog.AccessControl => "get_ac_log.fcgi",
            DeviceLog.OpenVpn => "get_openvpn_log.fcgi",
            DeviceLog.Wpa => "get_wpa_log.fcgi",
            DeviceLog.WpaSupplicant => "get_wpasupplicant_log.fcgi",
            _ => throw new ArgumentOutOfRangeException(nameof(log), log, "Log desconhecido."),
        };
        return GetTextAsync(endpoint, null, cancellationToken);
    }

    private async Task<string> GetTextAsync(string endpoint, string? query, CancellationToken cancellationToken)
    {
        var bytes = await _transport.GetBytesAsync(endpoint, cancellationToken, query).ConfigureAwait(false);
        return TextDecoding.Decode(bytes, null);
    }

    private static void ValidatePort(int port, string paramName)
    {
        if (port < 1 || port > 65535)
        {
            throw new ArgumentOutOfRangeException(paramName, port, "A porta precisa estar entre 1 e 65535.");
        }
    }

    private sealed class NtpServerStatusResponse
    {
        [JsonPropertyName("server_status")]
        public bool ServerStatus { get; set; }
    }

    private sealed class VpnIpResponse
    {
        [JsonPropertyName("ip")]
        public string? Ip { get; set; }
    }

    private sealed class Ieee8021XStatusResponse
    {
        [JsonPropertyName("status")]
        public long Status { get; set; }
    }
}
