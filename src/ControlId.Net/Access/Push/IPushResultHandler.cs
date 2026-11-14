using System.Threading;
using System.Threading.Tasks;

namespace ControlId.Access.Push;

/// <summary>
/// Recebe o resultado de um envio que ninguém está esperando: enfileirado por <see cref="PushServer.EnqueueAsync"/>
/// ou deixado pendente por uma instância anterior do <see cref="PushServer"/> (store durável).
/// Chamado no máximo uma vez por envio, depois de ele sair do store. Uma exceção do handler chega a quem chamou
/// <see cref="PushServer.HandleResultAsync"/>, mas o envio não volta para a fila.
/// </summary>
public interface IPushResultHandler
{
    /// <summary>Trata o resultado.</summary>
    Task HandleAsync(PushCompletion completion, CancellationToken cancellationToken);
}
