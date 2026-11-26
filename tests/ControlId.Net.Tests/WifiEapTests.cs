using System.Net;
using System.Text;
using ControlId.Access;

namespace ControlId.Tests;

public class WifiEapTests
{
    private static readonly ControlIdDeviceOptions Options = new() { BaseAddress = new Uri("http://10.0.0.5") };

    private static FakeDeviceHandler LoggedDevice() => new FakeDeviceHandler().OnJson("login.fcgi", """{"session":"s"}""");

    [Theory]
    [InlineData(WifiSource.Internal, "internal")]
    [InlineData(WifiSource.Usb, "usb")]
    [InlineData(WifiSource.Disabled, "disabled")]
    public async Task Set_wifi_source_sends_the_adapter(WifiSource source, string expected)
    {
        var device = LoggedDevice().OnJson("set_wifi_source.fcgi", "");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.SetWifiSourceAsync(source);

        var request = device.RequestsTo("set_wifi_source.fcgi").Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("{\"source\":\"" + expected + "\"}", request.Body);
    }

    [Fact]
    public async Task Scan_parses_the_networks()
    {
        var device = LoggedDevice().OnJson(
            "wifi_scan.fcgi",
            """{"networks":[{"ssid":"Corporativa","bssid":"a1:b2:c3:d4:e5:f6","freq":5180,"signal":-45},{"ssid":"Visitantes","bssid":"a1:b2:c3:d4:e5:f7","freq":2412,"signal":-72}]}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var networks = await client.ScanWifiAsync();

        Assert.Equal(HttpMethod.Post, device.RequestsTo("wifi_scan.fcgi").Single().Method);
        Assert.Equal(2, networks.Count);
        Assert.Equal("Corporativa", networks[0].Ssid);
        Assert.Equal("a1:b2:c3:d4:e5:f6", networks[0].Bssid);
        Assert.Equal(5180, networks[0].Frequency);
        Assert.Equal(-72, networks[1].Signal);
    }

    [Fact]
    public async Task Scan_without_networks_returns_an_empty_list()
    {
        var device = LoggedDevice().OnJson("wifi_scan.fcgi", "{}");
        using var client = new AccessClient(Options, device.CreateClient());

        Assert.Empty(await client.ScanWifiAsync());
    }

    [Theory]
    [InlineData(0, WifiConnectionResult.Connected)]
    [InlineData(1, WifiConnectionResult.WrongPassword)]
    [InlineData(2, WifiConnectionResult.NetworkNotFound)]
    [InlineData(3, WifiConnectionResult.Timeout)]
    [InlineData(4, WifiConnectionResult.UnknownError)]
    [InlineData(99, WifiConnectionResult.Unknown)]
    public async Task Set_wifi_settings_parses_the_result(int code, WifiConnectionResult expected)
    {
        var device = LoggedDevice().OnJson("set_wifi_settings.fcgi", "{\"result\":" + code + "}");
        using var client = new AccessClient(Options, device.CreateClient());

        Assert.Equal(expected, await client.SetWifiSettingsAsync("Corporativa", "senha123"));
    }

    [Fact]
    public async Task Set_wifi_settings_sends_ssid_psk_and_optional_bssid()
    {
        var device = LoggedDevice().OnJson("set_wifi_settings.fcgi", """{"result":0}""");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.SetWifiSettingsAsync("Corporativa", "senha123");
        await client.SetWifiSettingsAsync("Aberta", string.Empty, "a1:b2:c3:d4:e5:f6");

        var bodies = device.RequestsTo("set_wifi_settings.fcgi").Select(r => r.Body).ToList();
        Assert.Equal("""{"ssid":"Corporativa","psk":"senha123"}""", bodies[0]);
        Assert.Equal("""{"ssid":"Aberta","psk":"","bssid":"a1:b2:c3:d4:e5:f6"}""", bodies[1]);
    }

    [Fact]
    public async Task Set_wifi_settings_validates_arguments_without_leaking_the_psk()
    {
        var device = LoggedDevice().OnJson("set_wifi_settings.fcgi", """{"result":0}""");
        using var client = new AccessClient(Options, device.CreateClient());

        await Assert.ThrowsAsync<ArgumentException>(() => client.SetWifiSettingsAsync(" ", "senha-secreta"));
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => client.SetWifiSettingsAsync("Rede", null!));
        Assert.DoesNotContain("senha-secreta", ex.ToString());
        Assert.Empty(device.RequestsTo("set_wifi_settings.fcgi"));
    }

    [Fact]
    public async Task Get_wifi_data_parses_the_state()
    {
        var device = LoggedDevice().OnJson(
            "get_wifi_data.fcgi",
            """{"available_adapters":["internal"],"ssid":"Corporativa","psk":"senha123","priority":"wired","status":"connected","connected_ssid":"Corporativa","source":"internal"}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var data = await client.GetWifiDataAsync();

        Assert.Equal(HttpMethod.Post, device.RequestsTo("get_wifi_data.fcgi").Single().Method);
        Assert.Equal(new[] { "internal" }, data.AvailableAdapters);
        Assert.Equal("Corporativa", data.Ssid);
        Assert.Equal("senha123", data.Psk);
        Assert.Equal("Corporativa", data.ConnectedSsid);
        Assert.True(data.IsConnected);
        Assert.Equal(WifiSource.Internal, data.SourceKind);
        Assert.Equal(NetworkInterfaceKind.Wired, data.PriorityInterface);
    }

    [Fact]
    public async Task Get_wifi_data_disconnected_and_disabled()
    {
        var device = LoggedDevice().OnJson(
            "get_wifi_data.fcgi",
            """{"available_adapters":[],"ssid":"","psk":"","priority":"wireless","status":"not_connected","connected_ssid":"","source":"disabled"}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var data = await client.GetWifiDataAsync();

        Assert.False(data.IsConnected);
        Assert.Equal(WifiSource.Disabled, data.SourceKind);
        Assert.Equal(NetworkInterfaceKind.Wireless, data.PriorityInterface);
        Assert.Empty(data.AvailableAdapters);
    }

    [Fact]
    public void Wifi_data_to_string_masks_the_psk()
    {
        var data = new WifiData { Ssid = "Corporativa", Psk = "senha-super-secreta", Status = "connected" };

        var text = data.ToString();

        Assert.DoesNotContain("senha-super-secreta", text);
        Assert.Contains("Psk = ***", text);
        Assert.Contains("Corporativa", text);
    }

    [Fact]
    public async Task Forget_wifi_network_requires_confirmation()
    {
        var device = LoggedDevice().OnJson("forget_wifi_connected_network.fcgi", "");
        using var client = new AccessClient(Options, device.CreateClient());

        await Assert.ThrowsAsync<ArgumentException>(() => client.ForgetWifiConnectedNetworkAsync(DataLossConfirmation.None));
        Assert.Empty(device.RequestsTo("forget_wifi_connected_network.fcgi"));

        await client.ForgetWifiConnectedNetworkAsync(DataLossConfirmation.Confirmed);

        var request = device.RequestsTo("forget_wifi_connected_network.fcgi").Single();
        Assert.Equal(HttpMethod.Post, request.Method);
    }

    [Theory]
    [InlineData(NetworkInterfaceKind.Wired, "1", true)]
    [InlineData(NetworkInterfaceKind.Wireless, "2", false)]
    public async Task Check_ip_conflict_sends_interface_and_ip(NetworkInterfaceKind networkInterface, string code, bool conflict)
    {
        var device = LoggedDevice().OnJson("check_ip_conflict.fcgi", "{\"conflict\":" + (conflict ? "true" : "false") + "}");
        using var client = new AccessClient(Options, device.CreateClient());

        Assert.Equal(conflict, await client.CheckIpConflictAsync(networkInterface, "192.168.0.129"));

        Assert.Equal("{\"interface\":\"" + code + "\",\"ip\":\"192.168.0.129\"}", device.RequestsTo("check_ip_conflict.fcgi").Single().Body);
        await Assert.ThrowsAsync<ArgumentException>(() => client.CheckIpConflictAsync(networkInterface, " "));
    }

    [Theory]
    [InlineData("eap_802_1X_certificate_change.fcgi")]
    [InlineData("eap_802_1X_certificate_change_wifi.fcgi")]
    [InlineData("eap_tls_802_1X_certificate_change.fcgi")]
    [InlineData("eap_tls_802_1X_private_key_change.fcgi")]
    public async Task Eap_uploads_send_octet_stream_in_the_body(string endpoint)
    {
        var device = LoggedDevice().OnJson(endpoint, "");
        using var client = new AccessClient(Options, device.CreateClient());
        var bytes = Encoding.ASCII.GetBytes("-----BEGIN CERTIFICATE-----");

        switch (endpoint)
        {
            case "eap_802_1X_certificate_change.fcgi":
                await client.UploadEapCaCertificateAsync(bytes);
                await Assert.ThrowsAsync<ArgumentException>(() => client.UploadEapCaCertificateAsync(Array.Empty<byte>()));
                break;
            case "eap_802_1X_certificate_change_wifi.fcgi":
                await client.UploadEapCaCertificateWifiAsync(bytes);
                await Assert.ThrowsAsync<ArgumentException>(() => client.UploadEapCaCertificateWifiAsync(null!));
                break;
            case "eap_tls_802_1X_certificate_change.fcgi":
                await client.UploadEapTlsClientCertificateAsync(bytes);
                await Assert.ThrowsAsync<ArgumentException>(() => client.UploadEapTlsClientCertificateAsync(Array.Empty<byte>()));
                break;
            default:
                await client.UploadEapTlsPrivateKeyAsync(bytes);
                await Assert.ThrowsAsync<ArgumentException>(() => client.UploadEapTlsPrivateKeyAsync(Array.Empty<byte>()));
                break;
        }

        var request = device.RequestsTo(endpoint).Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("application/octet-stream", request.ContentType);
        Assert.Equal("-----BEGIN CERTIFICATE-----", request.Body);
        Assert.Equal("?session=s", request.Uri.Query);
    }

    [Fact]
    public async Task Eap_tls_csr_sends_the_password_and_returns_the_file()
    {
        var device = LoggedDevice().OnBytes("get_eap_tls_csr_file.fcgi", Encoding.ASCII.GetBytes("-----BEGIN CERTIFICATE REQUEST-----"), "application/x-pem-file");
        using var client = new AccessClient(Options, device.CreateClient());

        var first = await client.GetEapTlsCsrAsync("minhasenha");
        var second = await client.GetEapTlsCsrAsync();

        Assert.Equal("-----BEGIN CERTIFICATE REQUEST-----", first);
        Assert.Equal(first, second);
        var bodies = device.RequestsTo("get_eap_tls_csr_file.fcgi").Select(r => r.Body).ToList();
        Assert.Equal("""{"password":"minhasenha"}""", bodies[0]);
        Assert.Equal("{}", bodies[1]);
    }

    [Fact]
    public async Task Eap_tls_csr_failure_does_not_leak_the_password()
    {
        var device = LoggedDevice().On("get_eap_tls_csr_file.fcgi", HttpStatusCode.InternalServerError, "falha");
        using var client = new AccessClient(Options, device.CreateClient());

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => client.GetEapTlsCsrAsync("senha-da-chave"));

        Assert.DoesNotContain("senha-da-chave", ex.ToString());
    }

    [Fact]
    public async Task Eap_tls_private_key_persist_posts_without_parameters()
    {
        var device = LoggedDevice().OnJson("eap_tls_802_1X_private_key_persist.fcgi", "");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.PersistEapTlsPrivateKeyAsync();

        var request = device.RequestsTo("eap_tls_802_1X_private_key_persist.fcgi").Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("{}", request.Body);
    }
}
