using System.Text.Json;
using ControlId.Access;

namespace ControlId.Tests;

public class HidOemProvisioningTests
{
    private const string Kek = "65547522234A0B72CE20D458D91C1C2514362085C3C79A13641575F07CD4E627";
    private const string NewKek = "1111111111111111111111111111111111111111111111111111111111111111";

    private static readonly ControlIdDeviceOptions Options = new()
    {
        BaseAddress = new Uri("http://10.0.0.5"),
        Login = "admin",
        Password = "admin",
    };

    private static FakeDeviceHandler LoggedDevice(string endpoint, string response) =>
        new FakeDeviceHandler().OnJson("login.fcgi", """{"session":"s"}""").OnJson(endpoint, response);

    private static JsonElement BodyOf(FakeDeviceHandler device, string endpoint) =>
        JsonDocument.Parse(device.RequestsTo(endpoint).Single().Body).RootElement;

    [Fact]
    public void Only_an_access_client_creates_the_provisioning_client()
    {
        using var client = new AccessClient(Options, new FakeDeviceHandler().CreateClient());

        Assert.NotNull(client.CreateProvisioningClient());
        Assert.Throws<ArgumentNullException>(() => AccessProvisioningClientExtensions.CreateProvisioningClient(null!));
    }

    [Fact]
    public async Task Default_kek_in_plain_mode_sends_key_kek_and_key_id()
    {
        var device = LoggedDevice("hid_inject_default_kek.fcgi", """{"success":true}""");
        using var client = new AccessClient(Options, device.CreateClient());

        Assert.True(await client.CreateProvisioningClient().InjectHidDefaultKekAsync(NewKek, Kek, "4445464B45593031"));

        var body = BodyOf(device, "hid_inject_default_kek.fcgi");
        Assert.Equal(NewKek, body.GetProperty("key").GetString());
        Assert.Equal(Kek, body.GetProperty("kek").GetString());
        Assert.Equal("4445464B45593031", body.GetProperty("key_id").GetString());
        Assert.Equal(HttpMethod.Post, device.RequestsTo("hid_inject_default_kek.fcgi").Single().Method);
    }

    [Fact]
    public async Task Default_kek_in_wrapped_mode_omits_kek()
    {
        var device = LoggedDevice("hid_inject_default_kek.fcgi", """{"success":true}""");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.CreateProvisioningClient().InjectHidDefaultKekAsync(NewKek);

        var body = BodyOf(device, "hid_inject_default_kek.fcgi");
        Assert.False(body.TryGetProperty("kek", out _));
        Assert.False(body.TryGetProperty("key_id", out _));
    }

