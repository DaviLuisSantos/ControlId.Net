using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using ControlId;
using ControlId.Access;
using ControlId.DependencyInjection;
using ControlId.Rep;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registro de equipamentos Control iD no container. Cada equipamento tem um nome; o nome vazio é o "padrão",
/// que também fica disponível como <see cref="IAccessClient"/>/<see cref="IRepClient"/> injetável.
/// Para vários equipamentos, injete <see cref="IControlIdClientFactory"/>.
/// </summary>
/// <remarks>
/// Os métodos devolvem o <see cref="IHttpClientBuilder"/> do equipamento, para acrescentar handlers (proxy, retry,
/// logging). O cliente é criado uma vez e reaproveitado; mudanças posteriores na configuração exigem reiniciar a aplicação.
/// </remarks>
public static class ControlIdDeviceServiceCollectionExtensions
{
    /// <summary>Registra o equipamento de acesso padrão e o expõe como <see cref="IAccessClient"/>.</summary>
    public static IHttpClientBuilder AddControlIdAccess(this IServiceCollection services, Action<ControlIdDeviceOptions> configure)
    {
        var builder = services.AddControlIdAccess(string.Empty, configure);
        services.TryAddSingleton<IAccessClient>(sp => sp.GetRequiredService<IControlIdClientFactory>().GetAccessClient());
        return builder;
    }

    /// <summary>Registra um equipamento de acesso com nome; obtenha com <see cref="IControlIdClientFactory.GetAccessClient"/>.</summary>
    public static IHttpClientBuilder AddControlIdAccess(this IServiceCollection services, string name, Action<ControlIdDeviceOptions> configure)
    {
        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        services.Configure(name ?? throw new ArgumentNullException(nameof(name)), configure);
        return RegisterAccess(services, name);
    }

    /// <summary>
    /// Registra um equipamento de acesso com nome a partir de uma seção de configuração
    /// (<c>BaseAddress</c>, <c>Login</c>, <c>Password</c>, <c>Timeout</c>, <c>AcceptAnyServerCertificate</c>,
    /// <c>MaxConcurrentRequests</c>).
    /// </summary>
    public static IHttpClientBuilder AddControlIdAccess(this IServiceCollection services, string name, IConfiguration configuration)
    {
        if (configuration is null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        services.Configure<ControlIdDeviceOptions>(name ?? throw new ArgumentNullException(nameof(name)), configuration);
        return RegisterAccess(services, name);
    }

    /// <summary>
    /// Registra um equipamento de acesso para cada filha da seção, usando a chave como nome. Ex.:
    /// <c>"Access": { "portaria": { "BaseAddress": "http://..." }, "garagem": { ... } }</c>.
    /// </summary>
    public static IServiceCollection AddControlIdAccessDevices(this IServiceCollection services, IConfiguration section)
    {
        if (section is null)
        {
            throw new ArgumentNullException(nameof(section));
        }

        foreach (var device in section.GetChildren())
        {
            services.AddControlIdAccess(device.Key, device);
        }

        return services;
    }

    /// <summary>Registra o REP padrão e o expõe como <see cref="IRepClient"/>.</summary>
    public static IHttpClientBuilder AddControlIdRep(this IServiceCollection services, Action<ControlIdRepOptions> configure)
    {
        var builder = services.AddControlIdRep(string.Empty, configure);
        services.TryAddSingleton<IRepClient>(sp => sp.GetRequiredService<IControlIdClientFactory>().GetRepClient());
        return builder;
    }

    /// <summary>Registra um REP com nome; obtenha com <see cref="IControlIdClientFactory.GetRepClient"/>.</summary>
    public static IHttpClientBuilder AddControlIdRep(this IServiceCollection services, string name, Action<ControlIdRepOptions> configure)
    {
        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        services.Configure(name ?? throw new ArgumentNullException(nameof(name)), configure);
        return RegisterRep(services, name);
    }

    /// <summary>
    /// Registra um REP com nome a partir de uma seção de configuração
    /// (os campos de <see cref="ControlIdRepOptions"/>, incluindo <c>Mode</c>: <c>Portaria671</c> ou <c>Legacy</c>).
    /// </summary>
    public static IHttpClientBuilder AddControlIdRep(this IServiceCollection services, string name, IConfiguration configuration)
    {
        if (configuration is null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        services.Configure<ControlIdRepOptions>(name ?? throw new ArgumentNullException(nameof(name)), configuration);
        return RegisterRep(services, name);
    }

    /// <summary>Registra um REP para cada filha da seção, usando a chave como nome.</summary>
    public static IServiceCollection AddControlIdRepDevices(this IServiceCollection services, IConfiguration section)
    {
        if (section is null)
        {
            throw new ArgumentNullException(nameof(section));
        }

        foreach (var device in section.GetChildren())
        {
            services.AddControlIdRep(device.Key, device);
        }

        return services;
    }

    private static IHttpClientBuilder RegisterAccess(IServiceCollection services, string name)
    {
        AddCore(services).AccessDevices.Add(name);
        return AddHttpClient(services, ControlIdRegistry.AccessHttpClientName(name), sp =>
        {
            var options = sp.GetRequiredService<IOptionsMonitor<ControlIdDeviceOptions>>().Get(name);
            return (options.AcceptAnyServerCertificate, options.MaxConcurrentRequests);
        });
    }

    private static IHttpClientBuilder RegisterRep(IServiceCollection services, string name)
    {
        AddCore(services).RepDevices.Add(name);
        return AddHttpClient(services, ControlIdRegistry.RepHttpClientName(name), sp =>
        {
            var options = sp.GetRequiredService<IOptionsMonitor<ControlIdRepOptions>>().Get(name);
            return (options.AcceptAnyServerCertificate, options.MaxConcurrentRequests);
        });
    }

    private static IHttpClientBuilder AddHttpClient(IServiceCollection services, string httpClientName, Func<IServiceProvider, (bool AcceptAnyCertificate, int MaxConnections)> handlerSettings)
    {
        return services
            .AddHttpClient(httpClientName)
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                var settings = handlerSettings(sp);
                var handler = new HttpClientHandler();
                LimitConnections(handler, settings.MaxConnections);
                if (settings.AcceptAnyCertificate)
                {
                    handler.ServerCertificateCustomValidationCallback = static (_, _, _, _) => true;
                }

                return handler;
            })
            // O cliente do equipamento guarda o HttpClient (e a sessão) pela vida da aplicação, então o handler não é reciclado.
            .SetHandlerLifetime(Timeout.InfiniteTimeSpan);
    }

    // Alinha as conexões ao MaxConcurrentRequests (no .NET Framework o padrão seria 2 por servidor).
    private static void LimitConnections(HttpClientHandler handler, int maxConnections)
    {
        try
        {
            handler.MaxConnectionsPerServer = maxConnections;
        }
        catch (PlatformNotSupportedException)
        {
            // Fica o padrão do runtime; o limite do transporte continua valendo.
        }
    }

    private static ControlIdRegistry AddCore(IServiceCollection services)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        var registry = services
            .Where(d => d.ServiceType == typeof(ControlIdRegistry))
            .Select(d => d.ImplementationInstance)
            .OfType<ControlIdRegistry>()
            .FirstOrDefault();

        if (registry is null)
        {
            registry = new ControlIdRegistry();
            services.AddSingleton(registry);
            services.AddOptions();
            services.TryAddSingleton<IControlIdClientFactory, ControlIdClientFactory>();
        }

        return registry;
    }
}
