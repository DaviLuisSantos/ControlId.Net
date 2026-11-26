using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ControlId.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
/// <para>
/// Prazos e limites ficam em <see cref="PushServerOptions"/>: prazo do <see cref="SendAsync"/>, validade dos pendentes,
/// tamanho da fila por equipamento e prazo do handler. Uma falha do <see cref="IPushResultHandler"/> é logada e nunca
/// chega à resposta do equipamento.
/// </para>
/// </summary>
public sealed class PushServer
{
    // Gates em faixas: memória fixa qualquer que seja o deviceId da query. Equipamentos na mesma faixa (raro) dividem
    // o gate e são atendidos em série, o que só custa um pouco de espera; um mesmo equipamento continua sempre em série.
    internal const int GateCount = 256;

    // Prazo para tirar do store um envio cancelado ou vencido, feito em segundo plano.
    private static readonly TimeSpan BackgroundRemoveTimeout = TimeSpan.FromSeconds(10);

    private readonly IPushCommandStore _store;
    private readonly IPushResultHandler? _unclaimedResultHandler;
    private readonly ILogger _logger;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Func<TimeSpan, CancellationTokenSource> _deadlineFactory;
    private readonly TimeSpan? _commandTimeout;
    private readonly TimeSpan? _commandTtl;
    private readonly TimeSpan? _inFlightTimeout;
    private readonly int _maxPendingPerDevice;
    private readonly int _maxTrackedDevices;
    private readonly bool _requireUuid;
    private readonly bool? _useTransactions;
    private readonly TimeSpan _resultHandlerTimeout;
    private readonly SemaphoreSlim[] _gates = CreateGates();
    private readonly ConcurrentDictionary<long, DateTimeOffset> _lastSeen = new();
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<IReadOnlyList<PushResult>>> _waiters = new();

    // Envios cujo waiter já desistiu (cancelamento ou CommandTimeout) e que ainda podem estar no store: o resultado ou
    // a falha deles é descartado em vez de ir ao handler.
    private readonly ConcurrentDictionary<Guid, byte> _abandoned = new();

    // Equipamentos que recusaram o formato "transactions": os lotes deles saem como envios individuais.
    private readonly ConcurrentDictionary<long, byte> _noTransactions = new();

    // Lotes desmembrados em envios individuais. O grupo usa o id do lote original (é o id de quem espera); cada
    // envio individual aponta de volta para o grupo e a posição dele.
    private readonly ConcurrentDictionary<Guid, BatchGroup> _groups = new();
    private readonly ConcurrentDictionary<Guid, GroupMember> _groupByMember = new();
    private int _trackedDevices;
    private int _trackingLimitLogged;

    /// <summary>Cria o servidor com a fila em memória e as opções padrão.</summary>
    public PushServer()
        : this(new InMemoryPushCommandStore())
    {
    }

    /// <summary>Cria o servidor sobre um <paramref name="store"/> (fila durável, por exemplo), com as opções padrão.</summary>
    /// <param name="store">Fila de envios. Não a compartilhe com outra instância ativa.</param>
    /// <param name="unclaimedResultHandler">Recebe o resultado dos envios que nenhuma tarefa espera, ou <c>null</c> para descartá-los.</param>
    public PushServer(IPushCommandStore store, IPushResultHandler? unclaimedResultHandler = null)
        : this(store, new PushServerOptions(), unclaimedResultHandler)
    {
    }

