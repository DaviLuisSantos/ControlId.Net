using System.Net;
using System.Text.Json;
using ControlId.Access;

namespace ControlId.Tests;

public class AccessClientTests
{
    private static readonly ControlIdDeviceOptions Options = new()
    {
        BaseAddress = new Uri("http://10.0.0.5"),
        Login = "admin",
        Password = "secret",
    };

    [Fact]
    public async Task Logs_in_lazily_and_sends_session_in_query_string()
    {
        var device = new FakeDeviceHandler()
            .OnJson("login.fcgi", """{"session":"abc+/="}""")
            .OnJson("load_objects.fcgi", """{"users":[{"id":7,"name":"Walter White","registration":"1234"}]}""");

        using var client = new AccessClient(Options, device.CreateClient());

        var users = await client.GetUsersAsync();

        Assert.Single(users);
        Assert.Equal(7, users[0].Id);
        Assert.Equal("Walter White", users[0].Name);

        var login = Assert.Single(device.RequestsTo("login.fcgi"));
        Assert.Equal("""{"login":"admin","password":"secret"}""", login.Body);

        var load = Assert.Single(device.RequestsTo("load_objects.fcgi"));
        Assert.Equal("?session=abc%2B%2F%3D", load.Uri.Query);
        Assert.Equal("""{"object":"users"}""", load.Body);
    }

    [Fact]
    public async Task Reuses_session_between_calls()
    {
        var device = new FakeDeviceHandler()
            .OnJson("login.fcgi", """{"session":"s1"}""")
            .OnJson("load_objects.fcgi", """{"users":[]}""");

        using var client = new AccessClient(Options, device.CreateClient());

        await client.GetUsersAsync();
        await client.GetUsersAsync();

        Assert.Single(device.RequestsTo("login.fcgi"));
        Assert.Equal(2, device.RequestsTo("load_objects.fcgi").Count());
    }

    [Fact]
    public async Task Relogs_once_when_session_expires()
    {
        var device = new FakeDeviceHandler()
            .OnJson("login.fcgi", """{"session":"old"}""")
            .OnJson("login.fcgi", """{"session":"new"}""")
            .On("load_objects.fcgi", HttpStatusCode.Unauthorized)
            .OnJson("load_objects.fcgi", """{"users":[]}""");

        using var client = new AccessClient(Options, device.CreateClient());

        await client.GetUsersAsync();

        Assert.Equal(2, device.RequestsTo("login.fcgi").Count());
        Assert.Equal("?session=new", device.RequestsTo("load_objects.fcgi").Last().Uri.Query);
        Assert.Equal("new", client.Session);
    }

    [Fact]
    public async Task Wrong_credentials_throw_authentication_exception()
    {
        var device = new FakeDeviceHandler().On("login.fcgi", HttpStatusCode.Unauthorized, """{"error":"Invalid login","code":1}""");
        using var client = new AccessClient(Options, device.CreateClient());

        await Assert.ThrowsAsync<ControlIdAuthenticationException>(() => client.GetUsersAsync());
    }

    [Fact]
    public async Task Device_errors_are_surfaced_with_message_and_code()
    {
        var device = new FakeDeviceHandler()
            .OnJson("login.fcgi", """{"session":"s"}""")
            .On("create_objects.fcgi", HttpStatusCode.BadRequest, """{"error":"constraint failed","code":5}""");

        using var client = new AccessClient(Options, device.CreateClient());

        var ex = await Assert.ThrowsAsync<ControlIdException>(() => client.CreateUserAsync(new AccessUser { Name = "x" }));
        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal(5, ex.DeviceErrorCode);
        Assert.Contains("constraint failed", ex.Message);
    }

    [Fact]
    public async Task CreateUser_omits_null_fields_and_returns_id()
    {
        var device = new FakeDeviceHandler()
            .OnJson("login.fcgi", """{"session":"s"}""")
            .OnJson("create_objects.fcgi", """{"ids":[42]}""");

        using var client = new AccessClient(Options, device.CreateClient());

        var id = await client.CreateUserAsync(new AccessUser { Name = "Davi", Registration = "001" });

        Assert.Equal(42, id);
        Assert.Equal(
            """{"object":"users","values":[{"registration":"001","name":"Davi"}]}""",
            device.RequestsTo("create_objects.fcgi").Single().Body);
    }

    [Fact]
    public async Task GetAccessLogs_builds_where_and_order()
    {
        var device = new FakeDeviceHandler()
            .OnJson("login.fcgi", """{"session":"s"}""")
            .OnJson("load_objects.fcgi", """{"access_logs":[{"id":11,"time":1700000000,"event":7,"user_id":3}]}""");

        using var client = new AccessClient(Options, device.CreateClient());

        var logs = await client.GetAccessLogsAsync(afterId: 10, limit: 50);

        Assert.Equal(7, Assert.Single(logs).Event);

        using var body = JsonDocument.Parse(device.RequestsTo("load_objects.fcgi").Single().Body);
        Assert.Equal(10, body.RootElement.GetProperty("where").GetProperty("access_logs").GetProperty("id").GetProperty(">").GetInt64());
        Assert.Equal(50, body.RootElement.GetProperty("limit").GetInt32());
    }

    [Theory]
    [InlineData(TurnstileDirection.Clockwise, "allow=clockwise")]
    [InlineData(TurnstileDirection.Anticlockwise, "allow=anticlockwise")]
    [InlineData(TurnstileDirection.Both, "allow=both")]
    public async Task ReleaseTurnstile_sends_catra_action(TurnstileDirection direction, string expected)
    {
        var device = new FakeDeviceHandler()
            .OnJson("login.fcgi", """{"session":"s"}""")
            .OnJson("execute_actions.fcgi", "");

        using var client = new AccessClient(Options, device.CreateClient());

        await client.ReleaseTurnstileAsync(direction);

        Assert.Equal(
            $$"""{"actions":[{"action":"catra","parameters":"{{expected}}"}]}""",
            device.RequestsTo("execute_actions.fcgi").Single().Body);
    }

    [Fact]
    public async Task OpenDoor_sends_door_action()
    {
        var device = new FakeDeviceHandler()
            .OnJson("login.fcgi", """{"session":"s"}""")
            .OnJson("execute_actions.fcgi", "");

        using var client = new AccessClient(Options, device.CreateClient());

        await client.OpenDoorAsync(2);

        Assert.Equal(
            """{"actions":[{"action":"door","parameters":"door=2"}]}""",
            device.RequestsTo("execute_actions.fcgi").Single().Body);
    }
}
