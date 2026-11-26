using ControlId.Access;
using ControlId.Access.Online;
#if NET8_0_OR_GREATER
using System.Net;
using System.Text;
using System.Text.Json;
using ControlId.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
#endif

namespace ControlId.Tests;

/// <summary>Callbacks novos do modo online: <c>new_pin</c>, <c>new_face_template</c>, <c>face_template_create</c>, <c>tag_create</c> e <c>new_user_enrolled</c>.</summary>
public class OnlineNewRoutesParseTests
{
    private static KeyValuePair<string, string> P(string key, string value) => new(key, value);

    [Fact]
    public void New_routes_are_listed_in_the_endpoint_tables()
    {
        Assert.Contains("new_pin", OnlineEvents.IdentificationEndpoints);
        Assert.Contains("new_face_template", OnlineEvents.IdentificationEndpoints);
        Assert.Contains("face_template_create", OnlineEvents.EnrollEndpoints);
        Assert.Contains("tag_create", OnlineEvents.EnrollEndpoints);
    }

    [Fact]
    public void Every_identification_route_is_parsed_into_an_event()
    {
        foreach (var endpoint in OnlineEvents.IdentificationEndpoints)
        {
            Assert.NotNull(OnlineEvents.Parse(endpoint + ".fcgi", Array.Empty<KeyValuePair<string, string>>()));
        }
    }

    [Fact]
    public void Parses_pin_event()
    {
        var e = Assert.IsType<PinEvent>(OnlineEvents.Parse(
            "/api/new_pin.fcgi",
            new[] { P("device_id", "935107"), P("identifier_id", "2003398144"), P("component_id", "2003398145"), P("pin", "1234"), P("uuid", "u-1"), P("time", "1700000000") }));

        Assert.Equal(OnlineIdentificationKind.Pin, e.Kind);
        Assert.Equal("1234", e.Pin);
        Assert.Equal(935107, e.DeviceId);
        Assert.Equal("win0", e.IdentifierName);
        Assert.Equal("win1", e.ComponentName);
        Assert.Equal("u-1", e.Uuid);
        Assert.Equal(1700000000, e.Time);
    }

    [Fact]
    public void Parses_face_template_event_for_v7_and_v8()
    {
        var e = Assert.IsType<FaceTemplateEvent>(OnlineEvents.Parse(
            "new_face_template",
            new[] { P("device_id", "1"), P("identifier_id", "0"), P("template", "QUJD"), P("uuid", "u-2"), P("time", "1700000001") }));

        Assert.Equal(OnlineIdentificationKind.FaceTemplate, e.Kind);
        Assert.Equal("QUJD", e.Template);
        Assert.Null(e.TemplateFF);
        Assert.Null(e.TemplateMF);
        Assert.Null(e.TemplateMM);
        Assert.Equal("u-2", e.Uuid);
    }

    [Fact]
    public void Parses_face_template_event_for_v6_with_and_without_mask()
    {
        var noMask = Assert.IsType<FaceTemplateEvent>(OnlineEvents.Parse(
            "new_face_template", new[] { P("templateFF", "RkY="), P("templateMF", "TUY="), P("templateMM", string.Empty) }));
        var mask = Assert.IsType<FaceTemplateEvent>(OnlineEvents.Parse("new_face_template", new[] { P("templateMM", "TU0=") }));

        Assert.Null(noMask.Template);
        Assert.Equal("RkY=", noMask.TemplateFF);
        Assert.Equal("TUY=", noMask.TemplateMF);
        Assert.Null(noMask.TemplateMM);
        Assert.Equal("TU0=", mask.TemplateMM);
        Assert.Null(mask.TemplateFF);
    }

    [Fact]
    public void Parses_face_template_create_with_string_ids()
    {
        var e = OnlineEvents.ParseEnroll(
            "/api/face_template_create.fcgi",
            """{"user_id":"7","registration":"R-77","device_id":"935107","face_template":"QUJD"}""")!;

        Assert.Equal("face_template_create", e.Endpoint);
        Assert.Equal(RemoteEnrollType.Face, e.EnrollType);
        Assert.Equal(7, e.Result.UserId);
        Assert.Equal(935107, e.Result.DeviceId);
        Assert.Equal("R-77", e.Result.Registration);
        Assert.Equal("QUJD", e.Result.FaceTemplate);
    }

