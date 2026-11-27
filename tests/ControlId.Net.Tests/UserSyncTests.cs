using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using ControlId.Access;

namespace ControlId.Tests;

/// <summary>Sincronização de pessoas pelo estado desejado (#54), contra um equipamento em memória.</summary>
public class UserSyncTests
{
    private static ControlIdDeviceOptions Options() =>
        new() { BaseAddress = new Uri("http://10.0.0.5"), Login = "admin", Password = "admin" };

    private static byte[] Jpeg(int seed) => TestImages.Jpeg(640, 480, 200 + seed);

    /// <summary>Equipamento em memória com users, cards, user_groups, fotos e o upsert nativo.</summary>
    private sealed class SyncDevice : HttpMessageHandler
    {
        private static readonly string[] WriteEndpoints =
        {
            "create_objects.fcgi", "modify_objects.fcgi", "destroy_objects.fcgi", "create_or_modify_objects.fcgi",
            "user_set_image.fcgi", "user_set_image_list.fcgi", "user_hash_password.fcgi",
        };

        private readonly Dictionary<string, List<JsonObject>> _tables = new()
        {
            ["users"] = new List<JsonObject>(),
            ["cards"] = new List<JsonObject>(),
            ["user_groups"] = new List<JsonObject>(),
        };

        private readonly Dictionary<long, byte[]> _images = new();
        private long _nextCardId = 1;
        private int _hashCounter;

        public List<(string Endpoint, string Body)> Calls { get; } = new();

        /// <summary>Resposta forçada para uma chamada (devolve <c>null</c> para seguir o fluxo normal).</summary>
        public Func<string, string, HttpResponseMessage?>? Intercept { get; set; }

        public int Count(string endpoint) => Calls.Count(c => c.Endpoint == endpoint);

        public int WriteCount => Calls.Count(c => WriteEndpoints.Contains(c.Endpoint));

        public List<JsonObject> Table(string name) => _tables[name];

        public JsonObject? User(long id) => _tables["users"].FirstOrDefault(u => (long)u["id"]! == id);

        public void AddUser(long id, string name, Action<JsonObject>? configure = null)
        {
            var user = new JsonObject { ["id"] = id, ["name"] = name };
            configure?.Invoke(user);
            _tables["users"].Add(user);
        }

        public void AddCard(long userId, long value) =>
            _tables["cards"].Add(new JsonObject { ["id"] = _nextCardId++, ["user_id"] = userId, ["value"] = value });

        public void AddGroup(long userId, long groupId) =>
            _tables["user_groups"].Add(new JsonObject { ["user_id"] = userId, ["group_id"] = groupId });

        public IEnumerable<long> CardValues(long userId) =>
            _tables["cards"].Where(c => (long)c["user_id"]! == userId).Select(c => (long)c["value"]!).OrderBy(v => v).ToList();

        public IEnumerable<long> GroupIds(long userId) =>
            _tables["user_groups"].Where(c => (long)c["user_id"]! == userId).Select(c => (long)c["group_id"]!).OrderBy(v => v).ToList();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync();
            var endpoint = request.RequestUri!.AbsolutePath.TrimStart('/');
            Calls.Add((endpoint, body));

            if (endpoint != "login.fcgi" && Intercept?.Invoke(endpoint, body) is { } forced)
            {
                return forced;
            }

            switch (endpoint)
            {
                case "login.fcgi":
                    return Json("""{"session":"s"}""");
                case "load_objects.fcgi":
                    return Load(JsonNode.Parse(body)!.AsObject());
                case "create_objects.fcgi":
                    return Create(JsonNode.Parse(body)!.AsObject());
                case "modify_objects.fcgi":
                    return Modify(JsonNode.Parse(body)!.AsObject());
                case "destroy_objects.fcgi":
                    return Destroy(JsonNode.Parse(body)!.AsObject());
                case "create_or_modify_objects.fcgi":
                    return Upsert(JsonNode.Parse(body)!.AsObject());
                case "user_hash_password.fcgi":
                    _hashCounter++;
                    return Json("{\"password\":\"hash" + _hashCounter + "\",\"salt\":\"salt" + _hashCounter + "\"}");
                case "user_set_image_list.fcgi":
                    return SetImages(JsonNode.Parse(body)!.AsObject());
                case "user_get_image.fcgi":
                    return GetImage(request.RequestUri.Query);
                default:
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        }

