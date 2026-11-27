#if NET8_0_OR_GREATER
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ControlId.Access;
using ControlId.Access.Monitoring;
using ControlId.Access.Online;
using ControlId.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ControlId.Tests;

/// <summary>
/// Observador do corpo bruto (#65): o callback chega antes do <c>Parse</c> e do handler, com os segredos mascarados, sem
/// alterar a resposta. Requisições não autorizadas ou de equipamentos recusados não são observadas.
/// </summary>
[Collection("OnlineFallbackMetrics")] // o contador de fallback é global ao processo
public sealed class CallbackObserverTests : IAsyncDisposable
{
    private const string DoorBody = """{"door":{"id":1,"open":true},"device_id":10,"time":1575475894}""";

    private readonly ObserverRecorder _observer = new();
    private readonly OnlineRecorder _online = new();
    private readonly List<MonitorNotification> _notifications = new();
    private WebApplication? _app;

    private IReadOnlyList<ControlIdRawCallback> Observed => _observer.Callbacks.ToArray();

    private async Task<HttpClient> StartAsync(Action<WebApplication> map, Action<IServiceCollection>? services = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer(o => o.PreserveExecutionContext = true);
        builder.Services.AddSingleton(_observer);
        builder.Services.AddSingleton<IOnlineAccessHandler>(_online);
        builder.Services.AddControlIdCallbackObserver<RecordingObserver>();
        builder.Services.AddControlIdPush();
        builder.Services.AddControlIdMonitor((notification, _) =>
        {
            lock (_notifications)
            {
                _notifications.Add(notification);
            }

            return Task.CompletedTask;
        });
        services?.Invoke(builder.Services);

        _app = builder.Build();
        map(_app);
        await _app.StartAsync();
        return _app.GetTestClient();
    }

    private static FormUrlEncodedContent Card() =>
        new(new[]
        {
            new KeyValuePair<string, string>("device_id", "1"),
            new KeyValuePair<string, string>("card_value", "123"),
        });

    private static FormUrlEncodedContent Form(params (string Key, string Value)[] fields) =>
        new(fields.Select(f => new KeyValuePair<string, string>(f.Key, f.Value)));

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private static ByteArrayContent InvalidMultipart(byte[] body)
    {
        var content = new ByteArrayContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue("multipart/form-data");
        content.Headers.ContentType.Parameters.Add(new NameValueHeaderValue("boundary", "x"));
        return content;
    }

