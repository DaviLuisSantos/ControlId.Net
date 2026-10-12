using System.Text;
using ControlId.Access;
using ControlId.Access.Push;

namespace ControlId.Tests;

public class NetworkPhase5Tests
{
    private static readonly ControlIdDeviceOptions Options = new() { BaseAddress = new Uri("http://10.0.0.5") };

    private static FakeDeviceHandler LoggedDevice() => new FakeDeviceHandler().OnJson("login.fcgi", """{"session":"s"}""");

    private static FakeDeviceHandler OnGetJson(FakeDeviceHandler device, string endpoint, string json) =>
        device.OnBytes(endpoint, Encoding.UTF8.GetBytes(json), "application/json");

    [Fact]
    public async Task Diagnostics_match_documentation()
    {
        var device = LoggedDevice()
            .OnJson("ping_test.fcgi", """{"output":"PING 8.8.8.8 (8.8.8.8): 56 data bytes\n"}""")
            .OnJson("nslookup_test.fcgi", """{"output_ns":";; connection timed out\n"}""");
        OnGetJson(device, "connection_test.fcgi", """{"connection_result":false}""");
        using var client = new AccessClient(Options, device.CreateClient());

        Assert.False(await client.TestInternetConnectionAsync());
        Assert.StartsWith("PING 8.8.8.8", await client.PingAsync("www.google.com"));
        Assert.StartsWith(";; connection timed out", await client.NsLookupAsync("www.google.com", "8.8.8.8"));

        Assert.Equal(HttpMethod.Get, device.RequestsTo("connection_test.fcgi").Single().Method);
        Assert.Equal("""{"dst":"www.google.com"}""", device.RequestsTo("ping_test.fcgi").Single().Body);
        Assert.Equal("""{"dst":"www.google.com","dns":"8.8.8.8"}""", device.RequestsTo("nslookup_test.fcgi").Single().Body);
    }

    [Fact]
    public async Task Interlock_always_sends_the_three_options()
    {
        var device = LoggedDevice().OnJson("set_network_interlock.fcgi", "{}");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.SetNetworkInterlockAsync(enabled: true, bypassForApi: true, bypassForExitButton: false);

        Assert.Equal(
            """{"interlock_enabled":1,"api_bypass_enabled":1,"rex_bypass_enabled":0}""",
            device.RequestsTo("set_network_interlock.fcgi").Single().Body);
    }

    [Fact]
    public async Task Biometry_validation()
    {
        var device = LoggedDevice().OnJson("is_valid_biometry.fcgi", """{"isValid":true}""");
        using var client = new AccessClient(Options, device.CreateClient());

        Assert.True(await client.IsValidBiometryAsync("SUNSUzIx"));
        Assert.Equal("""{"biometry":"SUNSUzIx"}""", device.RequestsTo("is_valid_biometry.fcgi").Single().Body);
    }

    [Fact]
    public async Task Vpn_endpoints()
    {
        var device = LoggedDevice()
            .OnJson("set_vpn_information.fcgi", "")
            .OnJson("set_vpn_file.fcgi", "")
            .OnBytes("get_vpn_file.fcgi", new byte[] { 0x50, 0x4B }, "application/zip");
        OnGetJson(device, "get_vpn_information.fcgi", """{"enabled":true,"login_enabled":false,"login":"Admin","password":true}""");
        OnGetJson(device, "get_vpn_status.fcgi", """{"status":"0"}""");
        OnGetJson(device, "has_vpn_file.fcgi", """{"has_file":true}""");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.SetVpnAsync(new VpnSettings { Enabled = true, LoginEnabled = true, Login = "Admin", Password = "Admin" });
        await client.UploadVpnFileAsync(new byte[] { 1 }, VpnFileType.Zip);
        var info = await client.GetVpnInformationAsync();

        Assert.Equal("""{"enabled":true,"login_enabled":true,"login":"Admin","password":"Admin"}""", device.RequestsTo("set_vpn_information.fcgi").Single().Body);
        Assert.Equal("?session=s&file_type=zip", device.RequestsTo("set_vpn_file.fcgi").Single().Uri.Query);
        Assert.True(info.HasPassword);
        Assert.Equal(VpnStatus.Connected, await client.GetVpnStatusAsync()); // a documentação devolve o status como texto ("0")
        Assert.True(await client.HasVpnFileAsync());
        Assert.Equal(new byte[] { 0x50, 0x4B }, await client.GetVpnExampleFilesAsync());
    }

    [Fact]
    public async Task Ssl_8021x_and_osdp()
    {
        var device = LoggedDevice()
            .OnJson("ssl_certificate_change.fcgi", "")
            .OnJson("configure_802_1X.fcgi", "")
            .OnJson("set_osdp_installation_mode.fcgi", "");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.UploadSslCertificateAsync(Encoding.ASCII.GetBytes("-----BEGIN"));
        await client.Configure8021XAsync(true, "testing", "password");
        await client.SetOsdpInstallationModeAsync(true);

        Assert.Equal("application/octet-stream", device.RequestsTo("ssl_certificate_change.fcgi").Single().ContentType);
        Assert.Equal("""{"enabled":true,"inner_auth":0,"login":"testing","password":"password"}""", device.RequestsTo("configure_802_1X.fcgi").Single().Body);
        Assert.Equal("""{"enabled":true}""", device.RequestsTo("set_osdp_installation_mode.fcgi").Single().Body);
    }

    [Fact]
    public async Task IdCloud_and_licenses()
    {
        var device = LoggedDevice()
            .OnJson("change_idcloud_code.fcgi", "")
            .OnJson("upgrade_ten_thousand_face_templates.fcgi", "")
            .OnJson("upgrade_hundred_thousand_face_templates.fcgi", "")
            .OnJson("idflex_upgrade_enterprise.fcgi", "");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.ChangeIdCloudCodeAsync();
        await client.ApplyFaceLicenseAsync(FaceLicense.Pro10k, "ABCDE12345");
        await client.ApplyFaceLicenseAsync(FaceLicense.Pro100k, "XYZ");
        await client.UpgradeToEnterpriseAsync("KEY");

        Assert.Equal(HttpMethod.Get, device.RequestsTo("change_idcloud_code.fcgi").Single().Method);
        Assert.Equal("""{"password":"ABCDE12345"}""", device.RequestsTo("upgrade_ten_thousand_face_templates.fcgi").Single().Body);
        Assert.Single(device.RequestsTo("upgrade_hundred_thousand_face_templates.fcgi"));
        Assert.Equal("""{"password":"KEY"}""", device.RequestsTo("idflex_upgrade_enterprise.fcgi").Single().Body);
        await Assert.ThrowsAsync<ArgumentException>(() => client.ApplyFaceLicenseAsync(FaceLicense.Pro15k, ""));
    }

    [Fact]
    public async Task IdCloud_push_preset_matches_documentation()
    {
        var device = LoggedDevice().OnJson("set_configuration.fcgi", "{}");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.ConfigurePushAsync(PushConfiguration.IdCloud());

        Assert.Equal(
            """{"push_server":{"push_remote_address":"https://push.idsecure.com.br/api","push_request_timeout":"30000","push_request_period":"5"}}""",
            device.RequestsTo("set_configuration.fcgi").Single().Body);
    }
}
