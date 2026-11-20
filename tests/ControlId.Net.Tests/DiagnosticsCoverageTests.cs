using System.Net.Http;
using ControlId.Access;

namespace ControlId.Tests;

public class DiagnosticsCoverageTests
{
    private static readonly ControlIdDeviceOptions Options = new() { BaseAddress = new Uri("http://10.0.0.5") };

    private static FakeDeviceHandler LoggedDevice() => new FakeDeviceHandler().OnJson("login.fcgi", """{"session":"s"}""");

    [Fact]
    public async Task GetNtpServer_uses_get_and_reads_both_servers()
    {
        var device = LoggedDevice()
            .OnJson("get_ntp_server.fcgi", """{"server1":"pool.ntp.org","server2":"time.google.com"}""")
            .OnJson("get_ntp_server.fcgi", """{"server1":"a.example"}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var both = await client.GetNtpServerAsync();
        var single = await client.GetNtpServerAsync();

        Assert.Equal("pool.ntp.org", both.Server1);
        Assert.Equal("time.google.com", both.Server2);
        Assert.Equal("a.example", single.Server1);
        Assert.Null(single.Server2);
        Assert.All(device.RequestsTo("get_ntp_server.fcgi"), r => Assert.Equal(HttpMethod.Get, r.Method));
    }

    [Fact]
    public async Task SetNtpServer_sends_server2_only_when_informed()
    {
        var device = LoggedDevice().OnJson("set_ntp_server.fcgi", "").OnJson("set_ntp_server.fcgi", "");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.SetNtpServerAsync("pool.ntp.org", "time.google.com");
        await client.SetNtpServerAsync("pool.ntp.org");

        var bodies = device.RequestsTo("set_ntp_server.fcgi").Select(r => r.Body).ToArray();
        Assert.Equal("""{"server1":"pool.ntp.org","server2":"time.google.com"}""", bodies[0]);
        Assert.Equal("""{"server1":"pool.ntp.org"}""", bodies[1]);
        await Assert.ThrowsAsync<ArgumentException>(() => client.SetNtpServerAsync(" "));
    }

    [Fact]
    public async Task GetNtpServerStatus_reads_server_status()
    {
        var device = LoggedDevice()
            .OnJson("get_ntp_server_status.fcgi", """{"server_status":true}""")
            .OnJson("get_ntp_server_status.fcgi", """{"server_status":false}""");
        using var client = new AccessClient(Options, device.CreateClient());

        Assert.True(await client.GetNtpServerStatusAsync());
        Assert.False(await client.GetNtpServerStatusAsync());
        Assert.All(device.RequestsTo("get_ntp_server_status.fcgi"), r => Assert.Equal(HttpMethod.Get, r.Method));
    }

    [Fact]
    public async Task CheckPortAvailability_sends_range_and_parses_status()
    {
        var device = LoggedDevice()
            .OnJson("check_port_availability.fcgi", """{"status":"OK"}""")
            .OnJson("check_port_availability.fcgi", """{"status":"RTSP"}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var free = await client.CheckPortAvailabilityAsync(5000, 5100);
        var busy = await client.CheckPortAvailabilityAsync(554, 554);

        Assert.True(free.IsAvailable);
        Assert.False(busy.IsAvailable);
        Assert.Equal("RTSP", busy.Status);
        Assert.Equal("""{"initial_port":5000,"final_port":5100}""", device.RequestsTo("check_port_availability.fcgi").First().Body);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.CheckPortAvailabilityAsync(0, 10));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.CheckPortAvailabilityAsync(10, 70000));
        await Assert.ThrowsAsync<ArgumentException>(() => client.CheckPortAvailabilityAsync(200, 100));
    }

    [Fact]
    public async Task GetVpnIp_reads_ip_and_returns_empty_when_disconnected()
    {
        var device = LoggedDevice()
            .OnJson("get_vpn_ip.fcgi", """{"ip":"10.8.0.2"}""")
            .OnJson("get_vpn_ip.fcgi", """{"ip":""}""");
        using var client = new AccessClient(Options, device.CreateClient());

        Assert.Equal("10.8.0.2", await client.GetVpnIpAsync());
        Assert.Equal(string.Empty, await client.GetVpnIpAsync());
        Assert.All(device.RequestsTo("get_vpn_ip.fcgi"), r => Assert.Equal(HttpMethod.Get, r.Method));
    }

    [Fact]
    public async Task GetSslCertificate_returns_the_text_with_get()
    {
        const string Pem = "-----BEGIN CERTIFICATE-----\nABC\n-----END CERTIFICATE-----\n";
        var device = LoggedDevice().OnBytes("get_ssl_certificate.fcgi", System.Text.Encoding.UTF8.GetBytes(Pem), "text/plain");
        using var client = new AccessClient(Options, device.CreateClient());

        Assert.Equal(Pem, await client.GetSslCertificateAsync());
        Assert.Equal(HttpMethod.Get, device.RequestsTo("get_ssl_certificate.fcgi").Single().Method);
    }

    [Fact]
    public async Task Get8021XStatus_sends_interface_by_name_and_maps_status()
    {
        var device = LoggedDevice()
            .OnJson("get_802_1X_status.fcgi", """{"status":4}""")
            .OnJson("get_802_1X_status.fcgi", """{"status":11}""")
            .OnJson("get_802_1X_status.fcgi", """{"status":99}""");
        using var client = new AccessClient(Options, device.CreateClient());

        Assert.Equal(Ieee8021XStatus.Authenticated, await client.Get8021XStatusAsync());
        Assert.Equal(Ieee8021XStatus.Disabled, await client.Get8021XStatusAsync(NetworkInterfaceKind.Wireless));
        Assert.Equal(Ieee8021XStatus.Unknown, await client.Get8021XStatusAsync());

        var requests = device.RequestsTo("get_802_1X_status.fcgi").ToArray();
        Assert.All(requests, r => Assert.Equal(HttpMethod.Get, r.Method));
        Assert.Contains("interface=wired", requests[0].Uri.Query);
        Assert.Contains("interface=wireless", requests[1].Uri.Query);
    }

    [Theory]
    [InlineData(DeviceLog.AccessControl, "get_ac_log.fcgi")]
    [InlineData(DeviceLog.OpenVpn, "get_openvpn_log.fcgi")]
    [InlineData(DeviceLog.Wpa, "get_wpa_log.fcgi")]
    [InlineData(DeviceLog.WpaSupplicant, "get_wpasupplicant_log.fcgi")]
    public async Task GetDeviceLog_reads_the_endpoint_of_each_log_as_plain_text(DeviceLog log, string endpoint)
    {
        var device = LoggedDevice().OnBytes(endpoint, System.Text.Encoding.UTF8.GetBytes("linha 1\nlinha 2\n"), "text/plain");
        using var client = new AccessClient(Options, device.CreateClient());

        var text = await client.GetDeviceLogAsync(log);

        Assert.Equal("linha 1\nlinha 2\n", text);
        Assert.Equal(HttpMethod.Get, device.RequestsTo(endpoint).Single().Method);
    }

    [Fact]
    public async Task GetDeviceLog_rejects_an_undefined_log()
    {
        using var client = new AccessClient(Options, LoggedDevice().CreateClient());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.GetDeviceLogAsync((DeviceLog)99));
    }
}