    /// <summary>Cria o servidor sobre um <paramref name="store"/> com prazos e limites próprios.</summary>
    /// <param name="store">Fila de envios. Não a compartilhe com outra instância ativa.</param>
    /// <param name="options">Prazos e limites; lidos agora (alterações posteriores não têm efeito).</param>
    /// <param name="unclaimedResultHandler">Recebe o resultado dos envios que nenhuma tarefa espera, ou <c>null</c> para descartá-los.</param>
    /// <exception cref="ArgumentOutOfRangeException">Algum valor de <paramref name="options"/> é inválido.</exception>
    public PushServer(IPushCommandStore store, PushServerOptions options, IPushResultHandler? unclaimedResultHandler = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        options.Validate();
        _unclaimedResultHandler = unclaimedResultHandler;
        _logger = options.LoggerFactory?.CreateLogger(ControlIdTelemetry.LoggerCategory) ?? NullLogger.Instance;
        _clock = options.Clock ?? (static () => DateTimeOffset.UtcNow);
        _deadlineFactory = options.CommandDeadlineFactory ?? (static timeout => new CancellationTokenSource(timeout));
        _commandTimeout = options.CommandTimeout;
        _commandTtl = options.CommandTtl;
        _inFlightTimeout = options.InFlightTimeout;
        _maxPendingPerDevice = options.MaxPendingPerDevice;
        _maxTrackedDevices = options.MaxTrackedDevices;
        _requireUuid = options.RequireUuid;
        _useTransactions = options.UseTransactions;
        _resultHandlerTimeout = options.ResultHandlerTimeout;
    }

    /// <summary>Equipamentos com a última consulta registrada (testes).</summary>
    internal int TrackedDeviceCount => Volatile.Read(ref _trackedDevices);

    /// <summary>Tarefas de <see cref="SendAsync"/>/<see cref="SendBatchAsync"/> aguardando (testes).</summary>
    internal int WaiterCount => _waiters.Count;

    /// <summary>
    /// Enfileira o comando e espera o equipamento executá-lo. A espera termina com o resultado, com
    /// <see cref="PushServerOptions.CommandTimeout"/> (<see cref="TimeoutException"/>) ou com <paramref name="cancellationToken"/>;
    /// nos dois últimos casos o comando sai da fila. Lança <see cref="PushQueueFullException"/> se a fila do equipamento
    /// estiver cheia (<see cref="PushServerOptions.MaxPendingPerDevice"/>).
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
    /// Prazo, cancelamento e limite da fila como em <see cref="SendAsync"/>.
    /// <para>
    /// Nem todo firmware aceita lotes: o iDFace 6.9.12 trata o corpo como um comando só e responde
    /// "Invalid member 'endpoint'". Com <see cref="PushServerOptions.UseTransactions"/> em <c>null</c> (automático), o
    /// servidor percebe a recusa, lembra que aquele equipamento não aceita <c>transactions</c>, reenvia os comandos do lote
    /// como envios individuais e entrega os resultados de sempre; os próximos lotes do equipamento já saem individuais.
    /// Com <c>false</c>, todo lote sai individual; com <c>true</c>, a recusa chega como erro em todos os resultados.
    /// Num lote desmembrado, <see cref="GetPendingCountAsync"/> conta um envio por comando.
    /// </para>
    /// </summary>
    public Task<IReadOnlyList<PushResult>> SendBatchAsync(long deviceId, IEnumerable<PushCommand> commands, CancellationToken cancellationToken = default) =>
        WaitForResultsAsync(deviceId, ValidateBatch(commands), batch: true, cancellationToken);

    /// <summary>
    /// Enfileira o comando sem esperar o resultado e devolve o identificador do envio. O resultado chega ao
    /// <see cref="IPushResultHandler"/> (<see cref="PushCompletion.CommandId"/> é o valor devolvido). Lança
    /// <see cref="PushQueueFullException"/> se a fila do equipamento estiver cheia.
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

