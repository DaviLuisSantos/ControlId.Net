using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ControlId.Access;

public partial interface IAccessClient
{
    /// <summary><c>set_wifi_source.fcgi</c>: seleciona o adaptador Wi-Fi (interno, USB ou desligado). Firmware V8.</summary>
    Task SetWifiSourceAsync(WifiSource source, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>wifi_scan.fcgi</c>: varre os pontos de acesso ao alcance. A chamada é síncrona e pode levar até 20 segundos no
    /// equipamento. Firmware V8.
    /// </summary>
    Task<IReadOnlyList<WifiNetwork>> ScanWifiAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>set_wifi_settings.fcgi</c>: conecta a uma rede Wi-Fi e devolve o resultado da tentativa. A senha (<c>psk</c>)
    /// vai vazia para redes abertas e nunca entra em exceção nem log. <c>bssid</c> opcional força um ponto de acesso
    /// específico. Firmware V8.
    /// </summary>
    Task<WifiConnectionResult> SetWifiSettingsAsync(string ssid, string psk, string? bssid = null, CancellationToken cancellationToken = default);

    /// <summary><c>get_wifi_data.fcgi</c>: configuração atual e estado da conexão sem fio. Firmware V8.</summary>
    Task<WifiData> GetWifiDataAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>forget_wifi_connected_network.fcgi</c>: desconecta da rede sem fio atual e APAGA as credenciais armazenadas, sem
    /// volta. Exige <see cref="DataLossConfirmation.Confirmed"/>. Firmware V8.
    /// </summary>
    Task ForgetWifiConnectedNetworkAsync(DataLossConfirmation confirmation, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>check_ip_conflict.fcgi</c>: verifica se o endereço IP que será configurado na interface informada já está em uso
    /// na outra interface (cabeada e sem fio podem estar ativas ao mesmo tempo). Devolve <c>true</c> se houver conflito.
    /// Firmware V8.
    /// </summary>
    Task<bool> CheckIpConflictAsync(NetworkInterfaceKind networkInterface, string ip, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>eap_802_1X_certificate_change.fcgi</c>: envia o certificado CA do EAP-PEAP (802.1X) da interface cabeada,
    /// como <c>application/octet-stream</c>.
    /// </summary>
    Task UploadEapCaCertificateAsync(byte[] certificate, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>eap_802_1X_certificate_change_wifi.fcgi</c>: envia o certificado CA do EAP/802.1X da interface sem fio, como
    /// <c>application/octet-stream</c>. Firmware V8.
    /// </summary>
    Task UploadEapCaCertificateWifiAsync(byte[] certificate, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>eap_tls_802_1X_certificate_change.fcgi</c>: envia o certificado de cliente do EAP-TLS, como
    /// <c>application/octet-stream</c>. Firmware V6, V7 e V8.
    /// </summary>
    Task UploadEapTlsClientCertificateAsync(byte[] certificate, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>eap_tls_802_1X_private_key_change.fcgi</c>: envia uma chave privada pré-existente do EAP-TLS, como
    /// <c>application/octet-stream</c>. A chave nunca entra em exceção nem log. Para usar a chave gerada pelo fluxo de CSR,
    /// chame <see cref="PersistEapTlsPrivateKeyAsync"/>. Firmware V6, V7 e V8.
    /// </summary>
    Task UploadEapTlsPrivateKeyAsync(byte[] privateKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>get_eap_tls_csr_file.fcgi</c>: devolve o CSR do EAP-TLS. Se ainda não existir, o equipamento gera uma chave
    /// privada e o CSR protegidos por <paramref name="password"/> (obrigatória só nessa primeira chamada); se já existir, o
    /// arquivo é devolvido sem regenerar. A senha nunca entra em exceção nem log. Firmware V6, V7 e V8.
    /// </summary>
    Task<string> GetEapTlsCsrAsync(string? password = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>eap_tls_802_1X_private_key_persist.fcgi</c>: persiste a chave privada gerada junto com o CSR de
    /// <see cref="GetEapTlsCsrAsync"/>. Chame após obter o certificado assinado pela CA e antes de configurar o EAP-TLS.
    /// Firmware V6, V7 e V8.
    /// </summary>
    Task PersistEapTlsPrivateKeyAsync(CancellationToken cancellationToken = default);
}
