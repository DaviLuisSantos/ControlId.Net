using System;
using System.Threading;
using System.Threading.Tasks;

namespace ControlId.Access;

public partial interface IAccessClient
{
    /// <summary>
    /// Inicia o cadastro remoto assíncrono (<c>remote_enroll.fcgi</c> sem <c>sync</c>) e acompanha o equipamento por
    /// <c>enroller_state.fcgi</c> até ele sair do estado de cadastro. O resultado é montado de
    /// <see cref="EnrollerState.LastEnrollSucceeded"/> e <see cref="EnrollerState.LastEnrollError"/>; não depende do Monitor.
    /// <para>
    /// Se <paramref name="timeout"/> vencer, o cadastro é cancelado no equipamento e a chamada lança
    /// <see cref="ControlIdTimeoutException"/>. Se <paramref name="cancellationToken"/> for cancelado, o cadastro também é
    /// cancelado no equipamento e a <see cref="OperationCanceledException"/> segue para quem chamou.
    /// </para>
    /// <para>
    /// Limitações do iDFace 6.9.12: com <see cref="RemoteEnrollRequest.Save"/> <c>false</c> a tela de cadastro não abre
    /// (use <c>true</c> e <see cref="RemoteEnrollRequest.UserId"/>), e o estado final não traz o valor capturado: o resultado
    /// devolve só sucesso, erro e <see cref="RemoteEnrollResult.UserId"/>, sem cartão, PIN ou imagem. Para o PIN o
    /// equipamento costuma devolver o erro <c>UNKNOWN</c> mesmo quando grava; confira o usuário depois de terminar.
    /// </para>
    /// </summary>
    /// <param name="request">Tipo de cadastro e opções.</param>
    /// <param name="timeout">Prazo total da espera; padrão 60 s (o equipamento desiste por conta própria em cerca de 60 s).</param>
    /// <param name="pollInterval">Intervalo entre as consultas de estado; padrão 500 ms.</param>
    /// <param name="cancellationToken">Cancela a espera e o cadastro no equipamento.</param>
    Task<RemoteEnrollResult> EnrollAndWaitAsync(
        RemoteEnrollRequest request,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null,
        CancellationToken cancellationToken = default);
}
