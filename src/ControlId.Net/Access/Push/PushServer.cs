using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ControlId.Internal;

namespace ControlId.Access.Push;

/// <summary>
/// Lado servidor do modo Push: guarda uma fila de comandos por equipamento e entrega um envio por vez
/// (um comando ou um lote) quando o equipamento consulta <c>GET /push</c>. O resultado chega em
/// <c>POST /result</c> e completa a tarefa devolvida por <see cref="SendAsync"/> ou <see cref="SendBatchAsync"/>.
/// <para>
/// O servidor HTTP é seu: encaminhe <c>/push</c> para <see cref="HandlePushAsync"/> e <c>/result</c> para
/// <see cref="HandleResultAsync"/> (ou use o pacote <c>ControlId.Net.AspNetCore</c>). Os métodos síncronos
/// <see cref="HandlePush"/> e <see cref="HandleResult"/> bloqueiam sobre os assíncronos: prefira os <c>*Async</c>.
/// Uma instância deve atender todos os equipamentos (registre como singleton).
/// </para>
/// <para>
/// A fila fica num <see cref="IPushCommandStore"/> (em memória por padrão; um store durável preserva os
/// comandos pendentes num reinício). Use uma instância de <see cref="PushServer"/> por store. O comando é
/// serializado ao entrar na fila: alterá-lo depois não muda o que o equipamento recebe. Um envio que o equipamento
/// deixou sem resultado (órfão) falha e nunca é reenviado. O resultado de um envio que ninguém espera
/// (<see cref="EnqueueAsync"/>, ou pendente de uma instância anterior) vai ao <see cref="IPushResultHandler"/>.
/// </para>
/// </summary>
public sealed class PushServer
{
    private readonly IPushCommandStore _store;
    private readonly IPushResultHandler? _unclaimedResultHandler;
    private readonly ConcurrentDictionary<long, SemaphoreSlim> _gates = new();
    private readonly ConcurrentDictionary<long, DateTimeOffset> _lastSeen = new();
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<IReadOnlyList<PushResult>>> _waiters = new();

    /// <summary>Cria o servidor com a fila em memória.</summary>
    public PushServer()
        : this(new InMemoryPushCommandStore())
    {
    }

    /// <summary>Cria o servidor sobre um <paramref name="store"/> (fila durável, por exemplo).</summary>
    /// <param name="store">Fila de envios. Não a compartilhe com outra instância ativa.</param>
    /// <param name="unclaimedResultHandler">Recebe o resultado dos envios que nenhuma tarefa espera, ou <c>null</c> para descartá-los.</param>
    public PushServer(IPushCommandStore store, IPushResultHandler? unclaimedResultHandler = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _unclaimedResultHandler = unclaimedResultHandler;
    }

