using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ControlId;

/// <summary>
/// Executa a mesma operação em vários equipamentos em paralelo, com limite de equipamentos simultâneos.
/// A falha de um equipamento não interrompe os demais: cada um tem o próprio <see cref="DeviceResult"/>, na ordem de entrada.
/// </summary>
/// <remarks>
/// O limite aqui é de equipamentos ao mesmo tempo; dentro de cada equipamento continua valendo
/// <see cref="ControlIdDeviceOptions.MaxConcurrentRequests"/>. Cancelar o <see cref="CancellationToken"/> lança
/// <see cref="OperationCanceledException"/> em vez de devolver resultados parciais.
/// </remarks>
public static class ControlIdDevices
{
    /// <summary>Padrão de equipamentos atendidos ao mesmo tempo.</summary>
    public const int DefaultMaxParallelism = 8;

    /// <summary>Executa <paramref name="operation"/> em cada equipamento e devolve o valor (ou o erro) de cada um.</summary>
    /// <example><code>
    /// var equipamentos = new Dictionary&lt;string, IAccessClient&gt; { ["portaria"] = portaria, ["garagem"] = garagem };
    /// var ids = await ControlIdDevices.RunAsync(equipamentos, (d, ct) =&gt; d.CreateUserAsync(usuario, ct));
    /// </code></example>
    public static Task<IReadOnlyList<DeviceResult<TResult>>> RunAsync<TDevice, TResult>(
        IEnumerable<KeyValuePair<string, TDevice>> devices,
        Func<TDevice, CancellationToken, Task<TResult>> operation,
        int maxParallelism = DefaultMaxParallelism,
        CancellationToken cancellationToken = default)
    {
        var list = (devices ?? throw new ArgumentNullException(nameof(devices))).ToList();
        var byName = list.ToDictionary(d => d.Key, d => d.Value);
        return RunAsync(list.Select(d => d.Key), name => byName[name], operation, maxParallelism, cancellationToken);
    }

    /// <summary>Executa <paramref name="operation"/> em cada equipamento e devolve o sucesso (ou o erro) de cada um.</summary>
    public static Task<IReadOnlyList<DeviceResult>> RunAsync<TDevice>(
        IEnumerable<KeyValuePair<string, TDevice>> devices,
        Func<TDevice, CancellationToken, Task> operation,
        int maxParallelism = DefaultMaxParallelism,
        CancellationToken cancellationToken = default)
    {
        var list = (devices ?? throw new ArgumentNullException(nameof(devices))).ToList();
        var byName = list.ToDictionary(d => d.Key, d => d.Value);
        return RunAsync(list.Select(d => d.Key), name => byName[name], operation, maxParallelism, cancellationToken);
    }

    /// <summary>
    /// Executa <paramref name="operation"/> nos equipamentos <paramref name="names"/>, obtendo cada cliente com
    /// <paramref name="getDevice"/>. Um erro ao obter o cliente (ex.: configuração inválida) também fica só no resultado daquele equipamento.
    /// </summary>
    public static Task<IReadOnlyList<DeviceResult<TResult>>> RunAsync<TDevice, TResult>(
        IEnumerable<string> names,
        Func<string, TDevice> getDevice,
        Func<TDevice, CancellationToken, Task<TResult>> operation,
        int maxParallelism = DefaultMaxParallelism,
        CancellationToken cancellationToken = default)
    {
        Validate(names, getDevice, operation, maxParallelism);
        return RunCoreAsync(names.ToList(), getDevice, operation, maxParallelism, cancellationToken);
    }

    /// <inheritdoc cref="RunAsync{TDevice, TResult}(IEnumerable{string}, Func{string, TDevice}, Func{TDevice, CancellationToken, Task{TResult}}, int, CancellationToken)"/>
    public static Task<IReadOnlyList<DeviceResult>> RunAsync<TDevice>(
        IEnumerable<string> names,
        Func<string, TDevice> getDevice,
        Func<TDevice, CancellationToken, Task> operation,
        int maxParallelism = DefaultMaxParallelism,
        CancellationToken cancellationToken = default)
    {
        Validate(names, getDevice, operation, maxParallelism);
        return RunWithoutValueAsync(names.ToList(), getDevice, operation, maxParallelism, cancellationToken);
    }

    private static async Task<IReadOnlyList<DeviceResult>> RunWithoutValueAsync<TDevice>(
        IReadOnlyList<string> names,
        Func<string, TDevice> getDevice,
        Func<TDevice, CancellationToken, Task> operation,
        int maxParallelism,
        CancellationToken cancellationToken)
    {
        var results = await RunCoreAsync<TDevice, bool>(
            names,
            getDevice,
            async (device, ct) =>
            {
                await operation(device, ct).ConfigureAwait(false);
                return true;
            },
            maxParallelism,
            cancellationToken).ConfigureAwait(false);
        return results.Select(r => new DeviceResult(r.Device, r.Error, r.Elapsed)).ToList();
    }

    private static void Validate(object names, object getDevice, object operation, int maxParallelism)
    {
        if (names is null)
        {
            throw new ArgumentNullException(nameof(names));
        }

        if (getDevice is null)
        {
            throw new ArgumentNullException(nameof(getDevice));
        }

        if (operation is null)
        {
            throw new ArgumentNullException(nameof(operation));
        }

        if (maxParallelism < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxParallelism), maxParallelism, "O paralelismo precisa ser pelo menos 1.");
        }
    }

    private static async Task<IReadOnlyList<DeviceResult<TResult>>> RunCoreAsync<TDevice, TResult>(
        IReadOnlyList<string> names,
        Func<string, TDevice> getDevice,
        Func<TDevice, CancellationToken, Task<TResult>> operation,
        int maxParallelism,
        CancellationToken cancellationToken)
    {
        using var slots = new SemaphoreSlim(maxParallelism, maxParallelism);

        async Task<DeviceResult<TResult>> RunOneAsync(string name)
        {
            await slots.WaitAsync(cancellationToken).ConfigureAwait(false);
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var value = await operation(getDevice(name), cancellationToken).ConfigureAwait(false);
                return new DeviceResult<TResult>(name, value, null, stopwatch.Elapsed);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
            {
                return new DeviceResult<TResult>(name, default!, ex, stopwatch.Elapsed);
            }
            finally
            {
                slots.Release();
            }
        }

        return await Task.WhenAll(names.Select(RunOneAsync)).ConfigureAwait(false);
    }
}
