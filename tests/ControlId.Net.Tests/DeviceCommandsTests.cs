using System.Net;
using System.Text;
using System.Text.Json;
using ControlId.Access;
using ControlId.Access.Push;

namespace ControlId.Tests;

public class DeviceCommandsTests
{
    private static readonly ControlIdDeviceOptions Options = new()
    {
        BaseAddress = new Uri("http://10.0.0.5"),
        Login = "admin",
        Password = "admin",
    };

    private static FakeDeviceHandler LoggedDevice() => new FakeDeviceHandler().OnJson("login.fcgi", """{"session":"s"}""");

    private static async Task<(FakeDeviceHandler Device, string Body)> RunAsync(string endpoint, Func<AccessClient, Task> action, string response = "")
    {
        var device = LoggedDevice().OnJson(endpoint, response);
        using var client = new AccessClient(Options, device.CreateClient());
        await action(client);
        return (device, device.RequestsTo(endpoint).Single().Body);
    }

    [Fact]
    public async Task Message_to_screen_matches_documentation()
    {
        Assert.Equal(
            """{"message":"hello world!","timeout":3000}""",
            (await RunAsync("message_to_screen.fcgi", c => c.ShowMessageAsync("hello world!", TimeSpan.FromSeconds(3)))).Body);
        Assert.Equal(
            """{"message":"Fixa","timeout":0}""",
            (await RunAsync("message_to_screen.fcgi", c => c.ShowMessageAsync("Fixa"))).Body);
        Assert.Equal(
            """{"message":"","timeout":0}""",
            (await RunAsync("message_to_screen.fcgi", c => c.ClearMessageAsync())).Body);
    }

    [Fact]
    public async Task Buzzer_matches_documentation_and_validates_limits()
    {
        Assert.Equal(
            """{"frequency":4000,"duty_cycle":50,"timeout":1000}""",
            (await RunAsync("buzzer_buzz.fcgi", c => c.BuzzAsync(TimeSpan.FromSeconds(1)))).Body);

        using var client = new AccessClient(Options, LoggedDevice().CreateClient());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.BuzzAsync(TimeSpan.FromSeconds(4)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.BuzzAsync(TimeSpan.FromSeconds(1), dutyCycle: 101));
    }

    [Fact]
    public async Task ChangeLogin_updates_credentials_for_the_next_login()
    {
        var device = new FakeDeviceHandler()
            .OnJson("login.fcgi", """{"session":"s1"}""")
            .OnJson("login.fcgi", """{"session":"s2"}""")
            .OnJson("change_login.fcgi", "")
            .On("load_objects.fcgi", HttpStatusCode.Unauthorized) // sessão invalidada depois da troca
            .OnJson("load_objects.fcgi", """{"users":[]}""");
        var options = new ControlIdDeviceOptions { BaseAddress = new Uri("http://10.0.0.5"), Login = "admin", Password = "admin" };
        using var client = new AccessClient(options, device.CreateClient());

        await client.ChangeLoginAsync("WalterWhite", "Heisenberg");
        await client.GetUsersAsync();

        Assert.Equal("""{"login":"WalterWhite","password":"Heisenberg"}""", device.RequestsTo("change_login.fcgi").Single().Body);
        Assert.Equal("""{"login":"WalterWhite","password":"Heisenberg"}""", device.RequestsTo("login.fcgi").Last().Body);
        Assert.Equal("admin", options.Password); // o objeto do chamador não é alterado
    }

    [Fact]
    public async Task ChangeLogin_failure_keeps_old_credentials()
    {
        var device = LoggedDevice().On("change_login.fcgi", HttpStatusCode.BadRequest, """{"error":"invalid","code":1}""");
        using var client = new AccessClient(Options, device.CreateClient());

        await Assert.ThrowsAsync<ControlIdException>(() => client.ChangeLoginAsync("novo", "x"));
        await client.LoginAsync();

        Assert.Equal("""{"login":"admin","password":"admin"}""", device.RequestsTo("login.fcgi").Last().Body);
    }

