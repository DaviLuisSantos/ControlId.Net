using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using ControlId.Discovery;

namespace ControlId.Tests;

/// <summary>Descoberta de equipamentos (#58): os cinco status, a ordem das credenciais e a expansão de CIDR.</summary>
public class DiscoveryTests
{
    private const string Info =
        "{\"serial\":\"SN123\",\"version\":\"6.20.0\",\"model\":\"iDFace\",\"network\":{\"mac\":\"AA:BB:CC:00:11:22\"}}";

    private static readonly Uri A = new("http://10.0.0.1/");
    private static readonly Uri B = new("http://10.0.0.2/");
    private static readonly Uri C = new("http://10.0.0.3/");

    private static FakeDeviceHandler Device() =>
        new FakeDeviceHandler()
            .OnJson("login.fcgi", "{\"session\":\"abc\"}")
            .OnJson("system_information.fcgi", Info)
            .OnJson("logout.fcgi", "{}");

    private static Func<Uri, HttpMessageHandler> Route(Dictionary<Uri, HttpMessageHandler> handlers) =>
        uri => handlers[uri];

    private static DiscoveryCredential[] Admin() => new[] { new DiscoveryCredential("admin", "segredo") };

    private static Task<IReadOnlyList<DiscoveredDevice>> Scan(
        Dictionary<Uri, HttpMessageHandler> handlers, IEnumerable<DiscoveryCredential> credentials, DiscoveryOptions? options = null) =>
        ControlIdDiscovery.ScanCoreAsync(handlers.Keys.ToList(), credentials, options, Route(handlers), CancellationToken.None);

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw exception;
    }

    [Fact]
    public async Task Found_reads_model_firmware_serial_and_mac()
    {
        var handlers = new Dictionary<Uri, HttpMessageHandler> { [A] = Device() };

        var result = Assert.Single(await Scan(handlers, Admin()));

        Assert.Equal(DiscoveryStatus.Found, result.Status);
        Assert.Equal(A, result.Address);
        Assert.Equal("iDFace", result.Model);
        Assert.Equal("6.20.0", result.Firmware);
        Assert.Equal("SN123", result.Serial);
        Assert.Equal("AA:BB:CC:00:11:22", result.Mac);
        Assert.Equal("admin", result.CredentialLogin);
    }

    [Fact]
    public async Task Refused_connection_is_PortClosed()
    {
        var refused = new ThrowingHandler(new HttpRequestException("recusada", new SocketException((int)SocketError.ConnectionRefused)));
        var handlers = new Dictionary<Uri, HttpMessageHandler> { [A] = refused };

        var result = Assert.Single(await Scan(handlers, Admin()));

        Assert.Equal(DiscoveryStatus.PortClosed, result.Status);
        Assert.Null(result.Serial);
        Assert.Null(result.CredentialLogin);
    }

    [Fact]
    public async Task Server_without_login_endpoint_is_NotControlId()
    {
        var plain = new FakeDeviceHandler().On("login.fcgi", HttpStatusCode.NotFound, "<html>nginx</html>");
        var html = new FakeDeviceHandler().On("login.fcgi", HttpStatusCode.OK, "<html>Roteador</html>");
        var handlers = new Dictionary<Uri, HttpMessageHandler> { [A] = plain, [B] = html };

        var results = await Scan(handlers, Admin());

        Assert.All(results, r => Assert.Equal(DiscoveryStatus.NotControlId, r.Status));
    }

    [Fact]
    public async Task Rejected_credentials_are_CredentialRejected_after_trying_all()
    {
        var device = new FakeDeviceHandler().On("login.fcgi", HttpStatusCode.Unauthorized, "{\"error\":\"Invalid login\",\"code\":1}");
        var handlers = new Dictionary<Uri, HttpMessageHandler> { [A] = device };
        var credentials = new[] { new DiscoveryCredential("admin", "um"), new DiscoveryCredential("admin", "dois") };

        var result = Assert.Single(await Scan(handlers, credentials));

        Assert.Equal(DiscoveryStatus.CredentialRejected, result.Status);
        Assert.Null(result.CredentialLogin);
        Assert.Equal(2, device.RequestsTo("login.fcgi").Count());
    }

    [Fact]
    public async Task Device_error_after_connecting_is_Error()
    {
        var device = new FakeDeviceHandler().On("login.fcgi", HttpStatusCode.InternalServerError, "{\"error\":\"falha interna\",\"code\":7}");
        var handlers = new Dictionary<Uri, HttpMessageHandler> { [A] = device };

        var result = Assert.Single(await Scan(handlers, Admin()));

        Assert.Equal(DiscoveryStatus.Error, result.Status);
    }

    [Fact]
    public async Task Next_credential_is_tried_when_the_first_is_rejected_and_the_login_is_reported()
    {
        var device = new FakeDeviceHandler()
            .On("login.fcgi", HttpStatusCode.Unauthorized, "{\"error\":\"Invalid login\"}")
            .OnJson("login.fcgi", "{\"session\":\"abc\"}")
            .OnJson("system_information.fcgi", Info);
        var handlers = new Dictionary<Uri, HttpMessageHandler> { [A] = device };
        var credentials = new[] { new DiscoveryCredential("admin", "errada"), new DiscoveryCredential("gestor", "certa") };

        var result = Assert.Single(await Scan(handlers, credentials));

        Assert.Equal(DiscoveryStatus.Found, result.Status);
        Assert.Equal("gestor", result.CredentialLogin);
        var logins = device.RequestsTo("login.fcgi").ToList();
        Assert.Contains("\"errada\"", logins[0].Body);
        Assert.Contains("\"certa\"", logins[1].Body);
    }

    [Fact]
    public async Task Factory_defaults_are_tried_last_and_only_when_enabled()
    {
        var withoutDefaults = new FakeDeviceHandler().On("login.fcgi", HttpStatusCode.Unauthorized, "{}");
        var withDefaults = new FakeDeviceHandler()
            .On("login.fcgi", HttpStatusCode.Unauthorized, "{}")
            .OnJson("login.fcgi", "{\"session\":\"abc\"}")
            .OnJson("system_information.fcgi", Info);

        var off = Assert.Single(await Scan(new Dictionary<Uri, HttpMessageHandler> { [A] = withoutDefaults }, Admin()));
        var on = Assert.Single(await Scan(
            new Dictionary<Uri, HttpMessageHandler> { [A] = withDefaults }, Admin(), new DiscoveryOptions { TryFactoryDefaults = true }));

        Assert.Equal(DiscoveryStatus.CredentialRejected, off.Status);
        Assert.Single(withoutDefaults.RequestsTo("login.fcgi"));
        Assert.Equal(DiscoveryStatus.Found, on.Status);
        var logins = withDefaults.RequestsTo("login.fcgi").ToList();
        Assert.Equal(2, logins.Count);
        Assert.Contains("\"segredo\"", logins[0].Body);
        Assert.Contains("\"admin\"", logins[1].Body);
    }

    [Fact]
    public async Task Results_keep_the_order_of_the_addresses_with_mixed_outcomes()
    {
        var handlers = new Dictionary<Uri, HttpMessageHandler>
        {
            [A] = Device(),
            [B] = new ThrowingHandler(new HttpRequestException("recusada", new SocketException((int)SocketError.ConnectionRefused))),
            [C] = new FakeDeviceHandler().On("login.fcgi", HttpStatusCode.NotFound, "nada aqui"),
        };

        var results = await Scan(handlers, Admin(), new DiscoveryOptions { MaxParallelism = 2 });

        Assert.Equal(new[] { A, B, C }, results.Select(r => r.Address));
        Assert.Equal(
            new[] { DiscoveryStatus.Found, DiscoveryStatus.PortClosed, DiscoveryStatus.NotControlId },
            results.Select(r => r.Status));
    }

    [Fact]
    public async Task Password_never_appears_in_results_or_in_text_of_the_credential()
    {
        var handlers = new Dictionary<Uri, HttpMessageHandler> { [A] = Device() };
        var credential = new DiscoveryCredential("admin", "segredo-unico");

        var result = Assert.Single(await Scan(handlers, new[] { credential }));

        Assert.DoesNotContain("segredo-unico", credential.ToString());
        Assert.DoesNotContain("segredo-unico", string.Join("|", result.Address, result.Status, result.Model, result.Firmware, result.Serial, result.Mac, result.CredentialLogin));
    }

    [Fact]
    public async Task Cancellation_stops_the_scan()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var handlers = new Dictionary<Uri, HttpMessageHandler> { [A] = Device() };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ControlIdDiscovery.ScanCoreAsync(handlers.Keys.ToList(), Admin(), null, Route(handlers), cts.Token));
    }

    [Fact]
    public async Task Invalid_arguments_are_refused()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => ControlIdDiscovery.ScanAsync(new[] { A }, Array.Empty<DiscoveryCredential>()));
        await Assert.ThrowsAsync<ArgumentException>(() => ControlIdDiscovery.ScanAsync(new[] { new Uri("ftp://10.0.0.1/") }, Admin()));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            ControlIdDiscovery.ScanAsync(new[] { A }, Admin(), new DiscoveryOptions { MaxParallelism = 0 }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            ControlIdDiscovery.ScanAsync(new[] { A }, Admin(), new DiscoveryOptions { Timeout = TimeSpan.Zero }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            ControlIdDiscovery.ScanAsync(new[] { A }, Admin(), new DiscoveryOptions { ConnectTimeout = TimeSpan.Zero }));
    }

    [Fact]
    public void Options_have_the_documented_defaults()
    {
        var options = new DiscoveryOptions();

        Assert.Equal(32, options.MaxParallelism);
        Assert.Equal(TimeSpan.FromSeconds(1), options.ConnectTimeout);
        Assert.Equal(TimeSpan.FromSeconds(5), options.Timeout);
        Assert.False(options.TryFactoryDefaults);
    }

    [Fact]
    public void ExpandCidr_slash24_skips_network_and_broadcast()
    {
        var addresses = ControlIdDiscovery.ExpandCidr("192.168.0.0/24").ToList();

        Assert.Equal(254, addresses.Count);
        Assert.Equal(new Uri("http://192.168.0.1/"), addresses[0]);
        Assert.Equal(new Uri("http://192.168.0.254/"), addresses[addresses.Count - 1]);
        Assert.Equal(addresses.Count, addresses.Distinct().Count());
    }

    [Fact]
    public void ExpandCidr_aligns_the_address_to_the_network_and_uses_port_and_scheme()
    {
        var addresses = ControlIdDiscovery.ExpandCidr("10.1.2.77/30", 8443, "https").ToList();

        Assert.Equal(new[] { new Uri("https://10.1.2.77:8443/"), new Uri("https://10.1.2.78:8443/") }, addresses);
    }

    [Fact]
    public void ExpandCidr_handles_slash31_and_slash32()
    {
        Assert.Equal(2, ControlIdDiscovery.ExpandCidr("10.0.0.4/31").Count());
        Assert.Equal(new[] { new Uri("http://10.0.0.9/") }, ControlIdDiscovery.ExpandCidr("10.0.0.9/32"));
    }

    [Fact]
    public void ExpandCidr_crosses_octet_boundaries()
    {
        var addresses = ControlIdDiscovery.ExpandCidr("10.0.0.0/23").ToList();

        Assert.Equal(510, addresses.Count);
        Assert.Equal(new Uri("http://10.0.1.253/"), addresses[addresses.Count - 2]);
        Assert.Equal(new Uri("http://10.0.1.254/"), addresses[addresses.Count - 1]);
    }

    [Theory]
    [InlineData("192.168.0.0")]
    [InlineData("192.168.0.0/")]
    [InlineData("192.168.0.0/33")]
    [InlineData("192.168.0.0/-1")]
    [InlineData("192.168.0/24")]
    [InlineData("::1/64")]
    [InlineData("abc/24")]
    public void ExpandCidr_refuses_malformed_ranges(string cidr)
    {
        Assert.Throws<ArgumentException>(() => ControlIdDiscovery.ExpandCidr(cidr));
    }

    [Fact]
    public void ExpandCidr_refuses_bad_port_and_scheme()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ControlIdDiscovery.ExpandCidr("10.0.0.0/24", 0));
        Assert.Throws<ArgumentException>(() => ControlIdDiscovery.ExpandCidr("10.0.0.0/24", 80, "ftp"));
    }

#if NET8_0_OR_GREATER
    [Fact]
    public async Task Scan_of_unanswered_addresses_ends_near_the_connect_timeout()
    {
        // 10.255.255.x não responde (ou não tem rota, e a falha vem antes). Em ambos os casos a varredura inteira,
        // em paralelo, precisa ficar longe do Timeout de 30 s.
        var addresses = new[] { new Uri("http://10.255.255.1/"), new Uri("http://10.255.255.2/"), new Uri("http://10.255.255.3/") };
        var options = new DiscoveryOptions { ConnectTimeout = TimeSpan.FromSeconds(1), Timeout = TimeSpan.FromSeconds(30) };

        var watch = Stopwatch.StartNew();
        var results = await ControlIdDiscovery.ScanAsync(addresses, Admin(), options);
        watch.Stop();

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), $"Levou {watch.Elapsed}.");
        Assert.All(results, r => Assert.NotEqual(DiscoveryStatus.Found, r.Status));
    }
#endif
}
