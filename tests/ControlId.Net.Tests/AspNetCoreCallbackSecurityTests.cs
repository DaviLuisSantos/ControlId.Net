using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Net;
using System.Text;
using ControlId.Access;
using ControlId.Access.Monitoring;
using ControlId.Access.Online;
using ControlId.Access.Push;
using ControlId.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ControlId.Tests;

/// <summary>
/// Callbacks fechados por padrão, ordem token/corpo, limite de corpo, lista de equipamentos, divergência de
/// <c>device_id</c> no modo online, isolamento dos handlers do Monitor, <c>no-store</c>, checagem no <c>Map*</c> e métricas.
/// </summary>
public sealed class AspNetCoreCallbackSecurityTests : IAsyncDisposable
{
    private const string DoorBody = """{"door":{"id":1,"open":true},"device_id":10,"time":1575475894}""";

    private readonly LogSink _logs = new();
    private readonly OnlineRecorder _online = new();
    private readonly List<MonitorNotification> _notifications = new();
    private readonly BodyMeter _bodyMeter = new();
    private WebApplication? _app;

    private async Task<HttpClient> StartAsync(Action<WebApplication> map, Action<IServiceCollection>? services = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer(o => o.PreserveExecutionContext = true);
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(_logs);

        // Primeiro os registros do teste: handlers do Monitor registrados aqui rodam antes do gravador.
        services?.Invoke(builder.Services);
        builder.Services.AddControlIdPush();
        builder.Services.AddControlIdMonitor((notification, _) =>
        {
            lock (_notifications)
            {
                _notifications.Add(notification);
            }

            return Task.CompletedTask;
        });
        builder.Services.AddSingleton<IOnlineAccessHandler>(_online);

        _app = builder.Build();
        _app.Use(async (context, next) =>
        {
            // Conta quantos bytes do corpo as rotas realmente leram.
            context.Request.Body = new CountingStream(context.Request.Body, _bodyMeter);
            await next(context);
        });
        map(_app);
        await _app.StartAsync();
        return _app.GetTestClient();
    }

    private PushServer Push => _app!.Services.GetRequiredService<PushServer>();

    private DeviceContactTracker Tracker => _app!.Services.GetRequiredService<DeviceContactTracker>();

