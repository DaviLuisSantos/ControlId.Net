using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using ControlId.Access.Push;
using Microsoft.Extensions.Logging;

namespace ControlId.Tests;

/// <summary>Relógio manual para os prazos do <see cref="PushServer"/> (começa no instante real, para conviver com o store).</summary>
internal sealed class PushFakeClock
{
    private long _ticks = DateTimeOffset.UtcNow.UtcTicks;

    public DateTimeOffset UtcNow => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);

    public Func<DateTimeOffset> Now => () => UtcNow;

    public void Advance(TimeSpan by) => Interlocked.Add(ref _ticks, by.Ticks);
}

/// <summary>Logger em memória: guarda EventId, nível, mensagem e exceção.</summary>
internal sealed class PushLogCapture : ILoggerFactory
{
    private readonly ConcurrentQueue<PushLogEntry> _entries = new();

    public IReadOnlyList<PushLogEntry> Entries => _entries.ToList();

    public IReadOnlyList<PushLogEntry> WithId(int eventId) => Entries.Where(e => e.EventId == eventId).ToList();

    public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public void Dispose()
    {
    }

    private sealed class Logger(PushLogCapture owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            owner._entries.Enqueue(new PushLogEntry(category, logLevel, eventId.Id, formatter(state, exception), exception));
    }
}

internal sealed class PushLogEntry(string category, LogLevel level, int eventId, string message, Exception? exception)
{
    public string Category { get; } = category;

    public LogLevel Level { get; } = level;

    public int EventId { get; } = eventId;

    public string Message { get; } = message;

    public Exception? Exception { get; } = exception;
}

/// <summary>Handler de resultado configurável: grava as chamadas e executa <see cref="OnHandle"/>.</summary>
internal sealed class PushRecordingHandler : IPushResultHandler
{
    public ConcurrentQueue<PushCompletion> Completions { get; } = new();

    public ConcurrentQueue<CancellationToken> Tokens { get; } = new();

    public Func<PushCompletion, CancellationToken, Task>? OnHandle { get; set; }

    public Task HandleAsync(PushCompletion completion, CancellationToken cancellationToken)
    {
        Completions.Enqueue(completion);
        Tokens.Enqueue(cancellationToken);
        return OnHandle?.Invoke(completion, cancellationToken) ?? Task.CompletedTask;
    }
}

/// <summary>
/// Store em memória com ganchos para simular um store lento, travado ou com falha. Não implementa
/// <see cref="IPushCommandStoreMaintenance"/> (veja <see cref="PushControllableMaintenanceStore"/>).
/// </summary>
internal class PushControllableStore : IPushCommandStore
{
    private readonly TaskCompletionSource<bool> _removeFinished = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public PushControllableStore(InMemoryPushCommandStore? inner = null)
    {
        Inner = inner ?? new InMemoryPushCommandStore();
    }

    public InMemoryPushCommandStore Inner { get; }

    /// <summary>Roda antes de cada <see cref="RemoveAsync"/> (pode travar ou lançar).</summary>
    public Func<Guid, CancellationToken, Task>? BeforeRemove { get; set; }

    /// <summary>Roda depois de gravar no <see cref="AddAsync"/> (pode lançar: simula falha depois de gravar).</summary>
    public Func<PushEnvelope, CancellationToken, Task>? AfterAdd { get; set; }

    /// <summary>Roda antes de cada <see cref="TakeNextAsync"/> (pode travar).</summary>
    public Func<CancellationToken, Task>? BeforeTake { get; set; }

    /// <summary>Roda depois de cada <see cref="CompleteAsync"/>, antes de devolver.</summary>
    public Action? AfterComplete { get; set; }

    /// <summary>Completa quando o primeiro <see cref="RemoveAsync"/> termina (com sucesso ou falha).</summary>
    public Task RemoveFinished => _removeFinished.Task;

    public async Task AddAsync(PushEnvelope envelope, CancellationToken cancellationToken)
    {
        await Inner.AddAsync(envelope, cancellationToken);
        if (AfterAdd is { } afterAdd)
        {
            await afterAdd(envelope, cancellationToken);
        }
    }

    public async Task<PushTake> TakeNextAsync(long deviceId, string? uuid, CancellationToken cancellationToken)
    {
        if (BeforeTake is { } beforeTake)
        {
            await beforeTake(cancellationToken);
        }

        return await Inner.TakeNextAsync(deviceId, uuid, cancellationToken);
    }

    public async Task<PushEnvelope?> CompleteAsync(long deviceId, string? uuid, CancellationToken cancellationToken)
    {
        var envelope = await Inner.CompleteAsync(deviceId, uuid, cancellationToken);
        AfterComplete?.Invoke();
        return envelope;
    }

    public async Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            if (BeforeRemove is { } beforeRemove)
            {
                await beforeRemove(id, cancellationToken);
            }

            return await Inner.RemoveAsync(id, cancellationToken);
        }
        finally
        {
            _removeFinished.TrySetResult(true);
        }
    }

    public Task<int> CountAsync(long deviceId, CancellationToken cancellationToken) => Inner.CountAsync(deviceId, cancellationToken);
}

/// <summary><see cref="PushControllableStore"/> com a manutenção do store em memória.</summary>
internal sealed class PushControllableMaintenanceStore(InMemoryPushCommandStore? inner = null) : PushControllableStore(inner), IPushCommandStoreMaintenance
{
    public Task<IReadOnlyList<PushEnvelope>> RemoveExpiredAsync(DateTimeOffset pendingCreatedBefore, DateTimeOffset inFlightDeliveredBefore, CancellationToken cancellationToken) =>
        Inner.RemoveExpiredAsync(pendingCreatedBefore, inFlightDeliveredBefore, cancellationToken);
}

/// <summary>
/// Conta as medições de <c>controlid.push.commands</c> feitas no fluxo assíncrono de quem criou a captura (o listener é
/// global e outros testes rodam em paralelo; o <see cref="AsyncLocal{T}"/> separa as medições deste teste).
/// </summary>
internal sealed class PushMetricCapture : IDisposable
{
    private static readonly AsyncLocal<PushMetricCapture?> Current = new();
    private readonly MeterListener _listener = new();
    private readonly ConcurrentQueue<string> _outcomes = new();

    public PushMetricCapture()
    {
        Current.Value = this;
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == ControlIdTelemetry.MeterName && instrument.Name == "controlid.push.commands")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            if (!ReferenceEquals(Current.Value, this))
            {
                return;
            }

            foreach (var tag in tags)
            {
                if (tag.Key == "controlid.push.outcome")
                {
                    for (var i = 0; i < value; i++)
                    {
                        _outcomes.Enqueue((string)tag.Value!);
                    }
                }
            }
        });
        _listener.Start();
    }

    public int Count(string outcome) => _outcomes.Count(o => o == outcome);

    public void Dispose() => _listener.Dispose();
}
