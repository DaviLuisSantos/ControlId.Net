using ControlId.Access;

namespace ControlId.Tests;

public class OsdpCalibrationTests
{
    private const string Scbk = "0102030405060708090a0b0c0d0e0f10";

    private static readonly ControlIdDeviceOptions Options = new()
    {
        BaseAddress = new Uri("http://10.0.0.5"),
        Login = "admin",
        Password = "admin",
    };

    private static FakeDeviceHandler LoggedDevice(string endpoint, string response) =>
        new FakeDeviceHandler().OnJson("login.fcgi", """{"session":"s"}""").OnJson(endpoint, response);

    [Fact]
    public async Task Installation_mode_is_a_get_and_parses_the_flag()
    {
        var device = LoggedDevice("get_osdp_installation_mode.fcgi", """{"installation_mode":true}""");
        using var client = new AccessClient(Options, device.CreateClient());

        Assert.True(await client.GetOsdpInstallationModeAsync());
        Assert.Equal(HttpMethod.Get, device.RequestsTo("get_osdp_installation_mode.fcgi").Single().Method);
    }

    [Fact]
    public async Task Scbk_get_sends_an_empty_body_and_parses_the_key()
    {
        var device = LoggedDevice("osdp_scbk.fcgi", $$"""{"scbk":"{{Scbk}}"}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var info = await client.GetOsdpScbkAsync();

        Assert.Equal("{}", device.RequestsTo("osdp_scbk.fcgi").Single().Body);
        Assert.Equal(Scbk, info.Scbk);
    }

    [Fact]
    public async Task Scbk_set_sends_the_key_and_validates_it_without_leaking_it()
    {
        var device = LoggedDevice("osdp_scbk.fcgi", "");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.SetOsdpScbkAsync(Scbk);

        Assert.Equal($$"""{"scbk":"{{Scbk}}"}""", device.RequestsTo("osdp_scbk.fcgi").Single().Body);
        await Assert.ThrowsAsync<ArgumentException>(() => client.SetOsdpScbkAsync(""));
        var odd = await Assert.ThrowsAsync<ArgumentException>(() => client.SetOsdpScbkAsync("abc"));
        var notHex = await Assert.ThrowsAsync<ArgumentException>(() => client.SetOsdpScbkAsync("zz11"));
        Assert.DoesNotContain("abc", odd.Message);
        Assert.DoesNotContain("zz11", notHex.Message);
    }

    [Fact]
    public void Scbk_does_not_appear_in_to_string()
    {
        var info = new OsdpScbkInfo { Scbk = Scbk };

        Assert.DoesNotContain(Scbk, info.ToString());
    }

    [Fact]
    public async Task Connection_status_sends_the_panel_and_maps_address_keys()
    {
        var device = LoggedDevice("osdp_get_connection_status.fcgi", """{"0":1,"1":0}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var status = await client.GetOsdpConnectionStatusAsync(1);

        Assert.Equal("""{"panel_id":1}""", device.RequestsTo("osdp_get_connection_status.fcgi").Single().Body);
        Assert.Equal(2, status.Count);
        Assert.Equal(1, status[0]);
        Assert.Equal(0, status[1]);
    }

    [Fact]
    public async Task Output_capabilities_maps_the_literal_nun_outputs_key()
    {
        var device = LoggedDevice("osdp_get_output_capabilities.fcgi", """{"nun_outputs":2,"compliance_level":1}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var capabilities = await client.GetOsdpOutputCapabilitiesAsync(0);

        Assert.Equal("""{"address":0}""", device.RequestsTo("osdp_get_output_capabilities.fcgi").Single().Body);
        Assert.Equal(2, capabilities.NumOutputs);
        Assert.Equal(1, capabilities.ComplianceLevel);
    }

    [Fact]
    public async Task Secure_key_sends_panel_address_and_hex_key()
    {
        var device = LoggedDevice("osdp_set_secure_key.fcgi", "");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.SetOsdpSecureKeyAsync(1, 0, Scbk);

        Assert.Equal($$"""{"panel_id":1,"address":0,"scbk_hex":"{{Scbk}}"}""", device.RequestsTo("osdp_set_secure_key.fcgi").Single().Body);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => client.SetOsdpSecureKeyAsync(1, 0, "xyz1"));
        Assert.DoesNotContain("xyz1", ex.Message);
    }

    [Fact]
    public async Task Secure_type_sends_the_numeric_mode_and_rejects_undefined_values()
    {
        var device = LoggedDevice("osdp_set_secure_type.fcgi", "");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.SetOsdpSecureTypeAsync(1, 0, OsdpSecureType.SecureOnly);

        Assert.Equal("""{"panel_id":1,"address":0,"secure_type":2}""", device.RequestsTo("osdp_set_secure_type.fcgi").Single().Body);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.SetOsdpSecureTypeAsync(1, 0, (OsdpSecureType)9));
    }

    [Fact]
    public async Task Refresh_posts_to_the_endpoint()
    {
        var device = LoggedDevice("osdp_refresh.fcgi", "");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.RefreshOsdpAsync();

        var request = device.RequestsTo("osdp_refresh.fcgi").Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("{}", request.Body);
    }

    [Fact]
    public async Task Start_calibration_sends_the_timeout_only_when_given_and_validates_the_range()
    {
        var device = LoggedDevice("remote_camera_calibration.fcgi", "");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.StartCameraCalibrationAsync(120);
        await client.StartCameraCalibrationAsync();

        var requests = device.RequestsTo("remote_camera_calibration.fcgi").ToList();
        Assert.Equal("""{"timeout":120}""", requests[0].Body);
        Assert.Equal("{}", requests[1].Body);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.StartCameraCalibrationAsync(9));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.StartCameraCalibrationAsync(601));
    }

    [Fact]
    public async Task Cancel_calibration_posts_to_the_endpoint()
    {
        var device = LoggedDevice("cancel_remote_camera_calibration.fcgi", "");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.CancelCameraCalibrationAsync();

        Assert.Equal(HttpMethod.Post, device.RequestsTo("cancel_remote_camera_calibration.fcgi").Single().Method);
    }

    [Fact]
    public async Task Calibration_status_parses_all_fields()
    {
        var device = LoggedDevice("camera_calibration_status.fcgi", """{"calibrated":true,"discrepancy_offset":0.0123,"running":false}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var status = await client.GetCameraCalibrationStatusAsync();

        Assert.Equal(HttpMethod.Post, device.RequestsTo("camera_calibration_status.fcgi").Single().Method);
        Assert.True(status.Calibrated);
        Assert.Equal(0.0123, status.DiscrepancyOffset, 6);
        Assert.False(status.Running);
    }

    [Fact]
    public async Task Wait_calibration_polls_until_it_stops_running()
    {
        var device = LoggedDevice("camera_calibration_status.fcgi", """{"calibrated":false,"discrepancy_offset":0,"running":true}""")
            .OnJson("camera_calibration_status.fcgi", """{"calibrated":false,"discrepancy_offset":0,"running":true}""")
            .OnJson("camera_calibration_status.fcgi", """{"calibrated":true,"discrepancy_offset":0.5,"running":false}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var status = await client.WaitCameraCalibrationAsync(TimeSpan.FromMilliseconds(1));

        Assert.True(status.Calibrated);
        Assert.False(status.Running);
        Assert.Equal(3, device.RequestsTo("camera_calibration_status.fcgi").Count());
    }

    [Fact]
    public async Task Wait_calibration_respects_cancellation_and_validates_the_interval()
    {
        var device = LoggedDevice("camera_calibration_status.fcgi", """{"calibrated":false,"discrepancy_offset":0,"running":true}""");
        using var client = new AccessClient(Options, device.CreateClient());
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.WaitCameraCalibrationAsync(TimeSpan.FromMilliseconds(5), cts.Token));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.WaitCameraCalibrationAsync(TimeSpan.Zero));
    }
}
