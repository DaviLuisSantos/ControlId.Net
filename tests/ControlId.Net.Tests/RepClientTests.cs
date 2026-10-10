using System.Text.Json;
using ControlId.Rep;

namespace ControlId.Tests;

public class RepClientTests
{
    private static readonly ControlIdDeviceOptions Options = new()
    {
        BaseAddress = new Uri("https://10.0.0.9"),
        AcceptAnyServerCertificate = true,
    };

    private static FakeDeviceHandler DeviceWithSession() =>
        new FakeDeviceHandler().OnJson("login.fcgi", """{"session":"rep"}""");

    [Fact]
    public async Task Afd_from_nsr_uses_mode_671_and_returns_raw_text()
    {
        const string afd = "0000000011...\r\n0000000023...\r\n";
        var device = DeviceWithSession().OnJson("get_afd.fcgi", afd);
        using var client = new RepClient(Options, RepMode.Portaria671, device.CreateClient());

        var result = await client.GetAfdFromNsrAsync(14);

        Assert.Equal(afd, result);
        var request = device.RequestsTo("get_afd.fcgi").Single();
        Assert.Equal("?session=rep&mode=671", request.Uri.Query);
        Assert.Equal("""{"initial_nsr":14}""", request.Body);
    }

    [Fact]
    public async Task Legacy_mode_does_not_send_mode_parameter()
    {
        var device = DeviceWithSession().OnJson("get_afd.fcgi", "x");
        using var client = new RepClient(Options, RepMode.Legacy, device.CreateClient());

        await client.GetAfdFromDateAsync(new DateTime(2026, 10, 1));

        var request = device.RequestsTo("get_afd.fcgi").Single();
        Assert.Equal("?session=rep", request.Uri.Query);
        Assert.Equal("""{"initial_date":{"year":2026,"month":10,"day":1}}""", request.Body);
    }

    [Fact]
    public async Task GetAllUsers_pages_until_count()
    {
        var page1 = JsonSerializer.Serialize(new
        {
            count = 150,
            users = Enumerable.Range(1, 100).Select(i => new { cpf = 10000000000L + i, name = $"U{i}" }),
        });
        var page2 = JsonSerializer.Serialize(new
        {
            count = 150,
            users = Enumerable.Range(101, 50).Select(i => new { cpf = 10000000000L + i, name = $"U{i}" }),
        });

        var device = DeviceWithSession().OnJson("load_users.fcgi", page1).OnJson("load_users.fcgi", page2);
        using var client = new RepClient(Options, RepMode.Portaria671, device.CreateClient());

        var users = await client.GetAllUsersAsync();

        Assert.Equal(150, users.Count);
        Assert.Equal(
            new[] { """{"limit":100,"offset":0}""", """{"limit":100,"offset":100}""" },
            device.RequestsTo("load_users.fcgi").Select(r => r.Body));
    }

    [Fact]
    public async Task GetDateTime_maps_fields()
    {
        var device = DeviceWithSession().OnJson(
            "get_system_date_time.fcgi", """{"day":10,"month":10,"year":2026,"hour":18,"minute":5,"second":30}""");
        using var client = new RepClient(Options, RepMode.Portaria671, device.CreateClient());

        Assert.Equal(new DateTime(2026, 10, 10, 18, 5, 30), await client.GetDateTimeAsync());
    }

    [Fact]
    public async Task LoadUsers_rejects_page_size_above_100()
    {
        using var client = new RepClient(Options, RepMode.Portaria671, new FakeDeviceHandler().CreateClient());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.LoadUsersAsync(limit: 101));
    }
}
