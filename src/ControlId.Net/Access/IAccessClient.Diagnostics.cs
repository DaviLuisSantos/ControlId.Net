using System.Threading;
using System.Threading.Tasks;

namespace ControlId.Access;

public partial interface IAccessClient
{
    /// <summary><c>get_ntp_server.fcgi</c>: servidores NTP configurados.</summary>
    Task<NtpServers> GetNtpServerAsync(CancellationToken cancellationToken = default);

    /// <summary><c>set_ntp_server.fcgi</c>: define o servidor NTP primário e, opcionalmente, o secundário.</summary>
    Task SetNtpServerAsync(string server1, string? server2 = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>get_ntp_server_status.fcgi</c>: testa a conexão com o servidor NTP. Chamada bloqueante (timeout de até 4 segundos
    /// no equipamento); devolve <c>true</c> se a conexão deu certo.
    /// </summary>
    Task<bool> GetNtpServerStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>check_port_availability.fcgi</c>: verifica se o intervalo de portas conflita com algum serviço do equipamento.
    /// Firmware V6, V7 e V8.
    /// </summary>
    Task<PortAvailabilityResult> CheckPortAvailabilityAsync(int initialPort, int finalPort, CancellationToken cancellationToken = default);

    /// <summary><c>get_vpn_ip.fcgi</c>: endereço IP do equipamento na VPN; vazio se a VPN não estiver conectada.</summary>
    Task<string> GetVpnIpAsync(CancellationToken cancellationToken = default);

    /// <summary><c>get_ssl_certificate.fcgi</c>: certificado SSL carregado no servidor web (texto). Falha se não houver certificado.</summary>
    Task<string> GetSslCertificateAsync(CancellationToken cancellationToken = default);

    /// <summary><c>get_802_1X_status.fcgi</c>: estado da autenticação 802.1X da interface informada.</summary>
    Task<Ieee8021XStatus> Get8021XStatusAsync(NetworkInterfaceKind networkInterface = NetworkInterfaceKind.Wired, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lê um log de texto do equipamento: <c>get_ac_log.fcgi</c>, <c>get_openvpn_log.fcgi</c>, <c>get_wpa_log.fcgi</c>
    /// (apenas V7) ou <c>get_wpasupplicant_log.fcgi</c> (iDAccess, V6 e V8).
    /// </summary>
    Task<string> GetDeviceLogAsync(DeviceLog log, CancellationToken cancellationToken = default);
}