        private HttpResponseMessage Load(JsonObject request)
        {
            var table = request["object"]!.GetValue<string>();
            var rows = _tables[table].Where(r => Matches(table, r, request["where"])).Select(r => r.DeepClone()).ToArray();
            return Json(new JsonObject { [table] = new JsonArray(rows) }.ToJsonString());
        }

        private HttpResponseMessage Create(JsonObject request)
        {
            var table = request["object"]!.GetValue<string>();
            var ids = new JsonArray();
            foreach (var value in request["values"]!.AsArray())
            {
                var row = (JsonObject)value!.DeepClone();
                if (table == "cards")
                {
                    row["id"] = _nextCardId++;
                    ids.Add((JsonNode)(long)row["id"]!);
                }
                else if (table == "users" && row["id"] is { } id)
                {
                    ids.Add((JsonNode)(long)id);
                }

                _tables[table].Add(row);
            }

            return Json(new JsonObject { ["ids"] = ids }.ToJsonString());
        }

        private HttpResponseMessage Modify(JsonObject request)
        {
            var table = request["object"]!.GetValue<string>();
            var changes = 0;
            foreach (var row in _tables[table].Where(r => Matches(table, r, request["where"])).ToList())
            {
                foreach (var pair in request["values"]!.AsObject())
                {
                    row[pair.Key] = pair.Value?.DeepClone();
                }

                changes++;
            }

            return Json("{\"changes\":" + changes + "}");
        }

        private HttpResponseMessage Destroy(JsonObject request)
        {
            var table = request["object"]!.GetValue<string>();
            var removed = _tables[table].Where(r => Matches(table, r, request["where"])).ToList();
            foreach (var row in removed)
            {
                _tables[table].Remove(row);
                if (table == "users")
                {
                    // O equipamento leva a foto, os cartões e os vínculos junto com o usuário.
                    var id = (long)row["id"]!;
                    _images.Remove(id);
                    _tables["cards"].RemoveAll(c => (long)c["user_id"]! == id);
                    _tables["user_groups"].RemoveAll(g => (long)g["user_id"]! == id);
                }
            }

            return Json("{\"changes\":" + removed.Count + "}");
        }

        private HttpResponseMessage Upsert(JsonObject request)
        {
            var table = request["object"]!.GetValue<string>();
            var changes = 0;
            foreach (var value in request["values"]!.AsArray())
            {
                var item = value!.AsObject();
                var existing = _tables[table].FirstOrDefault(r => (long)r["id"]! == (long)item["id"]!);
                if (existing is null)
                {
                    _tables[table].Add((JsonObject)item.DeepClone());
                }
                else
                {
                    foreach (var pair in item)
                    {
                        existing[pair.Key] = pair.Value?.DeepClone();
                    }
                }

                changes++;
            }

            return Json("{\"changes\":" + changes + "}");
        }

        private HttpResponseMessage SetImages(JsonObject request)
        {
            var results = new JsonArray();
            foreach (var item in request["user_images"]!.AsArray())
            {
                var userId = (long)item!["user_id"]!;
                var user = User(userId);
                if (user is null)
                {
                    results.Add(new JsonObject { ["user_id"] = userId, ["success"] = false, ["errors"] = new JsonArray(new JsonObject { ["code"] = 2, ["message"] = "User not found" }) });
                    continue;
                }

                _images[userId] = Convert.FromBase64String(item["image"]!.GetValue<string>());
                user["image_timestamp"] = (long)item["timestamp"]!;
                results.Add(new JsonObject { ["user_id"] = userId, ["success"] = true, ["errors"] = new JsonArray() });
            }

            return Json(new JsonObject { ["results"] = results }.ToJsonString());
        }

