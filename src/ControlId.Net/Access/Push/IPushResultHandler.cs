using System.Threading;
using System.Threading.Tasks;

namespace ControlId.Access.Push;

/// <summary>
/// Recebe o resultado de um envio que ninguém está esperando: enfileirado por <see cref="PushServer.EnqueueAsync"/>
/// ou deixado pendente por uma instância anterior do <see cref="PushServer"/> (store durável).
/// Chamado no máximo uma vez por envio, depois de ele sair do store, inclusive para envios que falharam sem resultado
/// (expirados ou órfãos, com <see cref="PushCompletion.Error"/> preenchido).
/// </summary>
/// <remarks>
/// O handler recebe um token que vence em <see cref="PushServerOptions.ResultHandlerTimeout"/>, sem ligação com a
/// requisição do equipamento. Uma exceção ou o prazo vencido é logado pelo <see cref="PushServer"/> (EventId 1101) e não
/// chega a quem chamou <see cref="PushServer.HandleResultAsync"/>: a resposta ao equipamento segue normalmente e o envio
/// não volta para a fila. No ASP.NET Core (<c>AddControlIdPush</c>), todos os handlers registrados rodam, cada um isolado,
/// em um escopo criado para cada resultado.
/// </remarks>
public interface IPushResultHandler
{
    /// <summary>Trata o resultado.</summary>
    Task HandleAsync(PushCompletion completion, CancellationToken cancellationToken);
}
