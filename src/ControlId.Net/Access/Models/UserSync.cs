using System;
using System.Collections.Generic;

namespace ControlId.Access;

/// <summary>
/// Estado desejado de uma pessoa no equipamento, para <see cref="IAccessClient.SyncUserAsync"/> e
/// <see cref="IAccessClient.SyncUsersAsync"/>. Os campos que são <c>null</c> (exceto as datas, ver abaixo) não são
/// gerenciados: a sincronização os deixa como estão no equipamento.
/// </summary>
public sealed class DesiredUser
{
    /// <summary>ID da pessoa no equipamento (obrigatório e maior que zero): é a chave da comparação e do upsert.</summary>
    public long Id { get; set; }

    /// <summary>Nome (obrigatório).</summary>
    public string? Name { get; set; }

    /// <summary>Matrícula. <c>null</c> = não gerenciar.</summary>
    public string? Registration { get; set; }

    /// <summary>
    /// Início da validade, como hora de parede local do equipamento (o <see cref="DateTime.Kind"/> é ignorado, como em
    /// <c>LocalTime</c> dos logs). <c>null</c> = sem limite (o equipamento grava 0).
    /// </summary>
    public DateTime? BeginTime { get; set; }

    /// <summary>Fim da validade, como <see cref="BeginTime"/>. <c>null</c> = sem limite.</summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Senha em texto. É cifrada com <see cref="IAccessClient.HashPasswordAsync"/> só quando precisa mudar. O equipamento não
    /// devolve a senha em claro e o hash muda a cada chamada (o salt é aleatório), então a comparação usa uma marca mantida
    /// no <see cref="AccessClient"/> (em memória, uma por pessoa): na primeira sincronização de cada instância, a senha de
    /// uma pessoa que já existe é regravada, e as seguintes não escrevem nada se ela não mudou. <c>null</c> ou vazia = não
    /// gerenciar.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Valores dos cartões da pessoa. A sincronização cria os que faltam; os que sobram só são removidos com
    /// <see cref="UserSyncOptions.RemoveUnlisted"/> = <c>true</c> (nesse caso, uma lista vazia remove todos os cartões).
    /// <c>null</c> = não gerenciar. Um cartão que já pertence a outra pessoa é recusado, e o erro aparece só no resultado
    /// desta pessoa.
    /// </summary>
    public IReadOnlyCollection<long>? Cards { get; set; }

    /// <summary>
    /// IDs dos grupos (<c>user_groups</c>) da pessoa: cria os vínculos que faltam; os que sobram só são removidos com
    /// <see cref="UserSyncOptions.RemoveUnlisted"/> = <c>true</c>. <c>null</c> = não gerenciar.
    /// </summary>
    public IReadOnlyCollection<long>? GroupIds { get; set; }

    /// <summary>
    /// Foto (JPEG ou PNG, validada como em <see cref="IAccessClient.SetUserImagesAsync(System.Collections.Generic.IEnumerable{UserImage}, bool, System.Threading.CancellationToken)"/>).
    /// O equipamento não guarda um hash da foto; a comparação usa o <c>image_timestamp</c> do usuário e o SHA-256 da foto
    /// que este <see cref="AccessClient"/> enviou por último. Sem essa marca (outra instância ou outro processo), a foto do
    /// equipamento é baixada e comparada byte a byte: se o firmware regravar a imagem, ela é reenviada uma vez e a marca
    /// passa a valer. <c>null</c> = não gerenciar.
    /// </summary>
    public byte[]? Photo { get; set; }
}

/// <summary>Opções de <see cref="IAccessClient.SyncUsersAsync"/>.</summary>
public sealed class UserSyncOptions
{
    /// <summary>
    /// Remove do equipamento as pessoas que não estão na lista desejada. Exige
    /// <see cref="DataLossConfirmation"/> igual a <see cref="ControlId.DataLossConfirmation.Confirmed"/>, a não ser com
    /// <see cref="DryRun"/> = <c>true</c> (que só relata as remoções e não apaga nada).
    /// </summary>
    public bool RemoveMissing { get; set; }

    /// <summary>
    /// Remove os cartões e vínculos de grupo de cada pessoa que não estão em <see cref="DesiredUser.Cards"/> e
    /// <see cref="DesiredUser.GroupIds"/>. Não remove pessoas: para isso, use <see cref="RemoveMissing"/>. O padrão é
    /// <c>false</c>: sem remoção, a sincronização só cria o que falta e deixa o restante como está no equipamento.
    /// </summary>
    public bool RemoveUnlisted { get; set; }