    [Fact]
    public void Parses_tag_create()
    {
        var e = OnlineEvents.ParseEnroll("tag_create.fcgi", """{"user_id":4,"tag_value":"E2801160","device_id":9}""")!;

        Assert.Equal("tag_create", e.Endpoint);
        Assert.Equal(RemoteEnrollType.Tag, e.EnrollType);
        Assert.Equal(4, e.Result.UserId);
        Assert.Equal("E2801160", e.Result.TagValue);
        Assert.Equal(9, e.Result.DeviceId);
    }

    [Fact]
    public void Card_create_exposes_num_bits()
    {
        var e = OnlineEvents.ParseEnroll("card_create", """{"user_id":1,"card_value":5,"num_bits":26,"device_id":1}""")!;

        Assert.Equal(26, e.Result.NumBits);
        Assert.Null(e.Result.TagValue);
        Assert.Null(e.Result.Registration);
    }

    [Fact]
    public void Invalid_json_in_the_new_enroll_routes_is_a_controlid_exception()
    {
        Assert.Throws<ControlIdException>(() => OnlineEvents.ParseEnroll("tag_create", "{oops"));
        Assert.Throws<ControlIdException>(() => OnlineEvents.ParseEnroll("face_template_create", "{oops"));
    }

    [Fact]
    public void Parses_kiosk_enrollment()
    {
        var e = OnlineEvents.ParseKioskEnrollment(
            """{"user_id":12,"device_id":935107,"user_image":"AQID","face_template":"QUJD","image_timestamp":1700000000}""");

        Assert.Equal(12, e.UserId);
        Assert.Equal(935107, e.DeviceId);
        Assert.Equal(new byte[] { 1, 2, 3 }, e.UserImage);
        Assert.Equal("QUJD", e.FaceTemplate);
        Assert.Equal(1700000000, e.ImageTimestamp);
    }

    [Fact]
    public void Kiosk_enrollment_accepts_string_numbers_and_missing_fields()
    {
        var e = OnlineEvents.ParseKioskEnrollment("""{"user_id":"12","device_id":"3"}""");

        Assert.Equal(12, e.UserId);
        Assert.Equal(3, e.DeviceId);
        Assert.Null(e.UserImage);
        Assert.Null(e.FaceTemplate);
        Assert.Null(e.ImageTimestamp);
        Assert.Null(OnlineEvents.ParseKioskEnrollment(string.Empty).UserId);
    }

    [Fact]
    public void Kiosk_enrollment_rejects_malformed_bodies()
    {
        Assert.Throws<ControlIdException>(() => OnlineEvents.ParseKioskEnrollment("{oops"));
        Assert.Throws<ControlIdException>(() => OnlineEvents.ParseKioskEnrollment("""{"user_id":1,"user_image":"***não é base64***"}"""));
        Assert.Throws<ControlIdException>(() => OnlineEvents.ParseKioskEnrollment("null"));
        Assert.Throws<ArgumentNullException>(() => OnlineEvents.ParseKioskEnrollment(null!));
    }

    [Fact]
    public void Remote_enroll_request_refuses_the_tag_type()
    {
        var request = new RemoteEnrollRequest(RemoteEnrollType.Tag);

        var error = Assert.Throws<ArgumentException>(() => request.ToBody(sync: true));
        Assert.Contains("tag_create", error.Message, StringComparison.Ordinal);
    }
}

#if NET8_0_OR_GREATER
public sealed class OnlineNewRoutesEndpointTests : IAsyncDisposable
{
    private const string Token = "s3cret";

    private readonly Recorder _recorder = new();
    private WebApplication? _app;

