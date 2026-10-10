using System.Net;
using System.Text;
using System.Text.Json;
using ControlId.Access;
using ControlId.Rep;

namespace ControlId.Tests;

public class GenericEndpointsTests
{
    private static readonly ControlIdDeviceOptions Options = new()
    {
        BaseAddress = new Uri("http://10.0.0.5"),
        Login = "admin",
        Password = "secret",
    };

    private static FakeDeviceHandler LoggedDevice() => new FakeDeviceHandler().OnJson("login.fcgi", """{"session":"s"}""");

    [Theory]
    [InlineData("message_to_screen")]
    [InlineData("message_to_screen.fcgi")]
    [InlineData("/message_to_screen.fcgi")]
    public async Task SendRaw_normalizes_endpoint(string endpoint)
    {
        var device = LoggedDevice().OnJson("message_to_screen.fcgi", "");
        using var client = new AccessClient(Options, device.CreateClient());

        var response = await client.SendRawAsync(endpoint, new { message = "Bem-vindo", timeout = 5000 });

        Assert.Equal(string.Empty, response);
        var request = device.RequestsTo("message_to_screen.fcgi").Single();
        Assert.Equal("?session=s", request.Uri.Query);
        Assert.Equal("""{"message":"Bem-vindo","timeout":5000}""", request.Body);
    }