        private HttpResponseMessage GetImage(string query)
        {
            var userId = long.Parse(query.TrimStart('?').Split('&').First(p => p.StartsWith("user_id=", StringComparison.Ordinal)).Substring("user_id=".Length));
            if (!_images.TryGetValue(userId, out var image))
            {
                return Json("""{"error":"Image not found","code":4}""", HttpStatusCode.BadRequest);
            }

            var content = new ByteArrayContent(image);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }

        private static bool Matches(string table, JsonObject row, JsonNode? where)
        {
            if (where is not JsonObject conditions || conditions[table] is not JsonObject fields)
            {
                return true;
            }

            foreach (var pair in fields)
            {
                var actual = (long?)row[pair.Key];
                if (pair.Value is JsonObject operators)
                {
                    if (operators["IN"] is JsonArray list && !list.Any(v => (long)v! == actual))
                    {
                        return false;
                    }

                    if (operators[">"] is { } greater && !(actual > (long)greater))
                    {
                        return false;
                    }
                }
                else if (actual != (long?)pair.Value)
                {
                    return false;
                }
            }

            return true;
        }

        public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
            new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    private static DesiredUser Person(long id, Action<DesiredUser>? configure = null)
    {
        var user = new DesiredUser { Id = id, Name = "Pessoa " + id };
        configure?.Invoke(user);
        return user;
    }

    [Fact]
    public async Task Second_run_with_the_same_state_writes_nothing()
    {
        var device = new SyncDevice();
        using var client = new AccessClient(Options(), new HttpClient(device));
        var people = new[]
        {
            Person(1, p => { p.Registration = "R1"; p.Password = "1234"; p.Cards = new long[] { 111, 112 }; p.GroupIds = new long[] { 1, 2 }; p.Photo = Jpeg(1); p.BeginTime = new DateTime(2026, 1, 2, 8, 0, 0); }),
            Person(2, p => { p.Cards = new long[] { 221 }; p.Photo = Jpeg(2); }),
        };

        var first = await client.SyncUsersAsync(people);

        Assert.All(first, r => Assert.True(r.Success));
        Assert.All(first, r => Assert.True(r.Created));
        Assert.All(first, r => Assert.Equal(UserSyncStatus.Created, r.Status));
        Assert.Equal(new long[] { 111, 112 }, device.CardValues(1));
        Assert.Equal(new long[] { 1, 2 }, device.GroupIds(1));
        Assert.Equal("hash1", (string?)device.User(1)!["password"]);
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 8, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(), (long)device.User(1)!["begin_time"]!);
        Assert.Equal(new[] { true, true }, first.Select(r => r.PhotoUpdated));
        var writes = device.WriteCount;

        var second = await client.SyncUsersAsync(people);

        Assert.Equal(writes, device.WriteCount);
        Assert.All(second, r => Assert.True(r.Success));
        Assert.All(second, r => Assert.False(r.Changed));
        Assert.All(second, r => Assert.Equal(UserSyncStatus.Unchanged, r.Status));
        Assert.Equal(1, device.Count("user_hash_password.fcgi"));
    }

    [Fact]
    public async Task A_fresh_client_rewrites_only_the_password_and_the_photo_it_cannot_compare()
    {
        var device = new SyncDevice();
        var people = new[] { Person(1, p => { p.Password = "1234"; p.Photo = Jpeg(1); }) };
        using (var first = new AccessClient(Options(), new HttpClient(device)))
        {
            await first.SyncUsersAsync(people);
        }

        using var fresh = new AccessClient(Options(), new HttpClient(device));
        var imageWrites = device.Count("user_set_image_list.fcgi");

        var result = (await fresh.SyncUsersAsync(people)).Single();

        // A foto igual foi baixada e comparada: não foi reenviada. A senha não tem como ser comparada: é regravada.
        Assert.Equal(imageWrites, device.Count("user_set_image_list.fcgi"));
        Assert.False(result.PhotoUpdated);
        Assert.Equal(new[] { "password" }, result.ChangedFields);
        Assert.Equal(1, device.Count("user_get_image.fcgi"));

        var third = (await fresh.SyncUsersAsync(people)).Single();
        Assert.False(third.Changed);
    }