    private static HttpRequestMessage Request(HttpMethod method, string url, HttpContent? content = null, string? token = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        if (token is not null)
        {
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
        }

        return request;
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private static FormUrlEncodedContent Form(params (string Key, string Value)[] fields) =>
        new(fields.Select(f => new KeyValuePair<string, string>(f.Key, f.Value)));

    private static void AssertUnauthorized(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(response.Headers.WwwAuthenticate, h => h.Scheme == "Bearer");
    }

    [Fact]
    public async Task Routes_without_token_or_opt_in_answer_401_and_warn_once_per_route()
    {
        var http = await StartAsync(app =>
        {
            app.MapControlIdPush();
            app.MapControlIdMonitor();
            app.MapControlIdOnline("/api");
        });
        await Push.EnqueueAsync(7, new PushCommand("reboot"));

        AssertUnauthorized(await http.GetAsync("/push?deviceId=7&uuid=a"));
        AssertUnauthorized(await http.GetAsync("/push?deviceId=7&uuid=b"));
        AssertUnauthorized(await http.PostAsync("/result?deviceId=7&uuid=a", Json("""{"response":{}}""")));
        AssertUnauthorized(await http.PostAsync("/api/notifications/door", Json(DoorBody)));
        AssertUnauthorized(await http.PostAsync("/api/notifications/door", Json(DoorBody)));
        AssertUnauthorized(await http.PostAsync("/api/device_is_alive.fcgi?device_id=7", Json("{}")));
        AssertUnauthorized(await http.PostAsync("/api/new_card.fcgi", Form(("device_id", "7"), ("card_value", "1"))));
        AssertUnauthorized(await http.PostAsync("/api/card_create.fcgi?device_id=7", Json("""{"user_id":1,"card_value":1,"device_id":7}""")));

        // Nada chegou ao PushServer, ao tracker nem aos handlers; o corpo nem foi lido.
        Assert.Null(Push.GetLastSeen(7));
        Assert.Equal(1, Push.GetPendingCount(7));
        Assert.Null(Tracker.GetLastContact(7));
        Assert.Null(Tracker.GetLastContact(10));
        Assert.Empty(_notifications);
        Assert.Empty(_online.Calls);
        Assert.Equal(0, _bodyMeter.Read);

        // Um aviso por rota (push, result, notifications/{endpoint}, device_is_alive, new_card, card_create), não por requisição.
        var warnings = _logs.Of(2006);
        Assert.Equal(6, warnings.Count);
        Assert.All(warnings, w => Assert.Equal(LogLevel.Warning, w.Level));
        Assert.Single(warnings, w => w.Message.Contains("/push", StringComparison.Ordinal));
        Assert.Contains("RequireControlIdToken", warnings[0].Message, StringComparison.Ordinal);
        Assert.Contains("AllowAnonymousControlIdCallbacks", warnings[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Anonymous_opt_in_opens_every_route()
    {
        var http = await StartAsync(app =>
        {
            app.MapControlIdPush().AllowAnonymousControlIdCallbacks();
            app.MapControlIdMonitor().AllowAnonymousControlIdCallbacks();
            app.MapControlIdOnline("/api").AllowAnonymousControlIdCallbacks();
        });

        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/push?deviceId=7")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await http.PostAsync("/result?deviceId=7", Json("{}"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await http.PostAsync("/api/notifications/door", Json(DoorBody))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await http.PostAsync("/api/device_is_alive.fcgi?device_id=7", Json("{}"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await http.PostAsync("/api/new_card.fcgi", Form(("device_id", "7"), ("card_value", "1")))).StatusCode);
        Assert.Empty(_logs.Of(2006));
    }

    [Fact]
    public async Task Token_wins_when_the_group_also_allows_anonymous()
    {
        var http = await StartAsync(app =>
        {
            app.MapControlIdPush().AllowAnonymousControlIdCallbacks().RequireControlIdToken("s3cret");
            app.MapControlIdMonitor().RequireControlIdToken("s3cret").AllowAnonymousControlIdCallbacks();
        });

        AssertUnauthorized(await http.GetAsync("/push?deviceId=7"));
        AssertUnauthorized(await http.PostAsync("/api/notifications/door", Json(DoorBody)));
        Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(Request(HttpMethod.Get, "/push?deviceId=7", token: "s3cret"))).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await http.SendAsync(Request(HttpMethod.Post, "/api/notifications/door", Json(DoorBody), "s3cret"))).StatusCode);
        Assert.Equal(2, _logs.Of(2005).Count);
        Assert.Empty(_logs.Of(2006));
    }

    [Fact]
    public async Task Previous_token_is_accepted_during_rotation()
    {
        var http = await StartAsync(app => app.MapControlIdPush().RequireControlIdToken(o =>
        {
            o.Token = "novo";
            o.PreviousToken = "antigo";
        }));

        Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(Request(HttpMethod.Get, "/push?deviceId=1", token: "novo"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(Request(HttpMethod.Get, "/push?deviceId=1", token: "antigo"))).StatusCode);
        AssertUnauthorized(await http.SendAsync(Request(HttpMethod.Get, "/push?deviceId=1", token: "outro")));
    }

    [Fact]
    public async Task Device_token_is_not_called_without_a_bearer_header()
    {
        var calls = 0;
        var http = await StartAsync(app => app.MapControlIdMonitor().RequireControlIdToken(o => o.DeviceToken = id =>
        {
            Interlocked.Increment(ref calls);
            return id == 10 ? "do-10" : null;
        }));

        AssertUnauthorized(await http.PostAsync("/api/notifications/door", Json(DoorBody)));
        var basic = new HttpRequestMessage(HttpMethod.Post, "/api/notifications/door") { Content = Json(DoorBody) };
        basic.Headers.TryAddWithoutValidation("Authorization", "Basic ZG8tMTA=");
        AssertUnauthorized(await http.SendAsync(basic));
        Assert.Equal(0, Volatile.Read(ref calls));

        Assert.Equal(
            HttpStatusCode.OK,
            (await http.SendAsync(Request(HttpMethod.Post, "/api/notifications/door", Json(DoorBody), "do-10"))).StatusCode);
        Assert.Equal(1, Volatile.Read(ref calls));
    }

    [Fact]
    public async Task Shared_token_falls_back_for_unknown_device_tokens_only_when_enabled()
    {
        var http = await StartAsync(app =>
        {
            app.MapControlIdPush("/estrito").RequireControlIdToken(o =>
            {
                o.Token = "geral";
                o.DeviceToken = id => id == 7 ? "do-7" : null;
            });
            app.MapControlIdPush("/tolerante").RequireControlIdToken(o =>
            {
                o.Token = "geral";
                o.DeviceToken = id => id == 7 ? "do-7" : null;
                o.AllowTokenFallback = true;
            });
        });

        AssertUnauthorized(await http.SendAsync(Request(HttpMethod.Get, "/estrito/push?deviceId=8", token: "geral")));
        Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(Request(HttpMethod.Get, "/tolerante/push?deviceId=8", token: "geral"))).StatusCode);
        AssertUnauthorized(await http.SendAsync(Request(HttpMethod.Get, "/tolerante/push?deviceId=7", token: "geral")));
    }

    [Fact]
    public async Task Shared_token_is_checked_before_the_body_and_device_tokens_after_it()
    {
        var http = await StartAsync(
            app =>
            {
                app.MapControlIdMonitor("unico/notifications").RequireControlIdToken("s3cret");
                app.MapControlIdMonitor("porequip/notifications").RequireControlIdToken(o => o.DeviceToken = _ => "t");
            },
            services => services.Configure<ControlIdCallbackOptions>(o => o.MaxRequestBodySize = 16));
        var big = new string(' ', 4096) + DoorBody;

        // Token único: 401 sem ler um byte, mesmo com corpo acima do limite.
        AssertUnauthorized(await http.PostAsync("/unico/notifications/door", new ChunkedContent(Encoding.UTF8.GetBytes(big))));
        Assert.Equal(0, _bodyMeter.Read);
        Assert.Equal(
            HttpStatusCode.RequestEntityTooLarge,
            (await http.SendAsync(Request(HttpMethod.Post, "/unico/notifications/door", Json(big), "s3cret"))).StatusCode);

        // Token por equipamento: o deviceId está no corpo, então o limite vem antes do token.
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await http.PostAsync("/porequip/notifications/door", Json(big))).StatusCode);
        Assert.Empty(_notifications);
    }

    [Fact]
    public async Task Body_above_the_limit_is_413_without_reading_it_all()
    {
        const int limit = 1024;
        var http = await StartAsync(
            app =>
            {
                app.MapControlIdPush().AllowAnonymousControlIdCallbacks();
                app.MapControlIdMonitor().AllowAnonymousControlIdCallbacks();
                app.MapControlIdOnline("/api").AllowAnonymousControlIdCallbacks();
            },
            services => services.Configure<ControlIdCallbackOptions>(o => o.MaxRequestBodySize = limit));

        // Content-Length acima do limite: recusado sem ler.
        var declared = await http.PostAsync("/result?deviceId=1", new ByteArrayContent(new byte[limit + 1]));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, declared.StatusCode);
        Assert.Equal(0, _bodyMeter.Read);

        // Sem Content-Length (chunked): a leitura para no primeiro byte excedente.
        var chunked = await http.PostAsync("/api/notifications/door", new ChunkedContent(new byte[1024 * 1024]));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, chunked.StatusCode);
        Assert.InRange(_bodyMeter.Read, 1, limit + 1);

        var biometric = await http.PostAsync("/api/new_biometric_image.fcgi?device_id=1", new ChunkedContent(new byte[64 * 1024]));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, biometric.StatusCode);
        Assert.Empty(_notifications);
        Assert.Empty(_online.Calls);
        Assert.Null(Tracker.GetLastContact(1));
        Assert.Equal(3, _logs.Of(2010).Count);

        // No limite exato, passa.
        var exact = DoorBody + new string(' ', limit - DoorBody.Length);
        Assert.Equal(HttpStatusCode.OK, (await http.PostAsync("/api/notifications/door", new StringContent(exact))).StatusCode);
        Assert.Single(_notifications);
    }

    [Fact]
    public async Task Body_reader_lowers_the_server_limit_and_maps_its_413()
    {
        // Kestrel expõe o limite do servidor; o TestServer não. O leitor ajusta o limite e trata o 413 do servidor.
        var feature = new SizeFeature();
        var context = new DefaultHttpContext();
        context.Features.Set<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>(feature);
        context.Request.Body = new ThrowingStream(new BadHttpRequestException("grande", StatusCodes.Status413PayloadTooLarge));

        Assert.Null(await ControlId.AspNetCore.Internal.CallbackBody.ReadAsync(context, 100));
        Assert.Equal(100, feature.MaxRequestBodySize);

        var readOnly = new SizeFeature { IsReadOnly = true };
        var small = new DefaultHttpContext();
        small.Features.Set<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>(readOnly);
        small.Request.Body = new MemoryStream(new byte[] { 0xEF, 0xBB, 0xBF, (byte)'o', (byte)'k' });
        var body = await ControlId.AspNetCore.Internal.CallbackBody.ReadAsync(small, 5);
        Assert.Equal(30_000_000, readOnly.MaxRequestBodySize);
        Assert.Equal("ok", ControlId.AspNetCore.Internal.CallbackBody.ToText(body!));

        var declared = new DefaultHttpContext();
        declared.Request.ContentLength = 6;
        declared.Request.Body = new ThrowingStream(new InvalidOperationException("não deveria ler"));
        Assert.Null(await ControlId.AspNetCore.Internal.CallbackBody.ReadAsync(declared, 5));
    }

    [Fact]
    public void Body_limit_must_be_positive()
    {
        var options = new ControlIdCallbackOptions();
        Assert.Equal(4 * 1024 * 1024, options.MaxRequestBodySize);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxRequestBodySize = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxRequestBodySize = -1);
    }

    [Fact]
    public async Task Unknown_device_is_forbidden_without_touching_the_push_server_tracker_or_handlers()
    {
        var http = await StartAsync(
            app =>
            {
                app.MapControlIdPush().AllowAnonymousControlIdCallbacks();
                app.MapControlIdMonitor().AllowAnonymousControlIdCallbacks();
                app.MapControlIdOnline("/api").AllowAnonymousControlIdCallbacks();
            },
            services => services.Configure<ControlIdCallbackOptions>(o => o.IsKnownDevice = id => id == 10));
        await Push.EnqueueAsync(2, new PushCommand("reboot"));
        const string strangerDoor = """{"door":{"id":1,"open":true},"device_id":2,"time":1575475894}""";

        Assert.Equal(HttpStatusCode.Forbidden, (await http.GetAsync("/push?deviceId=2&uuid=a")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await http.PostAsync("/result?deviceId=2&uuid=a", Json("""{"response":{}}"""))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await http.PostAsync("/api/notifications/door", Json(strangerDoor))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await http.PostAsync("/api/device_is_alive.fcgi?device_id=2", Json("{}"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await http.PostAsync("/api/new_card.fcgi", Form(("device_id", "2"), ("card_value", "1")))).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await http.PostAsync("/api/card_create.fcgi", Json("""{"user_id":1,"card_value":1,"device_id":2}"""))).StatusCode);

        Assert.Null(Push.GetLastSeen(2));
        Assert.Equal(1, Push.GetPendingCount(2));
        Assert.Null(Tracker.GetLastContact(2));
        Assert.Empty(_notifications);
        Assert.Empty(_online.Calls);
        Assert.Equal(6, _logs.Of(2009).Count);

        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/push?deviceId=10")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await http.PostAsync("/api/notifications/door", Json(DoorBody))).StatusCode);
        Assert.Single(_notifications);
    }

    [Fact]
    public async Task Online_enroll_and_exit_button_with_divergent_device_id_are_forbidden()
    {
        var http = await StartAsync(app => app.MapControlIdOnline("/api").AllowAnonymousControlIdCallbacks());

        var enroll = await http.PostAsync("/api/card_create.fcgi?device_id=1", Json("""{"user_id":1,"card_value":5,"device_id":2}"""));
        var rex = await http.PostAsync("/api/new_rex_log.fcgi?device_id=1", Json("""{"device_id":2,"rex_log":{"event":11}}"""));

        Assert.Equal(HttpStatusCode.Forbidden, enroll.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, rex.StatusCode);
        Assert.Empty(_online.Calls);
        Assert.Null(Tracker.GetLastContact(1));
        Assert.Equal(2, _logs.Of(2007).Count);

        Assert.Equal(
            HttpStatusCode.OK,
            (await http.PostAsync("/api/card_create.fcgi?device_id=2", Json("""{"user_id":1,"card_value":5,"device_id":2}"""))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await http.PostAsync("/api/card_create.fcgi", Json("""{"user_id":1,"card_value":5,"device_id":2}"""))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await http.PostAsync("/api/new_rex_log.fcgi?device_id=2", Json("""{"device_id":2,"rex_log":{"event":11}}"""))).StatusCode);
        Assert.Equal(new[] { "enroll", "enroll", "exit" }, _online.Calls);
        Assert.Equal(DeviceContactSource.Online, Tracker.GetLastContact(2)!.Source);
    }

    [Fact]
    public async Task Online_device_token_authenticates_the_device_that_owns_the_body()
    {
        var http = await StartAsync(app => app.MapControlIdOnline("/api").RequireControlIdToken(o => o.DeviceToken = id => "t" + id));
        const string enrollOf2 = """{"user_id":1,"card_value":5,"device_id":2}""";

        // O token de 1 não grava o cadastro de 2, venha o device_id da query ou só do corpo.
        Assert.Equal(HttpStatusCode.Forbidden, (await http.SendAsync(Request(HttpMethod.Post, "/api/card_create.fcgi?device_id=1", Json(enrollOf2), "t1"))).StatusCode);
        AssertUnauthorized(await http.SendAsync(Request(HttpMethod.Post, "/api/card_create.fcgi", Json(enrollOf2), "t1")));
        AssertUnauthorized(await http.SendAsync(Request(HttpMethod.Post, "/api/new_rex_log.fcgi", Json("""{"device_id":2}"""), "t1")));
        AssertUnauthorized(await http.SendAsync(Request(HttpMethod.Post, "/api/new_card.fcgi", Form(("device_id", "2"), ("card_value", "1")), "t1")));
        Assert.Empty(_online.Calls);

        Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(Request(HttpMethod.Post, "/api/card_create.fcgi", Json(enrollOf2), "t2"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(Request(HttpMethod.Post, "/api/new_rex_log.fcgi", Json("""{"device_id":2}"""), "t2"))).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await http.SendAsync(Request(HttpMethod.Post, "/api/new_card.fcgi", Form(("device_id", "2"), ("card_value", "1")), "t2"))).StatusCode);
        Assert.Equal(new[] { "enroll", "exit", "authorize" }, _online.Calls);
    }

    [Fact]
    public async Task Monitor_handler_failure_does_not_stop_the_others_and_answers_500()
    {
        var http = await StartAsync(
            app => app.MapControlIdMonitor().AllowAnonymousControlIdCallbacks(),
            services => services.AddControlIdMonitor<ThrowingMonitorHandler>());

        var response = await http.PostAsync("/api/notifications/door", Json(DoorBody));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Single(_notifications); // o handler registrado depois do que falhou rodou
        var failure = Assert.Single(_logs.Of(2008));
        Assert.Equal(LogLevel.Error, failure.Level);
        Assert.Contains(nameof(ThrowingMonitorHandler), failure.Message, StringComparison.Ordinal);
        Assert.IsType<InvalidOperationException>(failure.Exception);
    }

    [Fact]
    public async Task Push_and_result_are_not_cached()
    {
        var http = await StartAsync(app => app.MapControlIdPush().AllowAnonymousControlIdCallbacks());
        await Push.EnqueueAsync(3, new PushCommand("reboot"));

        var push = await http.GetAsync("/push?deviceId=3&uuid=a");
        var result = await http.PostAsync("/result?deviceId=3&uuid=a", Json("""{"response":{}}"""));

        Assert.Contains("\"endpoint\":\"reboot\"", await push.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.True(push.Headers.CacheControl?.NoStore);
        Assert.True(result.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task Map_without_the_matching_registration_throws()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        await using var app = builder.Build();

        var push = Assert.Throws<InvalidOperationException>(() => app.MapControlIdPush());
        Assert.Contains("AddControlIdPush", push.Message, StringComparison.Ordinal);
        var online = Assert.Throws<InvalidOperationException>(() => app.MapControlIdOnline("/api"));
        Assert.Contains("AddControlIdOnline", online.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Map_online_accepts_a_scoped_handler()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(_online);
        builder.Services.AddControlIdOnline<ScopedOnlineHandler>();
        await using var app = builder.Build();

        app.MapControlIdOnline("/api").AllowAnonymousControlIdCallbacks();
    }

    [Fact]
    public async Task Metrics_count_callbacks_by_kind_and_outcome_and_time_the_handlers()
    {
        using var capture = new CallbackMetrics();
        var http = await StartAsync(
            app =>
            {
                app.MapControlIdPush().AllowAnonymousControlIdCallbacks();
                app.MapControlIdMonitor().AllowAnonymousControlIdCallbacks();
                app.MapControlIdOnline("/api").AllowAnonymousControlIdCallbacks();
                app.MapControlIdPush("/fechado");
            },
            services => services.Configure<ControlIdCallbackOptions>(o =>
            {
                o.IsKnownDevice = id => id != 99;
                o.MaxRequestBodySize = 4096;
            }));

        CallbackMetrics.Enabled.Value = true;
        await http.GetAsync("/push?deviceId=1");
        await http.GetAsync("/push");
        await http.GetAsync("/fechado/push?deviceId=1");
        await http.PostAsync("/result?deviceId=1", Json("{}"));
        await http.PostAsync("/result?deviceId=1", new ByteArrayContent(new byte[5000]));
        await http.PostAsync("/api/notifications/door", Json(DoorBody));
        await http.PostAsync("/api/notifications/door", Json("{oops"));
        await http.PostAsync("/api/new_card.fcgi", Form(("device_id", "1"), ("card_value", "1")));
        await http.PostAsync("/api/device_is_alive.fcgi?device_id=99", Json("{}"));
        CallbackMetrics.Enabled.Value = false;

        Assert.Equal(1, capture.Requests("push", "ok"));
        Assert.Equal(1, capture.Requests("push", "bad_request"));
        Assert.Equal(1, capture.Requests("push", "unauthorized"));
        Assert.Equal(1, capture.Requests("result", "ok"));
        Assert.Equal(1, capture.Requests("result", "too_large"));
        Assert.Equal(1, capture.Requests("monitor", "ok"));
        Assert.Equal(1, capture.Requests("monitor", "bad_request"));
        Assert.Equal(1, capture.Requests("online", "ok"));
        Assert.Equal(1, capture.Requests("online", "forbidden"));
        Assert.Equal(9, capture.Measurements.Count(m => m.Instrument == "controlid.server.callback.requests"));

        foreach (var kind in new[] { "push", "result", "monitor", "online" })
        {
            var durations = capture.Measurements
                .Where(m => m.Instrument == "controlid.server.callback.handler.duration" && Equals(m.Tags["controlid.callback.kind"], kind))
                .ToList();
            Assert.Single(durations);
            Assert.True(durations[0].Value >= 0);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private sealed class ThrowingMonitorHandler : IMonitorNotificationHandler
    {
        public Task HandleAsync(MonitorNotification notification, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("falha de propósito");
    }

    private sealed class OnlineRecorder : IOnlineAccessHandler
    {
        private readonly ConcurrentQueue<string> _calls = new();

        public IReadOnlyList<string> Calls => _calls.ToList();

        public Task<OnlineAuthorization> AuthorizeAsync(OnlineIdentificationEvent identification, CancellationToken cancellationToken)
        {
            _calls.Enqueue("authorize");
            return Task.FromResult(OnlineAuthorization.Deny());
        }

        public Task OnExitButtonAsync(ExitButtonEvent exitButton, CancellationToken cancellationToken)
        {
            _calls.Enqueue("exit");
            return Task.CompletedTask;
        }

        public Task OnRemoteEnrollAsync(OnlineEnrollEvent enrollment, CancellationToken cancellationToken)
        {
            _calls.Enqueue("enroll");
            return Task.CompletedTask;
        }
    }

    private sealed class ScopedOnlineHandler(OnlineRecorder recorder) : IOnlineAccessHandler
    {
        public Task<OnlineAuthorization> AuthorizeAsync(OnlineIdentificationEvent identification, CancellationToken cancellationToken) =>
            recorder.AuthorizeAsync(identification, cancellationToken);
    }

    /// <summary>Corpo sem <c>Content-Length</c> (transferência em partes).</summary>
    private sealed class ChunkedContent(byte[] data) : HttpContent
    {
        protected override async Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context)
        {
            try
            {
                for (var offset = 0; offset < data.Length; offset += 4096)
                {
                    await stream.WriteAsync(data.AsMemory(offset, Math.Min(4096, data.Length - offset)));
                }
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or InvalidOperationException)
            {
                // O servidor respondeu sem ler tudo.
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = -1;
            return false;
        }
    }

    private sealed class SizeFeature : Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature
    {
        public bool IsReadOnly { get; set; }

        public long? MaxRequestBodySize { get; set; } = 30_000_000;
    }

    private sealed class ThrowingStream(Exception exception) : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(exception);
    }

    private sealed class BodyMeter
    {
        private long _read;

        public long Read => Interlocked.Read(ref _read);

        public void Add(int count) => Interlocked.Add(ref _read, count);
    }

    private sealed class CountingStream(Stream inner, BodyMeter meter) : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = inner.Read(buffer, offset, count);
            meter.Add(read);
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = await inner.ReadAsync(buffer, cancellationToken);
            meter.Add(read);
            return read;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    internal sealed record LogEntry(string Category, int EventId, LogLevel Level, string Message, Exception? Exception);

    /// <summary>Guarda os logs do app do teste.</summary>
    private sealed class LogSink : ILoggerProvider
    {
        private readonly ConcurrentQueue<LogEntry> _entries = new();

        public List<LogEntry> Of(int eventId) => _entries.Where(e => e.EventId == eventId).ToList();

        public ILogger CreateLogger(string categoryName) => new Logger(categoryName, _entries);

        public void Dispose()
        {
        }

        private sealed class Logger(string category, ConcurrentQueue<LogEntry> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                entries.Enqueue(new LogEntry(category, eventId.Id, logLevel, formatter(state, exception), exception));
        }
    }

    /// <summary>
    /// Escuta as métricas de callback só das requisições deste teste: o <c>TestServer</c> preserva o contexto de execução,
    /// então o <see cref="AsyncLocal{T}"/> ligado no teste chega ao servidor (outros testes rodam em paralelo).
    /// </summary>
    private sealed class CallbackMetrics : IDisposable
    {
        public static readonly AsyncLocal<bool> Enabled = new();

        private readonly MeterListener _listener = new();
        private readonly ConcurrentQueue<TelemetryTests.Measurement> _measurements = new();

        public CallbackMetrics()
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == ControlIdTelemetry.MeterName && instrument.Name.StartsWith("controlid.server.callback.", StringComparison.Ordinal))
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<long>((i, v, tags, _) => Record(i, v, tags));
            _listener.SetMeasurementEventCallback<double>((i, v, tags, _) => Record(i, v, tags));
            _listener.Start();
        }

        public IReadOnlyList<TelemetryTests.Measurement> Measurements => _measurements.ToList();

        public double Requests(string kind, string outcome) => Measurements
            .Where(m => m.Instrument == "controlid.server.callback.requests"
                && Equals(m.Tags["controlid.callback.kind"], kind)
                && Equals(m.Tags["controlid.callback.outcome"], outcome))
            .Sum(m => m.Value);

        public void Dispose() => _listener.Dispose();

        private void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            if (!Enabled.Value)
            {
                return;
            }

            var dictionary = new Dictionary<string, object?>();
            foreach (var tag in tags)
            {
                dictionary[tag.Key] = tag.Value;
            }

            _measurements.Enqueue(new TelemetryTests.Measurement(instrument.Name, value, dictionary));
        }
    }
}