    /// <summary>
    /// Enfileira o comando e espera o equipamento executá-lo. Sem consultas do equipamento, a tarefa não termina:
    /// use <paramref name="cancellationToken"/> para impor um prazo. Cancelar antes da entrega tira o comando da fila.
    /// </summary>
    public async Task<PushResult> SendAsync(long deviceId, PushCommand command, CancellationToken cancellationToken = default)
    {
        if (command is null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        var results = await WaitForResultsAsync(deviceId, new[] { command }, batch: false, cancellationToken).ConfigureAwait(false);
        return results[0];
    }

    /// <summary>
    /// Envia vários comandos numa única consulta do equipamento (objeto <c>transactions</c>) e devolve um resultado
    /// por comando, na mesma ordem. Falhas de um comando não impedem os demais: confira <see cref="PushResult.Success"/>.
    /// <para>
    /// Nem todo firmware aceita lotes: o iDFace 6.9.12 trata o corpo como um comando só e responde
    /// "Invalid member 'endpoint'", que chega como erro em todos os resultados. Nesse caso, envie um a um com <see cref="SendAsync"/>.
    /// </para>
    /// </summary>
    public Task<IReadOnlyList<PushResult>> SendBatchAsync(long deviceId, IEnumerable<PushCommand> commands, CancellationToken cancellationToken = default) =>
        WaitForResultsAsync(deviceId, ValidateBatch(commands), batch: true, cancellationToken);

    /// <summary>
    /// Enfileira o comando sem esperar o resultado e devolve o identificador do envio. O resultado chega ao
    /// <see cref="IPushResultHandler"/> (<see cref="PushCompletion.CommandId"/> é o valor devolvido).
    /// </summary>
    public Task<Guid> EnqueueAsync(long deviceId, PushCommand command, CancellationToken cancellationToken = default)
    {
        if (command is null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        return AddCoreAsync(deviceId, new[] { command }, batch: false, waiter: null, cancellationToken);
    }

    /// <summary>Como <see cref="EnqueueAsync"/>, para vários comandos numa única consulta do equipamento (lote).</summary>
    public Task<Guid> EnqueueBatchAsync(long deviceId, IEnumerable<PushCommand> commands, CancellationToken cancellationToken = default) =>
        AddCoreAsync(deviceId, ValidateBatch(commands), batch: true, waiter: null, cancellationToken);

    /// <summary>Atalhos tipados (load/create/modify/destroy objects, ações) para um equipamento.</summary>
    public PushDevice Device(long deviceId) => new(this, deviceId);

    /// <summary>Última consulta do equipamento a <c>/push</c>, ou <c>null</c> se ele nunca apareceu (só nesta instância).</summary>
    public DateTimeOffset? GetLastSeen(long deviceId) =>
        _lastSeen.TryGetValue(deviceId, out var seen) ? seen : null;

    /// <summary>Envios (comandos avulsos ou lotes) aguardando entrega ou resultado.</summary>
    public Task<int> GetPendingCountAsync(long deviceId, CancellationToken cancellationToken = default) =>
        _store.CountAsync(deviceId, cancellationToken);

    /// <summary>Envios aguardando entrega ou resultado. Bloqueia sobre <see cref="GetPendingCountAsync"/>; prefira o assíncrono.</summary>
    public int GetPendingCount(long deviceId) =>
        GetPendingCountAsync(deviceId).GetAwaiter().GetResult();

    /// <summary>
    /// Trata <c>GET /push?deviceId=..&amp;uuid=..</c>. Devolve o JSON do próximo envio, ou string vazia
    /// se não houver nada (responda 200 com esse corpo).
    /// </summary>
    public async Task<string> HandlePushAsync(long deviceId, string? uuid, CancellationToken cancellationToken = default)
    {
        _lastSeen[deviceId] = DateTimeOffset.UtcNow;

        PushTake take;
        var gate = GetGate(deviceId);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // O equipamento só volta a /push depois de enviar o /result. Se o envio anterior ficou
            // sem resultado (reinício, queda de rede), ele não vai chegar mais.
            take = await _store.TakeNextAsync(deviceId, uuid, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }

        if (take.Orphan is { } orphan)
        {
            var message = $"O equipamento {deviceId} pediu um novo comando sem devolver o resultado de '{orphan.Description}'.";
            if (_waiters.TryRemove(orphan.Id, out var orphanWaiter))
            {
                orphanWaiter.TrySetException(new ControlIdException(message));
            }
            else if (_unclaimedResultHandler is not null)
            {
                // Envio sem tarefa esperando (EnqueueAsync, ou perdido num reinício): avisa pelo handler. Nunca é reenviado.
                var completion = new PushCompletion(orphan.Id, deviceId, Array.Empty<PushResult>(), message);
                await _unclaimedResultHandler.HandleAsync(completion, cancellationToken).ConfigureAwait(false);
            }
        }

        return take.Next?.Payload ?? string.Empty;
    }

    /// <summary>Versão síncrona de <see cref="HandlePushAsync"/> (bloqueia sobre ela; prefira a assíncrona).</summary>
    public string HandlePush(long deviceId, string? uuid) =>
        HandlePushAsync(deviceId, uuid).GetAwaiter().GetResult();

    /// <summary>
    /// Trata <c>POST /result?deviceId=..</c> com o corpo enviado pelo equipamento (responda 200 vazio).
    /// Devolve <c>false</c> se não havia envio aguardando esse resultado.
    /// </summary>
    public async Task<bool> HandleResultAsync(long deviceId, string? uuid, string body, CancellationToken cancellationToken = default)
    {
        JsonElement root = default;
        ControlIdException? invalid = null;
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            root = document.RootElement.Clone();
            if (uuid is null && root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("uuid", out var uuidElement) && uuidElement.ValueKind == JsonValueKind.String)
            {
                uuid = uuidElement.GetString();
            }
        }
        catch (JsonException ex)
        {
            invalid = new ControlIdException($"Resultado de Push inválido do equipamento {deviceId}.", responseBody: body, innerException: ex);
        }

        PushEnvelope? envelope;
        var gate = GetGate(deviceId);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            envelope = await _store.CompleteAsync(deviceId, uuid, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }

        if (envelope is null)
        {
            return false;
        }

        IReadOnlyList<PushResult>? results = null;
        if (invalid is null)
        {
            results = envelope.Batch
                ? ParseBatchResults(deviceId, envelope.Endpoints, root)
                : new[] { ParseSingleResult(deviceId, envelope.Endpoints[0], root) };
        }

        if (_waiters.TryRemove(envelope.Id, out var waiter))
        {
            if (results is not null)
            {
                waiter.TrySetResult(results);
            }
            else
            {
                waiter.TrySetException(invalid!);
            }
        }
        else if (_unclaimedResultHandler is not null)
        {
            var completion = new PushCompletion(envelope.Id, deviceId, results ?? Array.Empty<PushResult>(), invalid?.Message);
            await _unclaimedResultHandler.HandleAsync(completion, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>Versão síncrona de <see cref="HandleResultAsync"/> (bloqueia sobre ela; prefira a assíncrona).</summary>
    public bool HandleResult(long deviceId, string? uuid, string body) =>
        HandleResultAsync(deviceId, uuid, body).GetAwaiter().GetResult();

    private static PushResult ParseSingleResult(long deviceId, string endpoint, JsonElement root)
    {
        JsonElement? response = null;
        string? error = null;
        if (root.ValueKind == JsonValueKind.Object)
        {
            error = ReadError(root);
            if (root.TryGetProperty("response", out var responseElement))
            {
                response = Unwrap(responseElement);
            }
        }

        return new PushResult(deviceId, endpoint, response, error);
    }

    private static List<PushResult> ParseBatchResults(long deviceId, IReadOnlyList<string> endpoints, JsonElement root)
    {
        var byTransaction = new Dictionary<int, PushResult>();
        string? generalError = null;

        if (root.ValueKind == JsonValueKind.Object)
        {
            generalError = ReadError(root);

            // A tabela da documentação diz "transaction_results"; o exemplo, "transactions_results".
            if ((root.TryGetProperty("transactions_results", out var items) || root.TryGetProperty("transaction_results", out items))
                && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object
                        || !item.TryGetProperty("transactionid", out var idElement)
                        || !TryReadInt(idElement, out var transactionId)
                        || transactionId < 1
                        || transactionId > endpoints.Count)
                    {
                        continue;
                    }

                    var endpoint = endpoints[transactionId - 1];
                    item.TryGetProperty("response", out var responseElement);
                    var success = !item.TryGetProperty("success", out var successElement) || IsTrue(successElement);

                    byTransaction[transactionId] = success
                        ? new PushResult(deviceId, endpoint, responseElement.ValueKind == JsonValueKind.Undefined ? null : Unwrap(responseElement), null)
                        : new PushResult(deviceId, endpoint, null, ErrorText(responseElement) ?? "O equipamento informou falha sem mensagem.");
                }
            }
        }

        return endpoints
            .Select((endpoint, index) => byTransaction.TryGetValue(index + 1, out var result)
                ? result
                : new PushResult(deviceId, endpoint, null, generalError ?? "O equipamento não devolveu resultado para este comando do lote."))
            .ToList();
    }

    private static string? ReadError(JsonElement root) =>
        root.TryGetProperty("error", out var errorElement) ? ErrorText(errorElement) : null;

    private static string? ErrorText(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Undefined or JsonValueKind.Null => null,
        JsonValueKind.String => element.GetString(),
        _ => element.GetRawText(),
    };

    private static bool TryReadInt(JsonElement element, out int value)
    {
        value = 0;
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetInt32(out value),
            JsonValueKind.String => int.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value),
            _ => false,
        };
    }

