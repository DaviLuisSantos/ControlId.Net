using System.Net;
using System.Text;
using ControlId.Access;
using ControlId.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace ControlId.Tests;

/// <summary>Equipamento simulado lento, que mede o pico de requisições simultâneas e pode "expirar" a sessão uma vez.</summary>
internal sealed class SlowDevice : HttpMessageHandler
{
    private int _sessionNumber;
    private int _expireOnce;
    private int _inFlight;
    private int _maxInFlight;
    private int _logins;
    private int _requests;

    public int Logins => Volatile.Read(ref _logins);

    public int Requests => Volatile.Read(ref _requests);

    public int MaxInFlight => Volatile.Read(ref _maxInFlight);

    /// <summary>A próxima requisição recebe 401 e a sessão atual deixa de valer, como num reinício do equipamento.</summary>
    public void ExpireSession() => Interlocked.Exchange(ref _expireOnce, 1);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var inFlight = Interlocked.Increment(ref _inFlight);
        int max;
        while ((max = Volatile.Read(ref _maxInFlight)) < inFlight && Interlocked.CompareExchange(ref _maxInFlight, inFlight, max) != max)
        {
        }

        try
        {
            await Task.Delay(15, cancellationToken);
            if (request.RequestUri!.AbsolutePath.EndsWith("login.fcgi", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _logins);
                return Json("{\"session\":\"s" + Interlocked.Increment(ref _sessionNumber) + "\"}");
            }

            Interlocked.Increment(ref _requests);
            var current = "session=s" + Volatile.Read(ref _sessionNumber);
            if (Interlocked.CompareExchange(ref _expireOnce, 2, 1) == 1)
            {
                Interlocked.Increment(ref _sessionNumber);
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }

            if (Volatile.Read(ref _expireOnce) == 2 && !request.RequestUri.Query.Contains(current))
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }

            return Json("{\"users\":[]}");
        }
        finally
        {
            Interlocked.Decrement(ref _inFlight);
        }
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}

public class ConcurrencyTests
{
    private static ControlIdDeviceOptions Options(int? maxConcurrentRequests = null)
    {
        var options = new ControlIdDeviceOptions { BaseAddress = new Uri("http://10.0.0.5") };
        if (maxConcurrentRequests is { } max)
        {
            options.MaxConcurrentRequests = max;
        }

        return options;
    }

    [Fact]
    public async Task Parallel_calls_are_limited_by_default()
    {
        var device = new SlowDevice();
        using var client = new AccessClient(Options(), new HttpClient(device));

        await Task.WhenAll(Enumerable.Range(0, 40).Select(_ => client.GetUsersAsync()));

        Assert.Equal(ControlIdDeviceOptions.DefaultMaxConcurrentRequests, device.MaxInFlight);
        Assert.Equal(1, device.Logins);
        Assert.Equal(40, device.Requests);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    public async Task Parallel_calls_respect_the_configured_limit(int limit)
    {
        var device = new SlowDevice();
        using var client = new AccessClient(Options(limit), new HttpClient(device));

        await Task.WhenAll(Enumerable.Range(0, 40).Select(_ => client.GetUsersAsync()));

        Assert.Equal(limit, device.MaxInFlight);
        Assert.Equal(40, device.Requests);
    }

    [Fact]
    public async Task Each_device_has_its_own_limit()
    {
        var devices = Enumerable.Range(0, 5).Select(_ => new SlowDevice()).ToList();
        var clients = devices.Select((d, i) => new AccessClient(
            new ControlIdDeviceOptions { BaseAddress = new Uri($"http://10.0.0.{i + 1}"), MaxConcurrentRequests = 2 },
            new HttpClient(d))).ToList();

        await Task.WhenAll(clients.SelectMany(c => Enumerable.Range(0, 10).Select(_ => c.GetUsersAsync())));

        Assert.All(devices, d => Assert.Equal(2, d.MaxInFlight));
        clients.ForEach(c => c.Dispose());
    }

    [Fact]
    public async Task Session_expiring_under_load_does_not_deadlock_with_a_single_slot()
    {
        var device = new SlowDevice();
        using var client = new AccessClient(Options(1), new HttpClient(device));
        await client.GetUsersAsync();

        device.ExpireSession();
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            try
            {
                await client.GetUsersAsync();
                return true;
            }
            catch (ControlIdException)
            {
                return false;
            }
        })).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(1, device.MaxInFlight);
        Assert.Equal(2, device.Logins);
        Assert.All(results, Assert.True);
    }

    [Fact]
    public void Limit_must_be_positive()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new AccessClient(Options(0), new HttpClient(new SlowDevice())));
        Assert.Equal(nameof(ControlIdDeviceOptions.MaxConcurrentRequests), ex.ParamName);
    }

    [Fact]
    public async Task Limit_comes_from_dependency_injection_options()
    {
        var device = new SlowDevice();
        var services = new ServiceCollection();
        services.AddControlIdAccess(o =>
        {
            o.BaseAddress = new Uri("http://10.0.0.5");
            o.MaxConcurrentRequests = 3;
        });
        services.ConfigureAll<HttpClientFactoryOptions>(o => o.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = device));

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IAccessClient>();
        await Task.WhenAll(Enumerable.Range(0, 30).Select(_ => client.GetUsersAsync()));

        Assert.Equal(3, device.MaxInFlight);
    }
}