    private async Task<HttpClient> StartAsync(
        Action<WebApplication>? map = null,
        Action<IServiceCollection>? services = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(_recorder);
        builder.Services.AddControlIdOnline<Handler>();
        services?.Invoke(builder.Services);

        _app = builder.Build();
        if (map is null)
        {
            _app.MapControlIdOnline("/api").AllowAnonymousControlIdCallbacks();
        }
        else
        {
            map(_app);
        }

        await _app.StartAsync();
        return _app.GetTestClient();
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private static FormUrlEncodedContent Form(params (string Key, string Value)[] fields) =>
        new(fields.Select(f => new KeyValuePair<string, string>(f.Key, f.Value)));

    private static HttpRequestMessage Post(string url, HttpContent content, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        if (token is not null)
        {
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
        }

        return request;
    }

    [Fact]
    public async Task New_pin_is_authorized_by_the_handler_and_answers_result()
    {
        var http = await StartAsync();

        var response = await http.PostAsync("/api/new_pin.fcgi", Form(("device_id", "1"), ("identifier_id", "0"), ("pin", "1234"), ("uuid", "u")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var result = json.RootElement.GetProperty("result");
        Assert.Equal(7, result.GetProperty("event").GetInt32());
        Assert.Equal(6, result.GetProperty("user_id").GetInt32());
        Assert.Equal("1234", Assert.IsType<PinEvent>(Assert.Single(_recorder.Identifications)).Pin);
    }

    [Fact]
    public async Task New_face_template_is_authorized_by_the_handler_and_answers_result()
    {
        var http = await StartAsync();

        var granted = await http.PostAsync("/api/new_face_template.fcgi", Form(("device_id", "1"), ("template", "QUJD"), ("uuid", "u")));
        var denied = await http.PostAsync("/api/new_face_template.fcgi", Form(("device_id", "1"), ("template", "outro")));

        using var grantedJson = JsonDocument.Parse(await granted.Content.ReadAsStringAsync());
        using var deniedJson = JsonDocument.Parse(await denied.Content.ReadAsStringAsync());
        Assert.Equal(7, grantedJson.RootElement.GetProperty("result").GetProperty("event").GetInt32());
        Assert.Equal(6, deniedJson.RootElement.GetProperty("result").GetProperty("event").GetInt32());
        Assert.Collection(
            _recorder.Identifications,
            e => Assert.Equal("QUJD", Assert.IsType<FaceTemplateEvent>(e).Template),
            e => Assert.Equal("outro", Assert.IsType<FaceTemplateEvent>(e).Template));
    }

    [Fact]
    public async Task Face_template_create_and_tag_create_reach_the_enroll_handler_with_empty_ok()
    {
        var http = await StartAsync();

        var face = await http.PostAsync(
            "/api/face_template_create.fcgi", Json("""{"user_id":"7","registration":"R1","device_id":"935107","face_template":"QUJD"}"""));
        var tag = await http.PostAsync("/api/tag_create.fcgi", Json("""{"user_id":4,"tag_value":"E28011","device_id":935107}"""));

        Assert.Equal(HttpStatusCode.OK, face.StatusCode);
        Assert.Equal(string.Empty, await face.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, tag.StatusCode);
        Assert.Equal(string.Empty, await tag.Content.ReadAsStringAsync());
        Assert.Collection(
            _recorder.Enrollments,
            e =>
            {
                Assert.Equal(RemoteEnrollType.Face, e.EnrollType);
                Assert.Equal("QUJD", e.Result.FaceTemplate);
                Assert.Equal("R1", e.Result.Registration);
            },
            e =>
            {
                Assert.Equal(RemoteEnrollType.Tag, e.EnrollType);
                Assert.Equal("E28011", e.Result.TagValue);
            });
    }

    [Fact]
    public async Task New_user_enrolled_reaches_the_kiosk_handler_with_empty_ok()
    {
        var http = await StartAsync();

        var response = await http.PostAsync(
            "/api/new_user_enrolled.fcgi",
            Json("""{"user_id":12,"device_id":935107,"user_image":"AQID","face_template":"QUJD","image_timestamp":1700000000}"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
        var kiosk = Assert.Single(_recorder.Kiosk);
        Assert.Equal(12, kiosk.UserId);
        Assert.Equal(new byte[] { 1, 2, 3 }, kiosk.UserImage);
        Assert.Equal("QUJD", kiosk.FaceTemplate);
        Assert.Equal(1700000000, kiosk.ImageTimestamp);
        Assert.Empty(_recorder.Enrollments);
    }

    [Fact]
    public async Task New_user_enrolled_with_invalid_body_answers_400()
    {
        var http = await StartAsync();

        var response = await http.PostAsync("/api/new_user_enrolled.fcgi", Json("{oops"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_recorder.Kiosk);
    }

    [Fact]
    public async Task Handlers_without_the_kiosk_override_accept_the_route()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControlIdOnline((_, _) => Task.FromResult(OnlineAuthorization.Deny()));
        _app = builder.Build();
        _app.MapControlIdOnline("/api").AllowAnonymousControlIdCallbacks();
        await _app.StartAsync();
        var http = _app.GetTestClient();

        var response = await http.PostAsync("/api/new_user_enrolled.fcgi", Json("""{"user_id":1,"device_id":2}"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task All_five_routes_require_the_token()
    {
        var http = await StartAsync(app => app.MapControlIdOnline("/api").RequireControlIdToken(Token));

        var calls = new (string Url, Func<HttpContent> Body)[]
        {
            ("/api/new_pin.fcgi?device_id=1", () => Form(("device_id", "1"), ("pin", "1234"))),
            ("/api/new_face_template.fcgi?device_id=1", () => Form(("device_id", "1"), ("template", "QUJD"))),
            ("/api/face_template_create.fcgi?device_id=1", () => Json("""{"user_id":"7","device_id":"1","face_template":"QUJD"}""")),
            ("/api/tag_create.fcgi?device_id=1", () => Json("""{"user_id":4,"tag_value":"E28011","device_id":1}""")),
            ("/api/new_user_enrolled.fcgi?device_id=1", () => Json("""{"user_id":12,"device_id":1}""")),
        };

        foreach (var (url, body) in calls)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await http.SendAsync(Post(url, body()))).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await http.SendAsync(Post(url, body(), "errado"))).StatusCode);
        }

        Assert.Empty(_recorder.Identifications);
        Assert.Empty(_recorder.Enrollments);
        Assert.Empty(_recorder.Kiosk);

        foreach (var (url, body) in calls)
        {
            Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(Post(url, body(), Token))).StatusCode);
        }

        Assert.Equal(2, _recorder.Identifications.Count);
        Assert.Equal(2, _recorder.Enrollments.Count);
        Assert.Single(_recorder.Kiosk);
    }

