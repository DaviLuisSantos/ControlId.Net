using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace ControlId.Access;

/// <summary>Origem do último contato recebido de um equipamento.</summary>
public enum DeviceContactSource
{
    /// <summary>Requisição Push.</summary>
    Push,
    /// <summary>Notificação do Monitor.</summary>
    Monitor,
    /// <summary>Callback do modo Online.</summary>
    Online,
}

/// <summary>Último contato registrado de um equipamento.</summary>
public sealed class DeviceContact
{
    internal DeviceContact(DateTimeOffset at, DeviceContactSource source)
    {
        At = at;
        Source = source;
    }

    /// <summary>Momento do contato.</summary>
    public DateTimeOffset At { get; }

    /// <summary>Origem do contato.</summary>
    public DeviceContactSource Source { get; }
}

/// <summary>
/// Registra o último contato por equipamento. Thread-safe; o último registro vence. Acompanha no máximo um número fixo
/// de equipamentos (padrão: 10.000): acima dele, equipamentos novos são ignorados e os já conhecidos continuam
/// atualizados, então um <c>deviceId</c> arbitrário vindo da rede não faz a memória crescer sem limite.
/// </summary>
public sealed class DeviceContactTracker
{
    private const int DefaultMaxDevices = 10_000;

    private readonly ConcurrentDictionary<long, DeviceContact> _contacts = new();
    private readonly int _maxDevices;
    private int _count;

    /// <summary>Cria o registro com o limite padrão de 10.000 equipamentos.</summary>
    public DeviceContactTracker()
        : this(DefaultMaxDevices)
    {
    }

    /// <summary>Cria o registro com um limite de equipamentos acompanhados.</summary>
    /// <param name="maxDevices">Máximo de equipamentos distintos; acima dele, equipamentos novos são ignorados.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxDevices"/> menor que 1.</exception>
    public DeviceContactTracker(int maxDevices)
    {
        if (maxDevices < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDevices), maxDevices, "O limite de equipamentos precisa ser pelo menos 1.");
        }

        _maxDevices = maxDevices;
    }

    /// <summary>Registra um contato agora. Equipamento novo acima do limite é ignorado.</summary>
    public void Record(long deviceId, DeviceContactSource source)
    {
        var contact = new DeviceContact(DateTimeOffset.UtcNow, source);
        if (_contacts.ContainsKey(deviceId))
        {
            _contacts[deviceId] = contact;
            return;
        }

        if (Interlocked.Increment(ref _count) > _maxDevices)
        {
            Interlocked.Decrement(ref _count);
            if (_contacts.ContainsKey(deviceId))
            {
                // Outro registro do mesmo equipamento ocupou a vaga enquanto isso.
                _contacts[deviceId] = contact;
            }

            return;
        }

        if (!_contacts.TryAdd(deviceId, contact))
        {
            Interlocked.Decrement(ref _count);
            _contacts[deviceId] = contact;
        }
    }

    /// <summary>Último contato do equipamento, ou null.</summary>
    public DeviceContact? GetLastContact(long deviceId)
        => _contacts.TryGetValue(deviceId, out var c) ? c : null;

    /// <summary>Instantâneo de todos os contatos.</summary>
    public IReadOnlyDictionary<long, DeviceContact> GetAll()
        => new Dictionary<long, DeviceContact>(_contacts);
}
