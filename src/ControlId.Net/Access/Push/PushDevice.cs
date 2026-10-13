using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ControlId.Internal;

namespace ControlId.Access.Push;

/// <summary>
/// Operações tipadas sobre um equipamento em modo Push. Cada chamada espera o equipamento
/// buscar o comando e devolver o resultado, então use <see cref="CancellationToken"/> com prazo.
/// </summary>
public sealed class PushDevice
{
    private readonly PushServer _server;

    internal PushDevice(PushServer server, long deviceId)
    {
        _server = server;
        DeviceId = deviceId;
    }

    /// <summary>ID do equipamento.</summary>
    public long DeviceId { get; }

    /// <summary>Envia um comando qualquer e devolve o resultado bruto.</summary>
    public Task<PushResult> SendAsync(PushCommand command, CancellationToken cancellationToken = default) =>
        _server.SendAsync(DeviceId, command, cancellationToken);

    /// <summary>
    /// Envia vários comandos numa única consulta do equipamento; um resultado por comando, na mesma ordem. Veja em
    /// <see cref="PushServer.SendBatchAsync"/> os firmwares que não aceitam lotes.
    /// </summary>
    public Task<IReadOnlyList<PushResult>> SendBatchAsync(IEnumerable<PushCommand> commands, CancellationToken cancellationToken = default) =>
        _server.SendBatchAsync(DeviceId, commands, cancellationToken);

    /// <summary><c>load_objects</c>.</summary>
    public async Task<IReadOnlyList<T>> LoadObjectsAsync<T>(LoadObjectsRequest request, CancellationToken cancellationToken = default)
    {
        var result = await SendAsync(PushCommand.LoadObjects(request), cancellationToken).ConfigureAwait(false);
        var response = result.ResponseAs<Dictionary<string, List<T>>>();
        return response.TryGetValue(request.Object, out var items) ? items : new List<T>();
    }

    /// <summary><c>create_objects</c> — devolve os IDs criados.</summary>
    public async Task<IReadOnlyList<long>> CreateObjectsAsync<T>(string @object, IEnumerable<T> values, CancellationToken cancellationToken = default)
    {
        var result = await SendAsync(PushCommand.CreateObjects(@object, values), cancellationToken).ConfigureAwait(false);
        return result.ResponseAs<IdsResponse>().Ids ?? new List<long>();
    }

    /// <summary><c>modify_objects</c> — devolve a quantidade de registros alterados.</summary>
    public async Task<int> ModifyObjectsAsync(string @object, object values, object where, CancellationToken cancellationToken = default)
    {
        var result = await SendAsync(PushCommand.ModifyObjects(@object, values, where), cancellationToken).ConfigureAwait(false);
        return result.ResponseAs<ChangesResponse>().Changes;
    }

    /// <summary><c>destroy_objects</c> — devolve a quantidade de registros removidos.</summary>
    public async Task<int> DestroyObjectsAsync(string @object, object where, CancellationToken cancellationToken = default)
    {
        var result = await SendAsync(PushCommand.DestroyObjects(@object, where), cancellationToken).ConfigureAwait(false);
        return result.ResponseAs<ChangesResponse>().Changes;
    }

    /// <summary><c>execute_actions</c>.</summary>
    public async Task ExecuteActionsAsync(IEnumerable<DeviceAction> actions, CancellationToken cancellationToken = default)
    {
        var result = await SendAsync(PushCommand.ExecuteActions(actions.ToArray()), cancellationToken).ConfigureAwait(false);
        result.EnsureSuccess();
    }

    /// <summary>Aciona o relé da porta (<c>door=N</c>).</summary>
    public Task OpenDoorAsync(int door = 1, CancellationToken cancellationToken = default) =>
        ExecuteActionsAsync(new[] { DeviceAction.OpenDoor(door) }, cancellationToken);
}
