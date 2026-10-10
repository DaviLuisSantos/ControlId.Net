using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using ControlId.Access;
using ControlId.Rep;
using Microsoft.Extensions.Options;

namespace ControlId.DependencyInjection;

/// <summary>Nomes registrados, preenchidos durante o <c>AddControlId...</c>.</summary>
internal sealed class ControlIdRegistry
{
    public HashSet<string> AccessDevices { get; } = new(StringComparer.Ordinal);

    public HashSet<string> RepDevices { get; } = new(StringComparer.Ordinal);

    public static string AccessHttpClientName(string name) => "ControlId.Access:" + name;

    public static string RepHttpClientName(string name) => "ControlId.Rep:" + name;
}

internal sealed class ControlIdClientFactory : IControlIdClientFactory, IDisposable
{
    private readonly ControlIdRegistry _registry;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<ControlIdDeviceOptions> _accessOptions;
    private readonly IOptionsMonitor<ControlIdRepOptions> _repOptions;
    private readonly ConcurrentDictionary<string, Lazy<AccessClient>> _accessClients = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Lazy<RepClient>> _repClients = new(StringComparer.Ordinal);
    private bool _disposed;

    public ControlIdClientFactory(
        ControlIdRegistry registry,
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<ControlIdDeviceOptions> accessOptions,
        IOptionsMonitor<ControlIdRepOptions> repOptions)
    {
        _registry = registry;
        _httpClientFactory = httpClientFactory;
        _accessOptions = accessOptions;
        _repOptions = repOptions;
    }

    public IReadOnlyCollection<string> AccessDeviceNames => _registry.AccessDevices.ToList();

    public IReadOnlyCollection<string> RepDeviceNames => _registry.RepDevices.ToList();

    public IAccessClient GetAccessClient(string name = "")
    {
        EnsureRegistered(name ?? throw new ArgumentNullException(nameof(name)), _registry.AccessDevices, "de acesso", "AddControlIdAccess");
        return Get(_accessClients, name, () =>
        {
            var options = _accessOptions.Get(name);
            var http = CreateHttpClient(ControlIdRegistry.AccessHttpClientName(name), options.Timeout);
            return Create(() => new AccessClient(options, http), "de acesso", name);
        });
    }

    public IRepClient GetRepClient(string name = "")
    {
        EnsureRegistered(name ?? throw new ArgumentNullException(nameof(name)), _registry.RepDevices, "REP", "AddControlIdRep");
        return Get(_repClients, name, () =>
        {
            var options = _repOptions.Get(name);
            var http = CreateHttpClient(ControlIdRegistry.RepHttpClientName(name), options.Timeout);
            return Create(() => new RepClient(options.ToDeviceOptions(), options.Mode, http), "REP", name);
        });
    }

    public void Dispose()
    {
        _disposed = true;
        foreach (var client in _accessClients.Values.Where(c => c.IsValueCreated))
        {
            client.Value.Dispose();
        }

        foreach (var client in _repClients.Values.Where(c => c.IsValueCreated))
        {
            client.Value.Dispose();
        }
    }

    private T Get<T>(ConcurrentDictionary<string, Lazy<T>> cache, string name, Func<T> create)
        where T : class
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ControlIdClientFactory));
        }

        var lazy = cache.GetOrAdd(name, _ => new Lazy<T>(create));
        try
        {
            return lazy.Value;
        }
        catch
        {
            // Configuração inválida: não guarda a falha, para uma correção (ex.: reload do appsettings) valer na próxima chamada.
            cache.TryRemove(name, out _);
            throw;
        }
    }

    private HttpClient CreateHttpClient(string httpClientName, TimeSpan timeout)
    {
        var http = _httpClientFactory.CreateClient(httpClientName);
        http.Timeout = timeout;
        return http;
    }

    private static T Create<T>(Func<T> create, string kind, string name)
    {
        try
        {
            return create();
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException($"Configuração inválida do equipamento {kind} {Describe(name)}: {ex.Message}", ex);
        }
    }

    private static void EnsureRegistered(string name, HashSet<string> registered, string kind, string method)
    {
        if (registered.Contains(name))
        {
            return;
        }

        var known = registered.Count == 0 ? "nenhum" : string.Join(", ", registered.Select(Describe));
        throw new InvalidOperationException(
            $"Equipamento {kind} {Describe(name)} não registrado. Registre com services.{method}(...). Registrados: {known}.");
    }

    private static string Describe(string name) => name.Length == 0 ? "padrão" : $"'{name}'";
}
