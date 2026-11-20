using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ControlId.Access;
using ControlId.Rep;

[assembly: InternalsVisibleTo("ControlId.Net.Tests")]

namespace ControlId.DependencyInjection;

/// <summary>Resultado da sonda de um equipamento (<c>system_information</c> no acesso, <c>get_about</c> no REP).</summary>
internal sealed class ControlIdProbeResult
{
    public ControlIdProbeResult(TimeSpan elapsed, Exception? error, AccessSystemInformation? access = null, RepAbout? rep = null)
    {
        Elapsed = elapsed;
        Error = error;
        Access = access;
        Rep = rep;
    }

    public TimeSpan Elapsed { get; }

    public Exception? Error { get; }

    public bool Succeeded => Error is null;

    public AccessSystemInformation? Access { get; }

    public RepAbout? Rep { get; }
}

/// <summary>A sonda usada pelos health checks: uma leitura que não altera nada, com prazo próprio.</summary>
internal static class ControlIdProbe
{
    public const string AccessKind = "access";

    public const string RepKind = "rep";

    public static string Key(string kind, string device) => kind + ":" + device;

    /// <summary>Nunca lança, exceto <see cref="OperationCanceledException"/> pelo <paramref name="cancellationToken"/>.</summary>
    public static async Task<ControlIdProbeResult> RunAsync(object client, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        try
        {
            switch (client)
            {
                case IAccessClient access:
                    var info = await access.GetSystemInformationAsync(deadline.Token).ConfigureAwait(false);
                    return new ControlIdProbeResult(stopwatch.Elapsed, null, access: info);
                case IRepClient rep:
                    var about = await rep.GetAboutAsync(deadline.Token).ConfigureAwait(false);
                    return new ControlIdProbeResult(stopwatch.Elapsed, null, rep: about);
                default:
                    throw new ArgumentException("Cliente de equipamento desconhecido.", nameof(client));
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // O prazo vale por equipamento: um que não responde vira falha só dele.
            return new ControlIdProbeResult(
                stopwatch.Elapsed,
                new TimeoutException($"Sem resposta em {timeout.TotalSeconds.ToString(CultureInfo.InvariantCulture)} s."));
        }
        catch (Exception ex) when (!(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            return new ControlIdProbeResult(stopwatch.Elapsed, ex);
        }
    }
}

/// <summary>
/// Cache por equipamento das sondas dos health checks, compartilhado entre o check agregado (<c>AddControlIdDevices</c>) e os
/// por equipamento (<c>AddControlIdDeviceChecks</c>). Dentro da janela, uma sonda em andamento ou concluída é reaproveitada:
/// os dois checks juntos fazem uma requisição por equipamento.
/// </summary>
/// <remarks>
/// Com cache, a sonda roda desvinculada do cancelamento de quem chamou (só o prazo dela vale), para quem desistir de esperar
/// não derrubar a resposta dos outros; quem chamou pode parar de esperar pelo próprio token. Falhas também ficam no cache
/// até a janela vencer, para um equipamento fora do ar não ser sondado a cada verificação.
/// </remarks>
internal sealed class ControlIdProbeCache
{
    private static readonly double TicksPerTimestamp = (double)TimeSpan.TicksPerSecond / Stopwatch.Frequency;

    private readonly Func<TimeSpan> _clock;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly object _lock = new();

    public ControlIdProbeCache()
        : this(static () => TimeSpan.FromTicks((long)(Stopwatch.GetTimestamp() * TicksPerTimestamp)))
    {
    }

    /// <summary>Relógio monotônico trocável (testes).</summary>
    internal ControlIdProbeCache(Func<TimeSpan> clock) => _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    /// <summary>
    /// Devolve a sonda do equipamento <paramref name="key"/>. <paramref name="cacheDuration"/> zero desliga o cache (sonda
    /// sempre, com o token de quem chamou). <c>Shared</c> indica resultado reaproveitado de outra chamada.
    /// </summary>
    public async Task<(ControlIdProbeResult Result, bool Shared)> GetAsync(
        string key,
        TimeSpan cacheDuration,
        Func<CancellationToken, Task<ControlIdProbeResult>> probe,
        CancellationToken cancellationToken)
    {
        if (cacheDuration <= TimeSpan.Zero)
        {
            return (await probe(cancellationToken).ConfigureAwait(false), false);
        }

        Entry entry;
        var shared = true;
        lock (_lock)
        {
            if (!_entries.TryGetValue(key, out entry!) || IsExpired(entry, cacheDuration))
            {
                entry = new Entry();
                _entries[key] = entry;
                shared = false;
            }
        }

        if (!shared)
        {
            _ = CompleteAsync(entry, probe);
        }

        return (await WaitAsync(entry.Completion.Task, cancellationToken).ConfigureAwait(false), shared);
    }

    // Chamado sob _lock.
    private bool IsExpired(Entry entry, TimeSpan cacheDuration) =>
        entry.CompletedAt is { } completedAt && _clock() - completedAt >= cacheDuration;

    private async Task CompleteAsync(Entry entry, Func<CancellationToken, Task<ControlIdProbeResult>> probe)
    {
        ControlIdProbeResult result;
        try
        {
            result = await probe(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            result = new ControlIdProbeResult(TimeSpan.Zero, ex);
        }

        lock (_lock)
        {
            entry.CompletedAt = _clock();
        }

        entry.Completion.TrySetResult(result);
    }

    private static async Task<T> WaitAsync<T>(Task<T> task, CancellationToken cancellationToken)
    {
        if (task.IsCompleted || !cancellationToken.CanBeCanceled)
        {
            return await task.ConfigureAwait(false);
        }

        var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using (cancellationToken.Register(static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true), cancelled))
        {
            if (await Task.WhenAny(task, cancelled.Task).ConfigureAwait(false) != task)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        return await task.ConfigureAwait(false);
    }

    private sealed class Entry
    {
        public TaskCompletionSource<ControlIdProbeResult> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        // Null enquanto a sonda está em andamento; protegido por _lock.
        public TimeSpan? CompletedAt { get; set; }
    }
}
