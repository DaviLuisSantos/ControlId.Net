using System.Net;
using System.Text;
using System.Text.Json;
using ControlId.Access;

namespace ControlId.Tests;

/// <summary>
/// <c>CreateOrModifyObjectsAsync</c> em equipamento sem <c>create_or_modify_objects</c> (iDFace 6.9.12): cai para
/// <c>modify_objects</c> por id e <c>create_objects</c> só para o que não existia (#33, #35).
/// </summary>
public class UpsertFallbackTests
{
    public enum UpsertMode { Native, InvalidCommand, NotFound }

    private static ControlIdDeviceOptions Options(int? maxObjects = null)
    {
        var options = new ControlIdDeviceOptions { BaseAddress = new Uri("http://10.0.0.5"), Login = "admin", Password = "admin" };
        if (maxObjects is { } max)
        {
            options.MaxObjectsPerRequest = max;
        }

        return options;
    }

    private static Dictionary<string, object> Item(long id, string name) => new() { ["id"] = id, ["name"] = name };

    /// <summary>Equipamento em memória: uma tabela id -> nome, com ou sem o upsert nativo.</summary>
    private sealed class StatefulDevice : HttpMessageHandler
    {
        private readonly UpsertMode _mode;
        private readonly string _firmware;

        public StatefulDevice(UpsertMode mode, string firmware = "6.13.1")
        {
            _mode = mode;
            _firmware = firmware;
        }

        public Dictionary<long, string> Rows { get; } = new();

        public List<(string Endpoint, string Body)> Calls { get; } = new();

        public int Count(string endpoint) => Calls.Count(c => c.Endpoint == endpoint);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync();
            var endpoint = request.RequestUri!.AbsolutePath.TrimStart('/');
            Calls.Add((endpoint, body));

            switch (endpoint)
            {
                case "login.fcgi":
                    return Json("""{"session":"s"}""");
                case "system_information.fcgi":
                    return Json("{\"version\":\"" + _firmware + "\",\"model\":\"iDFace\"}");
                case "create_or_modify_objects.fcgi":
                    return UpsertNative(body);
                case "modify_objects.fcgi":
                    return Modify(body);
                case "create_objects.fcgi":
                    return Create(body);
                default:
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        }

        private HttpResponseMessage UpsertNative(string body)
        {
            if (_mode == UpsertMode.InvalidCommand)
            {
                return Json("""{"error":"Invalid command","code":1}""", HttpStatusCode.BadRequest);
            }

            if (_mode == UpsertMode.NotFound)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            using var doc = JsonDocument.Parse(body);
            var changes = 0;
            foreach (var value in doc.RootElement.GetProperty("values").EnumerateArray())
            {
                Rows[value.GetProperty("id").GetInt64()] = value.GetProperty("name").GetString()!;
                changes++;
            }

            return Json("{\"changes\":" + changes + "}");
        }

        private HttpResponseMessage Modify(string body)
        {
            using var doc = JsonDocument.Parse(body);
            var id = doc.RootElement.GetProperty("where").GetProperty("users").GetProperty("id").GetInt64();
            if (!Rows.ContainsKey(id))
            {
                return Json("""{"changes":0}""");
            }

            Rows[id] = doc.RootElement.GetProperty("values").GetProperty("name").GetString()!;
            return Json("""{"changes":1}""");
        }

        private HttpResponseMessage Create(string body)
        {
            using var doc = JsonDocument.Parse(body);
            var ids = new List<long>();
            foreach (var value in doc.RootElement.GetProperty("values").EnumerateArray())
            {
                var id = value.GetProperty("id").GetInt64();
                Rows.Add(id, value.GetProperty("name").GetString()!);
                ids.Add(id);
            }

            return Json("{\"ids\":[" + string.Join(",", ids) + "]}");
        }

        private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
            new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    [Fact]
    public async Task Native_upsert_is_used_when_the_device_has_it()
    {
        var device = new StatefulDevice(UpsertMode.Native);
        using var client = new AccessClient(Options(), new HttpClient(device));

        var changes = await client.CreateOrModifyObjectsAsync("users", new[] { Item(1, "a"), Item(2, "b") });

        Assert.Equal(2, changes);
        Assert.Equal(1, device.Count("create_or_modify_objects.fcgi"));
        Assert.Equal(0, device.Count("modify_objects.fcgi"));
        Assert.Equal(2, device.Rows.Count);
    }