    /// <summary>
    /// Confirmação de perda de dados, exigida por <see cref="RemoveMissing"/> (exceto com <see cref="DryRun"/> =
    /// <c>true</c>). Não é usada para <see cref="RemoveUnlisted"/>.
    /// </summary>
    public DataLossConfirmation DataLossConfirmation { get; set; }

    /// <summary>
    /// Faz só a comparação, sem escrever no equipamento: nada é criado, alterado ou removido, nenhuma senha é cifrada
    /// (<see cref="IAccessClient.HashPasswordAsync"/>) e nenhuma foto é enviada, só as leituras necessárias
    /// (<c>load_objects</c> e, para comparar uma foto sem marca, <see cref="IAccessClient.GetUserImageAsync"/>). As marcas
    /// internas de senha e foto desta instância não são gravadas nem apagadas, então a execução real seguinte compara o
    /// mesmo estado e aplica a diferença relatada aqui.
    /// </summary>
    /// <remarks>
    /// <para>
    /// O <see cref="UserSyncResult"/> descreve o que <b>seria</b> feito (<see cref="UserSyncResult.Created"/>,
    /// <see cref="UserSyncResult.ChangedFields"/>, <see cref="UserSyncResult.CardsAdded"/>,
    /// <see cref="UserSyncResult.CardsRemoved"/>, <see cref="UserSyncResult.GroupsAdded"/>,
    /// <see cref="UserSyncResult.GroupsRemoved"/>, <see cref="UserSyncResult.PhotoUpdated"/> e
    /// <see cref="UserSyncResult.Removed"/>), respeitando <see cref="RemoveUnlisted"/> e <see cref="RemoveMissing"/>.
    /// </para>
    /// <para>
    /// A checagem de cartão que já pertence a outra pessoa continua valendo e gera o mesmo erro no resultado. Com
    /// <c>true</c>, <see cref="RemoveMissing"/> não exige <see cref="DataLossConfirmation"/>, porque nada é apagado. O
    /// padrão é <c>false</c>.
    /// </para>
    /// </remarks>
    public bool DryRun { get; set; }
}

/// <summary>Resultado resumido da sincronização de uma pessoa.</summary>
public enum UserSyncStatus
{
    /// <summary>Nada mudou.</summary>
    Unchanged = 0,

    /// <summary>A pessoa não existia e foi criada.</summary>
    Created = 1,

    /// <summary>Algum campo, cartão, vínculo de grupo ou foto da pessoa mudou.</summary>
    Changed = 2,

    /// <summary>A pessoa foi removida por <see cref="UserSyncOptions.RemoveMissing"/>.</summary>
    Removed = 3,

    /// <summary>Algum item falhou.</summary>
    Failed = 4,

    /// <summary>
    /// Algum erro veio de uma escrita sem resposta (<see cref="ControlIdOutcomeUnknownException"/>): não se sabe se o
    /// equipamento a executou.
    /// </summary>
    OutcomeUnknown = 5,
}

/// <summary>Um item que não pôde ser aplicado a uma pessoa na sincronização.</summary>
public sealed class UserSyncError
{
    internal UserSyncError(string item, string message, Exception? exception)
    {
        Item = item;
        Message = message;
        Exception = exception;
    }

    /// <summary>O que falhou: <c>user</c>, <c>password</c>, <c>cards</c>, <c>card:&lt;valor&gt;</c>, <c>groups</c>, <c>group:&lt;id&gt;</c> ou <c>photo</c>.</summary>
    public string Item { get; }

    /// <summary>Motivo.</summary>
    public string Message { get; }

    /// <summary>Exceção original, quando o erro veio de uma chamada ao equipamento.</summary>
    public Exception? Exception { get; }

    /// <inheritdoc/>
    public override string ToString() => Item + ": " + Message;
}

/// <summary>
/// Resultado da sincronização de uma pessoa: o que mudou e o que falhou, sem derrubar as demais. Com
/// <see cref="UserSyncOptions.DryRun"/> = <c>true</c>, os mesmos membros descrevem o que mudaria, sem que nada tenha sido
/// escrito no equipamento.
/// </summary>
public sealed class UserSyncResult
{
    internal readonly List<string> ChangedFieldList = new();
    internal readonly List<long> CardsAddedList = new();
    internal readonly List<long> CardsRemovedList = new();
    internal readonly List<long> GroupsAddedList = new();
    internal readonly List<long> GroupsRemovedList = new();
    internal readonly List<UserSyncError> ErrorList = new();

