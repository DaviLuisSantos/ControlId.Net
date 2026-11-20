using System.Net;
using System.Reflection;
using System.Text;
using ControlId.Access;
using ControlId.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlId.MinDeps.Tests;

/// <summary>
/// Roda o build netstandard2.0 das bibliotecas no .NET Framework 4.8 com as dependências nas versões mínimas (ADR 0030):
/// o cliente funciona com elas e nenhuma foi puxada para uma major mais nova.
/// </summary>
public class MinimumDependenciesTests
{
    private static readonly ControlIdDeviceOptions Options = new()
    {
        BaseAddress = new Uri("http://10.0.0.5"),
        Login = "admin",
        Password = "secret",
    };

    /// <summary>Uma dependência de cada pacote com versão mínima declarada. A major carregada tem de ser a 8.</summary>
    public static IEnumerable<object[]> MinimumAssemblies() => new[]
    {
        new object[] { typeof(System.Text.Json.JsonSerializer) },
        new object[] { typeof(IAsyncDisposable) },
        new object[] { typeof(System.Diagnostics.Metrics.Meter) },
        new object[] { typeof(ILogger) },
        new object[] { typeof(IHostedService) },
        new object[] { typeof(IHttpClientFactory) },
        new object[] { typeof(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult) },
    };

    [Theory]
    [MemberData(nameof(MinimumAssemblies))]
    public void Dependencies_stay_on_the_minimum_major(Type anchor)
    {
        var name = anchor.Assembly.GetName();

        Assert.True(name.Version!.Major == 8, $"{name.Name} {name.Version} deveria estar na major 8 (mínima do netstandard2.0).");
    }

    [Fact]
    public async Task Access_client_reads_users_with_minimum_dependencies()
    {
        var device = new StubDevice()
            .On("login.fcgi", """{"session":"abc"}""")
            .On("load_objects.fcgi", """{"users":[{"id":7,"name":"Walter White","registration":"1234"}]}""");

        using var client = new AccessClient(Options, new HttpClient(device));

        var users = await client.GetUsersAsync();

        var user = Assert.Single(users);
        Assert.Equal(7, user.Id);
        Assert.Equal("Walter White", user.Name);
    }

    [Fact]
    public async Task Dependency_injection_resolves_the_client_with_minimum_dependencies()
    {
        var device = new StubDevice()
            .On("login.fcgi", """{"session":"abc"}""")
            .On("load_objects.fcgi", """{"users":[]}""");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControlIdAccess(o =>
        {
            o.BaseAddress = new Uri("http://10.0.0.5");
            o.Password = "secret";
        }).ConfigurePrimaryHttpMessageHandler(() => device);

        using var provider = services.BuildServiceProvider();
        var users = await provider.GetRequiredService<IAccessClient>().GetUsersAsync();

        Assert.Empty(users);
    }

    private sealed class StubDevice : HttpMessageHandler
    {
        private readonly Dictionary<string, string> _routes = new();

        public StubDevice On(string endpoint, string body)
        {
            _routes[endpoint] = body;
            return this;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var endpoint = request.RequestUri!.AbsolutePath.TrimStart('/');
            return Task.FromResult(_routes.TryGetValue(endpoint, out var body)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