    [Fact]
    public async Task Divergent_device_id_on_enroll_routes_is_forbidden()
    {
        var http = await StartAsync();

        var face = await http.PostAsync(
            "/api/face_template_create.fcgi?device_id=1", Json("""{"user_id":"7","device_id":"2","face_template":"QUJD"}"""));
        var tag = await http.PostAsync("/api/tag_create.fcgi?device_id=1", Json("""{"user_id":4,"tag_value":"E28011","device_id":2}"""));
        var kiosk = await http.PostAsync("/api/new_user_enrolled.fcgi?device_id=1", Json("""{"user_id":12,"device_id":2}"""));

        Assert.Equal(HttpStatusCode.Forbidden, face.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, tag.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, kiosk.StatusCode);
        Assert.Empty(_recorder.Enrollments);
        Assert.Empty(_recorder.Kiosk);

        Assert.Equal(
            HttpStatusCode.OK,
            (await http.PostAsync("/api/new_user_enrolled.fcgi?device_id=2", Json("""{"user_id":12,"device_id":2}"""))).StatusCode);
        Assert.Single(_recorder.Kiosk);
    }

    [Fact]
    public async Task Unknown_device_is_forbidden_on_the_new_identification_routes()
    {
        var http = await StartAsync(
            services: services => services.Configure<ControlIdCallbackOptions>(o => o.IsKnownDevice = id => id == 1));

        var pin = await http.PostAsync("/api/new_pin.fcgi", Form(("device_id", "2"), ("pin", "1234")));
        var face = await http.PostAsync("/api/new_face_template.fcgi", Form(("device_id", "2"), ("template", "QUJD")));

        Assert.Equal(HttpStatusCode.Forbidden, pin.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, face.StatusCode);
        Assert.Empty(_recorder.Identifications);
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private sealed class Recorder
    {
        public List<OnlineIdentificationEvent> Identifications { get; } = new();

        public List<OnlineEnrollEvent> Enrollments { get; } = new();

        public List<KioskEnrollmentEvent> Kiosk { get; } = new();
    }

    private sealed class Handler : IOnlineAccessHandler
    {
        private readonly Recorder _recorder;

        public Handler(Recorder recorder) => _recorder = recorder;

        public Task<OnlineAuthorization> AuthorizeAsync(OnlineIdentificationEvent identification, CancellationToken cancellationToken)
        {
            _recorder.Identifications.Add(identification);
            return Task.FromResult(identification switch
            {
                PinEvent { Pin: "1234" } => OnlineAuthorization.Grant(6, "Neal Caffrey", DeviceAction.OpenDoor(1)),
                FaceTemplateEvent { Template: "QUJD" } => OnlineAuthorization.Grant(6, "Neal Caffrey", DeviceAction.OpenDoor(1)),
                _ => OnlineAuthorization.Deny(),
            });
        }

        public Task OnRemoteEnrollAsync(OnlineEnrollEvent enrollment, CancellationToken cancellationToken)
        {
            _recorder.Enrollments.Add(enrollment);
            return Task.CompletedTask;
        }

        public Task OnKioskEnrollmentAsync(KioskEnrollmentEvent enrollment, CancellationToken cancellationToken)
        {
            _recorder.Kiosk.Add(enrollment);
            return Task.CompletedTask;
        }
    }
}
#endif