    [Fact]
    public async Task SendAsync_deserializes_and_appends_query()
    {
        var device = LoggedDevice().OnJson("user_hash_password.fcgi", """{"password":"abc","salt":"xyz"}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var result = await client.SendAsync<JsonElement>("user_hash_password", new { password = "1234" }, "?extra=1");

        Assert.Equal("xyz", result.GetProperty("salt").GetString());
        Assert.Equal("?session=s&extra=1", device.RequestsTo("user_hash_password.fcgi").Single().Uri.Query);
    }

    [Fact]
    public async Task SendAsync_without_body_sends_empty_object()
    {
        var device = LoggedDevice().OnJson("reboot.fcgi", "{}");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.SendAsync<JsonElement>("reboot");

        Assert.Equal("{}", device.RequestsTo("reboot.fcgi").Single().Body);
    }

    [Fact]
    public async Task SendBytes_posts_binary_with_content_type()
    {
        var device = LoggedDevice().OnJson("logo_change.fcgi", "");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.SendBytesAsync("logo_change", Encoding.ASCII.GetBytes("PNG"), "application/octet-stream", "id=1");

        var request = device.RequestsTo("logo_change.fcgi").Single();
        Assert.Equal("application/octet-stream", request.ContentType);
        Assert.Equal("PNG", request.Body);
        Assert.Equal("?session=s&id=1", request.Uri.Query);
    }

    [Fact]
    public async Task Download_supports_get_and_post()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var device = LoggedDevice()
            .OnBytes("logo.fcgi", bytes, "image/png")
            .OnBytes("report_generate.fcgi", bytes, "text/csv");
        using var client = new AccessClient(Options, device.CreateClient());

        Assert.Equal(bytes, await client.DownloadAsync("logo", queryString: "id=1", method: HttpMethod.Get));
        Assert.Equal(bytes, await client.DownloadAsync("report_generate", new { report = new { @object = "users" } }));

        Assert.Equal(HttpMethod.Get, device.RequestsTo("logo.fcgi").Single().Method);
        Assert.Equal(HttpMethod.Post, device.RequestsTo("report_generate.fcgi").Single().Method);
        Assert.Equal("""{"report":{"object":"users"}}""", device.RequestsTo("report_generate.fcgi").Single().Body);
    }

    [Fact]
    public async Task Generic_calls_relogin_and_surface_errors()
    {
        var device = new FakeDeviceHandler()
            .OnJson("login.fcgi", """{"session":"old"}""")
            .OnJson("login.fcgi", """{"session":"new"}""")
            .On("buzzer_buzz.fcgi", HttpStatusCode.Unauthorized)
            .On("buzzer_buzz.fcgi", HttpStatusCode.BadRequest, """{"error":"Invalid frequency","code":3}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var ex = await Assert.ThrowsAsync<ControlIdException>(() => client.SendRawAsync("buzzer_buzz", new { frequency = 99999 }));

        Assert.Equal(3, ex.DeviceErrorCode);
        Assert.Equal("?session=new", device.RequestsTo("buzzer_buzz.fcgi").Last().Uri.Query);
    }

    [Theory]
    [InlineData("")]
    [InlineData("load_objects.fcgi?x=1")]
    [InlineData("http://10.0.0.5/load_objects.fcgi")]
    [InlineData("api/load_objects")]
    public async Task Invalid_endpoints_are_rejected_before_sending(string endpoint)
    {
        var device = LoggedDevice();
        using var client = new AccessClient(Options, device.CreateClient());

        await Assert.ThrowsAsync<ArgumentException>(() => client.SendRawAsync(endpoint));
        Assert.Empty(device.Requests);
    }

    [Fact]
    public async Task Download_rejects_other_methods()
    {
        using var client = new AccessClient(Options, LoggedDevice().CreateClient());

        await Assert.ThrowsAsync<ArgumentException>(() => client.DownloadAsync("x", method: HttpMethod.Put));
    }

    [Fact]
    public async Task Rep_generic_call_does_not_add_mode()
    {
        var device = LoggedDevice().OnJson("get_info.fcgi", """{"ok":true}""");
        using var client = new RepClient(Options, RepMode.Portaria671, device.CreateClient());

        Assert.True((await client.SendAsync<JsonElement>("get_info")).GetProperty("ok").GetBoolean());
        Assert.Equal("?session=s", device.RequestsTo("get_info.fcgi").Single().Uri.Query);
    }

    [Fact]
    public async Task Both_clients_share_the_generic_interface()
    {
        var device = LoggedDevice().OnJson("ping.fcgi", "pong");
        using var access = new AccessClient(Options, device.CreateClient());
        using var rep = new RepClient(Options, RepMode.Legacy, device.CreateClient());

        foreach (IControlIdClient client in new IControlIdClient[] { access, rep })
        {
            Assert.Equal("pong", await client.SendRawAsync("ping"));
        }
    }

    [Fact]
    public async Task GetConfiguration_reads_several_sections()
    {
        var device = LoggedDevice().OnJson(
            "get_configuration.fcgi",
            """{"general":{"beep_enabled":"1","relay1_timeout":"3000"},"monitor":{"port":8000}}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var config = await client.GetConfigurationAsync(new Dictionary<string, IEnumerable<string>>
        {
            ["general"] = new[] { "beep_enabled", "relay1_timeout" },
            ["monitor"] = new[] { "port" },
        });

        Assert.Equal(
            """{"general":["beep_enabled","relay1_timeout"],"monitor":["port"]}""",
            device.RequestsTo("get_configuration.fcgi").Single().Body);
        Assert.Equal("3000", config["general"]["relay1_timeout"]);
        Assert.Equal("8000", config["monitor"]["port"]); // número de firmware tolerado como texto
    }

    [Fact]
    public async Task GetConfiguration_single_section()
    {
        var device = LoggedDevice().OnJson("get_configuration.fcgi", """{"monitor":{"hostname":"192.168.0.20"}}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var monitor = await client.GetConfigurationAsync("monitor", new[] { "hostname", "port" });

        Assert.Equal("192.168.0.20", monitor["hostname"]);
        Assert.False(monitor.ContainsKey("port"));
    }

    [Fact]
    public async Task GetConfiguration_missing_section_is_empty()
    {
        using var client = new AccessClient(Options, LoggedDevice().OnJson("get_configuration.fcgi", "{}").CreateClient());

        Assert.Empty(await client.GetConfigurationAsync("general", new[] { "beep_enabled" }));
    }
}
