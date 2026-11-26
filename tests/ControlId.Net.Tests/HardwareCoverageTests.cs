using System.Text;
using ControlId.Access;

namespace ControlId.Tests;

public class HardwareCoverageTests
{
    private static readonly ControlIdDeviceOptions Options = new()
    {
        BaseAddress = new Uri("http://10.0.0.5"),
        Login = "admin",
        Password = "admin",
    };

    private static FakeDeviceHandler LoggedDevice(string endpoint, string response) =>
        new FakeDeviceHandler().OnJson("login.fcgi", """{"session":"s"}""").OnJson(endpoint, response);

    private static string ValidTemplate() => Convert.ToBase64String(new byte[1024]);

    [Fact]
    public async Task Face_template_match_sends_user_id_only_when_given_and_parses_scores()
    {
        const string response = """{"match":true,"info":{"match_user_id":42,"match_score":850,"match_similarity":4120}}""";
        var template = ValidTemplate();

        var device = LoggedDevice("face_template_match.fcgi", response).OnJson("face_template_match.fcgi", """{"match":false}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var one = await client.MatchFaceTemplateAsync(template, 42);
        var many = await client.MatchFaceTemplateAsync(template);

        var requests = device.RequestsTo("face_template_match.fcgi").ToList();
        Assert.Equal($$"""{"face_template":"{{template}}","user_id":42}""", requests[0].Body);
        Assert.Equal($$"""{"face_template":"{{template}}"}""", requests[1].Body);
        Assert.True(one.Match);
        Assert.Equal(42, one.Info!.MatchUserId);
        Assert.Equal(850, one.Info.MatchScore);
        Assert.Equal(4120, one.Info.MatchSimilarity);
        Assert.False(many.Match);
        Assert.Null(many.Info);
    }

    [Fact]
    public async Task Face_template_match_validates_the_template()
    {
        using var client = new AccessClient(Options, LoggedDevice("face_template_match.fcgi", "{}").CreateClient());

        await Assert.ThrowsAsync<ArgumentException>(() => client.MatchFaceTemplateAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => client.MatchFaceTemplateAsync("not base64!"));
        await Assert.ThrowsAsync<ArgumentException>(() => client.MatchFaceTemplateAsync(Convert.ToBase64String(new byte[10])));
    }

    [Fact]
    public async Task Face_template_count_distinct_is_a_get_and_returns_count()
    {
        var device = LoggedDevice("face_template_count_distinct.fcgi", """{"count":128}""");
        using var client = new AccessClient(Options, device.CreateClient());

        Assert.Equal(128, await client.CountFaceTemplatesAsync());
        Assert.Equal(HttpMethod.Get, device.RequestsTo("face_template_count_distinct.fcgi").Single().Method);
    }

    [Fact]
    public async Task Template_match_concatenates_bodies_and_sends_sizes_in_the_query()
    {
        var device = LoggedDevice("template_match.fcgi", """{"template":"YmVzdA=="}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var best = await client.MatchFingerprintTemplatesAsync(new[]
        {
            Encoding.ASCII.GetBytes("AAAA"),
            Encoding.ASCII.GetBytes("BB"),
            Encoding.ASCII.GetBytes("CCC"),
        });

        var request = device.RequestsTo("template_match.fcgi").Single();
        Assert.Equal("YmVzdA==", best);
        Assert.Equal("AAAABBCCC", request.Body);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("application/octet-stream", request.ContentType);
        Assert.Contains("temp_num=3&size0=4&size1=2&size2=3", request.Uri.Query);
    }

    [Fact]
    public async Task Template_match_rejects_empty_input()
    {
        using var client = new AccessClient(Options, LoggedDevice("template_match.fcgi", "{}").CreateClient());

        await Assert.ThrowsAsync<ArgumentException>(() => client.MatchFingerprintTemplatesAsync(Array.Empty<byte[]>()));
        await Assert.ThrowsAsync<ArgumentException>(() => client.MatchFingerprintTemplatesAsync(new[] { new byte[] { 1 }, Array.Empty<byte>() }));
    }

    [Fact]
    public async Task Io_state_posts_without_body_and_parses_gpios_relays_and_secboxes()
    {
        const string response = """
            {"gpios":[
              {"index":0,"role":"tamper","enabled":true,"available":true,"in":true,"value":1,"idle":1,"active":false,"notify":true,"notified":0},
              {"index":1,"role":"gpio_ext","ext_id":1,"mode":7,"activation_mode":1,"debounce_ms":50,"enabled":true,"available":true,"in":true,"value":0,"idle":0,"active":false,"notify":true,"notified":3},
              {"index":2,"role":"generic","enabled":false,"available":false}],
             "relays":[{"id":1,"gpio":4,"enabled":true,"timeout_ms":3000,"auto_close":true,"output_mode":0,"available":true,"active":true}],
             "sec_boxes":[{"id":9,"serial":"SB1","version":"1.2","enabled":true,"relay":true,"rex":false,"rex_active":false,"door_sensor":true,"door_sensor_enabled":true,"door_sensor_idle":1,"door_sensor_active":false,"open":true}]}
            """;
        var device = LoggedDevice("io_state.fcgi", response);
        using var client = new AccessClient(Options, device.CreateClient());

        var state = await client.GetIoStateAsync();

        var request = device.RequestsTo("io_state.fcgi").Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(3, state.Gpios.Count);
        Assert.Equal("tamper", state.Gpios[0].Role);
        Assert.Null(state.Gpios[0].ExtId);
        Assert.Equal(1, state.Gpios[1].ExtId);
        Assert.Equal(50, state.Gpios[1].DebounceMs);
        Assert.Equal(3, state.Gpios[1].Notified);
        Assert.False(state.Gpios[2].Available);
        Assert.Null(state.Gpios[2].Value);
        Assert.Equal(3000, state.Relays.Single().TimeoutMs);
        Assert.True(state.Relays.Single().Active);
        Assert.Equal("SB1", state.SecBoxes.Single().Serial);
        Assert.True(state.SecBoxes.Single().Open);
    }

    [Fact]
    public async Task Is_identifying_reads_the_flag()
    {
        var device = LoggedDevice("is_identifying.fcgi", """{"is_identifying":true}""");
        using var client = new AccessClient(Options, device.CreateClient());

        Assert.True(await client.IsIdentifyingAsync());
        Assert.Equal(HttpMethod.Get, device.RequestsTo("is_identifying.fcgi").Single().Method);
    }

    [Fact]
    public async Task Turnstile_counters_are_parsed()
    {
        var device = LoggedDevice(
            "get_catra_info.fcgi",
            """{"left_turns":1,"right_turns":2,"entrance_turns":3,"exit_turns":4,"total_turns":5}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var counters = await client.GetTurnstileCountersAsync();

        Assert.Equal(HttpMethod.Get, device.RequestsTo("get_catra_info.fcgi").Single().Method);
        Assert.Equal((1, 2, 3, 4, 5), (counters.LeftTurns, counters.RightTurns, counters.EntranceTurns, counters.ExitTurns, counters.TotalTurns));
    }

    [Fact]
    public async Task Last_snapshot_decodes_base64_to_bytes()
    {
        var device = LoggedDevice("get_last_snapshot.fcgi", $$"""{"image_base64":"{{Convert.ToBase64String(new byte[] { 0xFF, 0xD8, 0xFF })}}"}""");
        using var client = new AccessClient(Options, device.CreateClient());

        Assert.Equal(new byte[] { 0xFF, 0xD8, 0xFF }, await client.GetLastSnapshotAsync());
        Assert.Equal(HttpMethod.Get, device.RequestsTo("get_last_snapshot.fcgi").Single().Method);
    }

    [Fact]
    public async Task Rfid_module_returns_the_module_name()
    {
        var device = LoggedDevice("device_rfid_module.fcgi", """{"module":"HID"}""");
        using var client = new AccessClient(Options, device.CreateClient());

        Assert.Equal("HID", await client.GetRfidModuleAsync());
        Assert.Equal(HttpMethod.Get, device.RequestsTo("device_rfid_module.fcgi").Single().Method);
    }

    [Fact]
    public async Task Hid_last_card_is_parsed()
    {
        var device = LoggedDevice(
            "get_hid_last_card.fcgi",
            """{"csn":"A1B2C3D4","pac":"00123456","type":"hid","bits":26,"custom":"0x1a2b3c4d"}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var card = await client.GetHidLastCardAsync();

        Assert.Equal(HttpMethod.Get, device.RequestsTo("get_hid_last_card.fcgi").Single().Method);
        Assert.Equal("A1B2C3D4", card.Csn);
        Assert.Equal("00123456", card.Pac);
        Assert.Equal("hid", card.Type);
        Assert.Equal(26, card.Bits);
        Assert.Equal("0x1a2b3c4d", card.Custom);
    }

    [Fact]
    public async Task Wiegand_card_read_is_parsed()
    {
        var device = LoggedDevice(
            "get_wiegand_card_read.fcgi",
            """{"card_read_value":"0A1B2C3D","card_read_num_bits":26,"facility_code":"10","card_number":"12345","parities":0,"last_read_time":"2024-01-15 10:30:00"}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var read = await client.GetLastWiegandReadAsync();

        Assert.Equal(HttpMethod.Get, device.RequestsTo("get_wiegand_card_read.fcgi").Single().Method);
        Assert.Equal("0A1B2C3D", read.CardReadValue);
        Assert.Equal(26, read.CardReadNumBits);
        Assert.Equal("10", read.FacilityCode);
        Assert.Equal("12345", read.CardNumber);
        Assert.Equal(0, read.Parities);
        Assert.Equal("2024-01-15 10:30:00", read.LastReadTime);
    }
}
