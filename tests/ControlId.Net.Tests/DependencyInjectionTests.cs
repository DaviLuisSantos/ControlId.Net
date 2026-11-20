using System.Net.Http;
using ControlId.Access;
using ControlId.DependencyInjection;
using ControlId.Rep;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
#if NET8_0_OR_GREATER
using Microsoft.Extensions.Hosting;
#endif
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ControlId.Tests;

public class DependencyInjectionTests
{
    private static FakeDeviceHandler Device() => new FakeDeviceHandler()
        .OnJson("login.fcgi", """{"session":"s"}""")
        .OnJson("load_objects.fcgi", """{"users":[]}""")
        .OnJson("get_afd.fcgi", "");

    // Troca o handler de todos os HttpClients pelo equipamento simulado.
    private static void UseFakeDevice(IServiceCollection services, FakeDeviceHandler device) =>
        services.ConfigureAll<HttpClientFactoryOptions>(o => o.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = device));

    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public async Task Clients_log_through_the_container_logger_factory()
    {
        var logs = new CapturingLoggerFactory();
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(logs);
        services.AddControlIdAccess(o =>
        {
            o.BaseAddress = new Uri("http://10.0.0.5:8080");
            o.Password = "secret";
        });
        UseFakeDevice(services, Device());

        await using var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<IAccessClient>().GetUsersAsync();

        Assert.Contains(logs.Entries, e => e.Category == ControlIdTelemetry.LoggerCategory && e.EventId == 1001 && e.Message.Contains("10.0.0.5:8080"));
    }

    private sealed class CapturingLoggerFactory : ILoggerFactory
    {
        public List<(string Category, int EventId, string Message)> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this, categoryName);

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public void Dispose()
        {
        }

        private sealed class CapturingLogger : ILogger
        {
            private readonly CapturingLoggerFactory _owner;
            private readonly string _category;

            public CapturingLogger(CapturingLoggerFactory owner, string category)
            {
                _owner = owner;
                _category = category;
            }

            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (_owner.Entries)
                {
                    _owner.Entries.Add((_category, eventId.Id, formatter(state, exception)));
                }
            }
        }
    }

    [Fact]
    public async Task Default_access_device_is_injectable_and_reused()
    {
        var device = Device();
        var services = new ServiceCollection();
        services.AddControlIdAccess(o =>
        {
            o.BaseAddress = new Uri("http://10.0.0.5");
            o.Password = "secret";
        });
        UseFakeDevice(services, device);

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IAccessClient>();

        await client.GetUsersAsync();
        await provider.GetRequiredService<IAccessClient>().GetUsersAsync();

        Assert.Same(client, provider.GetRequiredService<IAccessClient>());
        Assert.Same(client, provider.GetRequiredService<IControlIdClientFactory>().GetAccessClient());
        Assert.Single(device.RequestsTo("login.fcgi")); // sessão compartilhada entre as resoluções
        Assert.Equal("""{"login":"admin","password":"secret"}""", device.RequestsTo("login.fcgi").Single().Body);
        Assert.All(device.Requests, r => Assert.Equal("10.0.0.5", r.Uri.Host));
    }

    [Fact]
    public async Task Named_devices_from_configuration()
    {
        var device = Device();
        var configuration = Config(new()
        {
            ["ControlId:Access:portaria:BaseAddress"] = "http://10.0.0.1",
            ["ControlId:Access:garagem:BaseAddress"] = "http://10.0.0.2",
            ["ControlId:Access:garagem:Login"] = "operador",
            ["ControlId:Rep:rh:BaseAddress"] = "https://10.0.0.3",
            ["ControlId:Rep:rh:Mode"] = "Legacy",
            ["ControlId:Rep:rh:Timeout"] = "00:00:05",
        });

        var services = new ServiceCollection();
        services.AddControlIdAccessDevices(configuration.GetSection("ControlId:Access"));
        services.AddControlIdRepDevices(configuration.GetSection("ControlId:Rep"));
        UseFakeDevice(services, device);

        await using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IControlIdClientFactory>();

        Assert.Equal(new[] { "garagem", "portaria" }, factory.AccessDeviceNames.OrderBy(n => n));
        Assert.Equal(new[] { "rh" }, factory.RepDeviceNames);
        Assert.NotSame(factory.GetAccessClient("portaria"), factory.GetAccessClient("garagem"));

        await factory.GetAccessClient("portaria").GetUsersAsync();
        await factory.GetAccessClient("garagem").GetUsersAsync();

        var rep = factory.GetRepClient("rh");
        Assert.Equal(RepMode.Legacy, rep.Mode);
        await rep.GetAfdAsync();

        Assert.Equal(
            new[] { "10.0.0.1", "10.0.0.2", "10.0.0.3" },
            device.RequestsTo("login.fcgi").Select(r => r.Uri.Host));
        Assert.Contains("operador", device.RequestsTo("login.fcgi").Single(r => r.Uri.Host == "10.0.0.2").Body);
        Assert.Equal("?session=s", device.RequestsTo("get_afd.fcgi").Single().Uri.Query); // modo legado: sem mode=671

        // Sem registro padrão, IAccessClient/IRepClient não ficam injetáveis direto.
        Assert.Null(provider.GetService<IAccessClient>());
        Assert.Null(provider.GetService<IRepClient>());
    }

    [Fact]
    public async Task Unknown_name_lists_registered_devices()
    {
        var services = new ServiceCollection();
        services.AddControlIdAccess("portaria", o => o.BaseAddress = new Uri("http://10.0.0.1"));

        await using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IControlIdClientFactory>();

        var ex = Assert.Throws<InvalidOperationException>(() => factory.GetAccessClient("garagem"));
        Assert.Contains("'garagem'", ex.Message);
        Assert.Contains("'portaria'", ex.Message);
        Assert.Throws<InvalidOperationException>(() => factory.GetRepClient());
    }

    [Fact]
    public async Task Invalid_configuration_names_the_device()
    {
        var services = new ServiceCollection();
        services.AddControlIdAccess("portaria", _ => { }); // sem BaseAddress

        await using var provider = services.BuildServiceProvider();

        var ex = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IControlIdClientFactory>().GetAccessClient("portaria"));
        Assert.Contains("'portaria'", ex.Message);
        Assert.Contains("BaseAddress", ex.Message);
    }

    // Até a 0.8.0-alpha.1 o REP aceitava qualquer certificado por padrão; agora a validação TLS padrão vale para todos.
    [Fact]
    public async Task Rep_and_access_validate_the_server_certificate_by_default()
    {
        var services = new ServiceCollection();
        services.AddControlIdRep(o => o.BaseAddress = new Uri("https://10.0.0.3"));
        services.AddControlIdAccess(o => o.BaseAddress = new Uri("https://10.0.0.4"));

        await using var provider = services.BuildServiceProvider();
        var handlers = provider.GetRequiredService<IHttpMessageHandlerFactory>();

        Assert.False(new ControlIdRepOptions().AcceptAnyServerCertificate);
        Assert.Null(PrimaryHandler(handlers.CreateHandler("ControlId.Rep:")).ServerCertificateCustomValidationCallback);
        Assert.Null(PrimaryHandler(handlers.CreateHandler("ControlId.Access:")).ServerCertificateCustomValidationCallback);
    }

    [Fact]
    public async Task Rep_accepts_any_certificate_only_when_asked_or_pinned()
    {
        var services = new ServiceCollection();
        services.AddControlIdRep("aberto", o =>
        {
            o.BaseAddress = new Uri("https://10.0.0.3");
            o.AcceptAnyServerCertificate = true;
        });
        services.AddControlIdRep("fixado", o =>
        {
            o.BaseAddress = new Uri("https://10.0.0.4");
            o.ServerCertificateSha256 = new string('A', 64);
        });

        await using var provider = services.BuildServiceProvider();
        var handlers = provider.GetRequiredService<IHttpMessageHandlerFactory>();

        Assert.NotNull(PrimaryHandler(handlers.CreateHandler("ControlId.Rep:aberto")).ServerCertificateCustomValidationCallback);
        Assert.NotNull(PrimaryHandler(handlers.CreateHandler("ControlId.Rep:fixado")).ServerCertificateCustomValidationCallback);
    }

    [Fact]
    public async Task Startup_validation_rejects_a_missing_base_address_naming_the_device()
    {
        var services = new ServiceCollection();
        services.AddControlIdAccess("portaria", o => o.Password = "secret"); // sem BaseAddress
        services.AddControlIdAccess("garagem", o => o.BaseAddress = new Uri("http://10.0.0.2"));

        await using var provider = services.BuildServiceProvider();

        // É o que o host chama no StartAsync (ValidateOnStart).
        var ex = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
        Assert.Equal("portaria", ex.OptionsName);
        Assert.Contains("'portaria'", ex.Message);
        Assert.Contains("BaseAddress", ex.Message);
        Assert.DoesNotContain("garagem", ex.Message);
    }

    [Fact]
    public async Task Startup_validation_names_the_default_device_and_covers_the_rep()
    {
        var services = new ServiceCollection();
        services.AddControlIdRep(o => o.BaseAddress = new Uri("relativo", UriKind.Relative));

        await using var provider = services.BuildServiceProvider();

        var ex = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
        Assert.Contains("REP padrão", ex.Message);
        Assert.Contains("URI absoluta", ex.Message);
    }

    [Theory]
    [InlineData("MaxConcurrentRequests", "0")]
    [InlineData("ReadRetries", "9")]
    [InlineData("ServerCertificateSha256", "ABC")]
    [InlineData("Timeout", "00:00:00")]
    public async Task Startup_validation_rejects_out_of_range_options_from_configuration(string field, string value)
    {
        var configuration = Config(new()
        {
            ["Access:portaria:BaseAddress"] = "http://10.0.0.1",
            ["Access:portaria:" + field] = value,
        });
        var services = new ServiceCollection();
        services.AddControlIdAccessDevices(configuration.GetSection("Access"));

        await using var provider = services.BuildServiceProvider();

        var ex = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
        Assert.Contains("'portaria'", ex.Message);
    }

    [Fact]
    public async Task Startup_validation_passes_with_valid_devices_and_ignores_unregistered_names()
    {
        var services = new ServiceCollection();
        services.AddControlIdAccess(o => o.BaseAddress = new Uri("http://10.0.0.1"));
        services.AddControlIdRep("rh", o => o.BaseAddress = new Uri("https://10.0.0.3"));

        await using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IStartupValidator>().Validate();
        // Um nome que não é equipamento registrado não passa pelo validador (BaseAddress nulo seria inválido).
        Assert.Null(provider.GetRequiredService<IOptionsMonitor<ControlIdDeviceOptions>>().Get("outro").BaseAddress);
    }

