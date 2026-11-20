using ControlId.Access;
using ControlId.Testing;

namespace ControlId.Tests;

public class RecordReplayTests
{
    private static readonly ControlIdDeviceOptions Options = new()
    {
        BaseAddress = new Uri("http://10.0.0.5"),
        Login = "admin",
        Password = "secret",
    };

    private static FakeDeviceHandler NewDevice() => new FakeDeviceHandler()
        .OnJson("login.fcgi", """{"session":"abc123"}""")
        .OnJson("load_objects.fcgi", """{"users":[{"id":7,"name":"Walter White","registration":"1234"}]}""");

    private static async Task<Cassette> RecordAsync()
    {
        var cassette = new Cassette();
        var device = NewDevice();
        using var http = new HttpClient(new RecordingHandler(device, cassette));
        using var client = new AccessClient(Options, http);

        var users = await client.GetUsersAsync();
        Assert.Single(users);
        return cassette;
    }

    [Fact]
    public async Task Records_with_the_fake_and_replays_without_it()
    {
        var cassette = await RecordAsync();
        var path = Path.Combine(Path.GetTempPath(), "cassete-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            cassette.Save(path);
            var loaded = Cassette.Load(path);

            var replay = new ReplayHandler(loaded);
            using var http = new HttpClient(replay);
            using var client = new AccessClient(Options, http);

            var users = await client.GetUsersAsync();

            Assert.Equal(7, Assert.Single(users).Id);
            Assert.Equal(0, replay.Remaining);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Masks_secrets_in_the_cassette()
    {
        var cassette = await RecordAsync();
        var path = Path.Combine(Path.GetTempPath(), "cassete-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            cassette.Save(path);
            var text = File.ReadAllText(path);

            Assert.DoesNotContain("secret", text);
            Assert.DoesNotContain("abc123", text);
            Assert.DoesNotContain("admin", text);
            Assert.Contains("***", text);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Replay_fails_with_clear_message_when_body_diverges()
    {
        var cassette = await RecordAsync();
        using var http = new HttpClient(new ReplayHandler(cassette)) { BaseAddress = new Uri("http://10.0.0.5") };

        using var login = new StringContent("""{"login":"admin","password":"secret"}""", System.Text.Encoding.UTF8, "application/json");
        (await http.PostAsync("login.fcgi", login)).Dispose();

        using var other = new StringContent("""{"object":"groups"}""", System.Text.Encoding.UTF8, "application/json");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => http.PostAsync("load_objects.fcgi?session=xyz", other));

        Assert.Contains("requisição 2", ex.Message);
        Assert.Contains("o corpo divergiu", ex.Message);
        Assert.Contains("\"users\"", ex.Message);
        Assert.Contains("\"groups\"", ex.Message);
    }

    [Fact]
    public async Task Replay_fails_when_endpoint_differs()
    {
        var cassette = await RecordAsync();
        using var http = new HttpClient(new ReplayHandler(cassette)) { BaseAddress = new Uri("http://10.0.0.5") };

        using var body = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => http.PostAsync("is_alive.fcgi", body));

        Assert.Contains("o endpoint esperado era '/login.fcgi'", ex.Message);
    }
}
