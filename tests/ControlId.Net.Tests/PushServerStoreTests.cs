using System.Collections.Concurrent;
using ControlId.Access.Push;

namespace ControlId.Tests;

/// <summary>Contrato do store aplicado ao <see cref="InMemoryPushCommandStore"/>.</summary>
public class InMemoryPushStoreTests : PushStoreContractTests
{
    protected override IPushCommandStore CreateStore() => new InMemoryPushCommandStore();
}

/// <summary>Comportamento do <see cref="PushServer"/> sobre um <see cref="IPushCommandStore"/> e o <see cref="IPushResultHandler"/>.</summary>
public class PushServerStoreTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private sealed class RecordingHandler : IPushResultHandler
    {
        public ConcurrentQueue<PushCompletion> Completions { get; } = new();

        public Task HandleAsync(PushCompletion completion, CancellationToken cancellationToken)
        {
            Completions.Enqueue(completion);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Enqueue_delivers_and_sends_the_result_to_the_handler()
    {
        var handler = new RecordingHandler();
        var server = new PushServer(new InMemoryPushCommandStore(), handler);

        var id = await server.EnqueueAsync(1, new PushCommand("reboot"));

        Assert.Equal(1, await server.GetPendingCountAsync(1));
        Assert.Contains("\"endpoint\":\"reboot\"", await server.HandlePushAsync(1, "u"));
        Assert.True(await server.HandleResultAsync(1, "u", """{"response":{"ok":true}}"""));

        var completion = Assert.Single(handler.Completions);
        Assert.Equal(id, completion.CommandId);
        Assert.Equal(1, completion.DeviceId);
        Assert.Null(completion.Error);
        var result = Assert.Single(completion.Results);
        Assert.Equal("reboot", result.Endpoint);
        Assert.True(result.Success);
        Assert.Equal(0, await server.GetPendingCountAsync(1));
    }

    [Fact]
    public async Task Enqueue_batch_sends_one_result_per_command_to_the_handler()
    {
        var handler = new RecordingHandler();
        var server = new PushServer(new InMemoryPushCommandStore(), handler);

        var id = await server.EnqueueBatchAsync(1, new[] { new PushCommand("a"), new PushCommand("b") });
        Assert.Contains("\"transactions\"", await server.HandlePushAsync(1, "u"));
        await server.HandleResultAsync(1, "u", """{"transactions_results":[{"transactionid":2,"success":true},{"transactionid":1,"success":false,"response":"boom"}]}""");

        var completion = Assert.Single(handler.Completions);
        Assert.Equal(id, completion.CommandId);
        Assert.Equal(new[] { "a", "b" }, completion.Results.Select(r => r.Endpoint));
        Assert.Equal("boom", completion.Results[0].Error);
        Assert.True(completion.Results[1].Success);
    }

    [Fact]
    public async Task Orphan_without_a_waiter_is_reported_to_the_handler_and_not_resent()
    {
        var handler = new RecordingHandler();
        var server = new PushServer(new InMemoryPushCommandStore(), handler);

        var id = await server.EnqueueAsync(1, new PushCommand("reboot"));
        Assert.Contains("\"endpoint\":\"reboot\"", await server.HandlePushAsync(1, "u1"));

        // O equipamento volta a /push sem ter devolvido o resultado.
        Assert.Equal(string.Empty, await server.HandlePushAsync(1, "u2"));

        var completion = Assert.Single(handler.Completions);
        Assert.Equal(id, completion.CommandId);
        Assert.Empty(completion.Results);
        Assert.NotNull(completion.Error);
        Assert.Equal(0, await server.GetPendingCountAsync(1));
    }

    [Fact]
    public async Task Awaited_send_does_not_reach_the_handler()
    {
        var handler = new RecordingHandler();
        var server = new PushServer(new InMemoryPushCommandStore(), handler);
        var task = server.SendAsync(1, new PushCommand("x"));

        await server.HandlePushAsync(1, "u");
        await server.HandleResultAsync(1, "u", "{}");

        Assert.True((await task.WaitAsync(Timeout)).Success);
        Assert.Empty(handler.Completions);
    }

    [Fact]
    public async Task Handler_runs_at_most_once()
    {
        var handler = new RecordingHandler();
        var server = new PushServer(new InMemoryPushCommandStore(), handler);
        await server.EnqueueAsync(1, new PushCommand("x"));
        await server.HandlePushAsync(1, "u");

        Assert.True(await server.HandleResultAsync(1, "u", "{}"));
        Assert.False(await server.HandleResultAsync(1, "u", "{}"));

        Assert.Single(handler.Completions);
    }

    [Fact]
    public async Task Unclaimed_result_without_handler_is_accepted_and_dropped()
    {
        var server = new PushServer();
        await server.EnqueueAsync(1, new PushCommand("x"));
        await server.HandlePushAsync(1, "u");

        Assert.True(await server.HandleResultAsync(1, "u", "{}"));
        Assert.Equal(0, await server.GetPendingCountAsync(1));
    }

    [Fact]
    public async Task Unclaimed_invalid_result_reaches_the_handler_with_an_error()
    {
        var handler = new RecordingHandler();
        var server = new PushServer(new InMemoryPushCommandStore(), handler);
        await server.EnqueueAsync(1, new PushCommand("x"));
        await server.HandlePushAsync(1, "u");

        Assert.True(await server.HandleResultAsync(1, "u", "not json"));

        var completion = Assert.Single(handler.Completions);
        Assert.NotNull(completion.Error);
        Assert.Empty(completion.Results);
    }

    [Fact]
    public async Task Restarted_server_delivers_the_pending_command_and_routes_the_result_to_the_handler()
    {
        var store = new InMemoryPushCommandStore();
        var before = new PushServer(store);
        var id = await before.EnqueueAsync(1, new PushCommand("set_configuration", new { general = new { language = "pt_BR" } }));

        // Reinício simulado: instância nova, mesmo store.
        var handler = new RecordingHandler();
        var after = new PushServer(store, handler);

        Assert.Equal(1, await after.GetPendingCountAsync(1));
        Assert.Contains("\"endpoint\":\"set_configuration\"", await after.HandlePushAsync(1, "u"));
        Assert.True(await after.HandleResultAsync(1, "u", """{"response":{}}"""));

        var completion = Assert.Single(handler.Completions);
        Assert.Equal(id, completion.CommandId);
        Assert.True(Assert.Single(completion.Results).Success);
    }

    [Fact]
    public async Task Restart_while_a_command_is_in_flight_routes_the_late_result_to_the_handler()
    {
        var store = new InMemoryPushCommandStore();
        var before = new PushServer(store);
        var waiting = before.SendAsync(1, new PushCommand("x"));
        await before.HandlePushAsync(1, "u");

        var handler = new RecordingHandler();
        var after = new PushServer(store, handler);
        Assert.True(await after.HandleResultAsync(1, "u", "{}"));

        Assert.Single(handler.Completions);
        Assert.False(waiting.IsCompleted);
    }

    [Fact]
    public async Task Cancelling_removes_the_command_from_the_store()
    {
        var store = new InMemoryPushCommandStore();
        var server = new PushServer(store);
        using var cts = new CancellationTokenSource();
        var task = server.SendAsync(1, new PushCommand("x"), cts.Token);
        Assert.Equal(1, await store.CountAsync(1, CancellationToken.None));

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task.WaitAsync(Timeout));
        Assert.Equal(0, await store.CountAsync(1, CancellationToken.None));
        Assert.Equal(string.Empty, await server.HandlePushAsync(1, "u"));
    }

    [Fact]
    public async Task Cancelling_after_delivery_drops_the_in_flight_command()
    {
        var store = new InMemoryPushCommandStore();
        var server = new PushServer(store);
        using var cts = new CancellationTokenSource();
        var task = server.SendAsync(1, new PushCommand("x"), cts.Token);
        await server.HandlePushAsync(1, "u");

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task.WaitAsync(Timeout));
        Assert.Equal(0, await store.CountAsync(1, CancellationToken.None));
        Assert.False(await server.HandleResultAsync(1, "u", "{}"));
    }

    [Fact]
    public async Task Orphan_fails_the_waiter_and_is_never_resent()
    {
        var store = new InMemoryPushCommandStore();
        var handler = new RecordingHandler();
        var server = new PushServer(store, handler);
        var task = server.SendAsync(1, new PushCommand("x"));

        Assert.NotEqual(string.Empty, await server.HandlePushAsync(1, "a"));
        Assert.Equal(string.Empty, await server.HandlePushAsync(1, "b"));
        Assert.Equal(string.Empty, await server.HandlePushAsync(1, "c"));

        var ex = await Assert.ThrowsAsync<ControlIdException>(() => task.WaitAsync(Timeout));
        Assert.Contains("x", ex.Message);
        Assert.Equal(0, await store.CountAsync(1, CancellationToken.None));
        Assert.Empty(handler.Completions);
    }

    [Fact]
    public async Task Orphan_of_an_enqueued_command_is_not_resent()
    {
        var server = new PushServer();
        await server.EnqueueAsync(1, new PushCommand("x"));

        Assert.NotEqual(string.Empty, await server.HandlePushAsync(1, "a"));
        Assert.Equal(string.Empty, await server.HandlePushAsync(1, "b"));
        Assert.Equal(0, await server.GetPendingCountAsync(1));
    }

    [Fact]
    public async Task Payload_is_serialized_when_enqueued()
    {
        var server = new PushServer();
        var command = new PushCommand("x", new { value = 1 });
        await server.EnqueueAsync(1, command);

        command.Body = new { value = 2 };
        command.Verb = "GET";

        var payload = await server.HandlePushAsync(1, "u");
        Assert.Contains("\"value\":1", payload);
        Assert.Contains("\"verb\":\"POST\"", payload);
    }

    [Fact]
    public async Task Devices_run_in_parallel()
    {
        var server = new PushServer(new InMemoryPushCommandStore(), new RecordingHandler());
        var devices = Enumerable.Range(1, 16).Select(i => (long)i).ToArray();

        var sends = devices.ToDictionary(d => d, d => server.SendAsync(d, new PushCommand("cmd" + d)));

        await Task.WhenAll(devices.Select(async device =>
        {
            for (var round = 0; round < 3; round++)
            {
                var payload = await server.HandlePushAsync(device, "u" + round);
                if (round == 0)
                {
                    Assert.Contains("\"endpoint\":\"cmd" + device + "\"", payload);
                    Assert.True(await server.HandleResultAsync(device, "u0", "{}"));
                }
                else
                {
                    Assert.Equal(string.Empty, payload);
                }
            }
        }));

        foreach (var device in devices)
        {
            Assert.Equal("cmd" + device, (await sends[device].WaitAsync(Timeout)).Endpoint);
            Assert.Equal(0, await server.GetPendingCountAsync(device));
        }
    }

    [Fact]
    public async Task Concurrent_sends_to_one_device_keep_order_and_lose_nothing()
    {
        var server = new PushServer();
        var sends = Enumerable.Range(0, 20).Select(i => server.SendAsync(1, new PushCommand("c" + i))).ToList();

        for (var i = 0; i < 20; i++)
        {
            Assert.Contains("\"endpoint\":\"c" + i + "\"", await server.HandlePushAsync(1, "u" + i));
            Assert.True(await server.HandleResultAsync(1, "u" + i, "{}"));
        }

        var results = await Task.WhenAll(sends).WaitAsync(Timeout);
        Assert.Equal(Enumerable.Range(0, 20).Select(i => "c" + i), results.Select(r => r.Endpoint));
    }

    [Fact]
    public async Task Sync_and_async_handlers_behave_the_same()
    {
        var sync = new PushServer();
        var async = new PushServer();
        var syncTask = sync.SendAsync(1, new PushCommand("x", new { a = 1 }));
        var asyncTask = async.SendAsync(1, new PushCommand("x", new { a = 1 }));

        Assert.Equal(sync.GetPendingCount(1), await async.GetPendingCountAsync(1));
        Assert.Equal(sync.HandlePush(1, "u"), await async.HandlePushAsync(1, "u"));
        Assert.Equal(sync.HandlePush(1, "v"), await async.HandlePushAsync(1, "v"));
        await Assert.ThrowsAsync<ControlIdException>(() => syncTask.WaitAsync(Timeout));
        await Assert.ThrowsAsync<ControlIdException>(() => asyncTask.WaitAsync(Timeout));

        var syncSecond = sync.SendAsync(1, new PushCommand("y"));
        var asyncSecond = async.SendAsync(1, new PushCommand("y"));
        sync.HandlePush(1, "w");
        await async.HandlePushAsync(1, "w");
        Assert.True(sync.HandleResult(1, "w", "{}"));
        Assert.True(await async.HandleResultAsync(1, "w", "{}"));
        Assert.Equal((await syncSecond.WaitAsync(Timeout)).Endpoint, (await asyncSecond.WaitAsync(Timeout)).Endpoint);
        Assert.False(sync.HandleResult(1, "w", "{}"));
        Assert.False(await async.HandleResultAsync(1, "w", "{}"));
    }

    [Fact]
    public async Task Cancelled_token_before_enqueue_does_not_touch_the_store()
    {
        var store = new InMemoryPushCommandStore();
        var server = new PushServer(store);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => server.SendAsync(1, new PushCommand("x"), cts.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => server.EnqueueAsync(1, new PushCommand("x"), cts.Token));

        Assert.Equal(0, await store.CountAsync(1, CancellationToken.None));
    }

    [Fact]
    public async Task Null_arguments_and_empty_batches_are_rejected()
    {
        var server = new PushServer();

        Assert.Throws<ArgumentNullException>(() => new PushServer(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => server.EnqueueAsync(1, null!));
        Assert.Throws<ArgumentException>(() => { _ = server.EnqueueBatchAsync(1, Array.Empty<PushCommand>()); });
    }
}
