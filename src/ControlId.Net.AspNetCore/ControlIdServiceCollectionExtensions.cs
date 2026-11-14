using System;
using System.Threading;
using System.Threading.Tasks;
using ControlId.Access;
using ControlId.Access.Monitoring;
using ControlId.Access.Push;
using ControlId.AspNetCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registro dos serviços do ControlId.Net.AspNetCore.</summary>
public static class ControlIdServiceCollectionExtensions
{
    /// <summary>
    /// Registra o <see cref="PushServer"/> como singleton (uma fila para todos os equipamentos), sobre o
    /// <see cref="IPushCommandStore"/> registrado no container (fila em memória se não houver) e com o
    /// <see cref="IPushResultHandler"/> registrado, se houver. Store e handler só são resolvidos no uso,
    /// então a ordem dos registros é indiferente.
    /// </summary>
    public static IServiceCollection AddControlIdPush(this IServiceCollection services)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        services.TryAddSingleton<DeviceContactTracker>();
        services.TryAddSingleton<IPushCommandStore, InMemoryPushCommandStore>();
        services.TryAddSingleton(static provider => new PushServer(
            provider.GetRequiredService<IPushCommandStore>(),
            provider.GetService<IPushResultHandler>()));
        return services;
    }

    /// <summary>
    /// Registra o handler que recebe o resultado dos envios que ninguém espera (<see cref="PushServer.EnqueueAsync"/> ou
    /// pendentes de uma instância anterior). O <typeparamref name="THandler"/> é scoped: cada resultado roda em um
    /// escopo próprio, então ele pode receber DbContext etc.
    /// </summary>
    public static IServiceCollection AddControlIdPushResultHandler<THandler>(this IServiceCollection services)
        where THandler : class, IPushResultHandler
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        services.TryAddScoped<THandler>();
        services.TryAddSingleton<IPushResultHandler>(static provider =>
            new ScopedPushResultHandler<THandler>(provider.GetRequiredService<IServiceScopeFactory>()));
        return services;
    }

    /// <summary>Registra um handler de notificações do Monitor (scoped: pode receber DbContext etc.).</summary>
    public static IServiceCollection AddControlIdMonitor<THandler>(this IServiceCollection services)
        where THandler : class, IMonitorNotificationHandler
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        services.TryAddSingleton<DeviceContactTracker>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IMonitorNotificationHandler, THandler>());
        return services;
    }

    /// <summary>Registra uma função como handler de notificações do Monitor.</summary>
    public static IServiceCollection AddControlIdMonitor(
        this IServiceCollection services, Func<MonitorNotification, CancellationToken, Task> handler)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        services.TryAddSingleton<DeviceContactTracker>();
        services.AddSingleton<IMonitorNotificationHandler>(new DelegateMonitorNotificationHandler(handler));
        return services;
    }

    private sealed class ScopedPushResultHandler<THandler> : IPushResultHandler
        where THandler : class, IPushResultHandler
    {
        private readonly IServiceScopeFactory _scopes;

        public ScopedPushResultHandler(IServiceScopeFactory scopes) => _scopes = scopes;

        public async Task HandleAsync(PushCompletion completion, CancellationToken cancellationToken)
        {
            var scope = _scopes.CreateAsyncScope();
            await using (scope.ConfigureAwait(false))
            {
                await scope.ServiceProvider.GetRequiredService<THandler>()
                    .HandleAsync(completion, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private sealed class DelegateMonitorNotificationHandler : IMonitorNotificationHandler
    {
        private readonly Func<MonitorNotification, CancellationToken, Task> _handler;

        public DelegateMonitorNotificationHandler(Func<MonitorNotification, CancellationToken, Task> handler) => _handler = handler;

        public Task HandleAsync(MonitorNotification notification, CancellationToken cancellationToken) =>
            _handler(notification, cancellationToken);
    }
}
