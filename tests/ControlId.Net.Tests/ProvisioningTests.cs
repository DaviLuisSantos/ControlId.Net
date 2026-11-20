using System.Net;
using ControlId.Access;

namespace ControlId.Tests;

public class ProvisioningTests
{
    private static readonly ControlIdDeviceOptions Options = new() { BaseAddress = new Uri("http://10.0.0.5") };

    private static FakeDeviceHandler LoggedDevice() => new FakeDeviceHandler().OnJson("login.fcgi", """{"session":"s"}""");

    [Fact]
    public async Task Pro20k_license_posts_to_the_twenty_thousand_endpoint()
    {
        var device = LoggedDevice().OnJson("upgrade_twenty_thousand_face_templates.fcgi", "");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.ApplyFaceLicenseAsync(FaceLicense.Pro20k, "ABCDE12345");

        var request = device.RequestsTo("upgrade_twenty_thousand_face_templates.fcgi").Single();
        Assert.Equal("""{"password":"ABCDE12345"}""", request.Body);
        Assert.Equal(HttpMethod.Post, request.Method);
    }

    [Fact]
    public async Task Fingerprint_capacity_upgrade_sends_the_password()
    {
        var device = LoggedDevice().OnJson("upgrade_ten_thousand_templates.fcgi", "");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.UpgradeFingerprintCapacityAsync("ABCDE12345");

        Assert.Equal("""{"password":"ABCDE12345"}""", device.RequestsTo("upgrade_ten_thousand_templates.fcgi").Single().Body);
    }

    [Fact]
    public async Task Fingerprint_capacity_upgrade_rejects_empty_password_without_calling_the_device()
    {
        var device = LoggedDevice();
        using var client = new AccessClient(Options, device.CreateClient());

        await Assert.ThrowsAsync<ArgumentException>(() => client.UpgradeFingerprintCapacityAsync(""));

        Assert.Empty(device.RequestsTo("upgrade_ten_thousand_templates.fcgi"));
    }

    [Theory]
    [InlineData("10k")]
    [InlineData("30k")]
    [InlineData("20k")]
    [InlineData("100k")]
    public async Task Pro_operation_mode_sends_the_mode(string mode)
    {
        var device = LoggedDevice().OnJson("set_pro_operation_mode_and_reboot.fcgi", "");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.SetProOperationModeAndRebootAsync(mode);

        Assert.Equal($$"""{"pro_operation_mode":"{{mode}}"}""", device.RequestsTo("set_pro_operation_mode_and_reboot.fcgi").Single().Body);
    }

    [Theory]
    [InlineData("")]
    [InlineData("40k")]
    public async Task Pro_operation_mode_rejects_unknown_values_without_calling_the_device(string mode)
    {
        var device = LoggedDevice();
        using var client = new AccessClient(Options, device.CreateClient());

        await Assert.ThrowsAsync<ArgumentException>(() => client.SetProOperationModeAndRebootAsync(mode));

        Assert.Empty(device.RequestsTo("set_pro_operation_mode_and_reboot.fcgi"));
    }

    [Fact]
    public async Task Redirect_by_device_id_sends_routing_in_query_and_body_untouched()
    {
        var device = LoggedDevice().OnJson("request_redirect.fcgi", """{"ok":true}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var response = await client.RedirectRequestAsync(RedirectTarget.ByDeviceId(2), "load_all", new { a = 1 });

        var request = device.RequestsTo("request_redirect.fcgi").Single();
        Assert.Contains("command=load_all", request.Uri.Query);
        Assert.Contains("device_id=2", request.Uri.Query);
        Assert.DoesNotContain("address=", request.Uri.Query);
        Assert.Equal("""{"a":1}""", request.Body);
        Assert.Equal("""{"ok":true}""", response);
    }

    [Fact]
    public async Task Redirect_by_address_sends_credentials_escaped_and_extra_query()
    {
        var device = LoggedDevice().OnJson("request_redirect.fcgi", "{}");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.RedirectRequestAsync(
            RedirectTarget.ByAddress("192.168.0.10", "admin", "p&ss w"),
            "load_all",
            query: new Dictionary<string, string> { ["x"] = "1" });

        var request = device.RequestsTo("request_redirect.fcgi").Single();
        Assert.Contains("address=192.168.0.10", request.Uri.Query);
        Assert.Contains("login=admin", request.Uri.Query);
        Assert.Contains("password=p%26ss%20w", request.Uri.Query);
        Assert.Contains("x=1", request.Uri.Query);
        Assert.Equal("{}", request.Body);
    }

    [Fact]
    public async Task Redirect_rejects_reserved_query_names_and_empty_command()
    {
        var device = LoggedDevice();
        using var client = new AccessClient(Options, device.CreateClient());

        await Assert.ThrowsAsync<ArgumentException>(() => client.RedirectRequestAsync(RedirectTarget.ByDeviceId(2), ""));
        await Assert.ThrowsAsync<ArgumentException>(() => client.RedirectRequestAsync(
            RedirectTarget.ByDeviceId(2), "load_all", query: new Dictionary<string, string> { ["password"] = "x" }));
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.RedirectRequestAsync(null!, "load_all"));

        Assert.Empty(device.RequestsTo("request_redirect.fcgi"));
    }

    [Fact]
    public void Redirect_target_does_not_print_the_password()
    {
        var text = RedirectTarget.ByAddress("192.168.0.10", "admin", "segredo-do-destino").ToString();

        Assert.DoesNotContain("segredo-do-destino", text);
        Assert.Contains("192.168.0.10", text);
    }

    [Fact]
    public async Task Redirect_failure_does_not_expose_the_destination_password()
    {
        var device = LoggedDevice().On("request_redirect.fcgi", HttpStatusCode.InternalServerError, "erro");
        using var client = new AccessClient(Options, device.CreateClient());

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => client.RedirectRequestAsync(
            RedirectTarget.ByAddress("192.168.0.10", "admin", "segredo-do-destino"), "load_all"));

        Assert.DoesNotContain("segredo-do-destino", ex.ToString());
        Assert.DoesNotContain("segredo-do-destino", ex.Message);
    }
}
