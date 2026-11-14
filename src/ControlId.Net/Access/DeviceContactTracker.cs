using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

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

/// <summary>Registra o último contato por equipamento. Thread-safe; o último registro vence.</summary>
public sealed class DeviceContactTracker
{
    private readonly ConcurrentDictionary<long, DeviceContact> _contacts = new();

    /// <summary>Registra um contato agora.</summary>
    public void Record(long deviceId, DeviceContactSource source)
    {
        _contacts[deviceId] = new DeviceContact(DateTimeOffset.UtcNow, source);
    }

    /// <summary>Último contato do equipamento, ou null.</summary>
    public DeviceContact? GetLastContact(long deviceId)
        => _contacts.TryGetValue(deviceId, out var c) ? c : null;

    /// <summary>Instantâneo de todos os contatos.</summary>
    public IReadOnlyDictionary<long, DeviceContact> GetAll()
        => new Dictionary<long, DeviceContact>(_contacts);
}
