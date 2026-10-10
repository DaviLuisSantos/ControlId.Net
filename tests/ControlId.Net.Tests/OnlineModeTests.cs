using System.Text.Json;
using ControlId.Access;
using ControlId.Access.Online;

namespace ControlId.Tests;

public class OnlineModeTests
{
    private static readonly ControlIdDeviceOptions Options = new() { BaseAddress = new Uri("http://10.0.0.5") };

    private static FakeDeviceHandler LoggedDevice() => new FakeDeviceHandler().OnJson("login.fcgi", """{"session":"s"}""");

    private static KeyValuePair<string, string> P(string key, string value) => new(key, value);

    [Fact]
    public void Identifier_id_is_decoded_as_in_the_documentation()
    {
        var e = OnlineEvents.Parse("new_card", new[] { P("identifier_id", "2003398144") })!;

        Assert.Equal("win0", e.IdentifierName);
    }

    [Fact]
    public void Parses_user_identified_from_form()
    {
        var form = OnlineEvents.ParseForm(
            "device_id=935107&identifier_id=0&event=7&user_id=6&user_name=Neal+Caffrey&duress=0&face_mask=true&time=1700000000&portal_id=1&uuid=ab%2Bcd&user_has_image=2");

        var e = Assert.IsType<UserIdentifiedEvent>(OnlineEvents.Parse("/api/new_user_identified.fcgi", form));

        Assert.Equal(OnlineIdentificationKind.UserIdentified, e.Kind);
        Assert.Equal(935107, e.DeviceId);
        Assert.Equal(AccessEventType.AccessGranted, e.Event);
        Assert.Equal(6, e.UserId);
        Assert.Equal("Neal Caffrey", e.UserName);
        Assert.False(e.Duress);
        Assert.True(e.FaceMask);
        Assert.False(e.UserHasImage); // 2 = não tem foto
        Assert.Equal("ab+cd", e.Uuid);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700000000), e.TimeUtc);
        Assert.Equal("Neal Caffrey", e.Values["user_name"]);
    }

    [Fact]
    public void Parses_enterprise_events()
    {
        var card = Assert.IsType<CardEvent>(OnlineEvents.Parse("new_card.fcgi", new[] { P("card_value", "132456789"), P("panic", "1") }));
        Assert.Equal(132456789, card.CardValue);
        Assert.True(card.Panic);

        Assert.Equal("ABC", Assert.IsType<QrCodeEvent>(OnlineEvents.Parse("new_qrcode", new[] { P("qrcode_value", "ABC") })).QrCodeValue);
        Assert.Equal("E200", Assert.IsType<UhfTagEvent>(OnlineEvents.Parse("new_uhf_tag", new[] { P("uhf_tag", "E200") })).UhfTag);

        var password = Assert.IsType<UserIdAndPasswordEvent>(OnlineEvents.Parse("new_user_id_and_password", new[] { P("user_id", "9"), P("password", "1234") }));
        Assert.Equal(9, password.UserId);
        Assert.Equal("1234", password.Password);

        var image = Assert.IsType<BiometricEvent>(OnlineEvents.Parse(
            "new_biometric_image", new[] { P("width", "2"), P("height", "2"), P("variance", "80") }, new byte[] { 1, 2, 3, 4 }));
        Assert.Equal(OnlineIdentificationKind.BiometricImage, image.Kind);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, image.Data);
        Assert.Equal(2, image.Width);

        Assert.Equal(OnlineIdentificationKind.BiometricTemplate, OnlineEvents.Parse("new_biometric_template", Array.Empty<KeyValuePair<string, string>>())!.Kind);
    }

    [Fact]
    public void Missing_or_invalid_fields_are_null()
    {
        var e = Assert.IsType<UserIdentifiedEvent>(OnlineEvents.Parse("new_user_identified", new[] { P("user_id", "abc"), P("duress", "") }));

        Assert.Null(e.UserId);
        Assert.Null(e.Duress);
        Assert.Null(e.DeviceId);
        Assert.Null(e.IdentifierName);
    }

    [Fact]
    public void Unknown_endpoint_returns_null()
    {
        Assert.Null(OnlineEvents.Parse("device_is_alive.fcgi", Array.Empty<KeyValuePair<string, string>>()));
    }

    [Fact]
    public void Grant_response_matches_documentation_example()
    {
        var grant = OnlineAuthorization.Grant(6, "Neal Caffrey", DeviceAction.OpenDoor(1), DeviceAction.OpenDoor(2));
        grant.UserImage = false;
        grant.PortalId = 1;

        Assert.Equal(
            """{"result":{"event":7,"user_id":6,"user_name":"Neal Caffrey","user_image":false,"portal_id":1,"actions":[{"action":"door","parameters":"door=1"},{"action":"door","parameters":"door=2"}]}}""",
            grant.ToResponseJson());
    }

    [Fact]
    public void Action_factories_match_documentation()
    {
        Assert.Equal("id=65793, reason=1", DeviceAction.OpenSecBox().Parameters);
        Assert.Equal("allow=clockwise", DeviceAction.ReleaseTurnstile(TurnstileDirection.Clockwise).Parameters);
        Assert.Equal("""{"result":{"event":6,"message":"Fora do horário"}}""", OnlineAuthorization.Deny(message: "Fora do horário").ToResponseJson());
        Assert.Equal("""{"result":{"event":3}}""", OnlineAuthorization.NotIdentified().ToResponseJson());
    }

    [Fact]
    public async Task ConfigureOnlineMode_creates_server_and_enables_pro()
    {
        var device = LoggedDevice()
            .OnJson("load_objects.fcgi", """{"devices":[]}""")
            .OnJson("create_objects.fcgi", """{"ids":[473359]}""")
            .OnJson("set_configuration.fcgi", "{}");
        using var client = new AccessClient(Options, device.CreateClient());

        var serverId = await client.ConfigureOnlineModeAsync(new OnlineModeConfiguration
        {
            ServerAddress = "http://meuservidor.com.br/api",
            ServerName = "Meu servidor 1",
            MaxRequestAttempts = 3,
            RequestTimeout = TimeSpan.FromSeconds(3),
            ExtractTemplate = false,
        });

        Assert.Equal(473359, serverId);
        Assert.Equal(
            """{"object":"devices","where":{"devices":{"ip":"http://meuservidor.com.br/api"}}}""",
            device.RequestsTo("load_objects.fcgi").Single().Body);
        Assert.Equal(
            """{"object":"devices","values":[{"name":"Meu servidor 1","ip":"http://meuservidor.com.br/api","public_key":""}]}""",
            device.RequestsTo("create_objects.fcgi").Single().Body);
        Assert.Equal(
            """{"online_client":{"server_id":"473359","extract_template":"0","max_request_attempts":"3","request_timeout":"3000"},"general":{"online":"1","local_identification":"1","ihm_enterprise_mode":"0"}}""",
            device.RequestsTo("set_configuration.fcgi").Single().Body);
    }

    [Fact]
    public async Task ConfigureOnlineMode_reuses_existing_server_and_sets_enterprise()
    {
        var device = LoggedDevice()
            .OnJson("load_objects.fcgi", """{"devices":[{"id":12,"name":"x","ip":"10.0.0.1:5000"}]}""")
            .OnJson("set_configuration.fcgi", "{}");
        using var client = new AccessClient(Options, device.CreateClient());

        var serverId = await client.ConfigureOnlineModeAsync(new OnlineModeConfiguration
        {
            ServerAddress = "10.0.0.1:5000",
            Mode = OnlineMode.Enterprise,
            IsIhm = true,
        });

        Assert.Equal(12, serverId);
        Assert.Empty(device.RequestsTo("create_objects.fcgi"));
        using var body = JsonDocument.Parse(device.RequestsTo("set_configuration.fcgi").Single().Body);
        var general = body.RootElement.GetProperty("general");
        Assert.Equal("0", general.GetProperty("local_identification").GetString());
        Assert.Equal("1", general.GetProperty("ihm_enterprise_mode").GetString());
        Assert.Equal("12", body.RootElement.GetProperty("online_client").GetProperty("server_id").GetString());
    }

    [Theory]
    [InlineData("", 1)]
    [InlineData("http://x", 6)]
    public async Task ConfigureOnlineMode_validates_before_sending(string address, int timeoutSeconds)
    {
        var device = LoggedDevice();
        using var client = new AccessClient(Options, device.CreateClient());

        await Assert.ThrowsAnyAsync<ArgumentException>(() => client.ConfigureOnlineModeAsync(new OnlineModeConfiguration
        {
            ServerAddress = address,
            RequestTimeout = TimeSpan.FromSeconds(timeoutSeconds),
        }));
        Assert.Empty(device.Requests);
    }

    [Fact]
    public async Task Disable_and_remote_authorization()
    {
        var device = LoggedDevice()
            .OnJson("set_configuration.fcgi", "{}")
            .OnJson("remote_user_authorization.fcgi", "");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.DisableOnlineModeAsync();
        await client.AuthorizeRemotelyAsync(OnlineAuthorization.Grant(6, "Brian Cox", DeviceAction.OpenDoor(1)));

        Assert.Equal("""{"general":{"online":"0"}}""", device.RequestsTo("set_configuration.fcgi").Single().Body);
        Assert.Equal(
            """{"event":7,"user_id":6,"user_name":"Brian Cox","actions":[{"action":"door","parameters":"door=1"}]}""",
            device.RequestsTo("remote_user_authorization.fcgi").Single().Body);
    }

    [Fact]
    public void Parses_online_enroll_results_from_documentation_examples()
    {
        var card = OnlineEvents.ParseEnroll("/api/card_create.fcgi", """{"user_id": 1, "card_value": 132456789, "device_id": 935107}""")!;
        Assert.Equal(RemoteEnrollType.Card, card.EnrollType);
        Assert.Equal(132456789, card.Result.CardValue);
        Assert.Equal(935107, card.Result.DeviceId);
        Assert.True(card.Result.Success); // sem "success" nem "error"

        var face = OnlineEvents.ParseEnroll("face_create", """{"user_id": 1, "device_id": 935107, "face": {"image": "/9j/4A==", "width": 300, "height": 300}}""")!;
        Assert.Equal(RemoteEnrollType.Face, face.EnrollType);
        Assert.Equal(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, face.Result.Face!.Image);
        Assert.Equal(300, face.Result.Face.Width);

        var finger = OnlineEvents.ParseEnroll("fingerprint_create.fcgi", """
            {"user_id": 1, "finger_type": 0, "device_id": 935107,
             "fingerprints": [{"image": "AQID", "width": 300, "height": 200}, {"image": "BAUG", "width": 300, "height": 200}]}
            """)!;
        Assert.Equal(RemoteEnrollType.Biometry, finger.EnrollType);
        Assert.Equal(2, finger.Result.Fingerprints!.Count);
        Assert.Equal(0, finger.Result.FingerType);

        Assert.Equal("1324", OnlineEvents.ParseEnroll("pin_create", """{"user_id": 1, "pin_value": "1324", "device_id": 935107}""")!.Result.PinValue);
        Assert.Equal("1324", OnlineEvents.ParseEnroll("password_create", """{"user_id": 1, "password_value": "1324", "device_id": 935107}""")!.Result.PasswordValue);

        var template = OnlineEvents.ParseEnroll("template_create", """{"user_id": 1, "device_id": 2, "templates": ["abc"]}""")!;
        Assert.Equal("template_create", template.Endpoint);
        Assert.Equal(RemoteEnrollType.Biometry, template.EnrollType);
        Assert.Equal("abc", template.Result.Raw.GetProperty("templates")[0].GetString()); // formato não documentado: via Raw
    }

    [Fact]
    public void Enroll_parser_ignores_other_routes_and_rejects_invalid_json()
    {
        Assert.Null(OnlineEvents.ParseEnroll("new_card", "{}"));
        Assert.Throws<ControlIdException>(() => OnlineEvents.ParseEnroll("card_create", "{oops"));
    }

    [Fact]
    public void AccessLog_exposes_event_type()
    {
        Assert.Equal(AccessEventType.ExitButton, new AccessLog { Event = 11 }.EventType);
    }
}