    [Fact]
    public async Task Invalid_key_is_rejected_without_echoing_the_value()
    {
        using var client = new AccessClient(Options, new FakeDeviceHandler().CreateClient());
        var provisioning = client.CreateProvisioningClient();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => provisioning.InjectHidDefaultKekAsync("SECRETZZ"));
        Assert.DoesNotContain("SECRETZZ", ex.Message);
        await Assert.ThrowsAsync<ArgumentException>(() => provisioning.InjectHidDefaultKekAsync(NewKek, keyId: "0102"));
        await Assert.ThrowsAsync<ArgumentException>(() => provisioning.InjectHidMediaKeyAsync("030101", "03F001", "ABC"));
    }

    [Theory]
    [InlineData("hid_inject_custom_kek.fcgi")]
    [InlineData("hid_inject_media_key.fcgi")]
    public async Task Custom_kek_and_media_key_send_references(string endpoint)
    {
        var device = LoggedDevice(endpoint, """{"success":true}""");
        using var client = new AccessClient(Options, device.CreateClient());
        var provisioning = client.CreateProvisioningClient();

        var ok = endpoint == "hid_inject_custom_kek.fcgi"
            ? await provisioning.InjectHidCustomKekAsync("03F001", "03F000", NewKek, Kek)
            : await provisioning.InjectHidMediaKeyAsync("03F001", "03F000", NewKek, Kek);

        Assert.True(ok);
        var body = BodyOf(device, endpoint);
        Assert.Equal("03F001", body.GetProperty("target_ref").GetString());
        Assert.Equal("03F000", body.GetProperty("kek_ref").GetString());
        Assert.Equal(NewKek, body.GetProperty("key").GetString());
        Assert.Equal(Kek, body.GetProperty("kek").GetString());
    }

    [Fact]
    public async Task Key_info_parses_type_and_id()
    {
        var device = LoggedDevice("hid_get_key_info.fcgi", """{"key_type":"KEK","key_id":"4445464B45593031"}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var info = await client.CreateProvisioningClient().GetHidKeyInfoAsync("03F000");

        Assert.True(info.IsKek);
        Assert.False(info.IsMediaKey);
        Assert.Equal("4445464B45593031", info.KeyId);
        Assert.Equal("03F000", BodyOf(device, "hid_get_key_info.fcgi").GetProperty("target_ref").GetString());
    }

    [Fact]
    public async Task Set_oid_sends_the_json_type_of_each_overload()
    {
        var device = LoggedDevice("hid_set_oid.fcgi", """{"success":true}""");
        using var client = new AccessClient(Options, device.CreateClient());
        var provisioning = client.CreateProvisioningClient();

        await provisioning.SetHidOidAsync("1.1.1.2.3.5", true);
        await provisioning.SetHidOidAsync("1.1.1.2.3.5", 42);
        await provisioning.SetHidOidAsync("1.1.1.2.3.1", new byte[] { 0xA2, 0x61, 0x0F });

        var values = device.RequestsTo("hid_set_oid.fcgi")
            .Select(r => JsonDocument.Parse(r.Body).RootElement.GetProperty("value"))
            .ToList();
        Assert.Equal(JsonValueKind.True, values[0].ValueKind);
        Assert.Equal(42, values[1].GetInt32());
        Assert.Equal("A2610F", values[2].GetString());
    }

    [Theory]
    [InlineData("1.1.1.2.3")]
    [InlineData("1.1.1.2.3.x")]
    [InlineData("")]
    public async Task Malformed_oid_is_rejected(string oid)
    {
        using var client = new AccessClient(Options, new FakeDeviceHandler().CreateClient());

        await Assert.ThrowsAsync<ArgumentException>(() => client.CreateProvisioningClient().GetHidOidAsync(oid));
    }

    [Theory]
    [InlineData("""{"value":true}""", HidOidValueKind.Boolean, "true")]
    [InlineData("""{"value":7}""", HidOidValueKind.Integer, "7")]
    [InlineData("""{"value":"A26130"}""", HidOidValueKind.Bytes, "A26130")]
    public async Task Get_oid_infers_the_value_kind(string response, HidOidValueKind kind, string text)
    {
        var device = LoggedDevice("hid_get_oid.fcgi", response);
        using var client = new AccessClient(Options, device.CreateClient());

        var value = await client.CreateProvisioningClient().GetHidOidAsync("1.1.1.2.3.1");

        Assert.Equal(kind, value.Kind);
        Assert.Equal(text, value.ToString());
        if (kind == HidOidValueKind.Bytes)
        {
            Assert.Equal(new byte[] { 0xA2, 0x61, 0x30 }, value.ToBytes());
        }
    }

    [Fact]
    public async Task Reset_requires_confirmation_and_sends_start()
    {
        var device = LoggedDevice("hid_reset_config.fcgi", """{"resetting":true,"status":"running"}""");
        using var client = new AccessClient(Options, device.CreateClient());
        var provisioning = client.CreateProvisioningClient();

        await Assert.ThrowsAsync<ArgumentException>(() => provisioning.ResetHidConfigAsync(default));
        var status = await provisioning.ResetHidConfigAsync(DataLossConfirmation.Confirmed);

        Assert.True(status.IsResetting);
        Assert.False(status.IsDone);
        Assert.True(BodyOf(device, "hid_reset_config.fcgi").GetProperty("start").GetBoolean());
    }

    [Fact]
    public async Task Restore_sends_the_orco_file_as_octet_stream()
    {
        var device = LoggedDevice("hid_reset_config.fcgi", """{"resetting":true,"status":"running"}""");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.CreateProvisioningClient().RestoreHidConfigAsync(DataLossConfirmation.Confirmed, new byte[] { 1, 2, 3 });

        Assert.Equal("application/octet-stream", device.RequestsTo("hid_reset_config.fcgi").Single().ContentType);
    }

    [Fact]
    public async Task Wait_reset_polls_until_done_and_honors_cancellation()
    {
        var device = LoggedDevice("hid_reset_config.fcgi", """{"resetting":false,"status":"done"}""");
        using var client = new AccessClient(Options, device.CreateClient());
        var provisioning = client.CreateProvisioningClient();

        Assert.True((await provisioning.WaitHidConfigResetAsync(TimeSpan.FromMilliseconds(1))).IsDone);
        Assert.False(BodyOrEmpty(device).TryGetProperty("start", out _));

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provisioning.WaitHidConfigResetAsync(cancellationToken: cts.Token));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => provisioning.WaitHidConfigResetAsync(TimeSpan.Zero));
    }

    private static JsonElement BodyOrEmpty(FakeDeviceHandler device)
    {
        var body = device.RequestsTo("hid_reset_config.fcgi").First().Body;
        return JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body).RootElement;
    }

    [Fact]
    public async Task Oem_code_is_a_get()
    {
        var device = LoggedDevice("get_oem_code.fcgi", """{"oem_code":"CID"}""");
        using var client = new AccessClient(Options, device.CreateClient());

        Assert.Equal("CID", await client.CreateProvisioningClient().GetOemCodeAsync());
        Assert.Equal(HttpMethod.Get, device.RequestsTo("get_oem_code.fcgi").Single().Method);
    }

    [Fact]
    public async Task Set_oem_code_sends_code_and_license()
    {
        var device = LoggedDevice("set_oem_code.fcgi", "");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.CreateProvisioningClient().SetOemCodeAsync("MYOEM", "ABCDE12345");

        var body = BodyOf(device, "set_oem_code.fcgi");
        Assert.Equal("MYOEM", body.GetProperty("oem_code").GetString());
        Assert.Equal("ABCDE12345", body.GetProperty("license").GetString());
    }

    [Fact]
    public async Task Downgrade_versions_are_parsed()
    {
        var device = LoggedDevice("get_oem_downgrade_approved_versions.fcgi", """{"versions":["7.12.0","7.13.2"]}""");
        using var client = new AccessClient(Options, device.CreateClient());

        Assert.Equal(new[] { "7.12.0", "7.13.2" }, await client.CreateProvisioningClient().GetOemDowngradeApprovedVersionsAsync());
        Assert.Equal(HttpMethod.Post, device.RequestsTo("get_oem_downgrade_approved_versions.fcgi").Single().Method);
    }

    [Fact]
    public async Task Approved_firmware_is_validated_and_clear_sends_empty_string()
    {
        var device = LoggedDevice("set_oem_approved_fw.fcgi", "{}");
        using var client = new AccessClient(Options, device.CreateClient());
        var provisioning = client.CreateProvisioningClient();

        await Assert.ThrowsAsync<ArgumentException>(() => provisioning.SetOemApprovedFirmwareAsync("MYOEM", "L", "7.14"));
        await provisioning.SetOemApprovedFirmwareAsync("MYOEM", "ABCDE12345", "7.14.0");
        await provisioning.ClearOemApprovedFirmwareAsync("MYOEM", "ABCDE12345");

        var sent = device.RequestsTo("set_oem_approved_fw.fcgi")
            .Select(r => JsonDocument.Parse(r.Body).RootElement.GetProperty("approved_fw").GetString())
            .ToList();
        Assert.Equal(new[] { "7.14.0", "" }, sent);
    }
}
