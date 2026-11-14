using System.Net;
using System.Net.Http.Headers;
using System.Text;
using ControlId.Access;
using ControlId.Access.Monitoring;
using ControlId.Access.Online;
using ControlId.Access.Push;
using ControlId.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ControlId.Tests;

/// <summary>Token Bearer, último contato e rotas async do ASP.NET Core.</summary>
public sealed class AspNetCoreTokenTests : IAsyncDisposable
{
    private WebApplication? _app;

    private async Task<HttpClient> StartAsync(Action<IServiceCollection>? services = null, Action<WebApplication>? map = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControlIdPush();
        builder.Services.AddControlIdMonitor((_, _) => Task.CompletedTask);
        builder.Services.AddControlIdOnline((_, _) => Task.FromResult(OnlineAuthorization.Deny()));
        services?.Invoke(builder.Services);

        _app = builder.Build();
        map?.Invoke(_app);
        await _app.StartAsync();
        return _app.GetTestClient();
    }

    private DeviceContactTracker Tracker => _app!.Services.GetRequiredService<DeviceContactTracker>();

    private static HttpRequestMessage Get(string url, string? authorization = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        Authorize(request, authorization);
        return request;
    }

    private static HttpRequestMessage Post(string url, string content, string? authorization = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json"),
        };
        Authorize(request, authorization);
        return request;
    }

    private static void Authorize(HttpRequestMessage request, string? authorization)
    {
        if (authorization is not null)
        {
            request.Headers.TryAddWithoutValidation("Authorization", authorization);
        }
    }

    private static void AssertUnauthorized(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(response.Headers.WwwAuthenticate, h => h.Scheme == "Bearer");
    }

    [Fact]
    public async Task Push_with_token_requires_the_bearer_header()
    {
        var http = await StartAsync(map: app => app.MapControlIdPush().RequireControlIdToken("s3cret"));

        AssertUnauthorized(await http.SendAsync(Get("/push?deviceId=7&uuid=a")));
        AssertUnauthorized(await http.SendAsync(Get("/push?deviceId=7&uuid=a", "Bearer errado")));
        AssertUnauthorized(await http.SendAsync(Get("/push?deviceId=7&uuid=a", "Basic czNjcmV0")));
        Assert.Null(Tracker.GetLastContact(7));

        var ok = await http.SendAsync(Get("/push?deviceId=7&uuid=a", "Bearer s3cret"));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [Fact]
    public async Task Push_result_with_token_is_protected_and_delivered()
    {
        var http = await StartAsync(map: app => app.MapControlIdPush().RequireControlIdToken("s3cret"));
        var push = _app!.Services.GetRequiredService<PushServer>();
        var pending = push.SendAsync(7, new PushCommand("reboot"));

        var command = await http.SendAsync(Get("/push?deviceId=7&uuid=b", "Bearer s3cret"));
        Assert.Contains("\"endpoint\":\"reboot\"", await command.Content.ReadAsStringAsync());

        AssertUnauthorized(await http.SendAsync(Post("/result?deviceId=7&uuid=b", """{"response":{}}""")));
        Assert.False(pending.IsCompleted);

        var result = await http.SendAsync(Post("/result?deviceId=7&uuid=b", """{"response":{}}""", "Bearer s3cret"));
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        await pending.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Push_without_token_group_stays_open_and_feeds_the_tracker()
    {
        var http = await StartAsync(map: app => app.MapControlIdPush());

        var response = await http.GetAsync("/push?deviceId=9&uuid=a");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var contact = Tracker.GetLastContact(9);
        Assert.NotNull(contact);
        Assert.Equal(DeviceContactSource.Push, contact!.Source);
    }

    [Fact]
    public async Task Push_result_feeds_the_tracker()
    {
        var http = await StartAsync(map: app => app.MapControlIdPush());

        await http.PostAsync("/result?deviceId=3", new StringContent("{}"));

        Assert.Equal(DeviceContactSource.Push, Tracker.GetLastContact(3)!.Source);
    }

    [Fact]
    public async Task Device_token_wins_over_the_shared_token()
    {
        var http = await StartAsync(map: app => app.MapControlIdPush().RequireControlIdToken(o =>
        {
            o.Token = "geral";
            o.DeviceToken = id => id == 7 ? "do-7" : null;
        }));

        AssertUnauthorized(await http.SendAsync(Get("/push?deviceId=7", "Bearer geral")));
        Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(Get("/push?deviceId=7", "Bearer do-7"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(Get("/push?deviceId=8", "Bearer geral"))).StatusCode);
        AssertUnauthorized(await http.SendAsync(Get("/push?deviceId=8", "Bearer do-7")));
    }

    [Fact]
    public async Task Push_with_token_group_and_missing_device_id_is_unauthorized_before_bad_request()
    {
        var http = await StartAsync(map: app => app.MapControlIdPush().RequireControlIdToken("s3cret"));

        AssertUnauthorized(await http.SendAsync(Get("/push")));
        Assert.Equal(HttpStatusCode.BadRequest, (await http.SendAsync(Get("/push", "Bearer s3cret"))).StatusCode);
    }

    [Fact]
    public async Task Monitor_with_token_uses_the_notification_device_id()
    {
        var http = await StartAsync(map: app => app.MapControlIdMonitor().RequireControlIdToken(o =>
        {
            o.DeviceToken = id => id == 10 ? "do-10" : null;
        }));
        const string body = """{"door":{"id":1,"open":true},"device_id":10,"time":1575475894}""";

        AssertUnauthorized(await http.SendAsync(Post("/api/notifications/door", body)));
        AssertUnauthorized(await http.SendAsync(Post("/api/notifications/door", body, "Bearer outro")));
        Assert.Null(Tracker.GetLastContact(10));

        var ok = await http.SendAsync(Post("/api/notifications/door", body, "Bearer do-10"));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(DeviceContactSource.Monitor, Tracker.GetLastContact(10)!.Source);
    }

    [Fact]
    public async Task Monitor_without_token_group_feeds_the_tracker_and_ignores_missing_device_id()
    {
        var http = await StartAsync(map: app => app.MapControlIdMonitor());

        await http.PostAsync("/api/notifications/device_is_alive", new StringContent("""{"access_logs":3,"device_id":4}"""));
        await http.PostAsync("/api/notifications/device_is_alive", new StringContent("""{"access_logs":3}"""));

        Assert.Equal(DeviceContactSource.Monitor, Tracker.GetLastContact(4)!.Source);
        Assert.Single(Tracker.GetAll());
    }

    [Fact]
    public async Task Online_with_token_requires_the_bearer_header_on_every_route()
    {
        var http = await StartAsync(map: app => app.MapControlIdOnline("/api").RequireControlIdToken("s3cret"));

        AssertUnauthorized(await http.SendAsync(Post("/api/device_is_alive.fcgi?device_id=5", "{}")));
        AssertUnauthorized(await http.SendAsync(Post("/api/session_is_valid.fcgi?device_id=5", "{}", "Bearer x")));
        AssertUnauthorized(await http.SendAsync(Get("/api/user_get_image.fcgi?device_id=5&user_id=1")));
        AssertUnauthorized(await http.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/new_card.fcgi")
        {
            Content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("device_id", "5"), new KeyValuePair<string, string>("card_value", "1") }),
        }));
        Assert.Null(Tracker.GetLastContact(5));

        var alive = await http.SendAsync(Post("/api/device_is_alive.fcgi?device_id=5", "{}", "Bearer s3cret"));
        Assert.Equal(HttpStatusCode.OK, alive.StatusCode);
        Assert.Equal(DeviceContactSource.Online, Tracker.GetLastContact(5)!.Source);

        var card = new HttpRequestMessage(HttpMethod.Post, "/api/new_card.fcgi")
        {
            Content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("device_id", "6"), new KeyValuePair<string, string>("card_value", "1") }),
        };
        card.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "s3cret");
        Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(card)).StatusCode);
        Assert.Equal(DeviceContactSource.Online, Tracker.GetLastContact(6)!.Source);
    }

    [Fact]
    public async Task Online_without_token_group_stays_open()
    {
        var http = await StartAsync(map: app => app.MapControlIdOnline("/api"));

        var response = await http.PostAsync("/api/session_is_valid.fcgi", new StringContent("{}"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Unclaimed_result_goes_to_a_scoped_handler_in_its_own_scope()
    {
        var http = await StartAsync(
            services =>
            {
                services.AddSingleton<ScopeLog>();
                services.AddControlIdPushResultHandler<ScopedResultHandler>();
            },
            app => app.MapControlIdPush());
        var push = _app!.Services.GetRequiredService<PushServer>();
        var first = await push.EnqueueAsync(1, new PushCommand("reboot"));
        var second = await push.EnqueueAsync(1, new PushCommand("reboot"));

        await http.GetAsync("/push?deviceId=1&uuid=a");
        await http.PostAsync("/result?deviceId=1&uuid=a", new StringContent("""{"response":{}}""", Encoding.UTF8, "application/json"));
        await http.GetAsync("/push?deviceId=1&uuid=b");
        await http.PostAsync("/result?deviceId=1&uuid=b", new StringContent("""{"response":{}}""", Encoding.UTF8, "application/json"));

        var log = _app.Services.GetRequiredService<ScopeLog>();
        Assert.Equal(new[] { first, second }, log.Commands);
        Assert.Equal(2, log.Scopes.Distinct().Count());
    }

    [Fact]
    public async Task AddControlIdPush_uses_the_registered_store_regardless_of_order()
    {
        var store = new InMemoryPushCommandStore();
        await StartAsync(services => services.AddSingleton<IPushCommandStore>(store));

        var push = _app!.Services.GetRequiredService<PushServer>();
        await push.EnqueueAsync(2, new PushCommand("reboot"));

        Assert.Equal(1, await store.CountAsync(2, CancellationToken.None));
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private sealed class ScopeLog
    {
        public List<Guid> Commands { get; } = new();

        public List<Guid> Scopes { get; } = new();
    }

    private sealed class ScopedResultHandler : IPushResultHandler
    {
        private readonly ScopeLog _log;
        private readonly Guid _scope = Guid.NewGuid();

        public ScopedResultHandler(ScopeLog log) => _log = log;

        public Task HandleAsync(PushCompletion completion, CancellationToken cancellationToken)
        {
            lock (_log)
            {
                _log.Commands.Add(completion.CommandId);
                _log.Scopes.Add(_scope);
            }

            return Task.CompletedTask;
        }
    }
}