    [Fact]
    public async Task Password_is_hashed_only_when_it_changes()
    {
        var device = new SyncDevice();
        using var client = new AccessClient(Options(), new HttpClient(device));

        await client.SyncUserAsync(Person(1, p => p.Password = "1234"));
        await client.SyncUserAsync(Person(1, p => p.Password = "1234"));
        Assert.Equal(1, device.Count("user_hash_password.fcgi"));

        var changed = await client.SyncUserAsync(Person(1, p => p.Password = "9999"));

        Assert.Equal(2, device.Count("user_hash_password.fcgi"));
        Assert.Equal(new[] { "password" }, changed.ChangedFields);
        Assert.Equal("hash2", (string?)device.User(1)!["password"]);
        Assert.Equal("salt2", (string?)device.User(1)!["salt"]);
    }

    [Fact]
    public async Task Changing_one_field_sends_only_that_field()
    {
        var device = new SyncDevice();
        device.AddUser(1, "Antigo", u => u["registration"] = "R1");
        using var client = new AccessClient(Options(), new HttpClient(device));

        var result = await client.SyncUserAsync(Person(1, p => { p.Name = "Novo"; p.Registration = "R1"; }));

        Assert.Equal(new[] { "name" }, result.ChangedFields);
        Assert.False(result.Created);
        Assert.Equal(UserSyncStatus.Changed, result.Status);
        var body = Assert.Single(device.Calls, c => c.Endpoint == "create_or_modify_objects.fcgi").Body;
        Assert.Contains("\"name\":\"Novo\"", body);
        Assert.DoesNotContain("registration", body);
        Assert.Equal("Novo", (string?)device.User(1)!["name"]);
    }

    [Fact]
    public async Task Cards_and_groups_outside_the_desired_state_are_removed_and_missing_ones_are_created()
    {
        var device = new SyncDevice();
        device.AddUser(1, "Pessoa 1");
        device.AddCard(1, 1);
        device.AddCard(1, 2);
        device.AddCard(1, 3);
        device.AddGroup(1, 1);
        device.AddGroup(1, 2);
        using var client = new AccessClient(Options(), new HttpClient(device));

        var result = (await client.SyncUsersAsync(
            new[] { Person(1, p => { p.Cards = new long[] { 2, 4 }; p.GroupIds = new long[] { 2, 3 }; }) },
            new UserSyncOptions { RemoveUnlisted = true })).Single();

        Assert.True(result.Success);
        Assert.Equal(new long[] { 2, 4 }, device.CardValues(1));
        Assert.Equal(new long[] { 1, 3 }, result.CardsRemoved.OrderBy(v => v));
        Assert.Equal(new long[] { 4 }, result.CardsAdded);
        Assert.Equal(new long[] { 2, 3 }, device.GroupIds(1));
        Assert.Equal(new long[] { 1 }, result.GroupsRemoved);
        Assert.Equal(new long[] { 3 }, result.GroupsAdded);
    }

    [Fact]
    public async Task Surplus_cards_and_groups_are_kept_by_default()
    {
        var device = new SyncDevice();
        device.AddUser(1, "Pessoa 1");
        device.AddCard(1, 1);
        device.AddCard(1, 2);
        device.AddGroup(1, 1);
        device.AddGroup(1, 2);
        using var client = new AccessClient(Options(), new HttpClient(device));

        var result = await client.SyncUserAsync(Person(1, p => { p.Cards = new long[] { 2, 4 }; p.GroupIds = new long[] { 2, 3 }; }));

        Assert.True(result.Success);
        Assert.Equal(new long[] { 1, 2, 4 }, device.CardValues(1));
        Assert.Empty(result.CardsRemoved);
        Assert.Equal(new long[] { 4 }, result.CardsAdded);
        Assert.Equal(new long[] { 1, 2, 3 }, device.GroupIds(1));
        Assert.Empty(result.GroupsRemoved);
        Assert.Equal(new long[] { 3 }, result.GroupsAdded);
        Assert.Equal(UserSyncStatus.Changed, result.Status);
    }

