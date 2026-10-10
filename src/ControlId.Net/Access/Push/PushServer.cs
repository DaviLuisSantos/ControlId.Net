using System;
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
/// O servidor HTTP é seu: encaminhe <c>/push</c> para <see cref="HandlePush"/> e <c>/result</c> para
/// <see cref="HandleResult"/> (ou use o pacote <c>ControlId.Net.AspNetCore</c>).
/// Uma instância deve atender todos os equipamentos (registre como singleton).
/// </para>
/// </summary>
public sealed class PushServer
{
    private readonly object _lock = new();
    private readonly Dictionary<long, DeviceState> _devices = new();

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

        var results = await EnqueueAsync(deviceId, new Pending(new[] { command }, batch: false), cancellationToken).ConfigureAwait(false);
        return results[0];
    }

    /// <summary>
    /// Envia vários comandos numa única consulta do equipamento (objeto <c>transactions</c>) e devolve um resultado
    /// por comando, na mesma ordem. Falhas de um comando não impedem os demais: confira <see cref="PushResult.Success"/>.
    /// </summary>
    public Task<IReadOnlyList<PushResult>> SendBatchAsync(long deviceId, IEnumerable<PushCommand> commands, CancellationToken cancellationToken = default)
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

        return EnqueueAsync(deviceId, new Pending(list, batch: true), cancellationToken);
    }

    /// <summary>Atalhos tipados (load/create/modify/destroy objects, ações) para um equipamento.</summary>
    public PushDevice Device(long deviceId) => new(this, deviceId);

    /// <summary>Última consulta do equipamento a <c>/push</c>, ou <c>null</c> se ele nunca apareceu.</summary>
    public DateTimeOffset? GetLastSeen(long deviceId)
    {
        lock (_lock)
        {
            return _devices.TryGetValue(deviceId, out var state) ? state.LastSeen : null;
        }
    }

    /// <summary>Envios (comandos avulsos ou lotes) aguardando entrega ou resultado.</summary>
    public int GetPendingCount(long deviceId)
    {
        lock (_lock)
        {
            return _devices.TryGetValue(deviceId, out var state) ? state.Queue.Count + (state.InFlight is null ? 0 : 1) : 0;
        }
    }

    /// <summary>
    /// Trata <c>GET /push?deviceId=..&amp;uuid=..</c>. Devolve o JSON do próximo envio, ou string vazia
    /// se não houver nada (responda 200 com esse corpo).
    /// </summary>
    public string HandlePush(long deviceId, string? uuid)
    {
        Pending? orphan;
        Pending? next = null;
        lock (_lock)
        {
            var state = GetState(deviceId);
            state.LastSeen = DateTimeOffset.UtcNow;

            // O equipamento só volta a /push depois de enviar o /result. Se o envio anterior ficou
            // sem resultado (reinício, queda de rede), ele não vai chegar mais.
            orphan = state.InFlight;
            state.InFlight = null;

            while (state.Queue.First?.Value is { } first)
            {
                state.Queue.RemoveFirst();
                if (!first.Completion.Task.IsCompleted)
                {
                    next = first;
                    next.Uuid = uuid;
                    state.InFlight = next;
                    break;
                }
            }
        }

        orphan?.Completion.TrySetException(new ControlIdException(
            $"O equipamento {deviceId} pediu um novo comando sem devolver o resultado de '{orphan.Description}'."));

        if (next is null)
        {
            return string.Empty;
        }

        if (!next.Batch)
        {
            return ControlIdJson.Serialize(next.Commands[0]);
        }

        // A documentação mostra transactionid como string ("1") no envio e como número no resultado.
        var transactions = next.Commands.Select((command, index) => new PushTransaction(index + 1, command)).ToList();
        return ControlIdJson.Serialize(new Dictionary<string, object> { ["transactions"] = transactions });
    }

    /// <summary>
    /// Trata <c>POST /result?deviceId=..</c> com o corpo enviado pelo equipamento (responda 200 vazio).
    /// Devolve <c>false</c> se não havia envio aguardando esse resultado.
    /// </summary>
    public bool HandleResult(long deviceId, string? uuid, string body)
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

        Pending? pending;
        lock (_lock)
        {
            if (!_devices.TryGetValue(deviceId, out var state) || state.InFlight is null)
            {
                return false;
            }

            pending = state.InFlight;
            if (uuid is not null && pending.Uuid is not null && uuid != pending.Uuid)
            {
                return false;
            }

            state.InFlight = null;
        }

        if (invalid is not null)
        {
            pending.Completion.TrySetException(invalid);
        }
        else
        {
            var results = pending.Batch ? ParseBatchResults(deviceId, pending.Commands, root) : new[] { ParseSingleResult(deviceId, pending.Commands[0], root) };
            pending.Completion.TrySetResult(results);
        }

        return true;
    }

    private static PushResult ParseSingleResult(long deviceId, PushCommand command, JsonElement root)
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

        return new PushResult(deviceId, command.Endpoint, response, error);
    }

    private static IReadOnlyList<PushResult> ParseBatchResults(long deviceId, IReadOnlyList<PushCommand> commands, JsonElement root)
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
                        || transactionId > commands.Count)
                    {
                        continue;
                    }

                    var command = commands[transactionId - 1];
                    item.TryGetProperty("response", out var responseElement);
                    var success = !item.TryGetProperty("success", out var successElement) || IsTrue(successElement);

                    byTransaction[transactionId] = success
                        ? new PushResult(deviceId, command.Endpoint, responseElement.ValueKind == JsonValueKind.Undefined ? null : Unwrap(responseElement), null)
                        : new PushResult(deviceId, command.Endpoint, null, ErrorText(responseElement) ?? "O equipamento informou falha sem mensagem.");
                }
            }
        }

        return commands
            .Select((command, index) => byTransaction.TryGetValue(index + 1, out var result)
                ? result
                : new PushResult(deviceId, command.Endpoint, null, generalError ?? "O equipamento não devolveu resultado para este comando do lote."))
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

    private async Task<IReadOnlyList<PushResult>> EnqueueAsync(long deviceId, Pending pending, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            GetState(deviceId).Queue.AddLast(pending);
        }

        using (cancellationToken.Register(() => Cancel(deviceId, pending, cancellationToken)))
        {
            return await pending.Completion.Task.ConfigureAwait(false);
        }
    }

    private void Cancel(long deviceId, Pending pending, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (_devices.TryGetValue(deviceId, out var state))
            {
                state.Queue.Remove(pending);
                if (ReferenceEquals(state.InFlight, pending))
                {
                    state.InFlight = null;
                }
            }
        }

        pending.Completion.TrySetCanceled(cancellationToken);
    }

    private DeviceState GetState(long deviceId)
    {
        if (!_devices.TryGetValue(deviceId, out var state))
        {
            _devices[deviceId] = state = new DeviceState();
        }

        return state;
    }

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

    private sealed class DeviceState
    {
        public LinkedList<Pending> Queue { get; } = new();

        public Pending? InFlight { get; set; }

        public DateTimeOffset? LastSeen { get; set; }
    }

    private sealed class Pending
    {
        public Pending(IReadOnlyList<PushCommand> commands, bool batch)
        {
            Commands = commands;
            Batch = batch;
        }

        public IReadOnlyList<PushCommand> Commands { get; }

        public bool Batch { get; }

        public string Description => Batch ? $"lote de {Commands.Count} comandos" : Commands[0].Endpoint;

        public TaskCompletionSource<IReadOnlyList<PushResult>> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string? Uuid { get; set; }
    }
}
