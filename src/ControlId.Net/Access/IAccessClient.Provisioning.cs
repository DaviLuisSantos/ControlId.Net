using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ControlId.Access;

public partial interface IAccessClient
{
    /// <summary>
    /// <c>upgrade_ten_thousand_templates.fcgi</c> — aumenta para 10.000 a capacidade de templates de digital do iDFlex,
    /// iDAccess H2, iDFit H2, iDAccess Pro e iDAccess Nano. Exige a senha de licença fornecida pela Control iD, que não
    /// aparece em exceção nem em log.
    /// </summary>
    /// <param name="password">Senha de licença.</param>
    /// <param name="cancellationToken">Cancela a chamada.</param>
    /// <exception cref="System.ArgumentException"><paramref name="password"/> é nula ou vazia.</exception>
    Task UpgradeFingerprintCapacityAsync(string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>set_pro_operation_mode_and_reboot.fcgi</c> — alterna o modo Pro de capacidade de templates faciais já licenciado,
    /// sem nova senha. Exige a licença Pro 10k ativada. Firmware V6, V7 e V8 do iDFace e do iDFace Max. ATENÇÃO: o
    /// equipamento REINICIA cerca de 1 segundo após responder, e a troca de modo no iDFace Max apaga os dados.
    /// </summary>
    /// <param name="mode">iDFace: <c>10k</c>, <c>15k</c> ou <c>30k</c>; iDFace Max: <c>20k</c>, <c>50k</c> ou <c>100k</c>.</param>
    /// <param name="cancellationToken">Cancela a chamada.</param>
    /// <exception cref="System.ArgumentException"><paramref name="mode"/> não é um dos valores da documentação.</exception>
    Task SetProOperationModeAndRebootAsync(string mode, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>request_redirect.fcgi</c> — encaminha uma requisição FCGI a outro equipamento da rede e devolve a resposta dele
    /// como texto. ATENÇÃO: no modo por endereço, o login e a senha do destino vão na QUERY STRING da requisição, visível
    /// em logs de proxy e do equipamento; prefira o modo por ID.
    /// </summary>
    /// <param name="target">Destino: <see cref="RedirectTarget.ByDeviceId(int)"/> ou <see cref="RedirectTarget.ByAddress(string, string, string)"/>.</param>
    /// <param name="command">Endpoint de destino sem o sufixo <c>.fcgi</c>, por exemplo <c>load_all</c>.</param>
    /// <param name="body">Corpo repassado ao destino, serializado como JSON; <c>null</c> envia <c>{}</c>.</param>
    /// <param name="query">Parâmetros extras de query repassados ao destino; não pode usar os nomes de roteamento.</param>
    /// <param name="cancellationToken">Cancela a chamada.</param>
    /// <exception cref="System.ArgumentException"><paramref name="command"/> vazio ou <paramref name="query"/> com nome reservado.</exception>
    Task<string> RedirectRequestAsync(
        RedirectTarget target,
        string command,
        object? body = null,
        IReadOnlyDictionary<string, string>? query = null,
        CancellationToken cancellationToken = default);
}