    [Fact]
    public async Task An_empty_desired_list_removes_all_cards_and_groups_only_with_RemoveUnlisted()
    {
        var device = new SyncDevice();
        device.AddUser(1, "Pessoa 1");
        device.AddCard(1, 1);
        device.AddGroup(1, 1);
        using var client = new AccessClient(Options(), new HttpClient(device));

        var kept = await client.SyncUserAsync(Person(1, p => { p.Cards = Array.Empty<long>(); p.GroupIds = Array.Empty<long>(); }));

        Assert.Equal(UserSyncStatus.Unchanged, kept.Status);
        Assert.Equal(new long[] { 1 }, device.CardValues(1));
        Assert.Equal(new long[] { 1 }, device.GroupIds(1));

        var removed = (await client.SyncUsersAsync(
            new[] { Person(1, p => { p.Cards = Array.Empty<long>(); p.GroupIds = Array.Empty<long>(); }) },
            new UserSyncOptions { RemoveUnlisted = true })).Single();

        Assert.Equal(UserSyncStatus.Changed, removed.Status);
        Assert.Empty(device.CardValues(1));
        Assert.Empty(device.GroupIds(1));
        Assert.Equal(new long[] { 1 }, removed.CardsRemoved);
        Assert.Equal(new long[] { 1 }, removed.GroupsRemoved);
    }

    [Fact]
    public async Task Null_cards_groups_and_photo_are_left_alone()
    {
        var device = new SyncDevice();
        device.AddUser(1, "Pessoa 1");
        device.AddCard(1, 9);
        device.AddGroup(1, 9);
        using var client = new AccessClient(Options(), new HttpClient(device));

        var result = await client.SyncUserAsync(Person(1));

        Assert.False(result.Changed);
        Assert.Equal(new long[] { 9 }, device.CardValues(1));
        Assert.Equal(new long[] { 9 }, device.GroupIds(1));
        Assert.Equal(0, device.WriteCount);
        Assert.Equal(1, device.Calls.Count(c => c.Endpoint == "load_objects.fcgi"));
    }

    [Fact]
    public async Task By_default_people_outside_the_desired_list_are_not_removed()
    {
        var device = new SyncDevice();
        device.AddUser(1, "Pessoa 1");
        device.AddUser(2, "Sobrando");
        using var client = new AccessClient(Options(), new HttpClient(device));

        var result = await client.SyncUserAsync(Person(1));

        Assert.True(result.Success);
        Assert.Equal(UserSyncStatus.Unchanged, result.Status);
        Assert.Equal(2, device.Table("users").Count);
        Assert.Equal(0, device.Count("destroy_objects.fcgi"));
    }

    [Fact]
    public async Task Card_owned_by_someone_else_shows_up_only_in_that_persons_report()
    {
        var device = new SyncDevice();
        device.AddUser(9, "Dono");
        device.AddCard(9, 111);
        using var client = new AccessClient(Options(), new HttpClient(device));

        var results = await client.SyncUsersAsync(new[]
        {
            Person(1, p => p.Cards = new long[] { 111, 112 }),
            Person(2, p => p.Cards = new long[] { 300 }),
        });

        var a = results[0];
        var b = results[1];
        Assert.False(a.Success);
        Assert.Equal("card:111", Assert.Single(a.Errors).Item);
        Assert.Equal(new long[] { 112 }, a.CardsAdded);
        Assert.True(b.Success);
        Assert.Equal(new long[] { 300 }, b.CardsAdded);
        Assert.Equal(new long[] { 111 }, device.CardValues(9));
        Assert.Equal(new long[] { 112 }, device.CardValues(1));
    }

