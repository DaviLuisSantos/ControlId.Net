using System;

namespace ControlId;

/// <summary>Resultado de uma operação num equipamento, dentro de uma execução em vários (<see cref="ControlIdDevices"/>).</summary>
public class DeviceResult
{
    internal DeviceResult(string device, Exception? error, TimeSpan elapsed)
    {
        Device = device;
        Error = error;
        Elapsed = elapsed;
    }

    /// <summary>Nome do equipamento.</summary>
    public string Device { get; }

    /// <summary><c>true</c> se a operação terminou sem erro.</summary>
    public bool Succeeded => Error is null;

    /// <summary>Erro da operação neste equipamento (ex.: <see cref="ControlIdException"/> por timeout ou falha de rede), ou <c>null</c>.</summary>
    public Exception? Error { get; }

    /// <summary>Tempo da operação neste equipamento, sem contar a espera por uma vaga.</summary>
    public TimeSpan Elapsed { get; }

    /// <inheritdoc/>
    public override string ToString() => Succeeded ? $"{Device}: ok" : $"{Device}: {Error!.Message}";
}

/// <summary>Resultado, com valor, de uma operação num equipamento.</summary>
public sealed class DeviceResult<T> : DeviceResult
{
    private readonly T _value;

    internal DeviceResult(string device, T value, Exception? error, TimeSpan elapsed)
        : base(device, error, elapsed) => _value = value;

    /// <summary>Valor devolvido pela operação.</summary>
    /// <exception cref="InvalidOperationException">A operação falhou neste equipamento (o erro vem em <c>InnerException</c>).</exception>
    public T Value => Succeeded ? _value : throw new InvalidOperationException($"A operação falhou no equipamento '{Device}'.", Error);
}