    [Theory]
    [InlineData(UpsertMode.InvalidCommand)]
    [InlineData(UpsertMode.NotFound)]
    public async Task Falls_back_to_modify_and_create_when_the_command_is_refused(UpsertMode mode)
    {
        var device = new StatefulDevice(mode);
        device.Rows[2] = "antigo";
        using var client = new AccessClient(Options(), new HttpClient(device));

        var changes = await client.CreateOrModifyObjectsAsync("users", new[] { Item(1, "a"), Item(2, "b"), Item(3, "c") });

        Assert.Equal(3, changes);
        Assert.Equal(1, device.Count("create_or_modify_objects.fcgi"));
        Assert.Equal(3, device.Count("modify_objects.fcgi"));
        Assert.Equal(1, device.Count("create_objects.fcgi"));
        Assert.Equal(new Dictionary<long, string> { [1] = "a", [2] = "b", [3] = "c" }, device.Rows);
    }

    [Fact]
    public async Task Repeating_the_upsert_does_not_change_the_state_and_does_not_retry_the_refused_command()
    {
        var device = new StatefulDevice(UpsertMode.InvalidCommand);
        using var client = new AccessClient(Options(), new HttpClient(device));
        var values = new[] { Item(1, "a"), Item(2, "b") };

        await client.CreateOrModifyObjectsAsync("users", values);
        var snapshot = device.Rows.ToDictionary(r => r.Key, r => r.Value);
        var creates = device.Count("create_objects.fcgi");
        var changes = await client.CreateOrModifyObjectsAsync("users", values);

        Assert.Equal(2, changes);
        Assert.Equal(snapshot, device.Rows);
        Assert.Equal(creates, device.Count("create_objects.fcgi"));
        Assert.Equal(1, device.Count("create_or_modify_objects.fcgi"));
    }

    [Fact]
    public async Task A_command_pre_marked_by_GetCapabilities_goes_straight_to_the_fallback()
    {
        var device = new StatefulDevice(UpsertMode.Native, firmware: "6.9.12");
        using var client = new AccessClient(Options(), new HttpClient(device));
        await client.GetCapabilitiesAsync();

        var changes = await client.CreateOrModifyObjectsAsync("users", new[] { Item(1, "a"), Item(2, "b") });

        Assert.Equal(2, changes);
        Assert.Equal(0, device.Count("create_or_modify_objects.fcgi"));
        Assert.Equal(2, device.Count("modify_objects.fcgi"));
        Assert.Equal(1, device.Count("create_objects.fcgi"));
        Assert.Equal(2, device.Rows.Count);
    }

    [Fact]
    public async Task Fallback_creates_in_batches_of_MaxObjectsPerRequest()
    {
        var device = new StatefulDevice(UpsertMode.InvalidCommand);
        using var client = new AccessClient(Options(maxObjects: 2), new HttpClient(device));

        var changes = await client.CreateOrModifyObjectsAsync("users", Enumerable.Range(1, 5).Select(i => Item(i, "n" + i)).ToList());

        Assert.Equal(5, changes);
        Assert.Equal(5, device.Count("modify_objects.fcgi"));
        Assert.Equal(3, device.Count("create_objects.fcgi"));
        Assert.Equal(5, device.Rows.Count);
    }

    [Fact]
    public async Task Fallback_accepts_typed_objects_with_an_id()
    {
        var device = new StatefulDevice(UpsertMode.InvalidCommand);
        device.Rows[7] = "antigo";
        using var client = new AccessClient(Options(), new HttpClient(device));

        var changes = await client.CreateOrModifyObjectsAsync(
            "users", new[] { new AccessUser { Id = 7, Name = "novo" }, new AccessUser { Id = 8, Name = "outro" } });

        Assert.Equal(2, changes);
        Assert.Equal("novo", device.Rows[7]);
        Assert.Equal("outro", device.Rows[8]);
    }

    [Fact]
    public async Task Fallback_without_an_id_throws_ArgumentException_before_modifying_anything()
    {
        var device = new StatefulDevice(UpsertMode.InvalidCommand);
        using var client = new AccessClient(Options(), new HttpClient(device));
        var values = new[] { Item(1, "a"), new Dictionary<string, object> { ["name"] = "sem id" } };

        await Assert.ThrowsAsync<ArgumentException>(() => client.CreateOrModifyObjectsAsync("users", values));

        Assert.Equal(0, device.Count("modify_objects.fcgi"));
        Assert.Equal(0, device.Count("create_objects.fcgi"));
        Assert.Empty(device.Rows);
    }
}