    [Fact]
    public async Task Card_refused_by_the_device_does_not_stop_the_rest_of_that_person_or_the_others()
    {
        var device = new SyncDevice
        {
            Intercept = (endpoint, body) => endpoint == "create_objects.fcgi" && body.Contains("\"value\":666")
                ? SyncDevice.Json("""{"error":"Card refused","code":5}""", HttpStatusCode.BadRequest)
                : null,
        };
        using var client = new AccessClient(Options(), new HttpClient(device));

        var results = await client.SyncUsersAsync(new[]
        {
            Person(1, p => p.Cards = new long[] { 666, 1 }),
            Person(2, p => p.Cards = new long[] { 2 }),
        });

        var error = Assert.Single(results[0].Errors);
        Assert.Equal("card:666", error.Item);
        Assert.NotNull(error.Exception);
        Assert.Equal(UserSyncStatus.Failed, results[0].Status);
        Assert.Equal(new long[] { 1 }, results[0].CardsAdded);
        Assert.True(results[1].Success);
        Assert.Equal(new long[] { 1 }, device.CardValues(1));
        Assert.Equal(new long[] { 2 }, device.CardValues(2));
    }

    [Fact]
    public async Task A_person_whose_user_write_fails_is_isolated_and_skipped()
    {
        var device = new SyncDevice
        {
            Intercept = (endpoint, body) => endpoint == "create_or_modify_objects.fcgi" && body.Contains("\"id\":2")
                ? SyncDevice.Json("""{"error":"Constraint failed","code":9}""", HttpStatusCode.BadRequest)
                : null,
        };
        using var client = new AccessClient(Options(), new HttpClient(device));

        var results = await client.SyncUsersAsync(new[]
        {
            Person(1, p => p.Cards = new long[] { 10 }),
            Person(2, p => p.Cards = new long[] { 20 }),
            Person(3, p => p.Cards = new long[] { 30 }),
        });

        Assert.True(results[0].Success);
        Assert.False(results[1].Success);
        Assert.Equal("user", Assert.Single(results[1].Errors).Item);
        Assert.False(results[1].Created);
        Assert.True(results[2].Success);
        Assert.Empty(device.CardValues(2));
        Assert.Equal(new long[] { 10 }, device.CardValues(1));
        Assert.Equal(new long[] { 30 }, device.CardValues(3));
    }

    [Fact]
    public async Task Status_is_OutcomeUnknown_when_a_write_may_have_reached_the_device()
    {
        var device = new SyncDevice
        {
            Intercept = (endpoint, body) =>
            {
                if (endpoint == "create_or_modify_objects.fcgi")
                {
                    throw new TaskCanceledException("O prazo do cliente venceu.");
                }

                return null;
            },
        };
        using var client = new AccessClient(Options(), new HttpClient(device));

        var result = await client.SyncUserAsync(Person(1));

        Assert.False(result.Success);
        Assert.False(result.Created);
        Assert.Equal(UserSyncStatus.OutcomeUnknown, result.Status);
        Assert.IsType<ControlIdOutcomeUnknownException>(Assert.Single(result.Errors).Exception);
    }

    [Fact]
    public async Task Photo_rejected_by_validation_is_reported_only_for_that_person()
    {
        var device = new SyncDevice();
        using var client = new AccessClient(Options(), new HttpClient(device));

        var results = await client.SyncUsersAsync(new[]
        {
            Person(1, p => p.Photo = TestImages.Jpeg(4000, 3000)),
            Person(2, p => p.Photo = Jpeg(2)),
        });

        Assert.False(results[0].Success);
        Assert.Equal("photo", Assert.Single(results[0].Errors).Item);
        Assert.False(results[0].PhotoUpdated);
        Assert.True(results[1].Success);
        Assert.True(results[1].PhotoUpdated);
    }

    [Fact]
    public async Task Changed_photo_is_sent_again_and_unchanged_photo_is_not()
    {
        var device = new SyncDevice();
        using var client = new AccessClient(Options(), new HttpClient(device));
        await client.SyncUserAsync(Person(1, p => p.Photo = Jpeg(1)));

        var same = await client.SyncUserAsync(Person(1, p => p.Photo = Jpeg(1)));
        var other = await client.SyncUserAsync(Person(1, p => p.Photo = Jpeg(7)));

        Assert.False(same.PhotoUpdated);
        Assert.True(other.PhotoUpdated);
        Assert.Equal(2, device.Count("user_set_image_list.fcgi"));
    }

