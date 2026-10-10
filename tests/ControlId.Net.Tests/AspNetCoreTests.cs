using System.Net;
using System.Text;
using ControlId.Access;
using ControlId.Access.Monitoring;
using ControlId.Access.Push;
using ControlId.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ControlId.Tests;

public sealed class AspNetCoreTests : IAsyncDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly List<MonitorNotification> _received = new();
    private WebApplication? _app;

    private async Task<HttpClient> StartAsync(Action<IServiceCollection>? services = null, Action<WebApplication>? map = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControlIdPush();
        builder.Services.AddControlIdMonitor((notification, _) =>
        {
            lock (_received)
            {
                _received.Add(notification);
            }

            return Task.CompletedTask;
        });
        services?.Invoke(builder.Services);

        _app = builder.Build();
        if (map is null)
        {
            _app.MapControlIdPush();
            _app.MapControlIdMonitor();
        }
        else
        {
            map(_app);
        }

        await _app.StartAsync();
        return _app.GetTestClient();
    }

    private PushServer Push => _app!.Services.GetRequiredService<PushServer>();

    [Fact]
    public async Task Push_round_trip_over_http()
    {
        var http = await StartAsync();

        var empty = await http.GetAsync("/push?deviceId=7&uuid=a");
        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        Assert.Equal(string.Empty, await empty.Content.ReadAsStringAsync());

        var users = Push.Device(7).LoadObjectsAsync<AccessUser>(new LoadObjectsRequest("users"));

        var command = await http.GetAsync("/push?deviceId=7&uuid=b");
        Assert.Equal("application/json", command.Content.Headers.ContentType?.MediaType);
        Assert.Contains("\"endpoint\":\"load_objects\"", await command.Content.ReadAsStringAsync());

        var result = await http.PostAsync(
            "/result?deviceId=7&uuid=b",
            new StringContent("""{"response":{"users":[{"id":1,"name":"Walter White"}]}}""", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);

        Assert.Equal("Walter White", Assert.Single(await users.WaitAsync(Timeout)).Name);
    }

    [Fact]
    public async Task Push_without_device_id_is_bad_request()
    {
        var http = await StartAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await http.GetAsync("/push?uuid=a")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsync("/result", new StringContent("{}"))).StatusCode);
    }

    [Fact]
    public async Task Unexpected_result_still_answers_ok()
    {
        var http = await StartAsync();

        var response = await http.PostAsync("/result?deviceId=1", new StringContent("{}"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Monitor_dispatches_typed_notification()
    {
        var http = await StartAsync();

        var response = await http.PostAsync(
            "/api/notifications/door",
            new StringContent("""{"door":{"id":1,"open":true},"device_id":10,"time":1575475894}""", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var door = Assert.IsType<DoorNotification>(Assert.Single(_received));
        Assert.True(door.Door!.Open);
        Assert.Equal(10, door.DeviceId);
    }

    [Fact]
    public async Task Monitor_invalid_json_is_bad_request()
    {
        var http = await StartAsync();

        var response = await http.PostAsync("/api/notifications/dao", new StringContent("{oops"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_received);
    }

    [Fact]
    public async Task Monitor_uses_custom_path_and_scoped_handlers()
    {
        var http = await StartAsync(
            services => services.AddSingleton<Counter>().AddControlIdMonitor<CountingHandler>(),
            app => app.MapControlIdMonitor("/eventos/controlid/"));

        await http.PostAsync("/eventos/controlid/device_is_alive", new StringContent("""{"access_logs":3,"device_id":1}"""));

        Assert.IsType<DeviceIsAliveNotification>(Assert.Single(_received));
        Assert.Equal(1, CountingHandler.Count(_app!.Services));
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private sealed class CountingHandler : IMonitorNotificationHandler
    {
        private readonly Counter _counter;

        public CountingHandler(Counter counter) => _counter = counter;

        public static int Count(IServiceProvider services) => services.GetRequiredService<Counter>().Value;

        public Task HandleAsync(MonitorNotification notification, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _counter.Value);
            return Task.CompletedTask;
        }
    }

    private sealed class Counter
    {
        public int Value;
    }
}