    private static bool IsTrue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.Number => element.TryGetInt64(out var number) && number != 0,
        JsonValueKind.String => element.GetString() is "true" or "1",
        _ => false,
    };

    // A documentação descreve "response" como string; na prática pode vir JSON já aninhado ou JSON serializado em string.
    private static JsonElement Unwrap(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            var text = element.GetString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                try
                {
                    using var inner = JsonDocument.Parse(text!);
                    return inner.RootElement.Clone();
                }
                catch (JsonException)
                {
                    // Texto simples: mantém como string.
                }
            }
        }

        return element.Clone();
    }

    private static List<PushCommand> ValidateBatch(IEnumerable<PushCommand> commands)
    {
        if (commands is null)
        {
            throw new ArgumentNullException(nameof(commands));
        }

        var list = commands.ToList();
        if (list.Count == 0)
        {
            throw new ArgumentException("Informe ao menos um comando.", nameof(commands));
        }

        if (list.Any(c => c is null))
        {
            throw new ArgumentException("O lote contém um comando nulo.", nameof(commands));
        }

        return list;
    }

    private async Task<IReadOnlyList<PushResult>> WaitForResultsAsync(long deviceId, IReadOnlyList<PushCommand> commands, bool batch, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var waiter = new TaskCompletionSource<IReadOnlyList<PushResult>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var id = await AddCoreAsync(deviceId, commands, batch, waiter, cancellationToken).ConfigureAwait(false);

        using (cancellationToken.Register(() => _ = CancelAsync(deviceId, id, waiter, cancellationToken)))
        {
            return await waiter.Task.ConfigureAwait(false);
        }
    }

    // Serializa o payload agora: o que o equipamento receber não muda se o PushCommand for alterado depois.
    private async Task<Guid> AddCoreAsync(
        long deviceId,
        IReadOnlyList<PushCommand> commands,
        bool batch,
        TaskCompletionSource<IReadOnlyList<PushResult>>? waiter,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string payload;
        if (!batch)
        {
            payload = ControlIdJson.Serialize(commands[0]);
        }
        else
        {
            // A documentação mostra transactionid como string ("1") no envio e como número no resultado.
            var transactions = commands.Select((command, index) => new PushTransaction(index + 1, command)).ToList();
            payload = ControlIdJson.Serialize(new Dictionary<string, object> { ["transactions"] = transactions });
        }

        var id = Guid.NewGuid();
        var envelope = new PushEnvelope(id, deviceId, batch, commands.Select(c => c.Endpoint).ToList(), payload, DateTimeOffset.UtcNow);

        if (waiter is not null)
        {
            _waiters[id] = waiter;
        }

        try
        {
            var gate = GetGate(deviceId);
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await _store.AddAsync(envelope, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }
        catch
        {
            _waiters.TryRemove(id, out _);
            throw;
        }

        return id;
    }

    // Tira o envio do store (pendente ou em andamento) e só então cancela a tarefa de quem espera.
    private async Task CancelAsync(long deviceId, Guid id, TaskCompletionSource<IReadOnlyList<PushResult>> waiter, CancellationToken cancellationToken)
    {
        try
        {
            var gate = GetGate(deviceId);
            await gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                await _store.RemoveAsync(id, CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }
        catch (Exception)
        {
            // Falha do store ao remover: o cancelamento de quem espera segue assim mesmo.
        }
        finally
        {
            _waiters.TryRemove(id, out _);
            waiter.TrySetCanceled(cancellationToken);
        }
    }

    private SemaphoreSlim GetGate(long deviceId) => _gates.GetOrAdd(deviceId, _ => new SemaphoreSlim(1, 1));

    private sealed class PushTransaction
    {
        public PushTransaction(int transactionId, PushCommand command)
        {
            TransactionId = transactionId.ToString(CultureInfo.InvariantCulture);
            Command = command;
        }

        [JsonPropertyName("transactionid")]
        public string TransactionId { get; }

        [JsonIgnore]
        public PushCommand Command { get; }

        [JsonPropertyName("verb")]
        public string Verb => Command.Verb;

        [JsonPropertyName("endpoint")]
        public string Endpoint => Command.Endpoint;

        [JsonPropertyName("body")]
        public object? Body => Command.Body;

        [JsonPropertyName("contentType")]
        public string ContentType => Command.ContentType;

        [JsonPropertyName("queryString")]
        public string? QueryString => Command.QueryString;
    }
}