    [Fact]
    public async Task HashPassword_reads_hash_and_salt()
    {
        var device = LoggedDevice().OnJson("user_hash_password.fcgi", """{"password":"ab5d13","salt":"y2a2B"}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var hash = await client.HashPasswordAsync("abc123");

        Assert.Equal("ab5d13", hash.Hash);
        Assert.Equal("y2a2B", hash.Salt);
        Assert.Equal("""{"password":"abc123"}""", device.RequestsTo("user_hash_password.fcgi").Single().Body);
    }

    [Theory]
    [InlineData("reboot.fcgi")]
    [InlineData("reboot_recovery.fcgi")]
    [InlineData("delete_admins.fcgi")]
    [InlineData("led_rgb_refresh.fcgi")]
    public async Task Commands_without_parameters_send_empty_object(string endpoint)
    {
        Func<AccessClient, Task> action = endpoint switch
        {
            "reboot.fcgi" => c => c.RebootAsync(),
            "reboot_recovery.fcgi" => c => c.RebootToUpdateModeAsync(),
            "delete_admins.fcgi" => c => c.DeleteAllAdministratorsAsync(),
            _ => c => c.RefreshLedAsync(),
        };

        Assert.Equal("{}", (await RunAsync(endpoint, action)).Body);
    }

    [Fact]
    public async Task Factory_reset_sends_keep_network_flag()
    {
        Assert.Equal(
            """{"keep_network_info":true}""",
            (await RunAsync("reset_to_factory_default.fcgi", c => c.ResetToFactoryDefaultAsync(keepNetworkSettings: true))).Body);
    }

    [Fact]
    public async Task Network_matches_documentation_example()
    {
        var settings = new NetworkSettings("192.168.0.33", "255.255.255.0", "192.168.0.1")
        {
            CustomHostnameEnabled = true,
            DeviceHostname = "ControlID",
            WebServerPort = 80,
            SslEnabled = false,
            SelfSignedCertificate = true,
            DnsPrimary = "1.1.1.1",
            DnsSecondary = "1.1.4.4",
        };

        Assert.Equal(
            """{"ip":"192.168.0.33","netmask":"255.255.255.0","gateway":"192.168.0.1","custom_hostname_enabled":true,"device_hostname":"ControlID","web_server_port":80,"ssl_enabled":false,"self_signed_certificate":true,"dns_primary":"1.1.1.1","dns_secondary":"1.1.4.4"}""",
            (await RunAsync("set_system_network.fcgi", c => c.SetNetworkAsync(settings))).Body);
        Assert.Equal(
            """{"ip":"10.0.0.9","netmask":"255.0.0.0","gateway":"10.0.0.1"}""",
            (await RunAsync("set_system_network.fcgi", c => c.SetNetworkAsync(new NetworkSettings("10.0.0.9", "255.0.0.0", "10.0.0.1")))).Body);
        Assert.Throws<ArgumentException>(() => new NetworkSettings("", "255.0.0.0", "10.0.0.1"));
    }

    [Fact]
    public async Task Logo_commands_use_slot_in_query()
    {
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var device = LoggedDevice()
            .OnBytes("logo.fcgi", png, "image/png")
            .OnJson("logo_change.fcgi", "{}")
            .OnJson("logo_destroy.fcgi", "{}")
            .OnJson("set_configuration.fcgi", "{}");
        using var client = new AccessClient(Options, device.CreateClient());

        Assert.Equal(png, await client.GetLogoAsync(5));
        await client.SetLogoAsync(png, 5);
        await client.DeleteLogoAsync();
        await client.ShowLogoAsync(5);

        Assert.Equal("?session=s&id=5", device.RequestsTo("logo.fcgi").Single().Uri.Query);
        var change = device.RequestsTo("logo_change.fcgi").Single();
        Assert.Equal("?session=s&id=5", change.Uri.Query);
        Assert.Equal("application/octet-stream", change.ContentType);
        Assert.Equal("?session=s", device.RequestsTo("logo_destroy.fcgi").Single().Uri.Query);
        Assert.Equal("""{"general":{"show_logo":"5"}}""", device.RequestsTo("set_configuration.fcgi").Single().Body);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.ShowLogoAsync(9));
        await Assert.ThrowsAsync<ArgumentException>(() => client.SetLogoAsync(new byte[1024 * 1024]));
    }

    [Fact]
    public async Task Gpio_state_is_typed()
    {
        var device = LoggedDevice().OnJson(
            "gpio_state.fcgi", """{"enabled":1,"in":1,"pin":"door_sensor1","idle":0,"pullup":1,"value":1,"notify":1,"notified":12}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var state = await client.GetGpioStateAsync(5);

        Assert.Equal("door_sensor1", state.Pin);
        Assert.True(state.IsEnabled);
        Assert.True(state.IsInput);
        Assert.True(state.IsActive);
        Assert.Equal(12, state.Notified);
        Assert.Equal("""{"gpio":5}""", device.RequestsTo("gpio_state.fcgi").Single().Body);
    }

    [Fact]
    public async Task Usb_port_and_create_or_modify()
    {
        Assert.Equal(
            """{"general":{"usb_port_enabled":"0"}}""",
            (await RunAsync("set_configuration.fcgi", c => c.SetUsbPortEnabledAsync(false), "{}")).Body);

        var device = LoggedDevice().OnJson("create_or_modify_objects.fcgi", """{"changes":1}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var changes = await client.CreateOrModifyObjectsAsync("users", new[] { new AccessUser { Id = 1, Registration = "1234", Name = "Walter H. White" } });

        Assert.Equal(1, changes);
        Assert.Equal(
            """{"object":"users","values":[{"id":1,"registration":"1234","name":"Walter H. White"}]}""",
            device.RequestsTo("create_or_modify_objects.fcgi").Single().Body);
    }

    [Fact]
    public void Push_shortcuts_reuse_the_same_bodies()
    {
        Assert.Equal(
            """{"verb":"POST","endpoint":"message_to_screen","body":{"message":"Oi","timeout":2000},"contentType":"application/json"}""",
            JsonSerializer.Serialize(PushCommand.ShowMessage("Oi", TimeSpan.FromSeconds(2)), new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull }));
        Assert.Equal("buzzer_buzz", PushCommand.Buzz(TimeSpan.FromMilliseconds(500)).Endpoint);
        Assert.Throws<ArgumentOutOfRangeException>(() => PushCommand.Buzz(TimeSpan.FromSeconds(10)));
        Assert.Equal("reboot", PushCommand.Reboot().Endpoint);
    }
}
