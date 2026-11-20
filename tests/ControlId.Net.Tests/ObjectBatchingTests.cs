using System.Text.Json;
using ControlId.Access;

namespace ControlId.Tests;

/// <summary><c>create_objects</c> e <c>create_or_modify_objects</c> em lotes de <see cref="ControlIdDeviceOptions.MaxObjectsPerRequest"/> (#33).</summary>
public class ObjectBatchingTests
{
    private static ControlIdDeviceOptions Options(int? maxObjects = null)
    {
        var options = new ControlIdDeviceOptions { BaseAddress = new Uri("http://10.0.0.5"), Login = "admin", Password = "admin" };
        if (maxObjects is { } max)
        {
            options.MaxObjectsPerRequest = max;
        }

        return options;
    }

    private static FakeDeviceHandler LoggedDevice() => new FakeDeviceHandler().OnJson("login.fcgi", """{"session":"s"}""");

    private static List<Dictionary<string, object>> Items(int count) =>
        Enumerable.Range(1, count).Select(i => new Dictionary<string, object> { ["id"] = i, ["name"] = "u" + i }).ToList();

    private static int ValueCount(string body)
    {
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("values").GetArrayLength();
    }

    [Fact]
    public async Task CreateObjects_with_450_items_sends_three_requests_and_joins_the_ids()
    {
        var device = LoggedDevice()
            .OnJson("create_objects.fcgi", """{"ids":[1,2]}""")
            .OnJson("create_objects.fcgi", """{"ids":[3]}""")
            .OnJson("create_objects.fcgi", """{"ids":[4,5,6]}""");
        using var client = new AccessClient(Options(), device.CreateClient());

        var ids = await client.CreateObjectsAsync("users", Items(450));

        var requests = device.RequestsTo("create_objects.fcgi").ToList();
        Assert.Equal(new long[] { 1, 2, 3, 4, 5, 6 }, ids);
        Assert.Equal(new[] { 200, 200, 50 }, requests.Select(r => ValueCount(r.Body)));
    }

    [Fact]
    public async Task CreateObjects_up_to_the_limit_is_a_single_request()
    {
        var device = LoggedDevice().OnJson("create_objects.fcgi", """{"ids":[7]}""");
        using var client = new AccessClient(Options(), device.CreateClient());

        var ids = await client.CreateObjectsAsync("users", Items(200));

        Assert.Equal(new long[] { 7 }, ids);
        Assert.Equal(200, ValueCount(device.RequestsTo("create_objects.fcgi").Single().Body));
    }

    [Fact]
    public async Task CreateObjects_honors_a_custom_MaxObjectsPerRequest()
    {
        var device = LoggedDevice().OnJson("create_objects.fcgi", """{"ids":[1]}""");
        using var client = new AccessClient(Options(maxObjects: 2), device.CreateClient());

        await client.CreateObjectsAsync("users", Items(5));

        Assert.Equal(new[] { 2, 2, 1 }, device.RequestsTo("create_objects.fcgi").Select(r => ValueCount(r.Body)));
    }

    [Fact]
    public async Task CreateObjects_with_an_empty_list_keeps_sending_one_request()
    {
        var device = LoggedDevice().OnJson("create_objects.fcgi", """{"ids":[]}""");
        using var client = new AccessClient(Options(), device.CreateClient());

        var ids = await client.CreateObjectsAsync("users", Array.Empty<Dictionary<string, object>>());

        Assert.Empty(ids);
        Assert.Single(device.RequestsTo("create_objects.fcgi"));
    }

    [Fact]
    public async Task CreateOrModifyObjects_with_450_items_sends_three_requests_and_sums_the_changes()
    {
        var device = LoggedDevice()
            .OnJson("create_or_modify_objects.fcgi", """{"changes":200}""")
            .OnJson("create_or_modify_objects.fcgi", """{"changes":199}""")
            .OnJson("create_or_modify_objects.fcgi", """{"changes":50}""");
        using var client = new AccessClient(Options(), device.CreateClient());

        var changes = await client.CreateOrModifyObjectsAsync("users", Items(450));

        Assert.Equal(449, changes);
        Assert.Equal(new[] { 200, 200, 50 }, device.RequestsTo("create_or_modify_objects.fcgi").Select(r => ValueCount(r.Body)));
    }

    [Fact]
    public async Task CreateOrModifyObjects_up_to_the_limit_is_a_single_request()
    {
        var device = LoggedDevice().OnJson("create_or_modify_objects.fcgi", """{"changes":200}""");
        using var client = new AccessClient(Options(), device.CreateClient());

        var changes = await client.CreateOrModifyObjectsAsync("users", Items(200));

        Assert.Equal(200, changes);
        Assert.Single(device.RequestsTo("create_or_modify_objects.fcgi"));
    }
}
