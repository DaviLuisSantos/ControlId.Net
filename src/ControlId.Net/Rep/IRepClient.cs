using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ControlId.Rep.Afd;

namespace ControlId.Rep;

/// <summary>
/// Cliente da API dos relógios de ponto REP iDClass. A API roda em HTTPS com certificado
/// autoassinado — use <see cref="ControlIdDeviceOptions.AcceptAnyServerCertificate"/>.
/// </summary>
public interface IRepClient : IControlIdClient
{
    /// <summary>Modo usado nas chamadas que o suportam.</summary>
    RepMode Mode { get; }

    /// <summary>Força o login agora (opcional).</summary>
    Task LoginAsync(CancellationToken cancellationToken = default);

    /// <summary>Encerra a sessão.</summary>
    Task LogoutAsync(CancellationToken cancellationToken = default);

    /// <summary><c>get_about.fcgi</c>.</summary>
    Task<RepAbout> GetAboutAsync(CancellationToken cancellationToken = default);

    /// <summary><c>get_system_information.fcgi</c>.</summary>
    Task<RepSystemInformation> GetSystemInformationAsync(CancellationToken cancellationToken = default);

    /// <summary><c>get_system_date_time.fcgi</c>.</summary>
    Task<DateTime> GetDateTimeAsync(CancellationToken cancellationToken = default);

    /// <summary><c>set_system_date_time.fcgi</c>.</summary>
    Task SetDateTimeAsync(DateTime localTime, CancellationToken cancellationToken = default);

    /// <summary><c>count_users.fcgi</c>.</summary>
    Task<int> CountUsersAsync(CancellationToken cancellationToken = default);

    /// <summary><c>load_users.fcgi</c> paginado (máx. 100 por página).</summary>
    Task<RepUserPage> LoadUsersAsync(int limit = 100, int offset = 0, bool includeTemplates = false, CancellationToken cancellationToken = default);

    /// <summary>Percorre todas as páginas de <c>load_users.fcgi</c>.</summary>
    Task<IReadOnlyList<RepUser>> GetAllUsersAsync(bool includeTemplates = false, CancellationToken cancellationToken = default);

    /// <summary><c>add_users.fcgi</c>.</summary>
    Task AddUsersAsync(IEnumerable<RepUser> users, CancellationToken cancellationToken = default);

    /// <summary><c>remove_users.fcgi</c> — PIS (legado) ou CPF (Portaria 671).</summary>
    Task RemoveUsersAsync(IEnumerable<long> identifiers, CancellationToken cancellationToken = default);

    /// <summary><c>get_afd.fcgi</c> a partir de um NSR. Devolve o AFD em texto (decodificado de ISO-8859-1 quando preciso).</summary>
    Task<string> GetAfdFromNsrAsync(long initialNsr, CancellationToken cancellationToken = default);

    /// <summary><c>get_afd.fcgi</c> a partir de uma data. Devolve o AFD em texto.</summary>
    Task<string> GetAfdFromDateAsync(DateTime initialDate, CancellationToken cancellationToken = default);

    /// <summary><c>get_afd.fcgi</c> completo.</summary>
    Task<string> GetAfdAsync(CancellationToken cancellationToken = default);

    /// <summary>AFD a partir de um NSR, já interpretado. Para leitura incremental, use <see cref="AfdFile.LastNsr"/> + 1 na próxima chamada.</summary>
    Task<AfdFile> GetAfdFileFromNsrAsync(long initialNsr, CancellationToken cancellationToken = default);

    /// <summary>AFD a partir de uma data, já interpretado.</summary>
    Task<AfdFile> GetAfdFileFromDateAsync(DateTime initialDate, CancellationToken cancellationToken = default);

    /// <summary>AFD completo, já interpretado.</summary>
    Task<AfdFile> GetAfdFileAsync(CancellationToken cancellationToken = default);
}