    /// <summary>
    /// Última consulta do equipamento a <c>/push</c>, ou <c>null</c> se ele nunca apareceu (só nesta instância) ou se
    /// apareceu depois de atingido <see cref="PushServerOptions.MaxTrackedDevices"/>.
    /// </summary>
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
    /// se não houver nada (responda 200 com esse corpo). Envios pendentes mais velhos que
    /// <see cref="PushServerOptions.CommandTtl"/> são descartados (falham como expirados) e o seguinte é entregue.
    /// Com <see cref="PushServerOptions.RequireUuid"/>, uma consulta sem <paramref name="uuid"/> devolve vazio.
    /// </summary>
    public async Task<string> HandlePushAsync(long deviceId, string? uuid, CancellationToken cancellationToken = default)
    {
        if (_requireUuid && string.IsNullOrEmpty(uuid))
        {
            PushLog.UuidMissing(_logger, "/push", deviceId);
            return string.Empty;
        }

        Track(deviceId);

        PushEnvelope? next = null;
        PushEnvelope? orphan = null;
        List<PushEnvelope>? expired = null;
        try
        {
            var gate = GetGate(deviceId);
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // O equipamento só volta a /push depois de enviar o /result. Se o envio anterior ficou
                // sem resultado (reinício, queda de rede), ele não vai chegar mais.
                while (true)
                {
                    var take = await _store.TakeNextAsync(deviceId, uuid, cancellationToken).ConfigureAwait(false);
                    orphan ??= take.Orphan;

                    if (take.Next is { } candidate && IsExpired(candidate))
                    {
                        // Velho demais para executar: sai da fila e o seguinte é tentado. Só conta como expirado depois
                        // de removido; se a remoção falhar, ele vira órfão na próxima consulta.
                        await _store.RemoveAsync(candidate.Id, cancellationToken).ConfigureAwait(false);
                        (expired ??= new List<PushEnvelope>()).Add(candidate);
                        continue;
                    }

                    next = take.Next;
                    break;
                }
            }
            finally
            {
                gate.Release();
            }
        }
        finally
        {
            // Mesmo que o store falhe no meio, quem já saiu da fila é avisado. Os avisos nunca lançam.
            if (orphan is not null)
            {
                await FailAsync(
                    orphan,
                    PushOutcome.Orphaned,
                    $"O equipamento {deviceId} pediu um novo comando sem devolver o resultado de '{orphan.Description}'.",
                    "nova consulta sem resultado").ConfigureAwait(false);
            }

            if (expired is not null)
            {
                foreach (var envelope in expired)
                {
                    await FailExpiredAsync(envelope).ConfigureAwait(false);
                }
            }
        }

        if (next is null)
        {
            return string.Empty;
        }

