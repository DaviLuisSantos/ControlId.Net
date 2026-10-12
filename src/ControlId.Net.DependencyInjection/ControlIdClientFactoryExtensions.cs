using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ControlId.Access;
using ControlId.Rep;

namespace ControlId.DependencyInjection;

/// <summary>
/// Operações em vários equipamentos registrados, com <see cref="ControlIdDevices"/>: limite de equipamentos simultâneos e
/// um resultado por equipamento (a falha de um não interrompe os demais).
/// </summary>
public static class ControlIdClientFactoryExtensions
{
    /// <summary>Executa <paramref name="operation"/> em todos os equipamentos de acesso registrados.</summary>
    /// <example><code>
    /// var resultados = await equipamentos.RunOnAccessDevicesAsync((d, ct) =&gt; d.CreateUserAsync(usuario, ct));
    /// foreach (var falha in resultados.Where(r =&gt; !r.Succeeded)) { ... }
    /// </code></example>
    public static Task<IReadOnlyList<DeviceResult<TResult>>> RunOnAccessDevicesAsync<TResult>(
        this IControlIdClientFactory factory,
        Func<IAccessClient, CancellationToken, Task<TResult>> operation,
        int maxParallelism = ControlIdDevices.DefaultMaxParallelism,
        CancellationToken cancellationToken = default) =>
        RunOnAccessDevicesAsync(factory, NotNull(factory).AccessDeviceNames, operation, maxParallelism, cancellationToken);

    /// <summary>Executa <paramref name="operation"/> em todos os equipamentos de acesso registrados.</summary>
    public static Task<IReadOnlyList<DeviceResult>> RunOnAccessDevicesAsync(
        this IControlIdClientFactory factory,
        Func<IAccessClient, CancellationToken, Task> operation,
        int maxParallelism = ControlIdDevices.DefaultMaxParallelism,
        CancellationToken cancellationToken = default) =>
        RunOnAccessDevicesAsync(factory, NotNull(factory).AccessDeviceNames, operation, maxParallelism, cancellationToken);

    /// <summary>Executa <paramref name="operation"/> nos equipamentos de acesso <paramref name="names"/>.</summary>
    public static Task<IReadOnlyList<DeviceResult<TResult>>> RunOnAccessDevicesAsync<TResult>(
        this IControlIdClientFactory factory,
        IEnumerable<string> names,
        Func<IAccessClient, CancellationToken, Task<TResult>> operation,
        int maxParallelism = ControlIdDevices.DefaultMaxParallelism,
        CancellationToken cancellationToken = default) =>
        ControlIdDevices.RunAsync(names, NotNull(factory).GetAccessClient, operation, maxParallelism, cancellationToken);

    /// <summary>Executa <paramref name="operation"/> nos equipamentos de acesso <paramref name="names"/>.</summary>
    public static Task<IReadOnlyList<DeviceResult>> RunOnAccessDevicesAsync(
        this IControlIdClientFactory factory,
        IEnumerable<string> names,
        Func<IAccessClient, CancellationToken, Task> operation,
        int maxParallelism = ControlIdDevices.DefaultMaxParallelism,
        CancellationToken cancellationToken = default) =>
        ControlIdDevices.RunAsync(names, NotNull(factory).GetAccessClient, operation, maxParallelism, cancellationToken);

    /// <summary>Executa <paramref name="operation"/> em todos os REPs registrados.</summary>
    public static Task<IReadOnlyList<DeviceResult<TResult>>> RunOnRepDevicesAsync<TResult>(
        this IControlIdClientFactory factory,
        Func<IRepClient, CancellationToken, Task<TResult>> operation,
        int maxParallelism = ControlIdDevices.DefaultMaxParallelism,
        CancellationToken cancellationToken = default) =>
        ControlIdDevices.RunAsync(NotNull(factory).RepDeviceNames, factory.GetRepClient, operation, maxParallelism, cancellationToken);

    /// <summary>Executa <paramref name="operation"/> em todos os REPs registrados.</summary>
    public static Task<IReadOnlyList<DeviceResult>> RunOnRepDevicesAsync(
        this IControlIdClientFactory factory,
        Func<IRepClient, CancellationToken, Task> operation,
        int maxParallelism = ControlIdDevices.DefaultMaxParallelism,
        CancellationToken cancellationToken = default) =>
        ControlIdDevices.RunAsync(NotNull(factory).RepDeviceNames, factory.GetRepClient, operation, maxParallelism, cancellationToken);

    private static IControlIdClientFactory NotNull(IControlIdClientFactory factory) =>
        factory ?? throw new ArgumentNullException(nameof(factory));
}
