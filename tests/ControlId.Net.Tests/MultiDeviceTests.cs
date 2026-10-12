using System.Net;
using System.Text;
using ControlId.Access;
using ControlId.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace ControlId.Tests;

public class MultiDeviceTests
{
    /// <summary>Vários equipamentos atrás de um handler só; os hosts em <paramref name="offline"/> recusam a conexão.</summary>
    private sealed class Network : HttpMessageHandler
    {
        private readonly HashSet<string> _offline;

        public Network(params string[] offline) => _offline = new HashSet<string>(offline);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var host = request.RequestUri!.Host;
            if (_offline.Contains(host))
            {
                throw new HttpRequestException($"{host} não responde");
            }

            var body = request.RequestUri.AbsolutePath.EndsWith("login.fcgi", StringComparison.Ordinal)
                ? "{\"session\":\"s\"}"
                : "{\"ids\":[" + host.Split('.').Last() + "]}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    private static Dictionary<string, IAccessClient> Devices(HttpMessageHandler network, params int[] hosts) =>
        hosts.ToDictionary(
            h => "eq" + h,
            h => (IAccessClient)new AccessClient(new ControlIdDeviceOptions { BaseAddress = new Uri($"http://10.0.0.{h}") }, new HttpClient(network)));

    [Fact]
    public async Task One_device_failing_does_not_stop_the_others()
    {
        var devices = Devices(new Network("10.0.0.2"), 1, 2, 3);

        var results = await ControlIdDevices.RunAsync(devices, (d, ct) => d.CreateUserAsync(new AccessUser { Name = "Ana" }, ct));

        Assert.Equal(new[] { "eq1", "eq2", "eq3" }, results.Select(r => r.Device));
        Assert.Equal(new[] { true, false, true }, results.Select(r => r.Succeeded));
        Assert.Equal(1, results[0].Value);
        Assert.Equal(3, results[2].Value);
        Assert.IsType<ControlIdException>(results[1].Error);

        var ex = Assert.Throws<InvalidOperationException>(() => results[1].Value);
        Assert.Same(results[1].Error, ex.InnerException);
        Assert.Contains("eq2", results[1].ToString());
    }

    [Fact]
    public async Task Operations_without_value_report_success_per_device()
    {
        var devices = Devices(new Network("10.0.0.1"), 1, 2);

        IReadOnlyList<DeviceResult> results = await ControlIdDevices.RunAsync(devices, (d, ct) => d.LoginAsync(ct));

        Assert.False(results[0].Succeeded);
        Assert.True(results[1].Succeeded);
    }

    [Fact]
    public async Task Parallelism_is_limited()
    {
        var names = Enumerable.Range(1, 20).Select(i => "eq" + i).ToList();
        int inFlight = 0, max = 0;

        var results = await ControlIdDevices.RunAsync(
            names,
            name => name,
            async (name, ct) =>
            {
                var now = Interlocked.Increment(ref inFlight);
                lock (names)
                {
                    max = Math.Max(max, now);
                }

                await Task.Delay(10, ct);
                Interlocked.Decrement(ref inFlight);
                return name.Length;
            },
            maxParallelism: 3);

        Assert.Equal(3, max);
        Assert.All(results, r => Assert.True(r.Succeeded));
        Assert.Equal(names, results.Select(r => r.Device));
    }

    [Fact]
    public async Task Errors_resolving_the_device_or_thrown_synchronously_stay_in_the_result()
    {
        var results = await ControlIdDevices.RunAsync<string, int>(
            new[] { "ok", "sem-config", "sincrono" },
            name => name == "sem-config" ? throw new InvalidOperationException("sem BaseAddress") : name,
            (name, _) => name == "sincrono" ? throw new ArgumentException("ruim") : Task.FromResult(1));

        Assert.True(results[0].Succeeded);
        Assert.IsType<InvalidOperationException>(results[1].Error);
        Assert.IsType<ArgumentException>(results[2].Error);
    }

    [Fact]
    public async Task Cancellation_is_thrown_instead_of_partial_results()
    {
        using var cts = new CancellationTokenSource();
        var names = Enumerable.Range(1, 10).Select(i => "eq" + i);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ControlIdDevices.RunAsync(
            names,
            name => name,
            async (name, ct) =>
            {
                cts.Cancel();
                await Task.Delay(1000, ct);
                return 0;
            },
            maxParallelism: 2,
            cancellationToken: cts.Token));
    }

    [Fact]
    public void Arguments_are_validated()
    {
        var devices = new Dictionary<string, IAccessClient>();
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = ControlIdDevices.RunAsync(devices, (d, ct) => d.LoginAsync(ct), maxParallelism: 0); });
        Assert.Throws<ArgumentNullException>(() => { _ = ControlIdDevices.RunAsync(devices, (Func<IAccessClient, CancellationToken, Task>)null!); });
    }

    [Fact]
    public async Task Factory_runs_on_every_registered_device()
    {
        var services = new ServiceCollection();
        services.AddControlIdAccess("portaria", o => o.BaseAddress = new Uri("http://10.0.0.1"));
        services.AddControlIdAccess("garagem", o => o.BaseAddress = new Uri("http://10.0.0.2"));
        services.AddControlIdAccess("refeitorio", o => o.BaseAddress = new Uri("http://10.0.0.3"));
        services.AddControlIdAccess("sem-config", _ => { });
        services.ConfigureAll<HttpClientFactoryOptions>(o => o.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = new Network("10.0.0.2")));

        await using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IControlIdClientFactory>();

        var results = await factory.RunOnAccessDevicesAsync((d, ct) => d.CreateUserAsync(new AccessUser { Name = "Ana" }, ct));

        var byDevice = results.ToDictionary(r => r.Device);
        Assert.Equal(4, results.Count);
        Assert.Equal(1, byDevice["portaria"].Value);
        Assert.Equal(3, byDevice["refeitorio"].Value);
        Assert.IsType<ControlIdException>(byDevice["garagem"].Error);
        Assert.IsType<InvalidOperationException>(byDevice["sem-config"].Error);

        var some = await factory.RunOnAccessDevicesAsync(new[] { "portaria" }, (d, ct) => d.LoginAsync(ct));
        Assert.True(Assert.Single(some).Succeeded);
    }
}