#if NET8_0_OR_GREATER
    [Fact]
    public async Task Host_fails_to_start_when_a_device_has_no_base_address()
    {
        var builder = new HostApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Services.AddControlIdAccess("portaria", _ => { });

        using var host = builder.Build();

        var ex = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
        Assert.Contains("'portaria'", ex.Message);
        Assert.Contains("BaseAddress", ex.Message);
    }
#endif

    [Fact]
    public void Empty_device_sections_throw_at_registration_citing_the_path()
    {
        var configuration = Config(new() { ["ControlId:Other:x"] = "1" });
        var services = new ServiceCollection();

        var access = Assert.Throws<InvalidOperationException>(() => services.AddControlIdAccessDevices(configuration.GetSection("ControlId:Access")));
        var rep = Assert.Throws<InvalidOperationException>(() => services.AddControlIdRepDevices(configuration.GetSection("ControlId:Rep")));
        var root = Assert.Throws<InvalidOperationException>(() => services.AddControlIdAccessDevices(new ConfigurationBuilder().Build()));

        Assert.Contains("'ControlId:Access'", access.Message);
        Assert.Contains("AddControlIdAccessDevices", access.Message);
        Assert.Contains("'ControlId:Rep'", rep.Message);
        Assert.Contains("AddControlIdRepDevices", rep.Message);
        Assert.Contains("(raiz)", root.Message);
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IControlIdClientFactory));
    }

    [Fact]
    public async Task Disposing_the_container_disposes_the_factory()
    {
        var services = new ServiceCollection();
        services.AddControlIdAccess(o => o.BaseAddress = new Uri("http://10.0.0.5"));

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IControlIdClientFactory>();
        _ = factory.GetAccessClient();
        await provider.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => factory.GetAccessClient());
    }

    private static HttpClientHandler PrimaryHandler(HttpMessageHandler handler)
    {
        while (handler is DelegatingHandler delegating)
        {
            handler = delegating.InnerHandler!;
        }

        return Assert.IsType<HttpClientHandler>(handler);
    }
}
