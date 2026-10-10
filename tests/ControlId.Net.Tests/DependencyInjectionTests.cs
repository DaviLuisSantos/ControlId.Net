using System.Net.Http;
using ControlId.Access;
using ControlId.DependencyInjection;
using ControlId.Rep;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

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

    [Fact]
    public async Task Rep_accepts_self_signed_certificate_by_default_and_access_does_not()
    {
        var services = new ServiceCollection();
        services.AddControlIdRep(o => o.BaseAddress = new Uri("https://10.0.0.3"));
        services.AddControlIdAccess(o => o.BaseAddress = new Uri("https://10.0.0.4"));

        await using var provider = services.BuildServiceProvider();
        var handlers = provider.GetRequiredService<IHttpMessageHandlerFactory>();

        Assert.NotNull(PrimaryHandler(handlers.CreateHandler("ControlId.Rep:")).ServerCertificateCustomValidationCallback);
        Assert.Null(PrimaryHandler(handlers.CreateHandler("ControlId.Access:")).ServerCertificateCustomValidationCallback);
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