    private static HttpRequestMessage Request(string path, HttpContent content, params (string Name, string Value)[] headers)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = content };
        foreach (var (name, value) in headers)
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        return request;
    }

    private static async Task<JsonElement> ResultAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("result").Clone();
    }

    private static void AssertDefaultFallback(JsonElement result)
    {
        Assert.Equal((int)AccessEventType.AccessDenied, result.GetProperty("event").GetInt32());
        Assert.Equal("Sistema indisponível", result.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Invalid_body_is_observed_and_the_online_fallback_is_kept()
    {
        var http = await StartAsync(app => app.MapControlIdOnline("/api").AllowAnonymousControlIdCallbacks());
        var body = Encoding.UTF8.GetBytes("corpo que não é multipart");

        var response = await http.PostAsync("/api/new_card.fcgi?device_id=1", InvalidMultipart(body));

        // Corpo inválido: o observador recebe os bytes e a rota responde o fallback padrão (negado, 200).
        AssertDefaultFallback(await ResultAsync(response));
        Assert.Empty(_online.Identifications);

        var callback = Assert.Single(Observed);
        Assert.Equal(ControlIdCallbackKind.Online, callback.Kind);
        Assert.Equal("/api/new_card.fcgi", callback.Route);
        Assert.Equal(1L, callback.DeviceId);
        Assert.Equal(body, callback.Body.ToArray());
    }

    [Fact]
    public async Task Throwing_observer_does_not_change_the_authorizations_answer()
    {
        var http = await StartAsync(app => app.MapControlIdOnline("/api").AllowAnonymousControlIdCallbacks());
        _observer.Observe = static (_, _) => throw new InvalidOperationException("falha de propósito");

        var result = await ResultAsync(await http.PostAsync("/api/new_card.fcgi", Card()));

        Assert.Equal((int)AccessEventType.AccessGranted, result.GetProperty("event").GetInt32());
        Assert.Equal(6, result.GetProperty("user_id").GetInt64());
        Assert.Single(_online.Identifications);
        Assert.Single(Observed);
    }

    [Fact]
    public async Task Missing_authorization_is_not_observed()
    {
        var http = await StartAsync(app => app.MapControlIdOnline("/api").RequireControlIdToken("s3cret"));

        Assert.Equal(HttpStatusCode.Unauthorized, (await http.PostAsync("/api/new_card.fcgi", Card())).StatusCode);
        Assert.Empty(Observed);
        Assert.Empty(_online.Identifications);

        var wrong = Request("/api/new_card.fcgi", Card(), ("Authorization", "Bearer errado"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.SendAsync(wrong)).StatusCode);
        Assert.Empty(Observed);

        var valid = Request("/api/new_card.fcgi", Card(), ("Authorization", "Bearer s3cret"));
        Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(valid)).StatusCode);
        Assert.Single(Observed);
    }

    [Fact]
    public async Task Sensitive_field_is_masked_and_the_authorization_header_is_omitted()
    {
        var http = await StartAsync(app => app.MapControlIdOnline("/api").RequireControlIdToken("s3cret"));
        var content = Form(("device_id", "1"), ("user_id", "6"), ("password", "s3gredo"));
        var request = Request(
            "/api/new_user_id_and_password.fcgi",
            content,
            ("Authorization", "Bearer s3cret"),
            ("Proxy-Authorization", "Basic abc"),
            ("Cookie", "session=abc"),
            ("X-Trace", "abc"));

        var response = await http.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var callback = Assert.Single(Observed);
        Assert.False(callback.Headers.ContainsKey("Authorization"));
        Assert.False(callback.Headers.ContainsKey("Proxy-Authorization"));
        Assert.False(callback.Headers.ContainsKey("Cookie"));
        Assert.Equal("abc", callback.Headers["X-Trace"]);

        var observedBody = Encoding.UTF8.GetString(callback.Body.ToArray());
        Assert.Contains("password=***", observedBody, StringComparison.Ordinal);
        Assert.DoesNotContain("s3gredo", observedBody, StringComparison.Ordinal);

        // O mascaramento é só para o observador: o Parse/handler lê o corpo original.
        var identification = Assert.IsType<UserIdAndPasswordEvent>(Assert.Single(_online.Identifications));
        Assert.Equal("s3gredo", identification.Password);
    }

    [Fact]
    public async Task Invalid_monitor_body_is_observed_and_still_answers_400()
    {
        var http = await StartAsync(app => app.MapControlIdMonitor().AllowAnonymousControlIdCallbacks());
        var body = Encoding.UTF8.GetBytes("{oops");

        var response = await http.PostAsync("/api/notifications/dao?device_id=7", new ByteArrayContent(body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_notifications);
        var callback = Assert.Single(Observed);
        Assert.Equal(ControlIdCallbackKind.Monitor, callback.Kind);
        Assert.Equal("/api/notifications/dao", callback.Route);
        Assert.Equal(7L, callback.DeviceId);
        Assert.Equal(body, callback.Body.ToArray());
    }

    [Fact]
    public async Task Device_token_is_checked_before_the_observer_on_monitor()
    {
        var http = await StartAsync(app => app.MapControlIdMonitor().RequireControlIdToken(o => o.DeviceToken = id => id == 10 ? "do-10" : null));

        var wrong = Request("/api/notifications/door", Json(DoorBody), ("Authorization", "Bearer errado"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.SendAsync(wrong)).StatusCode);
        Assert.Empty(Observed);

        var valid = Request("/api/notifications/door", Json(DoorBody), ("Authorization", "Bearer do-10"));
        Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(valid)).StatusCode);
        var callback = Assert.Single(Observed);
        Assert.Equal(ControlIdCallbackKind.Monitor, callback.Kind);
        Assert.Equal(10L, callback.DeviceId);
    }

    [Fact]
    public async Task Refused_device_is_not_observed()
    {
        var http = await StartAsync(
            app =>
            {
                app.MapControlIdOnline("/api").AllowAnonymousControlIdCallbacks();
                app.MapControlIdMonitor().AllowAnonymousControlIdCallbacks();
            },
            services => services.Configure<ControlIdCallbackOptions>(o => o.IsKnownDevice = id => id == 1));

        Assert.Equal(HttpStatusCode.Forbidden, (await http.PostAsync("/api/notifications/door", Json(DoorBody))).StatusCode);
        Assert.Empty(Observed);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await http.PostAsync("/api/new_pin.fcgi", Form(("device_id", "2"), ("pin", "1234")))).StatusCode);
        Assert.Empty(Observed);

        Assert.Equal(HttpStatusCode.OK, (await http.PostAsync("/api/new_card.fcgi", Card())).StatusCode);
        Assert.Single(Observed);
    }

    [Fact]
    public async Task Device_token_online_parse_failure_is_observed_only_after_authorization()
    {
        var http = await StartAsync(app =>
            app.MapControlIdOnline("/api").RequireControlIdToken(o => o.DeviceToken = id => "t" + id));
        var body = Encoding.UTF8.GetBytes("corpo que não é multipart");

        var wrong = Request("/api/new_card.fcgi?device_id=1", InvalidMultipart(body), ("Authorization", "Bearer errado"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.SendAsync(wrong)).StatusCode);
        Assert.Empty(Observed);

        var valid = Request("/api/new_card.fcgi?device_id=1", InvalidMultipart(body), ("Authorization", "Bearer t1"));
        AssertDefaultFallback(await ResultAsync(await http.SendAsync(valid)));
        var callback = Assert.Single(Observed);
        Assert.Equal(ControlIdCallbackKind.Online, callback.Kind);
        Assert.Equal(1L, callback.DeviceId);
        Assert.Equal(body, callback.Body.ToArray());
    }

    [Fact]
    public async Task Push_result_is_observed_with_the_device_id_and_body()
    {
        var http = await StartAsync(app => app.MapControlIdPush().AllowAnonymousControlIdCallbacks());
        var body = Encoding.UTF8.GetBytes("""{"response":{}}""");

        var response = await http.PostAsync("/result?deviceId=7&uuid=a", new ByteArrayContent(body));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var callback = Assert.Single(Observed);
        Assert.Equal(ControlIdCallbackKind.PushResult, callback.Kind);
        Assert.Equal("/result", callback.Route);
        Assert.Equal(7L, callback.DeviceId);
        Assert.Equal(body, callback.Body.ToArray());
    }

    [Fact]
    public async Task Enroll_kiosk_and_exit_button_are_each_observed_once()
    {
        var http = await StartAsync(app => app.MapControlIdOnline("/api").AllowAnonymousControlIdCallbacks());

        Assert.Equal(
            HttpStatusCode.OK,
            (await http.PostAsync("/api/card_create.fcgi?device_id=1", Json("""{"user_id":1,"card_value":5,"device_id":1}"""))).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await http.PostAsync("/api/new_user_enrolled.fcgi?device_id=1", Json("""{"user_id":12,"device_id":1}"""))).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await http.PostAsync("/api/new_rex_log.fcgi?device_id=1", Json("""{"device_id":1,"rex_log":{"event":11}}"""))).StatusCode);

        Assert.Equal(3, Observed.Count);
        Assert.Equal(
            new[] { "/api/card_create.fcgi", "/api/new_user_enrolled.fcgi", "/api/new_rex_log.fcgi" },
            Observed.Select(callback => callback.Route));
        Assert.All(Observed, callback => Assert.Equal(ControlIdCallbackKind.Online, callback.Kind));
        Assert.All(Observed, callback => Assert.Equal(1L, callback.DeviceId));
        Assert.Single(_online.Enrollments);
        Assert.Single(_online.Kiosk);
        Assert.Single(_online.ExitButtons);
    }

    [Fact]
    public async Task Single_token_route_observes_before_parse_and_only_once()
    {
        var http = await StartAsync(app => app.MapControlIdOnline("/api").AllowAnonymousControlIdCallbacks());

        // A recusa por device_id divergente só é decidida depois do Parse: o observador já recebeu o callback.
        var response = await http.PostAsync(
            "/api/card_create.fcgi?device_id=1", Json("""{"user_id":1,"card_value":5,"device_id":2}"""));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(1L, Assert.Single(Observed).DeviceId);
        Assert.Empty(_online.Enrollments);
    }

    [Fact]
    public async Task Device_id_is_read_from_the_body_when_observing_before_parse()
    {
        var http = await StartAsync(app => app.MapControlIdMonitor().AllowAnonymousControlIdCallbacks());

        Assert.Equal(HttpStatusCode.OK, (await http.PostAsync("/api/notifications/door", Json(DoorBody))).StatusCode);

        Assert.Equal(10L, Assert.Single(Observed).DeviceId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Slow_observer_does_not_hold_the_answer_beyond_the_timeout(bool blocksSynchronously)
    {
        using var release = new ManualResetEventSlim();
        var never = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _observer.Observe = (_, token) =>
        {
            token.Register(() => cancelled.TrySetResult(true));
            if (blocksSynchronously)
            {
                release.Wait(TimeSpan.FromSeconds(30));
            }

            return never.Task;
        };
        var http = await StartAsync(
            app => app.MapControlIdOnline("/api").AllowAnonymousControlIdCallbacks(),
            services => services.Configure<ControlIdCallbackOptions>(o => o.ObserverTimeout = TimeSpan.FromMilliseconds(100)));
        http.Timeout = TimeSpan.FromSeconds(10);

        var response = await http.PostAsync("/api/new_card.fcgi", Card());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(_online.Identifications);
        Assert.Same(cancelled.Task, await Task.WhenAny(cancelled.Task, Task.Delay(TimeSpan.FromSeconds(5))));
        release.Set();
        never.TrySetResult(true);
    }

    [Fact]
    public void Observer_timeout_rejects_non_positive_values()
    {
        var options = new ControlIdCallbackOptions { ObserverTimeout = Timeout.InfiniteTimeSpan };

        Assert.Throws<ArgumentOutOfRangeException>(() => options.ObserverTimeout = TimeSpan.Zero);
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private sealed class ObserverRecorder
    {
        public ConcurrentQueue<ControlIdRawCallback> Callbacks { get; } = new();

        public Func<ControlIdRawCallback, CancellationToken, Task>? Observe { get; set; }
    }

    private sealed class RecordingObserver : IControlIdCallbackObserver
    {
        private readonly ObserverRecorder _recorder;

        public RecordingObserver(ObserverRecorder recorder) => _recorder = recorder;

        public async Task OnCallbackAsync(ControlIdRawCallback callback, CancellationToken cancellationToken)
        {
            _recorder.Callbacks.Enqueue(callback);
            if (_recorder.Observe is { } observe)
            {
                await observe(callback, cancellationToken);
            }
        }
    }

    private sealed class OnlineRecorder : IOnlineAccessHandler
    {
        public ConcurrentQueue<OnlineIdentificationEvent> Identifications { get; } = new();

        public ConcurrentQueue<OnlineEnrollEvent> Enrollments { get; } = new();

        public ConcurrentQueue<KioskEnrollmentEvent> Kiosk { get; } = new();

        public ConcurrentQueue<ExitButtonEvent> ExitButtons { get; } = new();

        public Task<OnlineAuthorization> AuthorizeAsync(OnlineIdentificationEvent identification, CancellationToken cancellationToken)
        {
            Identifications.Enqueue(identification);
            return Task.FromResult(OnlineAuthorization.Grant(6, "Neal Caffrey", DeviceAction.OpenDoor(1)));
        }

        public Task OnRemoteEnrollAsync(OnlineEnrollEvent enrollment, CancellationToken cancellationToken)
        {
            Enrollments.Enqueue(enrollment);
            return Task.CompletedTask;
        }

        public Task OnKioskEnrollmentAsync(KioskEnrollmentEvent enrollment, CancellationToken cancellationToken)
        {
            Kiosk.Enqueue(enrollment);
            return Task.CompletedTask;
        }

        public Task OnExitButtonAsync(ExitButtonEvent exitButton, CancellationToken cancellationToken)
        {
            ExitButtons.Enqueue(exitButton);
            return Task.CompletedTask;
        }
    }
}
#endif
