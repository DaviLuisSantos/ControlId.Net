using System;
using System.Threading;
using System.Threading.Tasks;
using ControlId.Access.Monitoring;
using ControlId.Access.Push;
using ControlId.AspNetCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registro dos serviços do ControlId.Net.AspNetCore.</summary>
public static class ControlIdServiceCollectionExtensions
{
    /// <summary>Registra o <see cref="PushServer"/> como singleton (uma fila para todos os equipamentos).</summary>
    public static IServiceCollection AddControlIdPush(this IServiceCollection services)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        services.TryAddSingleton<PushServer>();
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

        services.AddSingleton<IMonitorNotificationHandler>(new DelegateMonitorNotificationHandler(handler));
        return services;
    }

    private sealed class DelegateMonitorNotificationHandler : IMonitorNotificationHandler
    {
        private readonly Func<MonitorNotification, CancellationToken, Task> _handler;

        public DelegateMonitorNotificationHandler(Func<MonitorNotification, CancellationToken, Task> handler) => _handler = handler;

        public Task HandleAsync(MonitorNotification notification, CancellationToken cancellationToken) =>
            _handler(notification, cancellationToken);
    }
}
