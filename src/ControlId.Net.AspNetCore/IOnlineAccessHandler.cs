using System.Threading;
using System.Threading.Tasks;
using ControlId.Access.Online;

namespace ControlId.AspNetCore;

/// <summary>
/// Regras de acesso do modo online, chamadas pelas rotas de <c>MapControlIdOnline</c>.
/// Registre com <c>services.AddControlIdOnline&lt;THandler&gt;()</c> (scoped: pode receber DbContext etc.).
/// </summary>
/// <remarks>
/// O equipamento espera no máximo <c>online_client.request_timeout</c> (até 5 s). Exceções e respostas lentas contam como
/// falha de comunicação: depois de <c>max_request_attempts</c> falhas o equipamento entra em contingência e passa a
/// decidir sozinho com os dados locais.
/// </remarks>
public interface IOnlineAccessHandler
{
    /// <summary>
    /// Decide uma tentativa de acesso. Use <c>switch</c> em <paramref name="identification"/>:
    /// <see cref="UserIdentifiedEvent"/> no modo Pro; <see cref="CardEvent"/>, <see cref="QrCodeEvent"/>,
    /// <see cref="UhfTagEvent"/>, <see cref="UserIdAndPasswordEvent"/> e <see cref="BiometricEvent"/> no Enterprise.
    /// </summary>
    Task<OnlineAuthorization> AuthorizeAsync(OnlineIdentificationEvent identification, CancellationToken cancellationToken);

    /// <summary>Acesso pela botoeira (<c>new_rex_log</c>). Só informativo: o equipamento já liberou.</summary>
    Task OnExitButtonAsync(ExitButtonEvent exitButton, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Resultado de um cadastro remoto feito com o equipamento em modo online (<c>card_create</c>, <c>face_create</c>...).
    /// Nesse modo o resultado vem por aqui, e não pelo Monitor.
    /// </summary>
    Task OnRemoteEnrollAsync(OnlineEnrollEvent enrollment, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Foto do usuário, pedida pelo equipamento quando a autorização tem <see cref="OnlineAuthorization.UserImage"/>.
    /// Devolva JPEG, PNG ou BMP, ou <c>null</c> se não houver (responde 404).
    /// </summary>
    Task<byte[]?> GetUserImageAsync(long userId, CancellationToken cancellationToken) => Task.FromResult<byte[]?>(null);
}
