using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ControlId.Access;

public partial interface IAccessClient
{
    /// <summary>
    /// Leva uma pessoa ao estado desejado: cria ou atualiza o usuário, ajusta cartões e grupos e envia a foto. Equivale a
    /// <see cref="SyncUsersAsync"/> com uma só pessoa, sem remoção. Rodar de novo com o mesmo estado não escreve nada.
    /// </summary>
    /// <remarks>
    /// Falhas de um item (um cartão recusado, uma foto inválida) voltam em <see cref="UserSyncResult.Errors"/>, sem lançar
    /// exceção. Só dado inválido (<see cref="DesiredUser"/> sem ID ou sem nome) e falha ao ler o estado do equipamento lançam.
    /// </remarks>
    Task<UserSyncResult> SyncUserAsync(DesiredUser desired, CancellationToken cancellationToken = default);

    /// <summary>
    /// Leva as pessoas ao estado desejado. Lê <c>users</c>, <c>cards</c>, <c>user_groups</c> e o <c>image_timestamp</c> só das
    /// pessoas informadas e aplica a diferença com <see cref="CreateOrModifyObjectsAsync{T}"/> (usuários),
    /// <see cref="CreateObjectsAsync{T}"/>/<see cref="DestroyObjectsAsync"/> (cartões e grupos) e
    /// <see cref="SetUserImagesAsync(System.Collections.Generic.IEnumerable{UserImage}, bool, CancellationToken)"/> (fotos). Devolve um resultado por pessoa, na ordem
    /// de entrada, e depois um por pessoa removida.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uma falha por pessoa não derruba o lote: cada item que falha entra em <see cref="UserSyncResult.Errors"/> daquela
    /// pessoa, e as demais seguem. Se a gravação do usuário falha, os cartões, grupos e a foto dela são pulados. Não é
    /// transacional: uma execução interrompida deixa o que já foi gravado, e repetir a chamada termina o trabalho.
    /// </para>
    /// <para>
    /// A comparação de senha e de foto usa marcas mantidas em memória por esta instância (ver <see cref="DesiredUser.Password"/>
    /// e <see cref="DesiredUser.Photo"/>): a primeira sincronização de cada instância pode regravar senhas e fotos que
    /// o equipamento não deixa comparar. Depois dela, repetir o mesmo estado não faz nenhuma escrita.
    /// </para>
    /// <para>
    /// Com <see cref="UserSyncOptions.RemoveMissing"/>, as pessoas do equipamento que não estão em <paramref name="desired"/>
    /// são removidas (e a remoção vem antes da gravação de cartões, para um cartão poder trocar de dono).
    /// </para>
    /// </remarks>
    /// <exception cref="System.ArgumentException">Há pessoa nula, sem ID, sem nome, com ID repetido, foto vazia, ou
    /// <see cref="UserSyncOptions.RemoveMissing"/> sem <see cref="DataLossConfirmation.Confirmed"/>.</exception>
    Task<IReadOnlyList<UserSyncResult>> SyncUsersAsync(
        IEnumerable<DesiredUser> desired, UserSyncOptions? options = null, CancellationToken cancellationToken = default);
}