    internal UserSyncResult(long userId) => UserId = userId;

    /// <summary>ID da pessoa.</summary>
    public long UserId { get; }

    /// <summary>
    /// Tudo foi aplicado, ou, em <see cref="UserSyncOptions.DryRun"/>, tudo pôde ser comparado: não há erros.
    /// </summary>
    public bool Success => ErrorList.Count == 0;

    /// <summary>
    /// Houve alguma escrita bem-sucedida para esta pessoa ou, em <see cref="UserSyncOptions.DryRun"/>, alguma mudança que
    /// seria aplicada.
    /// </summary>
    public bool Changed =>
        Created || Removed || PhotoUpdated || ChangedFieldList.Count > 0
        || CardsAddedList.Count > 0 || CardsRemovedList.Count > 0 || GroupsAddedList.Count > 0 || GroupsRemovedList.Count > 0;

    /// <summary>A pessoa não existia e foi criada (ou, em <see cref="UserSyncOptions.DryRun"/>, seria criada).</summary>
    public bool Created { get; internal set; }

    /// <summary>
    /// A pessoa foi removida por <see cref="UserSyncOptions.RemoveMissing"/> (ou, em
    /// <see cref="UserSyncOptions.DryRun"/>, seria removida).
    /// </summary>
    public bool Removed { get; internal set; }

    /// <summary>A foto foi enviada (ou, em <see cref="UserSyncOptions.DryRun"/>, seria enviada).</summary>
    public bool PhotoUpdated { get; internal set; }

    /// <summary>
    /// Campos do usuário gravados (ou, em <see cref="UserSyncOptions.DryRun"/>, que seriam gravados): <c>name</c>,
    /// <c>registration</c>, <c>begin_time</c>, <c>end_time</c>, <c>password</c>.
    /// </summary>
    public IReadOnlyList<string> ChangedFields => ChangedFieldList;

    /// <summary>Cartões criados (ou, em <see cref="UserSyncOptions.DryRun"/>, que seriam criados).</summary>
    public IReadOnlyList<long> CardsAdded => CardsAddedList;

    /// <summary>Cartões removidos (ou, em <see cref="UserSyncOptions.DryRun"/>, que seriam removidos).</summary>
    public IReadOnlyList<long> CardsRemoved => CardsRemovedList;

    /// <summary>Vínculos com grupos criados (ou, em <see cref="UserSyncOptions.DryRun"/>, que seriam criados).</summary>
    public IReadOnlyList<long> GroupsAdded => GroupsAddedList;

    /// <summary>Vínculos com grupos removidos (ou, em <see cref="UserSyncOptions.DryRun"/>, que seriam removidos).</summary>
    public IReadOnlyList<long> GroupsRemoved => GroupsRemovedList;

    /// <summary>Erros por item. Um item com erro não impede os demais itens da mesma pessoa, nem as outras pessoas.</summary>
    public IReadOnlyList<UserSyncError> Errors => ErrorList;

    /// <summary>
    /// Resultado resumido: <see cref="UserSyncStatus.OutcomeUnknown"/> se algum erro tem
    /// <see cref="ControlIdOutcomeUnknownException"/>; senão <see cref="UserSyncStatus.Failed"/> se há erros; senão
    /// <see cref="UserSyncStatus.Removed"/>, <see cref="UserSyncStatus.Created"/>, <see cref="UserSyncStatus.Changed"/> ou
    /// <see cref="UserSyncStatus.Unchanged"/>, nessa ordem.
    /// </summary>
    public UserSyncStatus Status
    {
        get
        {
            foreach (var error in ErrorList)
            {
                if (error.Exception is ControlIdOutcomeUnknownException)
                {
                    return UserSyncStatus.OutcomeUnknown;
                }
            }

            if (ErrorList.Count > 0)
            {
                return UserSyncStatus.Failed;
            }

            if (Removed)
            {
                return UserSyncStatus.Removed;
            }

            if (Created)
            {
                return UserSyncStatus.Created;
            }

            return Changed ? UserSyncStatus.Changed : UserSyncStatus.Unchanged;
        }
    }

    internal void Fail(string item, string message, Exception? exception = null) =>
        ErrorList.Add(new UserSyncError(item, message, exception));
}
