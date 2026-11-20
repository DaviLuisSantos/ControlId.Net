using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using ControlId.Access;
using ControlId.Access.Monitoring;
using ControlId.Access.Push;
using ControlId.AspNetCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registro dos serviços do ControlId.Net.AspNetCore.</summary>
public static class ControlIdServiceCollectionExtensions
{
    /// <summary>
    /// Registra o <see cref="PushServer"/> como singleton (uma fila para todos os equipamentos), com as opções padrão de
    /// <see cref="PushServerOptions"/>, sobre o <see cref="IPushCommandStore"/> registrado no container (fila em memória se
    /// não houver), e o serviço de fundo que chama <see cref="PushServer.PurgeExpiredAsync"/> a cada
    /// <see cref="PushServerOptions.PurgeInterval"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// O resultado dos envios que ninguém espera vai a <b>todos</b> os <see cref="IPushResultHandler"/> registrados
    /// (<see cref="AddControlIdPushResultHandler{THandler}"/> ou registro direto, scoped ou singleton): a cada resultado é
    /// criado um escopo e os handlers rodam um depois do outro, cada um isolado; a falha de um é logada (2011) e não
    /// impede os seguintes. Sem nenhum handler registrado, esses resultados são descartados.
    /// </para>
    /// <para>
    /// As opções seguem o padrão Options (<c>IOptions&lt;PushServerOptions&gt;</c>): configure com
    /// <see cref="AddControlIdPush(IServiceCollection, Action{PushServerOptions})"/> ou <c>services.Configure&lt;PushServerOptions&gt;(...)</c>.
    /// Sem <see cref="PushServerOptions.LoggerFactory"/>, o servidor usa o <see cref="ILoggerFactory"/> do container.
    /// Chamar este método mais de uma vez não duplica nada. Store, handlers e opções só são lidos quando o servidor é criado
    /// (na partida do host, pelo serviço de varredura), então a ordem dos registros é indiferente e opções inválidas
    /// impedem a partida.
    /// </para>
    /// </remarks>
    /// <param name="services">Coleção de serviços.</param>
    public static IServiceCollection AddControlIdPush(this IServiceCollection services)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        services.AddOptions<PushServerOptions>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<PushServerOptions>, PushServerLoggerFactorySetup>(
            static provider => new PushServerLoggerFactorySetup(provider)));
        services.TryAddSingleton<DeviceContactTracker>();
        services.TryAddSingleton<IPushCommandStore, InMemoryPushCommandStore>();
        services.TryAddSingleton(static provider => CreatePushServer(provider));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, ControlIdPushMaintenanceService>(
            static provider => new ControlIdPushMaintenanceService(
                provider.GetRequiredService<PushServer>(),
                provider.GetRequiredService<IOptions<PushServerOptions>>().Value.PurgeInterval,
                provider.GetService<ILoggerFactory>()?.CreateLogger(ControlIdPushMaintenanceService.LoggerCategory))));
        return services;
    }

    /// <summary>
    /// Igual a <see cref="AddControlIdPush(IServiceCollection)"/>, configurando prazos e limites do <see cref="PushServer"/>.
    /// Pode ser chamado mais de uma vez (e junto com <c>services.Configure&lt;PushServerOptions&gt;</c>): as configurações
    /// são aplicadas na ordem dos registros, a última vence em cada propriedade.
    /// </summary>
    /// <param name="services">Coleção de serviços.</param>
    /// <param name="configure">Ajusta as <see cref="PushServerOptions"/>. Valores inválidos impedem a partida do host com <see cref="ArgumentOutOfRangeException"/>.</param>
    public static IServiceCollection AddControlIdPush(this IServiceCollection services, Action<PushServerOptions> configure)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        services.AddControlIdPush();
        services.Configure(configure);
        return services;
    }

    /// <summary>
    /// Registra um handler que recebe o resultado dos envios que ninguém espera (<see cref="PushServer.EnqueueAsync"/> ou
    /// pendentes de uma instância anterior). O <typeparamref name="THandler"/> é scoped: cada resultado roda em um
    /// escopo próprio, então ele pode receber DbContext etc. Handlers diferentes rodam todos, na ordem de registro;
    /// registrar o mesmo tipo de novo não tem efeito.
    /// </summary>
    /// <param name="services">Coleção de serviços.</param>
    public static IServiceCollection AddControlIdPushResultHandler<THandler>(this IServiceCollection services)
        where THandler : class, IPushResultHandler
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPushResultHandler, THandler>());
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

    private static PushServer CreatePushServer(IServiceProvider provider)
    {
        var options = provider.GetRequiredService<IOptions<PushServerOptions>>().Value;
        var store = provider.GetRequiredService<IPushCommandStore>();

        // Sem nenhum IPushResultHandler registrado, o servidor descarta os resultados sem dono (como sem o pacote).
        // Um container sem IServiceProviderIsService recebe o despachante de qualquer forma (sem handlers, não faz nada).
        var hasHandlers = provider.GetService<IServiceProviderIsService>() is not { } isService
            || isService.IsService(typeof(IPushResultHandler));
        var dispatcher = hasHandlers
            ? new ScopedPushResultDispatcher(
                provider.GetRequiredService<IServiceScopeFactory>(),
                provider.GetService<ILoggerFactory>()?.CreateLogger(ControlIdPushMaintenanceService.LoggerCategory) ?? NullLogger.Instance)
            : null;
        return new PushServer(store, options, dispatcher);
    }

    /// <summary>Completa <see cref="PushServerOptions.LoggerFactory"/> com o <see cref="ILoggerFactory"/> do container.</summary>
    private sealed class PushServerLoggerFactorySetup : IPostConfigureOptions<PushServerOptions>
    {
        private readonly IServiceProvider _provider;

        public PushServerLoggerFactorySetup(IServiceProvider provider) => _provider = provider;

        public void PostConfigure(string? name, PushServerOptions options) =>
            options.LoggerFactory ??= _provider.GetService<ILoggerFactory>();
    }

    /// <summary>
    /// Handler único entregue ao <see cref="PushServer"/>: a cada resultado cria um escopo e chama todos os
    /// <see cref="IPushResultHandler"/> registrados, um depois do outro (compartilham o escopo, e serviços scoped como o
    /// DbContext não aceitam uso concorrente). Não é registrado no container, então não chama a si mesmo.
    /// </summary>
    /// <remarks>
    /// A falha de um handler é logada aqui (2011, com o tipo do handler) e não impede os seguintes. No fim, se algum
    /// falhou, a exceção é relançada para o <see cref="PushServer"/> registrar o log 1101 do envio, como antes desta
    /// versão: uma falha só relança a própria exceção; várias, um <see cref="AggregateException"/> com todas. O prazo
    /// (<see cref="PushServerOptions.ResultHandlerTimeout"/>) vale para o conjunto: vencido, os handlers que ainda não
    /// rodaram são pulados.
    /// </remarks>
    private sealed class ScopedPushResultDispatcher : IPushResultHandler
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger _logger;

        public ScopedPushResultDispatcher(IServiceScopeFactory scopes, ILogger logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        public async Task HandleAsync(PushCompletion completion, CancellationToken cancellationToken)
        {
            List<Exception>? failures = null;
            var scope = _scopes.CreateAsyncScope();
            await using (scope.ConfigureAwait(false))
            {
                foreach (var handler in scope.ServiceProvider.GetServices<IPushResultHandler>())
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    try
                    {
                        await (handler.HandleAsync(completion, cancellationToken) ?? Task.CompletedTask).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        var type = handler.GetType();
                        Log.PushResultHandlerFailed(_logger, ex, type.FullName ?? type.Name, completion.CommandId, completion.DeviceId);
                        (failures ??= new List<Exception>()).Add(ex);
                    }
                }
            }

            if (failures is null)
            {
                return;
            }

            if (failures.Count == 1)
            {
                ExceptionDispatchInfo.Capture(failures[0]).Throw();
            }

            throw new AggregateException($"{failures.Count} IPushResultHandler falharam ao tratar o envio {completion.CommandId}.", failures);
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
