using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using ControlId;
using ControlId.Access;
using ControlId.DependencyInjection;
using ControlId.Internal;
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
/// <para>
/// As opções de cada equipamento registrado são validadas na partida do host (<c>ValidateOnStart</c>): <c>BaseAddress</c>
/// ausente ou relativo, impressão digital malformada ou limites fora da faixa fazem o <c>StartAsync</c> lançar
/// <see cref="OptionsValidationException"/> citando o equipamento, em vez de falhar só no primeiro uso.
/// </para>
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
    /// (<c>BaseAddress</c>, <c>Login</c>, <c>Password</c>, <c>Timeout</c>, <c>AcceptAnyServerCertificate</c>, <c>ServerCertificateSha256</c>,
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
    /// <exception cref="InvalidOperationException">A seção não existe ou não tem nenhum equipamento.</exception>
    public static IServiceCollection AddControlIdAccessDevices(this IServiceCollection services, IConfiguration section)
    {
        foreach (var device in DevicesOf(section, "AddControlIdAccessDevices"))
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
    /// <exception cref="InvalidOperationException">A seção não existe ou não tem nenhum equipamento.</exception>
    public static IServiceCollection AddControlIdRepDevices(this IServiceCollection services, IConfiguration section)
    {
        foreach (var device in DevicesOf(section, "AddControlIdRepDevices"))
        {
            services.AddControlIdRep(device.Key, device);
        }

        return services;
    }

    // Seção sem filhas é quase sempre caminho errado (ou appsettings ausente): falha no registro em vez de não registrar nada.
    private static IConfigurationSection[] DevicesOf(IConfiguration section, string method)
    {
        if (section is null)
        {
            throw new ArgumentNullException(nameof(section));
        }

        var devices = section.GetChildren().ToArray();
        if (devices.Length == 0)
        {
            var path = section is IConfigurationSection named ? named.Path : "(raiz)";
            throw new InvalidOperationException(
                $"A seção de configuração '{path}' passada a {method} não existe ou não tem nenhum equipamento. Esperado: " +
                $"\"{path}\": {{ \"portaria\": {{ \"BaseAddress\": \"http://...\" }} }}, com uma filha por equipamento.");
        }

        return devices;
    }

    private static IHttpClientBuilder RegisterAccess(IServiceCollection services, string name)
    {
        AddCore(services).AccessDevices.Add(name);
        services.AddOptions<ControlIdDeviceOptions>(name).ValidateOnStart();
        return AddHttpClient(services, ControlIdRegistry.AccessHttpClientName(name), sp =>
        {
            var options = sp.GetRequiredService<IOptionsMonitor<ControlIdDeviceOptions>>().Get(name);
            return (options.AcceptAnyServerCertificate, options.ServerCertificateSha256, options.MaxConcurrentRequests, options.ConnectTimeout);
        });
    }

    private static IHttpClientBuilder RegisterRep(IServiceCollection services, string name)
    {
        AddCore(services).RepDevices.Add(name);
        services.AddOptions<ControlIdRepOptions>(name).ValidateOnStart();
        return AddHttpClient(services, ControlIdRegistry.RepHttpClientName(name), sp =>
        {
            var options = sp.GetRequiredService<IOptionsMonitor<ControlIdRepOptions>>().Get(name);
            return (options.AcceptAnyServerCertificate, options.ServerCertificateSha256, options.MaxConcurrentRequests, (TimeSpan?)null);
        });
    }

    private static IHttpClientBuilder AddHttpClient(IServiceCollection services, string httpClientName, Func<IServiceProvider, (bool AcceptAnyCertificate, string? CertificateSha256, int MaxConnections, TimeSpan? ConnectTimeout)> handlerSettings)
    {
        return services
            .AddHttpClient(httpClientName)
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                var settings = handlerSettings(sp);
                return HttpHandlers.CreatePrimaryHandler(
                    settings.MaxConnections, settings.ConnectTimeout, settings.CertificateSha256, settings.AcceptAnyCertificate);
            })
            // O cliente do equipamento guarda o HttpClient (e a sessão) pela vida da aplicação, então o handler não é reciclado.
            .SetHandlerLifetime(Timeout.InfiniteTimeSpan);
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
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<ControlIdDeviceOptions>, ControlIdDeviceOptionsValidator>());
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<ControlIdRepOptions>, ControlIdRepOptionsValidator>());
        }

        return registry;
    }
}
