using System.Text.Json;
using ControlId.Access;
using ControlId.Access.Push;

namespace ControlId.Tests;

public class PushServerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public void Empty_queue_answers_empty_body()
    {
        var server = new PushServer();

        Assert.Equal(string.Empty, server.HandlePush(1, "u1"));
        Assert.NotNull(server.GetLastSeen(1));
        Assert.Null(server.GetLastSeen(2));
    }

    [Fact]
    public async Task Delivers_command_and_completes_with_result()
    {
        var server = new PushServer();
        var users = server.Device(1).LoadObjectsAsync<AccessUser>(new LoadObjectsRequest("users"));

        Assert.Equal(
            """{"verb":"POST","endpoint":"load_objects","body":{"object":"users"},"contentType":"application/json"}""",
            server.HandlePush(1, "u1"));
        Assert.False(users.IsCompleted);

        Assert.True(server.HandleResult(1, "u1", """{"response":{"users":[{"id":1,"name":"Walter White"}]}}"""));

        var result = await users.WaitAsync(Timeout);
        Assert.Equal("Walter White", Assert.Single(result).Name);
        Assert.Equal(0, server.GetPendingCount(1));
    }

    [Fact]
    public async Task Commands_go_one_at_a_time_in_order()
    {
        var server = new PushServer();
        var first = server.SendAsync(1, new PushCommand("first.fcgi"));
        var second = server.SendAsync(1, new PushCommand("second"));

        Assert.Equal(2, server.GetPendingCount(1));
        Assert.Contains("\"endpoint\":\"first\"", server.HandlePush(1, "a"));
        server.HandleResult(1, null, "{}");
        Assert.Contains("\"endpoint\":\"second\"", server.HandlePush(1, "b"));
        server.HandleResult(1, null, "{}");

        Assert.Equal("first", (await first.WaitAsync(Timeout)).Endpoint);
        Assert.Equal("second", (await second.WaitAsync(Timeout)).Endpoint);
    }

    [Fact]
    public void Queues_are_per_device()
    {
        var server = new PushServer();
        _ = server.SendAsync(1, new PushCommand("reboot"));

        Assert.Equal(string.Empty, server.HandlePush(2, "x"));
        Assert.NotEqual(string.Empty, server.HandlePush(1, "y"));
    }

    [Fact]
    public async Task Response_serialized_as_string_is_unwrapped()
    {
        var server = new PushServer();
        var task = server.Device(1).CreateObjectsAsync("users", new[] { new AccessUser { Name = "x" } });

        server.HandlePush(1, "u");
        server.HandleResult(1, "u", """{"response":"{\"ids\":[42]}"}""");

        Assert.Equal(new long[] { 42 }, await task.WaitAsync(Timeout));
    }

    [Fact]
    public async Task Device_error_is_reported()
    {
        var server = new PushServer();
        var task = server.Device(1).OpenDoorAsync(1);

        server.HandlePush(1, "u");
        server.HandleResult(1, "u", """{"error":"Invalid action"}""");

        var ex = await Assert.ThrowsAsync<ControlIdException>(() => task.WaitAsync(Timeout));
        Assert.Contains("Invalid action", ex.Message);
    }

    [Fact]
    public async Task Result_with_other_uuid_is_ignored()
    {
        var server = new PushServer();
        var task = server.SendAsync(1, new PushCommand("x"));

        server.HandlePush(1, "right");

        Assert.False(server.HandleResult(1, "wrong", "{}"));
        Assert.False(task.IsCompleted);
        Assert.True(server.HandleResult(1, "right", "{}"));
        await task.WaitAsync(Timeout);
    }

    [Fact]
    public void Result_without_pending_command_returns_false()
    {
        var server = new PushServer();

        Assert.False(server.HandleResult(1, "u", "{}"));
    }

    [Fact]
    public async Task New_poll_without_result_fails_the_orphan()
    {
        var server = new PushServer();
        var task = server.SendAsync(1, new PushCommand("x"));

        server.HandlePush(1, "a");
        server.HandlePush(1, "b"); // equipamento reiniciou sem enviar /result

        await Assert.ThrowsAsync<ControlIdException>(() => task.WaitAsync(Timeout));
    }

    [Fact]
    public async Task Cancelling_before_delivery_removes_from_queue()
    {
        var server = new PushServer();
        using var cts = new CancellationTokenSource();
        var task = server.SendAsync(1, new PushCommand("x"), cts.Token);

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task.WaitAsync(Timeout));
        Assert.Equal(0, server.GetPendingCount(1));
        Assert.Equal(string.Empty, server.HandlePush(1, "u"));
    }

    [Fact]
    public async Task Invalid_result_body_fails_the_command()
    {
        var server = new PushServer();
        var task = server.SendAsync(1, new PushCommand("x"));

        server.HandlePush(1, "u");
        server.HandleResult(1, "u", "not json");

        await Assert.ThrowsAsync<ControlIdException>(() => task.WaitAsync(Timeout));
    }

    [Fact]
    public void SetUserImage_command_uses_base64_octet_stream()
    {
        var server = new PushServer();
        _ = server.SendAsync(1, PushCommand.SetUserImage(7, new byte[] { 1, 2, 3 }, timestamp: DateTimeOffset.FromUnixTimeSeconds(1624997578)));

        using var json = JsonDocument.Parse(server.HandlePush(1, "u"));
        var root = json.RootElement;
        Assert.Equal("user_set_image", root.GetProperty("endpoint").GetString());
        Assert.Equal("AQID", root.GetProperty("body").GetString());
        Assert.Equal("application/octet-stream", root.GetProperty("contentType").GetString());
        Assert.Equal("user_id=7&timestamp=1624997578&match=0", root.GetProperty("queryString").GetString());
    }

    [Fact]
    public async Task Batch_sends_transactions_and_maps_results_in_order()
    {
        var server = new PushServer();
        var task = server.Device(1).SendBatchAsync(new[]
        {
            PushCommand.SetConfiguration(new { general = new { language = "en_US" } }),
            new PushCommand("message_to_screen", new { message = "hello world!", timeout = 5000 }),
            PushCommand.LoadObjects(new LoadObjectsRequest("users")),
        });

        Assert.Equal(1, server.GetPendingCount(1));
        Assert.Equal(
            """{"transactions":[""" +
            """{"transactionid":"1","verb":"POST","endpoint":"set_configuration","body":{"general":{"language":"en_US"}},"contentType":"application/json"},""" +
            """{"transactionid":"2","verb":"POST","endpoint":"message_to_screen","body":{"message":"hello world!","timeout":5000},"contentType":"application/json"},""" +
            """{"transactionid":"3","verb":"POST","endpoint":"load_objects","body":{"object":"users"},"contentType":"application/json"}]}""",
            server.HandlePush(1, "u"));

        // Fora de ordem, com response em string e uma falha.
        Assert.True(server.HandleResult(1, "u", """
            {"transactions_results":[
              {"transactionid":3,"success":true,"response":"{\"users\":[{\"id\":5}]}"},
              {"transactionid":1,"success":true,"response":"{}"},
              {"transactionid":"2","success":false,"response":"Invalid message"}
            ]}
            """));

        var results = await task.WaitAsync(Timeout);
        Assert.Equal(new[] { "set_configuration", "message_to_screen", "load_objects" }, results.Select(r => r.Endpoint));
        Assert.True(results[0].Success);
        Assert.False(results[1].Success);
        Assert.Equal("Invalid message", results[1].Error);
        Assert.Equal(5, results[2].ResponseAs<Dictionary<string, List<AccessUser>>>()["users"][0].Id);
    }

    [Fact]
    public async Task Batch_accepts_singular_results_key_and_flags_missing_items()
    {
        var server = new PushServer();
        var task = server.SendBatchAsync(1, new[] { new PushCommand("a"), new PushCommand("b") });

        server.HandlePush(1, "u");
        server.HandleResult(1, "u", """{"transaction_results":[{"transactionid":1,"success":true,"response":"{}"}]}""");

        var results = await task.WaitAsync(Timeout);
        Assert.True(results[0].Success);
        Assert.False(results[1].Success);
        Assert.Throws<ControlIdException>(() => results[1].EnsureSuccess());
    }

    [Fact]
    public async Task Batch_general_error_fails_every_command()
    {
        var server = new PushServer();
        var task = server.SendBatchAsync(1, new[] { new PushCommand("a"), new PushCommand("b") });

        server.HandlePush(1, "u");
        server.HandleResult(1, "u", """{"error":"Too many transactions"}""");

        var results = await task.WaitAsync(Timeout);
        Assert.All(results, r => Assert.Equal("Too many transactions", r.Error));
    }

    [Fact]
    public async Task Single_and_batch_share_the_queue()
    {
        var server = new PushServer();
        var single = server.SendAsync(1, new PushCommand("single"));
        var batch = server.SendBatchAsync(1, new[] { new PushCommand("a") });

        Assert.Contains("\"endpoint\":\"single\"", server.HandlePush(1, "1"));
        server.HandleResult(1, "1", "{}");
        Assert.Contains("\"transactions\"", server.HandlePush(1, "2"));
        server.HandleResult(1, "2", """{"transactions_results":[{"transactionid":1,"success":true}]}""");

        Assert.True((await single.WaitAsync(Timeout)).Success);
        Assert.True(Assert.Single(await batch.WaitAsync(Timeout)).Success);
    }

    [Fact]
    public void Empty_batch_is_rejected()
    {
        var server = new PushServer();

        // Validação de argumento é síncrona, como no resto do .NET.
        Assert.Throws<ArgumentException>(() => { _ = server.SendBatchAsync(1, Array.Empty<PushCommand>()); });
    }
}