    [Fact]
    public async Task RemoveMissing_removes_the_others_only_with_confirmation()
    {
        var device = new SyncDevice();
        device.AddUser(1, "Pessoa 1");
        device.AddUser(2, "Sobrando");
        using var client = new AccessClient(Options(), new HttpClient(device));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.SyncUsersAsync(new[] { Person(1) }, new UserSyncOptions { RemoveMissing = true }));
        Assert.Equal(2, device.Table("users").Count);
        Assert.Empty(device.Calls);

        var results = await client.SyncUsersAsync(
            new[] { Person(1) },
            new UserSyncOptions { RemoveMissing = true, DataLossConfirmation = DataLossConfirmation.Confirmed });

        Assert.Equal(2, results.Count);
        Assert.Equal(UserSyncStatus.Unchanged, results[0].Status);
        Assert.True(results[1].Removed);
        Assert.Equal(UserSyncStatus.Removed, results[1].Status);
        Assert.Equal(2, results[1].UserId);
        Assert.Equal(1L, (long)Assert.Single(device.Table("users"))["id"]!);
    }

    [Fact]
    public async Task RemoveMissing_frees_a_card_so_it_can_change_owner_in_the_same_run()
    {
        var device = new SyncDevice();
        device.AddUser(9, "Sobrando");
        device.AddCard(9, 111);
        using var client = new AccessClient(Options(), new HttpClient(device));

        var results = await client.SyncUsersAsync(
            new[] { Person(1, p => p.Cards = new long[] { 111 }) },
            new UserSyncOptions { RemoveMissing = true, DataLossConfirmation = DataLossConfirmation.Confirmed });

        Assert.True(results[0].Success);
        Assert.Equal(new long[] { 111 }, results[0].CardsAdded);
    }

    [Fact]
    public async Task Invalid_input_is_rejected_before_any_request()
    {
        var device = new SyncDevice();
        using var client = new AccessClient(Options(), new HttpClient(device));

        await Assert.ThrowsAsync<ArgumentNullException>(() => client.SyncUserAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.SyncUsersAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => client.SyncUserAsync(new DesiredUser { Id = 0, Name = "X" }));
        await Assert.ThrowsAsync<ArgumentException>(() => client.SyncUserAsync(new DesiredUser { Id = 1, Name = " " }));
        await Assert.ThrowsAsync<ArgumentException>(() => client.SyncUserAsync(Person(1, p => p.Photo = new byte[0])));
        await Assert.ThrowsAsync<ArgumentException>(() => client.SyncUsersAsync(new[] { Person(1), Person(1) }));
        Assert.Empty(device.Calls);
    }

    [Fact]
    public async Task Empty_list_makes_no_request()
    {
        var device = new SyncDevice();
        using var client = new AccessClient(Options(), new HttpClient(device));

        var results = await client.SyncUsersAsync(Array.Empty<DesiredUser>());

        Assert.Empty(results);
        Assert.Empty(device.Calls);
    }

    [Fact]
    public async Task Validity_dates_are_written_as_local_wall_clock_and_removed_when_null()
    {
        var device = new SyncDevice();
        using var client = new AccessClient(Options(), new HttpClient(device));
        var begin = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Local);
        var end = new DateTime(2026, 3, 31, 23, 59, 0, DateTimeKind.Utc);

        await client.SyncUserAsync(Person(1, p => { p.BeginTime = begin; p.EndTime = end; }));

        Assert.Equal(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(), (long)device.User(1)!["begin_time"]!);
        Assert.Equal(new DateTimeOffset(2026, 3, 31, 23, 59, 0, TimeSpan.Zero).ToUnixTimeSeconds(), (long)device.User(1)!["end_time"]!);

        var cleared = await client.SyncUserAsync(Person(1));

        Assert.Equal(new[] { "begin_time", "end_time" }, cleared.ChangedFields);
        Assert.Equal(0L, (long)device.User(1)!["begin_time"]!);
        Assert.Equal(0L, (long)device.User(1)!["end_time"]!);
    }
}
