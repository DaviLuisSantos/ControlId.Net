using System.Threading;
using System.Threading.Tasks;

namespace ControlId.Access;

public partial interface IAccessClient
{
    /// <summary>
    /// <c>master_password.fcgi</c> — define a senha mestra do equipamento a partir do texto simples. A senha não aparece
    /// em exceção nem em log.
    /// </summary>
    /// <exception cref="System.ArgumentException"><paramref name="password"/> é nula ou vazia.</exception>
    Task SetMasterPasswordAsync(string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>master_password.fcgi</c> sem <c>password</c> — devolve só o hash e o salt da senha mestra atual (o equipamento
    /// não devolve a senha).
    /// </summary>
    Task<MasterPasswordInfo> GetMasterPasswordInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>recovery_password.fcgi</c> — define ou altera a senha do modo de update, que protege a restauração de fábrica
    /// feita a partir dele. Requer sessão de administrador. Apenas firmware V7 (iDFace Max V7.x.x). A senha não aparece
    /// em exceção nem em log.
    /// </summary>
    /// <param name="password">Exatamente 6 dígitos numéricos; <c>001818</c> não é aceita pelo equipamento.</param>
    /// <param name="cancellationToken">Cancela a chamada.</param>
    /// <exception cref="System.ArgumentException"><paramref name="password"/> não tem 6 dígitos ou é <c>001818</c>.</exception>
    Task SetUpdateModePasswordAsync(string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>recovery_password.fcgi</c> com <c>enabled: false</c> — remove a senha do modo de update (sem erro se não havia
    /// senha). Requer sessão de administrador. Apenas firmware V7 (iDFace Max V7.x.x).
    /// </summary>
    Task RemoveUpdateModePasswordAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>recovery_password.fcgi</c> sem parâmetros — consulta se há senha do modo de update definida (<c>true</c>) sem
    /// alterá-la. Requer sessão de administrador. Apenas firmware V7 (iDFace Max V7.x.x).
    /// </summary>
    Task<bool> GetUpdateModePasswordStatusAsync(CancellationToken cancellationToken = default);
}
