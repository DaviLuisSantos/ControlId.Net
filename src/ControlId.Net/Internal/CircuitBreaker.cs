using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.Logging;

namespace ControlId.Internal;

/// <summary>
/// Circuit breaker de um equipamento. Conta falhas seguidas (rede, timeout do cliente, HTTP 5xx); no limite, abre e recusa
/// as requisições sem tocar o equipamento. Passada a pausa, deixa passar uma única sonda (<c>HalfOpen</c>): se ela der
/// certo o circuito fecha, se falhar reabre. Uma sonda sem veredito por mais de <see cref="ProbeDeadline"/> conta como
/// falha: o circuito reabre e o veredito tardio dela é ignorado. Com o limite 0 não faz nada.
/// </summary>
/// <remarks>
/// As transições são decididas sob o lock; o log e a métrica de cada uma saem depois de soltá-lo, então duas transições
/// quase simultâneas podem aparecer no log em ordem trocada.
/// </remarks>
internal sealed class CircuitBreaker
{
    /// <summary>Prazo da sonda quando o timeout das requisições é infinito (ou não positivo).</summary>
    internal static readonly TimeSpan InfiniteTimeoutProbeDeadline = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan MinimumProbeDeadline = TimeSpan.FromSeconds(1);

    private readonly object _gate = new();
    private readonly int _threshold;
    private readonly long _breakTicks;
    private readonly long _probeDeadlineTicks;
    private readonly Func<long> _clock;
    private readonly KeyValuePair<string, object?>[] _deviceTags;
    private readonly ILogger _logger;
    private readonly string _device;
    private readonly TimeSpan _breakDuration;

    private ControlIdCircuitState _state = ControlIdCircuitState.Closed;
    private int _failures;
    private long _openedAt;

    // Sonda em voo (0 = nenhuma). Cada sonda recebe um número novo: o veredito de uma sonda abandonada não vale pela seguinte.
    private long _probe;
    private long _probeSequence;
    private long _probeStartedAt;

    public CircuitBreaker(
        int failureThreshold,
        TimeSpan breakDuration,
        KeyValuePair<string, object?>[] deviceTags,
        ILogger logger,
        string device,
        Func<long>? clock = null,
        TimeSpan? requestTimeout = null)
    {
        _threshold = failureThreshold;
        _breakDuration = breakDuration;
        _breakTicks = ToTimestamp(breakDuration);
        ProbeDeadline = ProbeDeadlineFor(requestTimeout ?? TimeSpan.FromSeconds(30));
        _probeDeadlineTicks = ToTimestamp(ProbeDeadline);
        _deviceTags = deviceTags;
        _logger = logger;
        _device = device;
        _clock = clock ?? Stopwatch.GetTimestamp;
    }

    public bool Enabled => _threshold > 0;

    /// <summary>Quanto a sonda pode ficar sem veredito antes de contar como falha.</summary>
    public TimeSpan ProbeDeadline { get; }

    /// <summary>
    /// Estado atual. Passada a pausa de um circuito aberto, já o reporta (e registra) como <c>HalfOpen</c>; uma sonda
    /// abandonada além do prazo já o reporta como <c>Open</c>.
    /// </summary>
    public ControlIdCircuitState State
    {
        get
        {
            if (!Enabled)
            {
                return ControlIdCircuitState.Closed;
            }

            ControlIdCircuitState state;
            ControlIdCircuitState? changed;
            int failures;
            lock (_gate)
            {
                changed = Refresh();
                state = _state;
                failures = _failures;
            }

            Emit(changed, failures);
            return state;
        }
    }

    /// <summary>
    /// 2 × o timeout das requisições, no mínimo 1 s; com timeout infinito (ou não positivo), <see cref="InfiniteTimeoutProbeDeadline"/>.
    /// </summary>
    internal static TimeSpan ProbeDeadlineFor(TimeSpan requestTimeout)
    {
        if (requestTimeout <= TimeSpan.Zero)
        {
            // Inclui Timeout.InfiniteTimeSpan (-1 ms).
            return InfiniteTimeoutProbeDeadline;
        }

        var doubled = requestTimeout.Ticks > TimeSpan.MaxValue.Ticks / 2 ? TimeSpan.MaxValue : TimeSpan.FromTicks(requestTimeout.Ticks * 2);
        return doubled < MinimumProbeDeadline ? MinimumProbeDeadline : doubled;
    }

    /// <summary>
    /// Pede passagem para uma requisição. Devolve o número da sonda do <c>HalfOpen</c> (diferente de 0; quem recebe precisa
    /// chamar <see cref="Success"/>, <see cref="Failure"/> ou <see cref="Cancel"/> com ele) ou 0 para uma requisição comum.
    /// Lança <see cref="ControlIdCircuitOpenException"/> se o circuito não admite a requisição.
    /// </summary>
    public long Acquire()
    {
        if (!Enabled)
        {
            return 0;
        }

        ControlIdCircuitState? changed;
        int failures;
        long admitted = -1;
        var retryAfter = TimeSpan.Zero;
        lock (_gate)
        {
            changed = Refresh();
            failures = _failures;
            switch (_state)
            {
                case ControlIdCircuitState.Closed:
                    admitted = 0;
                    break;

                case ControlIdCircuitState.HalfOpen when _probe == 0:
                    _probe = ++_probeSequence;
                    _probeStartedAt = _clock();
                    admitted = _probe;
                    break;

                case ControlIdCircuitState.HalfOpen:
                    break;

                default:
                    var remaining = _breakTicks - (_clock() - _openedAt);
                    retryAfter = remaining <= 0 ? TimeSpan.Zero : TimeSpan.FromSeconds((double)remaining / Stopwatch.Frequency);
                    break;
            }
        }

        Emit(changed, failures);
        if (admitted >= 0)
        {
            return admitted;
        }

        Telemetry.CircuitRejected.Add(1, _deviceTags);
        throw new ControlIdCircuitOpenException(
            $"Circuito aberto para {_device}: o equipamento falhou várias vezes seguidas; nada foi enviado.", retryAfter);
    }

