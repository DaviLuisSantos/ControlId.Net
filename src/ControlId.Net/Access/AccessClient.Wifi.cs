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
    public Task SetWifiSourceAsync(WifiSource source, CancellationToken cancellationToken = default)
    {
        var name = source switch
        {
            WifiSource.Internal => "internal",
            WifiSource.Usb => "usb",
            WifiSource.Disabled => "disabled",
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Adaptador Wi-Fi desconhecido."),
        };
        return _transport.PostRawAsync("set_wifi_source.fcgi", new Dictionary<string, object> { ["source"] = name }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WifiNetwork>> ScanWifiAsync(CancellationToken cancellationToken = default)
    {
        var response = await _transport.PostAsync<WifiScanResponse>("wifi_scan.fcgi", null, cancellationToken).ConfigureAwait(false);
        return response.Networks ?? new List<WifiNetwork>();
    }

    /// <inheritdoc/>
    public async Task<WifiConnectionResult> SetWifiSettingsAsync(string ssid, string psk, string? bssid = null, CancellationToken cancellationToken = default)
    {
        Required(ssid, nameof(ssid));
        if (psk is null)
        {
            // Sem o valor na mensagem: a senha não pode vazar. Para rede aberta, passe string vazia.
            throw new ArgumentNullException(nameof(psk), "Informe a senha da rede (string vazia para rede aberta).");
        }

        var body = new Dictionary<string, object> { ["ssid"] = ssid, ["psk"] = psk };
        if (!string.IsNullOrWhiteSpace(bssid))
        {
            body["bssid"] = bssid!;
        }

        var response = await _transport.PostAsync<WifiResultResponse>("set_wifi_settings.fcgi", body, cancellationToken).ConfigureAwait(false);
        return EnumValues.ToDefined<WifiConnectionResult>(response.Result);
    }

    /// <inheritdoc/>
    public Task<WifiData> GetWifiDataAsync(CancellationToken cancellationToken = default) =>
        _transport.PostAsync<WifiData>("get_wifi_data.fcgi", null, cancellationToken);

    /// <inheritdoc/>
    public Task ForgetWifiConnectedNetworkAsync(DataLossConfirmation confirmation, CancellationToken cancellationToken = default)
    {
        DataLoss.Require(confirmation, nameof(ForgetWifiConnectedNetworkAsync));
        return _transport.PostRawAsync("forget_wifi_connected_network.fcgi", null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> CheckIpConflictAsync(NetworkInterfaceKind networkInterface, string ip, CancellationToken cancellationToken = default)
    {
        // Documentação: "1" é a interface cabeada; qualquer outro valor, a sem fio.
        var interfaceCode = networkInterface switch
        {
            NetworkInterfaceKind.Wired => "1",
            NetworkInterfaceKind.Wireless => "2",
            _ => throw new ArgumentOutOfRangeException(nameof(networkInterface), networkInterface, "Interface de rede desconhecida."),
        };
        var body = new Dictionary<string, object> { ["interface"] = interfaceCode, ["ip"] = Required(ip, nameof(ip)) };
        var response = await _transport.PostAsync<WifiIpConflictResponse>("check_ip_conflict.fcgi", body, cancellationToken).ConfigureAwait(false);
        return response.Conflict;
    }

    /// <inheritdoc/>
    public Task UploadEapCaCertificateAsync(byte[] certificate, CancellationToken cancellationToken = default) =>
        WifiUploadAsync("eap_802_1X_certificate_change.fcgi", certificate, nameof(certificate), "Informe o certificado.", cancellationToken);

    /// <inheritdoc/>
    public Task UploadEapCaCertificateWifiAsync(byte[] certificate, CancellationToken cancellationToken = default) =>
        WifiUploadAsync("eap_802_1X_certificate_change_wifi.fcgi", certificate, nameof(certificate), "Informe o certificado.", cancellationToken);

    /// <inheritdoc/>
    public Task UploadEapTlsClientCertificateAsync(byte[] certificate, CancellationToken cancellationToken = default) =>
        WifiUploadAsync("eap_tls_802_1X_certificate_change.fcgi", certificate, nameof(certificate), "Informe o certificado.", cancellationToken);

    /// <inheritdoc/>
    public Task UploadEapTlsPrivateKeyAsync(byte[] privateKey, CancellationToken cancellationToken = default) =>
        WifiUploadAsync("eap_tls_802_1X_private_key_change.fcgi", privateKey, nameof(privateKey), "Informe a chave privada.", cancellationToken);

    /// <inheritdoc/>
    public Task<string> GetEapTlsCsrAsync(string? password = null, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object>();
        if (!string.IsNullOrEmpty(password))
        {
            body["password"] = password!;
        }

        return _transport.PostRawAsync("get_eap_tls_csr_file.fcgi", body, cancellationToken);
    }

    /// <inheritdoc/>
    public Task PersistEapTlsPrivateKeyAsync(CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync("eap_tls_802_1X_private_key_persist.fcgi", null, cancellationToken);

    private Task<string> WifiUploadAsync(string endpoint, byte[] content, string paramName, string message, CancellationToken cancellationToken) =>
        content is null || content.Length == 0
            ? throw new ArgumentException(message, paramName)
            : _transport.PostBytesAsync(endpoint, content, "application/octet-stream", cancellationToken);

    private sealed class WifiScanResponse
    {
        [JsonPropertyName("networks")]
        public List<WifiNetwork>? Networks { get; set; }
    }

    private sealed class WifiResultResponse
    {
        [JsonPropertyName("result")]
        public long Result { get; set; }
    }

    private sealed class WifiIpConflictResponse
    {
        [JsonPropertyName("conflict")]
        public bool Conflict { get; set; }
    }
}
