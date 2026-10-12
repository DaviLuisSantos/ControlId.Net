using System.Net;
using System.Text;
using System.Text.Json;
using ControlId.Access;

namespace ControlId.Tests;

/// <summary>Equipamento simulado com uma tabela de verdade: aplica <c>id &gt;</c>, <c>limit</c> e <c>offset</c> do <c>load_objects</c>.</summary>
internal sealed class PagedDevice : HttpMessageHandler
{
    private readonly string _object;
    private readonly Func<long, string> _row;

    public PagedDevice(string @object, IEnumerable<long> ids, Func<long, string>? row = null)
    {
        _object = @object;
        Ids = ids.ToList();
        _row = row ?? (id => "{\"id\":" + id + "}");
    }

    public List<long> Ids { get; }

    /// <summary>Corpos dos <c>load_objects</c> recebidos.</summary>
    public List<JsonElement> Loads { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri!.AbsolutePath.EndsWith("login.fcgi", StringComparison.Ordinal))
        {
            return Json("{\"session\":\"s\"}");
        }

        var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync()).RootElement.Clone();
        Loads.Add(body);

        IEnumerable<long> rows = Ids;
        if (body.TryGetProperty("where", out var where) && where.TryGetProperty(_object, out var filter)
            && filter.TryGetProperty("id", out var id) && id.TryGetProperty(">", out var after))
        {
            rows = rows.Where(r => r > after.GetInt64());
        }

        if (body.TryGetProperty("limit", out var limit))
        {
            var offset = body.TryGetProperty("offset", out var o) ? o.GetInt32() : 0;
            rows = rows.Skip(offset).Take(limit.GetInt32());
        }

        return Json("{\"" + _object + "\":[" + string.Join(",", rows.Select(_row)) + "]}");
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}

public class PagingTests
{
    private static AccessClient Client(HttpMessageHandler device) =>
        new(new ControlIdDeviceOptions { BaseAddress = new Uri("http://10.0.0.5") }, new HttpClient(device));

    private static async Task<List<T>> ToListAsync<T>(IAsyncEnumerable<T> source)
    {
        var list = new List<T>();
        await foreach (var item in source)
        {
            list.Add(item);
        }

        return list;
    }

    [Fact]
    public async Task Users_are_read_in_pages_by_id()
    {
        var device = new PagedDevice("users", Enumerable.Range(1, 1234).Select(i => (long)i * 2));
        using var client = Client(device);

        var users = await ToListAsync(client.EnumerateUsersAsync(pageSize: 500));

        Assert.Equal(device.Ids, users.Select(u => u.Id!.Value));
        Assert.Equal(3, device.Loads.Count);
        Assert.Equal(
            """{"object":"users","where":{"users":{"id":{">":1000}}},"order":["ascending","id"],"limit":500}""",
            device.Loads[1].GetRawText());
        Assert.Equal(2000, device.Loads[2].GetProperty("where").GetProperty("users").GetProperty("id").GetProperty(">").GetInt64());
    }

    [Fact]
    public async Task Exact_multiple_of_page_size_ends_with_an_empty_page()
    {
        var device = new PagedDevice("users", Enumerable.Range(1, 1000).Select(i => (long)i));
        using var client = Client(device);

        var users = await ToListAsync(client.EnumerateUsersAsync(pageSize: 500));

        Assert.Equal(1000, users.Count);
        Assert.Equal(3, device.Loads.Count);
    }

    [Fact]
    public async Task Pages_are_fetched_only_when_consumed()
    {
        var device = new PagedDevice("users", Enumerable.Range(1, 5000).Select(i => (long)i));
        using var client = Client(device);

        var read = 0;
        await foreach (var _ in client.EnumerateUsersAsync(pageSize: 100))
        {
            if (++read == 150)
            {
                break;
            }
        }

        Assert.Equal(2, device.Loads.Count);
    }

    [Fact]
    public async Task Access_logs_start_after_the_given_id()
    {
        var device = new PagedDevice("access_logs", Enumerable.Range(1, 300).Select(i => (long)i), id => "{\"id\":" + id + ",\"event\":7}");
        using var client = Client(device);

        var logs = await ToListAsync(client.EnumerateAccessLogsAsync(afterId: 120, pageSize: 50));

        Assert.Equal(Enumerable.Range(121, 180).Select(i => (long)i), logs.Select(l => l.Id));
        Assert.Equal(4, device.Loads.Count);
        Assert.Equal(120, device.Loads[0].GetProperty("where").GetProperty("access_logs").GetProperty("id").GetProperty(">").GetInt64());
    }

    [Fact]
    public async Task Generic_request_pages_by_offset_within_its_own_offset_and_limit()
    {
        var device = new PagedDevice("user_groups", Enumerable.Range(1, 100).Select(i => (long)i));
        using var client = Client(device);
        var request = new LoadObjectsRequest("user_groups") { Offset = 10, Limit = 25, Order = new List<string> { "user_id" } };

        var rows = await ToListAsync(client.EnumerateObjectsAsync<JsonElement>(request, pageSize: 10));

        Assert.Equal(Enumerable.Range(11, 25).Select(i => (long)i), rows.Select(r => r.GetProperty("id").GetInt64()));
        Assert.Equal(new[] { (10, 10), (10, 20), (5, 30) }, device.Loads.Select(b => (b.GetProperty("limit").GetInt32(), b.GetProperty("offset").GetInt32())));
        Assert.All(device.Loads, b => Assert.Equal("user_id", b.GetProperty("order")[0].GetString()));
        Assert.Equal(10, request.Offset); // a requisição do chamador não muda
        Assert.Equal(25, request.Limit);
    }

    [Fact]
    public async Task Typed_objects_are_ordered_by_their_key()
    {
        var device = new PagedDevice("timed_alarms", Enumerable.Range(1, 7).Select(i => (long)i));
        using var client = Client(device);

        var alarms = await ToListAsync(client.EnumerateObjectsAsync<TimedAlarm>(pageSize: 3));

        Assert.Equal(7, alarms.Count);
        Assert.Equal(3, device.Loads.Count);
        Assert.All(device.Loads, b => Assert.Equal("""["ascending","id"]""", b.GetProperty("order").GetRawText()));
    }

    [Fact]
    public async Task Page_out_of_order_stops_instead_of_looping()
    {
        var device = new FakeDeviceHandler()
            .OnJson("login.fcgi", """{"session":"s"}""")
            .OnJson("load_objects.fcgi", """{"users":[{"id":1},{"id":1}]}""");
        using var client = Client(device);

        var ex = await Assert.ThrowsAsync<ControlIdException>(() => ToListAsync(client.EnumerateUsersAsync(pageSize: 2)));
        Assert.Contains("fora de ordem", ex.Message);
    }

    [Fact]
    public void Page_size_must_be_positive()
    {
        using var client = Client(new FakeDeviceHandler());

        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = client.EnumerateUsersAsync(pageSize: 0); });
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = client.EnumerateAccessLogsAsync(pageSize: -1); });
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = client.EnumerateObjectsAsync<TimedAlarm>(pageSize: 0); });
        Assert.Throws<ArgumentNullException>(() => { _ = client.EnumerateObjectsAsync<AccessUser>((LoadObjectsRequest)null!); });
    }

    [Fact]
    public async Task Cancellation_stops_the_enumeration()
    {
        var device = new PagedDevice("users", Enumerable.Range(1, 1000).Select(i => (long)i));
        using var client = Client(device);
        using var cts = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in client.EnumerateUsersAsync(pageSize: 100, cancellationToken: cts.Token))
            {
                cts.Cancel();
            }
        });

        Assert.Single(device.Loads);
    }
}
