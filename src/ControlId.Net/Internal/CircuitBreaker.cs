using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.Logging;

namespace ControlId.Internal;

/// <summary>
/// Circuit breaker de um equipamento. Conta falhas seguidas (rede, timeout do cliente, HTTP 5xx); no limite, abre e recusa
/// as requisições sem tocar o equipamento. Passada a pausa, deixa passar uma única sonda (<c>HalfOpen</c>): se ela der
/// certo o circuito fecha, se falhar reabre. Com o limite 0 não faz nada.
/// </summary>
internal sealed class CircuitBreaker
{
    private readonly object _gate = new();
    private readonly int _threshold;
    private readonly long _breakTicks;
    private readonly Func<long> _clock;
    private readonly KeyValuePair<string, object?>[] _deviceTags;
    private readonly ILogger _logger;
    private readonly string _device;
    private readonly TimeSpan _breakDuration;

    private ControlIdCircuitState _state = ControlIdCircuitState.Closed;
    private int _failures;
    private long _openedAt;
    private bool _probeInFlight;

    public CircuitBreaker(
        int failureThreshold,
        TimeSpan breakDuration,
        KeyValuePair<string, object?>[] deviceTags,
        ILogger logger,
        string device,
        Func<long>? clock = null)
    {
        _threshold = failureThreshold;
        _breakDuration = breakDuration;
        _breakTicks = (long)(breakDuration.TotalSeconds * Stopwatch.Frequency);
        _deviceTags = deviceTags;
        _logger = logger;
        _device = device;
        _clock = clock ?? Stopwatch.GetTimestamp;
    }

    public bool Enabled => _threshold > 0;

    /// <summary>Estado atual; passada a pausa de um circuito aberto, já o reporta (e registra) como <c>HalfOpen</c>.</summary>
    public ControlIdCircuitState State
    {
        get
        {
            if (!Enabled)
            {
                return ControlIdCircuitState.Closed;
            }

            lock (_gate)
            {
                Refresh();
                return _state;
            }
        }
    }

    /// <summary>
    /// Pede passagem para uma requisição. Devolve <c>true</c> se ela é a sonda do <c>HalfOpen</c> (quem recebe precisa
    /// chamar <see cref="Success"/>, <see cref="Failure"/> ou <see cref="Cancel"/>). Lança
    /// <see cref="ControlIdCircuitOpenException"/> se o circuito não admite a requisição.
    /// </summary>
    public bool Acquire()
    {
        if (!Enabled)
        {
            return false;
        }

        TimeSpan retryAfter;
        lock (_gate)
        {
            Refresh();
            switch (_state)
            {
                case ControlIdCircuitState.Closed:
                    return false;

                case ControlIdCircuitState.HalfOpen when !_probeInFlight:
                    _probeInFlight = true;
                    return true;

                case ControlIdCircuitState.HalfOpen:
                    retryAfter = TimeSpan.Zero;
                    break;

                default:
                    var remaining = _breakTicks - (_clock() - _openedAt);
                    retryAfter = remaining <= 0 ? TimeSpan.Zero : TimeSpan.FromSeconds((double)remaining / Stopwatch.Frequency);
                    break;
            }
        }

        Telemetry.CircuitRejected.Add(1, _deviceTags);
        throw new ControlIdCircuitOpenException(
            $"Circuito aberto para {_device}: o equipamento falhou várias vezes seguidas; nada foi enviado.", retryAfter);
    }

    /// <summary>A requisição recebeu resposta: de HTTP 500 em diante conta como falha, o resto como sucesso.</summary>
    public void Record(HttpStatusCode status, bool probe)
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

    public void Success(bool probe)
    {
        if (!Enabled)
        {
            return;
        }

        lock (_gate)
        {
            if (probe)
            {
                _probeInFlight = false;
                if (_state == ControlIdCircuitState.HalfOpen)
                {
                    _failures = 0;
                    Transition(ControlIdCircuitState.Closed);
                }

                return;
            }

            // Um retardatário que saiu antes de abrir não mexe no circuito já aberto.
            if (_state == ControlIdCircuitState.Closed)
            {
                _failures = 0;
            }
        }
    }

    public void Failure(bool probe)
    {
        if (!Enabled)
        {
            return;
        }

        lock (_gate)
        {
            if (probe)
            {
                _probeInFlight = false;
                if (_state == ControlIdCircuitState.HalfOpen)
                {
                    Open();
                }

                return;
            }

            if (_state != ControlIdCircuitState.Closed)
            {
                return;
            }

            _failures++;
            if (_failures >= _threshold)
            {
                Open();
            }
        }
    }

    /// <summary>A requisição não chegou a um veredito (cancelada, erro local): não conta, e a sonda libera a vez.</summary>
    public void Cancel(bool probe)
    {
        if (!probe || !Enabled)
        {
            return;
        }

        lock (_gate)
        {
            _probeInFlight = false;
        }
    }

    // Chamar com o _gate.
    private void Refresh()
    {
        if (_state == ControlIdCircuitState.Open && _clock() - _openedAt >= _breakTicks)
        {
            _probeInFlight = false;
            Transition(ControlIdCircuitState.HalfOpen);
        }
    }

    private void Open()
    {
        _openedAt = _clock();
        Transition(ControlIdCircuitState.Open);
    }

    private void Transition(ControlIdCircuitState state)
    {
        _state = state;

        var tags = new TagList { { "controlid.circuit.state", StateName(state) } };
        foreach (var tag in _deviceTags)
        {
            tags.Add(tag.Key, tag.Value);
        }

        Telemetry.CircuitTransitions.Add(1, tags);
        switch (state)
        {
            case ControlIdCircuitState.Open:
                Log.CircuitOpened(_logger, _device, _failures, _breakDuration.TotalSeconds);
                break;
            case ControlIdCircuitState.HalfOpen:
                Log.CircuitHalfOpen(_logger, _device);
                break;
            default:
                Log.CircuitClosed(_logger, _device);
                break;
        }
    }

    private static string StateName(ControlIdCircuitState state) => state switch
    {
        ControlIdCircuitState.Open => "open",
        ControlIdCircuitState.HalfOpen => "half_open",
        _ => "closed",
    };
}