    /// <summary>A requisição recebeu resposta: de HTTP 500 em diante conta como falha, o resto como sucesso.</summary>
    public void Record(HttpStatusCode status, long probe)
    {
        if ((int)status >= 500)
        {
            Failure(probe);
        }
        else
        {
            Success(probe);
        }
    }

    public void Success(long probe)
    {
        if (!Enabled)
        {
            return;
        }

        ControlIdCircuitState? changed = null;
        int failures;
        lock (_gate)
        {
            if (probe != 0)
            {
                // Sonda abandonada (prazo vencido ou ResetCircuit): o veredito dela não vale mais.
                if (probe == _probe)
                {
                    _probe = 0;
                    if (_state == ControlIdCircuitState.HalfOpen)
                    {
                        _failures = 0;
                        changed = Transition(ControlIdCircuitState.Closed);
                    }
                }
            }
            else if (_state == ControlIdCircuitState.Closed)
            {
                // Um retardatário que saiu antes de abrir não mexe no circuito já aberto.
                _failures = 0;
            }

            failures = _failures;
        }

        Emit(changed, failures);
    }

    public void Failure(long probe)
    {
        if (!Enabled)
        {
            return;
        }

        ControlIdCircuitState? changed = null;
        int failures;
        lock (_gate)
        {
            if (probe != 0)
            {
                if (probe == _probe)
                {
                    _probe = 0;
                    if (_state == ControlIdCircuitState.HalfOpen)
                    {
                        changed = Open();
                    }
                }
            }
            else if (_state == ControlIdCircuitState.Closed)
            {
                _failures++;
                if (_failures >= _threshold)
                {
                    changed = Open();
                }
            }

            failures = _failures;
        }

        Emit(changed, failures);
    }

    /// <summary>A requisição não chegou a um veredito (cancelada, erro local): não conta, e a sonda libera a vez.</summary>
    public void Cancel(long probe)
    {
        if (probe == 0 || !Enabled)
        {
            return;
        }

        lock (_gate)
        {
            if (probe == _probe)
            {
                _probe = 0;
            }
        }
    }

    /// <summary>
    /// Fecha o circuito na hora: zera as falhas e descarta a sonda em voo (o veredito dela passa a ser ignorado). Registra
    /// o log 1011. Com o circuit breaker desligado não faz nada.
    /// </summary>
    public void Reset()
    {
        if (!Enabled)
        {
            return;
        }

        bool changed;
        lock (_gate)
        {
            _failures = 0;
            _probe = 0;
            changed = _state != ControlIdCircuitState.Closed;
            _state = ControlIdCircuitState.Closed;
        }

        if (changed)
        {
            RecordTransition(ControlIdCircuitState.Closed);
        }

        Log.CircuitReset(_logger, _device);
    }

    // Chamar com o _gate. Devolve o novo estado se houve transição.
    private ControlIdCircuitState? Refresh()
    {
        if (_state == ControlIdCircuitState.Open && _clock() - _openedAt >= _breakTicks)
        {
            _probe = 0;
            return Transition(ControlIdCircuitState.HalfOpen);
        }

        if (_state == ControlIdCircuitState.HalfOpen && _probe != 0 && _clock() - _probeStartedAt >= _probeDeadlineTicks)
        {
            // Sonda sem veredito além do prazo (ex.: HttpClient com timeout infinito): conta como falha e reabre.
            _probe = 0;
            return Open();
        }

        return null;
    }

    // Chamar com o _gate.
    private ControlIdCircuitState Open()
    {
        _openedAt = _clock();
        return Transition(ControlIdCircuitState.Open);
    }

    // Chamar com o _gate: só muda o estado; Emit registra fora do lock.
    private ControlIdCircuitState Transition(ControlIdCircuitState state)
    {
        _state = state;
        return state;
    }

    private void Emit(ControlIdCircuitState? changed, int failures)
    {
        if (changed is not { } state)
        {
            return;
        }

        RecordTransition(state);
        switch (state)
        {
            case ControlIdCircuitState.Open:
                Log.CircuitOpened(_logger, _device, failures, _breakDuration.TotalSeconds);
                break;
            case ControlIdCircuitState.HalfOpen:
                Log.CircuitHalfOpen(_logger, _device);
                break;
            default:
                Log.CircuitClosed(_logger, _device);
                break;
        }
    }

    private void RecordTransition(ControlIdCircuitState state)
    {
        var tags = new TagList { { "controlid.circuit.state", StateName(state) } };
        foreach (var tag in _deviceTags)
        {
            tags.Add(tag.Key, tag.Value);
        }

        Telemetry.CircuitTransitions.Add(1, tags);
    }

    private static long ToTimestamp(TimeSpan value)
    {
        var ticks = value.TotalSeconds * Stopwatch.Frequency;
        return ticks >= long.MaxValue ? long.MaxValue : (long)ticks;
    }

    private static string StateName(ControlIdCircuitState state) => state switch
    {
        ControlIdCircuitState.Open => "open",
        ControlIdCircuitState.HalfOpen => "half_open",
        _ => "closed",
    };
}
