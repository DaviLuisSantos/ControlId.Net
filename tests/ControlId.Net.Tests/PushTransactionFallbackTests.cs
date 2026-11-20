using System.Text.Json;
using ControlId.Access;
using ControlId.Access.Push;

namespace ControlId.Tests;

public class PushTransactionFallbackTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private const string RefusedBatch = """{"error":"Invalid member 'endpoint'"}""";

    private static PushCommand[] ThreeCommands() => new[]
    {
        PushCommand.SetConfiguration(new { general = new { language = "en_US" } }),
        new PushCommand("message_to_screen", new { message = "hello", timeout = 5000 }),
        PushCommand.LoadObjects(new LoadObjectsRequest("users")),
    };

    private static string EndpointOf(string payload)
    {
        using var json = JsonDocument.Parse(payload);
        return json.RootElement.GetProperty("endpoint").GetString()!;
    }

    [Fact]
    public async Task Refused_batch_is_resent_as_individual_commands_with_all_results()
    {
        var server = new PushServer();
        var task = server.SendBatchAsync(1, ThreeCommands());

        Assert.Contains("\"transactions\"", server.HandlePush(1, "b"));
        Assert.True(server.HandleResult(1, "b", RefusedBatch));
        Assert.False(task.IsCompleted);
        Assert.Equal(3, server.GetPendingCount(1));

        var first = server.HandlePush(1, "1");
        Assert.DoesNotContain("transactions", first);
        Assert.DoesNotContain("transactionid", first);
        Assert.Equal(
            """{"verb":"POST","endpoint":"set_configuration","body":{"general":{"language":"en_US"}},"contentType":"application/json"}""",
            first);
        Assert.True(server.HandleResult(1, "1", """{"response":{}}"""));
        Assert.False(task.IsCompleted);

        Assert.Equal("message_to_screen", EndpointOf(server.HandlePush(1, "2")));
        Assert.True(server.HandleResult(1, "2", """{"error":"Invalid message"}"""));
        Assert.False(task.IsCompleted);

        Assert.Equal("load_objects", EndpointOf(server.HandlePush(1, "3")));
        Assert.True(server.HandleResult(1, "3", """{"response":{"users":[{"id":5}]}}"""));

        var results = await task.WaitAsync(Timeout);
        Assert.Equal(new[] { "set_configuration", "message_to_screen", "load_objects" }, results.Select(r => r.Endpoint));
        Assert.True(results[0].Success);
        Assert.False(results[1].Success);
        Assert.Equal("Invalid message", results[1].Error);
        Assert.True(results[2].Success);
        Assert.Equal(5, results[2].Response!.Value.GetProperty("users")[0].GetProperty("id").GetInt32());
        Assert.Equal(0, server.GetPendingCount(1));
    }

    [Fact]
    public async Task Next_batches_of_the_same_device_go_individual_from_the_start()
    {
        var server = new PushServer();
        var first = server.SendBatchAsync(1, ThreeCommands());
        server.HandlePush(1, "b");
        server.HandleResult(1, "b", RefusedBatch);
        for (var i = 0; i < 3; i++)
        {
            server.HandlePush(1, "i" + i);
            server.HandleResult(1, "i" + i, "{}");
        }

        await first.WaitAsync(Timeout);

        var second = server.SendBatchAsync(1, new[] { new PushCommand("a"), new PushCommand("b") });

        Assert.Equal(2, server.GetPendingCount(1));
        Assert.Equal("a", EndpointOf(server.HandlePush(1, "x")));
        server.HandleResult(1, "x", """{"response":{"v":1}}""");
        Assert.Equal("b", EndpointOf(server.HandlePush(1, "y")));
        server.HandleResult(1, "y", "{}");

        var results = await second.WaitAsync(Timeout);
        Assert.Equal(new[] { "a", "b" }, results.Select(r => r.Endpoint));
        Assert.All(results, r => Assert.True(r.Success));
    }

    [Fact]
    public async Task Other_devices_keep_using_transactions()
    {
        var server = new PushServer();
        _ = server.SendBatchAsync(1, ThreeCommands());
        server.HandlePush(1, "b");
        server.HandleResult(1, "b", RefusedBatch);

        var other = server.SendBatchAsync(2, new[] { new PushCommand("a") });

        Assert.Contains("\"transactions\"", server.HandlePush(2, "u"));
        server.HandleResult(2, "u", """{"transactions_results":[{"transactionid":1,"success":true}]}""");
        Assert.True(Assert.Single(await other.WaitAsync(Timeout)).Success);
    }

    [Fact]
    public async Task UseTransactions_false_never_sends_transactions()
    {
        var server = new PushServer(new InMemoryPushCommandStore(), new PushServerOptions { UseTransactions = false });
        var task = server.SendBatchAsync(1, new[] { new PushCommand("a"), new PushCommand("b") });

        Assert.Equal(2, server.GetPendingCount(1));
        Assert.Equal("a", EndpointOf(server.HandlePush(1, "x")));
        server.HandleResult(1, "x", "{}");
        Assert.Equal("b", EndpointOf(server.HandlePush(1, "y")));
        server.HandleResult(1, "y", "{}");

        Assert.Equal(new[] { "a", "b" }, (await task.WaitAsync(Timeout)).Select(r => r.Endpoint));
    }

    [Fact]
    public async Task UseTransactions_true_does_not_fall_back()
    {
        var server = new PushServer(new InMemoryPushCommandStore(), new PushServerOptions { UseTransactions = true });
        var task = server.SendBatchAsync(1, new[] { new PushCommand("a"), new PushCommand("b") });

        server.HandlePush(1, "u");
        server.HandleResult(1, "u", RefusedBatch);

        var results = await task.WaitAsync(Timeout);
        Assert.All(results, r => Assert.Contains("Invalid member", r.Error));
        Assert.Equal(0, server.GetPendingCount(1));
    }

    [Fact]
    public async Task Fallback_with_a_result_handler_delivers_one_completion_with_the_batch_id()
    {
        var handler = new PushRecordingHandler();
        var server = new PushServer(new InMemoryPushCommandStore(), handler);
        var id = await server.EnqueueBatchAsync(1, new[] { new PushCommand("a"), new PushCommand("b") });

        server.HandlePush(1, "u");
        server.HandleResult(1, "u", RefusedBatch);
        server.HandlePush(1, "x");
        server.HandleResult(1, "x", "{}");
        Assert.Empty(handler.Completions);
        server.HandlePush(1, "y");
        server.HandleResult(1, "y", "{}");

        var completion = Assert.Single(handler.Completions);
        Assert.Equal(id, completion.CommandId);
        Assert.Null(completion.Error);
        Assert.Equal(new[] { "a", "b" }, completion.Results.Select(r => r.Endpoint));
    }

    [Fact]
    public async Task Cancelling_during_the_fallback_removes_the_individual_commands()
    {
        var server = new PushServer();
        using var cts = new CancellationTokenSource();
        var task = server.SendBatchAsync(1, ThreeCommands(), cts.Token);

        server.HandlePush(1, "b");
        server.HandleResult(1, "b", RefusedBatch);
        Assert.Equal(3, server.GetPendingCount(1));

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task.WaitAsync(Timeout));
        for (var i = 0; i < 50 && server.GetPendingCount(1) > 0; i++)
        {
            await Task.Delay(20);
        }

        Assert.Equal(0, server.GetPendingCount(1));
        Assert.Equal(string.Empty, server.HandlePush(1, "z"));
    }

    [Fact]
    public async Task Orphaned_individual_command_fails_only_its_own_result()
    {
        var server = new PushServer();
        var task = server.SendBatchAsync(1, new[] { new PushCommand("a"), new PushCommand("b") });

        server.HandlePush(1, "u");
        server.HandleResult(1, "u", RefusedBatch);
        server.HandlePush(1, "x"); // entrega "a"
        server.HandlePush(1, "y"); // "a" ficou sem resultado: órfão; entrega "b"
        server.HandleResult(1, "y", "{}");

        var results = await task.WaitAsync(Timeout);
        Assert.False(results[0].Success);
        Assert.True(results[1].Success);
    }
}
