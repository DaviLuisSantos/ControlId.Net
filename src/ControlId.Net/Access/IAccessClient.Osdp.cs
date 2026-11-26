using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ControlId.Access;

public partial interface IAccessClient
{
    /// <summary><c>get_osdp_installation_mode.fcgi</c>: o modo de instalação do OSDP está ativo (o equipamento aceita negociar novas chaves).</summary>
    Task<bool> GetOsdpInstallationModeAsync(CancellationToken cancellationToken = default);

    /// <summary><c>osdp_scbk.fcgi</c> sem <c>scbk</c>: a Chave Base de Canal Seguro (SCBK) atual. V6, V7 e V8. A chave é um segredo.</summary>
    Task<OsdpScbkInfo> GetOsdpScbkAsync(CancellationToken cancellationToken = default);

    /// <summary><c>osdp_scbk.fcgi</c> com <c>scbk</c>: grava a SCBK da configuração do equipamento. V6, V7 e V8. A chave nunca vai para exceção nem log.</summary>
    /// <param name="scbk">Nova SCBK, em hexadecimal.</param>
    /// <param name="cancellationToken">Cancelamento.</param>
    Task SetOsdpScbkAsync(string scbk, CancellationToken cancellationToken = default);

    /// <summary><c>osdp_get_connection_status.fcgi</c>: status de conexão dos leitores de um painel OSDP. Apenas linha iDAccess.</summary>
    /// <param name="panelId">ID do painel OSDP.</param>
    /// <param name="cancellationToken">Cancelamento.</param>
    /// <returns>Status de conexão de cada leitor, indexado pelo endereço.</returns>
    Task<IReadOnlyDictionary<int, int>> GetOsdpConnectionStatusAsync(int panelId, CancellationToken cancellationToken = default);

    /// <summary><c>osdp_get_output_capabilities.fcgi</c>: capacidades de saída de um leitor OSDP. Apenas linha iDAccess.</summary>
    /// <param name="address">Endereço do leitor OSDP.</param>
    /// <param name="cancellationToken">Cancelamento.</param>
    Task<OsdpOutputCapabilities> GetOsdpOutputCapabilitiesAsync(int address, CancellationToken cancellationToken = default);

    /// <summary><c>osdp_set_secure_key.fcgi</c>: define a SCBK de um leitor de um painel OSDP. Apenas linha iDAccess. A chave nunca vai para exceção nem log.</summary>
    /// <param name="panelId">ID do painel OSDP.</param>
    /// <param name="address">Endereço do leitor OSDP.</param>
    /// <param name="scbkHex">Nova SCBK, em hexadecimal.</param>
    /// <param name="cancellationToken">Cancelamento.</param>
    Task SetOsdpSecureKeyAsync(int panelId, int address, string scbkHex, CancellationToken cancellationToken = default);

    /// <summary><c>osdp_set_secure_type.fcgi</c>: tipo de canal seguro de um leitor de um painel OSDP. Apenas linha iDAccess.</summary>
    /// <param name="panelId">ID do painel OSDP.</param>
    /// <param name="address">Endereço do leitor OSDP.</param>
    /// <param name="secureType">Tipo do canal seguro.</param>
    /// <param name="cancellationToken">Cancelamento.</param>
    Task SetOsdpSecureTypeAsync(int panelId, int address, OsdpSecureType secureType, CancellationToken cancellationToken = default);

    /// <summary><c>osdp_refresh.fcgi</c>: força a redescoberta dos dispositivos OSDP conectados. Apenas linha iDAccess.</summary>
    Task RefreshOsdpAsync(CancellationToken cancellationToken = default);
}