        Telemetry.RecordPush(PushOutcome.Delivered);
        return next.Payload;
    }

    /// <summary>Versão síncrona de <see cref="HandlePushAsync"/> (bloqueia sobre ela; prefira a assíncrona).</summary>
    public string HandlePush(long deviceId, string? uuid) =>
        HandlePushAsync(deviceId, uuid).GetAwaiter().GetResult();

    /// <summary>
    /// Trata <c>POST /result?deviceId=..</c> com o corpo enviado pelo equipamento (responda 200 vazio).
    /// Devolve <c>false</c> se não havia envio aguardando esse resultado (ou, com <see cref="PushServerOptions.RequireUuid"/>,
    /// se não veio <c>uuid</c> na query nem no corpo). Depois que o envio sai do store, <paramref name="cancellationToken"/>
    /// não interrompe mais a entrega do resultado, e uma falha do <see cref="IPushResultHandler"/> é logada sem lançar.
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

        if (_requireUuid && string.IsNullOrEmpty(uuid))
        {
            PushLog.UuidMissing(_logger, "/result", deviceId);
            return false;
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

        // Resultado de um envio individual que veio de um lote desmembrado: entra no grupo e, se foi o último, entrega tudo.
        if (_groupByMember.TryRemove(envelope.Id, out var member))
        {
            Telemetry.RecordPush(PushOutcome.Completed);
            var memberResult = invalid is null
                ? ParseSingleResult(deviceId, envelope.Endpoints[0], root)
                : new PushResult(deviceId, envelope.Endpoints[0], null, invalid.Message);
            await CompleteMemberAsync(member, memberResult).ConfigureAwait(false);
            return true;
        }

        // O equipamento recusou o formato "transactions": lembra e reenvia os comandos um a um, com os mesmos awaiters.
        if (results is not null
            && envelope.Batch
            && _useTransactions != true
            && IsTransactionsRefused(results)
            && !_abandoned.ContainsKey(envelope.Id)
            && TrySplitBatchPayload(envelope.Payload, envelope.Endpoints.Count, out var individualPayloads))
        {
            _noTransactions[deviceId] = 0;
            var group = new BatchGroup(envelope.Id, deviceId, envelope.Endpoints);
            try
            {
                await AddMembersAsync(group, individualPayloads, enforceLimit: false, CancellationToken.None).ConfigureAwait(false);
                return true;
            }
            catch (Exception)
            {
                // O store não aceitou os envios individuais: quem espera recebe a recusa do equipamento, como sem o fallback.
            }
        }

        if (_waiters.TryRemove(envelope.Id, out var waiter))
        {
            Telemetry.RecordPush(PushOutcome.Completed);
            if (results is not null)
            {
                waiter.TrySetResult(results);
            }
            else
            {
                waiter.TrySetException(invalid!);
            }
        }
        else if (!_abandoned.ContainsKey(envelope.Id))
        {
            Telemetry.RecordPush(PushOutcome.Completed);
            var completion = new PushCompletion(envelope.Id, deviceId, results ?? Array.Empty<PushResult>(), invalid?.Message);
            await InvokeHandlerAsync(completion).ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>Versão síncrona de <see cref="HandleResultAsync"/> (bloqueia sobre ela; prefira a assíncrona).</summary>
    public bool HandleResult(long deviceId, string? uuid, string body) =>
        HandleResultAsync(deviceId, uuid, body).GetAwaiter().GetResult();

    /// <summary>
    /// Varre o store e remove os envios vencidos de todos os equipamentos: pendentes mais velhos que
    /// <see cref="PushServerOptions.CommandTtl"/> e entregues sem resultado há mais de <see cref="PushServerOptions.InFlightTimeout"/>.
    /// Cada envio removido falha: a tarefa de <see cref="SendAsync"/> com <see cref="ControlIdException"/>, ou o
    /// <see cref="IPushResultHandler"/> com <see cref="PushCompletion.Error"/> preenchido. Devolve quantos foram removidos;
    /// <c>0</c> se o store não implementa <see cref="IPushCommandStoreMaintenance"/> ou se os dois prazos estão desligados.
    /// O pacote <c>ControlId.Net.AspNetCore</c> chama este método a cada <see cref="PushServerOptions.PurgeInterval"/>.
    /// </summary>
    public async Task<int> PurgeExpiredAsync(CancellationToken cancellationToken = default)
    {
        if (_store is not IPushCommandStoreMaintenance maintenance || (_commandTtl is null && _inFlightTimeout is null))
        {
            return 0;
        }

        var now = _clock();
        var removed = await maintenance.RemoveExpiredAsync(Before(now, _commandTtl), Before(now, _inFlightTimeout), cancellationToken).ConfigureAwait(false);
        if (removed is null || removed.Count == 0)
        {
            return 0;
        }

        foreach (var envelope in removed)
        {
            if (envelope.Status == PushEnvelopeStatus.InFlight)
            {
                await FailAsync(
                    envelope,
                    PushOutcome.Orphaned,
                    $"O equipamento {envelope.DeviceId} não devolveu o resultado de '{envelope.Description}' dentro de InFlightTimeout; o envio foi descartado.",
                    "InFlightTimeout").ConfigureAwait(false);
            }
            else
            {
                await FailExpiredAsync(envelope).ConfigureAwait(false);
            }
        }

        PushLog.Purged(_logger, removed.Count);
        return removed.Count;
    }

    private static DateTimeOffset Before(DateTimeOffset now, TimeSpan? span)
    {
        if (span is not { } value)
        {
            return DateTimeOffset.MinValue;
        }

        return value >= now - DateTimeOffset.MinValue ? DateTimeOffset.MinValue : now - value;
    }

    private bool IsExpired(PushEnvelope envelope) =>
        _commandTtl is { } ttl && _clock() - envelope.CreatedAt > ttl;

    private Task FailExpiredAsync(PushEnvelope envelope) =>
        FailAsync(
            envelope,
            PushOutcome.Expired,
            $"O envio '{envelope.Description}' para o equipamento {envelope.DeviceId} expirou na fila sem ser entregue (CommandTtl).",
            reason: null);

    // Falha um envio que saiu do store sem resultado: a tarefa de quem espera ou, sem ela, o handler. Nunca lança.
    private async Task FailAsync(PushEnvelope envelope, string outcome, string message, string? reason)
    {
        if (_abandoned.ContainsKey(envelope.Id))
        {
            // Quem esperava já desistiu (cancelamento ou CommandTimeout): nada a avisar.
            return;
        }

        Telemetry.RecordPush(outcome);
        if (reason is null)
        {
            PushLog.CommandExpired(_logger, envelope.Id, envelope.DeviceId);
        }
        else
        {
            PushLog.CommandOrphaned(_logger, envelope.Id, envelope.DeviceId, reason);
        }

        if (_groupByMember.TryRemove(envelope.Id, out var member))
        {
            // Um envio individual de lote desmembrado falhou: só o comando dele falha, os demais seguem.
            await CompleteMemberAsync(member, new PushResult(envelope.DeviceId, envelope.Endpoints.Count > 0 ? envelope.Endpoints[0] : string.Empty, null, message)).ConfigureAwait(false);
            return;
        }

        if (_waiters.TryRemove(envelope.Id, out var waiter))
        {
            waiter.TrySetException(new ControlIdException(message));
        }
        else if (_unclaimedResultHandler is not null)
        {
            // Envio sem tarefa esperando (EnqueueAsync, ou perdido num reinício): avisa pelo handler. Nunca é reenviado.
            await InvokeHandlerAsync(new PushCompletion(envelope.Id, envelope.DeviceId, Array.Empty<PushResult>(), message)).ConfigureAwait(false);
        }
    }

    // Chama o handler com prazo próprio, sem o token da requisição do equipamento. Exceção ou prazo vencido vira log 1101.
    private async Task InvokeHandlerAsync(PushCompletion completion)
    {
        if (_unclaimedResultHandler is not { } handler)
        {
            return;
        }

        using var timeout = new CancellationTokenSource(_resultHandlerTimeout);
        try
        {
            var task = handler.HandleAsync(completion, timeout.Token) ?? Task.CompletedTask;
            if (!task.IsCompleted)
            {
                // Um handler que ignora o token também é cortado: a espera termina no prazo e o handler segue sozinho.
                var expired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                using (timeout.Token.Register(static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true), expired))
                {
                    if (await Task.WhenAny(task, expired.Task).ConfigureAwait(false) != task && !task.IsCompleted)
                    {
                        _ = task.ContinueWith(static t => _ = t.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                        throw new TimeoutException($"O IPushResultHandler não terminou em {_resultHandlerTimeout.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture)} s (ResultHandlerTimeout).");
                    }
                }
            }

            await task.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            PushLog.ResultHandlerFailed(_logger, completion.CommandId, completion.DeviceId, ex);
        }
    }

    // Registra a consulta. Equipamentos novos só entram até MaxTrackedDevices: a query não faz a memória crescer sem limite.
    private void Track(long deviceId)
    {
        var now = _clock();
        if (_lastSeen.ContainsKey(deviceId))
        {
            _lastSeen[deviceId] = now;
            return;
        }

        if (Interlocked.Increment(ref _trackedDevices) > _maxTrackedDevices)
        {
            Interlocked.Decrement(ref _trackedDevices);
            if (Interlocked.Exchange(ref _trackingLimitLogged, 1) == 0)
            {
                PushLog.TrackingLimitReached(_logger, _maxTrackedDevices);
            }

            if (_lastSeen.ContainsKey(deviceId))
            {
                _lastSeen[deviceId] = now;
            }

            return;
        }

        if (!_lastSeen.TryAdd(deviceId, now))
        {
            // Outra consulta do mesmo equipamento entrou antes: a vaga não é usada.
            Interlocked.Decrement(ref _trackedDevices);
            _lastSeen[deviceId] = now;
        }
    }

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

        using var deadline = _commandTimeout is { } timeout ? _deadlineFactory(timeout) : null;
        using var timeoutRegistration = deadline is null
            ? default
            : deadline.Token.Register(() => Abandon(
                deviceId,
                id,
                PushOutcome.Timeout,
                () => waiter.TrySetException(new TimeoutException(
                    $"O equipamento {deviceId} não devolveu o resultado de '{Describe(commands, batch)}' em {_commandTimeout!.Value.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture)} s (CommandTimeout); o comando saiu da fila."))));
        using var cancelRegistration = cancellationToken.Register(() => Abandon(
            deviceId,
            id,
            PushOutcome.Cancelled,
            () => waiter.TrySetCanceled(cancellationToken)));

        return await waiter.Task.ConfigureAwait(false);
    }

    private static string Describe(IReadOnlyList<PushCommand> commands, bool batch) =>
        batch ? $"lote de {commands.Count} comandos" : commands[0].Endpoint;

    // Quem espera desistiu (cancelamento ou CommandTimeout): a tarefa falha já e o envio sai do store em segundo plano.
    // A remoção começa antes da falha, então um store que responde na hora já está limpo quando a tarefa termina; um store
    // lento ou travado não segura a tarefa.
    private void Abandon(long deviceId, Guid id, string outcome, Action fail)
    {
        // Marca antes de tirar o waiter: um /result concorrente que não achar o waiter vê a marca e não chama o handler.
        _abandoned[id] = 0;
        if (!_waiters.TryRemove(id, out _))
        {
            // O resultado (ou outra falha) chegou antes.
            _abandoned.TryRemove(id, out _);
            return;
        }

        Telemetry.RecordPush(outcome);

        // Lote desmembrado: os envios individuais ainda na fila também saem, e os resultados que chegarem são descartados.
        var group = DetachGroup(id);
        _ = RemoveAbandonedAsync(deviceId, id, group);
        fail();
    }

    private async Task RemoveAbandonedAsync(long deviceId, Guid id, BatchGroup? group)
    {
        try
        {
            await RemoveFromStoreAsync(deviceId, id, takeGate: true).ConfigureAwait(false);
            if (group is not null)
            {
                foreach (var memberId in group.MemberIds)
                {
                    await RemoveFromStoreAsync(deviceId, memberId, takeGate: true).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _abandoned.TryRemove(id, out _);
            if (group is not null)
            {
                foreach (var memberId in group.MemberIds)
                {
                    _abandoned.TryRemove(memberId, out _);
                }
            }
        }
    }

    // Tira o grupo do controle e marca os envios individuais como abandonados (a marca vem antes de sair do mapa).
    private BatchGroup? DetachGroup(Guid groupId)
    {
        if (!_groups.TryRemove(groupId, out var group))
        {
            return null;
        }

        foreach (var memberId in group.MemberIds)
        {
            _abandoned[memberId] = 0;
            _groupByMember.TryRemove(memberId, out _);
        }

        return group;
    }

    private bool ShouldSplitBatch(long deviceId) =>
        _useTransactions == false || (_useTransactions is null && _noTransactions.ContainsKey(deviceId));

    // Recusa do formato "transactions": todo comando do lote voltou com "Invalid member"/"Invalid command".
    private static bool IsTransactionsRefused(IReadOnlyList<PushResult> results) =>
        results.Count > 0 && results.All(r => r.Error is { } error
            && (error.IndexOf("Invalid member", StringComparison.OrdinalIgnoreCase) >= 0
                || error.IndexOf("Invalid command", StringComparison.OrdinalIgnoreCase) >= 0));

    // Refaz o JSON de cada comando do lote sem o transactionid: é o formato do envio individual.
    private static bool TrySplitBatchPayload(string payload, int expected, out List<string> payloads)
    {
        payloads = new List<string>();
        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("transactions", out var transactions)
                || transactions.ValueKind != JsonValueKind.Array
                || transactions.GetArrayLength() != expected)
            {
                return false;
            }

            foreach (var transaction in transactions.EnumerateArray())
            {
                if (transaction.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }

                using var stream = new MemoryStream();
                using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
                {
                    writer.WriteStartObject();
                    foreach (var property in transaction.EnumerateObject())
                    {
                        if (!property.NameEquals("transactionid"))
                        {
                            property.WriteTo(writer);
                        }
                    }

                    writer.WriteEndObject();
                }

                payloads.Add(Encoding.UTF8.GetString(stream.ToArray()));
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    // Registra o grupo e enfileira um envio individual por comando, em ordem. O limite da fila só vale para quem
    // enfileira agora (enforceLimit); no reenvio de um lote recusado ele não se aplica, para não perder os resultados.
    private async Task AddMembersAsync(BatchGroup group, List<string> payloads, bool enforceLimit, CancellationToken cancellationToken)
    {
        _groups[group.Id] = group;
        for (var i = 0; i < group.MemberIds.Length; i++)
        {
            _groupByMember[group.MemberIds[i]] = new GroupMember(group, i);
        }

        var added = new List<Guid>();
        try
        {
            var gate = GetGate(group.DeviceId);
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (enforceLimit
                    && _maxPendingPerDevice > 0
                    && await _store.CountAsync(group.DeviceId, cancellationToken).ConfigureAwait(false) + payloads.Count > _maxPendingPerDevice)
                {
                    Telemetry.RecordPush(PushOutcome.Rejected);
                    throw new PushQueueFullException(group.DeviceId, _maxPendingPerDevice);
                }

                try
                {
                    for (var i = 0; i < payloads.Count; i++)
                    {
                        var envelope = new PushEnvelope(group.MemberIds[i], group.DeviceId, batch: false, new[] { group.Endpoints[i] }, payloads[i], _clock());
                        added.Add(envelope.Id);
                        await _store.AddAsync(envelope, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch
                {
                    foreach (var id in added)
                    {
                        await RemoveFromStoreAsync(group.DeviceId, id, takeGate: false).ConfigureAwait(false);
                    }

                    throw;
                }

                // Quem espera desistiu enquanto os envios entravam: o que já entrou sai da fila.
                if (!_groups.ContainsKey(group.Id))
                {
                    foreach (var id in added)
                    {
                        await RemoveFromStoreAsync(group.DeviceId, id, takeGate: false).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                gate.Release();
            }
        }
        catch
        {
            _groups.TryRemove(group.Id, out _);
            foreach (var memberId in group.MemberIds)
            {
                _groupByMember.TryRemove(memberId, out _);
            }

            throw;
        }
    }

    // Guarda o resultado de um envio individual e, quando é o último do grupo, entrega todos na ordem do lote.
    private async Task CompleteMemberAsync(GroupMember member, PushResult result)
    {
        var group = member.Group;
        if (!group.TrySet(member.Index, result))
        {
            return;
        }

        _groups.TryRemove(group.Id, out _);
        var results = group.Results();
        if (_waiters.TryRemove(group.Id, out var waiter))
        {
            waiter.TrySetResult(results);
        }
        else if (!_abandoned.ContainsKey(group.Id))
        {
            await InvokeHandlerAsync(new PushCompletion(group.Id, group.DeviceId, results, null)).ConfigureAwait(false);
        }
    }

    // Lote enfileirado como um envio individual por comando (equipamento que não aceita "transactions").
    private async Task<Guid> AddSplitBatchAsync(
        long deviceId,
        IReadOnlyList<PushCommand> commands,
        TaskCompletionSource<IReadOnlyList<PushResult>>? waiter,
        CancellationToken cancellationToken)
    {
        var payloads = commands.Select(c => ControlIdJson.Serialize(c)).ToList();
        var group = new BatchGroup(Guid.NewGuid(), deviceId, commands.Select(c => c.Endpoint).ToList());
        if (waiter is not null)
        {
            _waiters[group.Id] = waiter;
        }

        try
        {
            await AddMembersAsync(group, payloads, enforceLimit: true, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            _waiters.TryRemove(group.Id, out _);
            throw;
        }

        Telemetry.RecordPush(PushOutcome.Enqueued);
        return group.Id;
    }

    // Remoção de melhor esforço, com prazo próprio. Nunca lança: a falha vira log 1106.
    private async Task RemoveFromStoreAsync(long deviceId, Guid id, bool takeGate)
    {
        try
        {
            using var timeout = new CancellationTokenSource(BackgroundRemoveTimeout);
            if (!takeGate)
            {
                await _store.RemoveAsync(id, timeout.Token).ConfigureAwait(false);
                return;
            }

            var gate = GetGate(deviceId);
            await gate.WaitAsync(timeout.Token).ConfigureAwait(false);
            try
            {
                await _store.RemoveAsync(id, timeout.Token).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }
        catch (Exception ex)
        {
            PushLog.CancelRemoveFailed(_logger, id, deviceId, ex);
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

        if (batch && ShouldSplitBatch(deviceId))
        {
            return await AddSplitBatchAsync(deviceId, commands, waiter, cancellationToken).ConfigureAwait(false);
        }

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
        var envelope = new PushEnvelope(id, deviceId, batch, commands.Select(c => c.Endpoint).ToList(), payload, _clock());

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
                // Contagem e inclusão sob o mesmo gate: envios concorrentes ao mesmo equipamento não passam do limite.
                if (_maxPendingPerDevice > 0
                    && await _store.CountAsync(deviceId, cancellationToken).ConfigureAwait(false) >= _maxPendingPerDevice)
                {
                    Telemetry.RecordPush(PushOutcome.Rejected);
                    throw new PushQueueFullException(deviceId, _maxPendingPerDevice);
                }

                try
                {
                    await _store.AddAsync(envelope, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // O store pode ter gravado antes de falhar (ou de ver o cancelamento): desfaz no que der.
                    await RemoveFromStoreAsync(deviceId, id, takeGate: false).ConfigureAwait(false);
                    throw;
                }
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

        Telemetry.RecordPush(PushOutcome.Enqueued);
        return id;
    }

    private static SemaphoreSlim[] CreateGates()
    {
        var gates = new SemaphoreSlim[GateCount];
        for (var i = 0; i < gates.Length; i++)
        {
            gates[i] = new SemaphoreSlim(1, 1);
        }

        return gates;
    }

    internal static int GateIndex(long deviceId) => (int)((uint)deviceId.GetHashCode() % GateCount);

    private SemaphoreSlim GetGate(long deviceId) => _gates[GateIndex(deviceId)];

    // Lote desmembrado em envios individuais: junta os resultados na ordem do lote.
    private sealed class BatchGroup
    {
        private readonly PushResult?[] _results;
        private readonly object _sync = new();
        private int _remaining;

        public BatchGroup(Guid id, long deviceId, IReadOnlyList<string> endpoints)
        {
            Id = id;
            DeviceId = deviceId;
            Endpoints = endpoints;
            MemberIds = new Guid[endpoints.Count];
            for (var i = 0; i < MemberIds.Length; i++)
            {
                MemberIds[i] = Guid.NewGuid();
            }

            _results = new PushResult?[endpoints.Count];
            _remaining = endpoints.Count;
        }

        public Guid Id { get; }

        public long DeviceId { get; }

        public IReadOnlyList<string> Endpoints { get; }

        public Guid[] MemberIds { get; }

        // Devolve true quando este foi o último resultado que faltava.
        public bool TrySet(int index, PushResult result)
        {
            lock (_sync)
            {
                if (_results[index] is not null)
                {
                    return false;
                }

                _results[index] = result;
                return --_remaining == 0;
            }
        }

        public List<PushResult> Results()
        {
            lock (_sync)
            {
                return _results.Select(r => r!).ToList();
            }
        }
    }

    private sealed class GroupMember
    {
        public GroupMember(BatchGroup group, int index)
        {
            Group = group;
            Index = index;
        }

        public BatchGroup Group { get; }

        public int Index { get; }
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
}
