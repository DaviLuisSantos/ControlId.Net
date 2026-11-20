using ControlId.Access.Push;
using Microsoft.Extensions.Logging;

namespace ControlId.Tests;

/// <summary>
/// Prazos, limites e isolamento de falhas do <see cref="PushServer"/> (<see cref="PushServerOptions"/>). O tempo é
/// controlado por <see cref="PushFakeClock"/>; os poucos prazos reais (CommandTimeout, ResultHandlerTimeout) usam 50 ms
/// e a espera de segurança de 5 s nunca é atingida num teste que passa.
/// </summary>
public class PushServerRobustnessTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Short = TimeSpan.FromMilliseconds(50);

    // ---- Handler: nunca derruba /push nem /result ----

    [Fact]
    public async Task Orphan_handler_failure_does_not_lose_the_next_command()
    {
        var logs = new PushLogCapture();
        var handler = new PushRecordingHandler();
        var calls = 0;
        handler.OnHandle = (_, _) => Interlocked.Increment(ref calls) == 1 ? throw new InvalidOperationException("handler quebrado") : Task.CompletedTask;
        var server = new PushServer(new InMemoryPushCommandStore(), new PushServerOptions { LoggerFactory = logs }, handler);

        var first = await server.EnqueueAsync(1, new PushCommand("first"));
        var second = await server.EnqueueAsync(1, new PushCommand("second"));
        Assert.Contains("\"endpoint\":\"first\"", await server.HandlePushAsync(1, "u1"));

        // O equipamento volta sem o resultado de "first": o handler do órfão lança, mas "second" é entregue.
        Assert.Contains("\"endpoint\":\"second\"", await server.HandlePushAsync(1, "u2"));
        Assert.True(await server.HandleResultAsync(1, "u2", "{}"));

        var completions = handler.Completions.ToList();
        Assert.Equal(new[] { first, second }, completions.Select(c => c.CommandId));
        Assert.NotNull(completions[0].Error);
        Assert.Null(completions[1].Error);
        var log = Assert.Single(logs.WithId(1101));
        Assert.Equal(LogLevel.Error, log.Level);
        Assert.Equal(ControlIdTelemetry.LoggerCategory, log.Category);
        Assert.Contains(first.ToString(), log.Message);
        Assert.IsType<InvalidOperationException>(log.Exception);
        Assert.Single(logs.WithId(1103));
    }

    [Fact]
    public async Task Result_handler_failure_returns_true_and_logs_1101()
    {
        var logs = new PushLogCapture();
        var handler = new PushRecordingHandler { OnHandle = (_, _) => throw new InvalidOperationException("boom") };
        var server = new PushServer(new InMemoryPushCommandStore(), new PushServerOptions { LoggerFactory = logs }, handler);
        var id = await server.EnqueueAsync(7, new PushCommand("x"));
        await server.HandlePushAsync(7, "u");

        Assert.True(await server.HandleResultAsync(7, "u", """{"response":{}}"""));

        var log = Assert.Single(logs.WithId(1101));
        Assert.Contains(id.ToString(), log.Message);
        Assert.Contains("7", log.Message);
        Assert.Equal(0, await server.GetPendingCountAsync(7));
    }

    [Fact]
    public async Task Faulted_handler_task_is_logged_like_a_throwing_handler()
    {
        var logs = new PushLogCapture();
        var handler = new PushRecordingHandler
        {
            OnHandle = async (_, _) =>
            {
                await Task.Yield();
                throw new InvalidOperationException("assíncrono");
            },
        };
        var server = new PushServer(new InMemoryPushCommandStore(), new PushServerOptions { LoggerFactory = logs }, handler);
        await server.EnqueueAsync(1, new PushCommand("x"));
        await server.HandlePushAsync(1, "u");

        Assert.True(await server.HandleResultAsync(1, "u", "{}").WaitAsync(Timeout));

        Assert.IsType<InvalidOperationException>(Assert.Single(logs.WithId(1101)).Exception);
    }

    [Fact]
    public async Task Result_handler_runs_even_if_the_request_is_aborted_after_the_store_completes()
    {
        var store = new PushControllableStore();
        var handler = new PushRecordingHandler();
        var server = new PushServer(store, handler);
        await server.EnqueueAsync(1, new PushCommand("x"));
        await server.HandlePushAsync(1, "u");

        // O equipamento fecha a conexão logo depois que o envio sai do store.
        using var request = new CancellationTokenSource();
        store.AfterComplete = request.Cancel;

        Assert.True(await server.HandleResultAsync(1, "u", "{}", request.Token));

        Assert.Single(handler.Completions);
        var token = Assert.Single(handler.Tokens);
        Assert.False(token.IsCancellationRequested);
        Assert.True(token.CanBeCanceled);
    }

    [Fact]
    public async Task Slow_handler_that_honours_the_token_is_cut_at_ResultHandlerTimeout()
    {
        var logs = new PushLogCapture();
        var handler = new PushRecordingHandler { OnHandle = (_, token) => Task.Delay(System.Threading.Timeout.Infinite, token) };
        var server = new PushServer(new InMemoryPushCommandStore(), new PushServerOptions { ResultHandlerTimeout = Short, LoggerFactory = logs }, handler);
        await server.EnqueueAsync(1, new PushCommand("x"));
        await server.HandlePushAsync(1, "u");

        Assert.True(await server.HandleResultAsync(1, "u", "{}").WaitAsync(Timeout));

        // O token do handler venceu; a exceção logada é o cancelamento do handler ou o TimeoutException do servidor,
        // conforme quem termina primeiro (os dois vêm do mesmo prazo).
        Assert.True(Assert.Single(handler.Tokens).IsCancellationRequested);
        var exception = Assert.Single(logs.WithId(1101)).Exception;
        Assert.True(exception is OperationCanceledException or TimeoutException, exception?.GetType().FullName);
    }

    [Fact]
    public async Task Slow_handler_that_ignores_the_token_is_cut_at_ResultHandlerTimeout()
    {
        var logs = new PushLogCapture();
        var never = new TaskCompletionSource<bool>();
        var handler = new PushRecordingHandler { OnHandle = (_, _) => never.Task };
        var server = new PushServer(new InMemoryPushCommandStore(), new PushServerOptions { ResultHandlerTimeout = Short, LoggerFactory = logs }, handler);
        await server.EnqueueAsync(1, new PushCommand("x"));
        await server.EnqueueAsync(1, new PushCommand("y"));
        await server.HandlePushAsync(1, "u");

        Assert.True(await server.HandleResultAsync(1, "u", "{}").WaitAsync(Timeout));
        Assert.IsType<TimeoutException>(Assert.Single(logs.WithId(1101)).Exception);

        // O servidor segue atendendo; o handler pendurado não segura o equipamento.
        Assert.Contains("\"endpoint\":\"y\"", await server.HandlePushAsync(1, "v"));
        never.SetResult(true);
    }

    // ---- CommandTimeout ----

    [Fact]
    public async Task CommandTimeout_throws_TimeoutException_and_removes_the_pending_command()
    {
        var store = new InMemoryPushCommandStore();
        var server = new PushServer(store, new PushServerOptions { CommandTimeout = Short });

        var task = server.SendAsync(1, new PushCommand("x"));

        var ex = await Assert.ThrowsAsync<TimeoutException>(() => task.WaitAsync(Timeout));
        Assert.Contains("x", ex.Message);
        Assert.Equal(0, await store.CountAsync(1, CancellationToken.None));
        Assert.Equal(string.Empty, await server.HandlePushAsync(1, "u"));
        Assert.Equal(0, server.WaiterCount);
    }

    [Fact]
    public async Task CommandTimeout_after_delivery_drops_the_in_flight_command_and_ignores_the_late_result()
    {
        var store = new InMemoryPushCommandStore();
        var handler = new PushRecordingHandler();
        var deadlines = new List<(TimeSpan Timeout, CancellationTokenSource Source)>();
        var options = new PushServerOptions
        {
            CommandTimeout = TimeSpan.FromMinutes(2),
            CommandDeadlineFactory = timeout =>
            {
                var source = new CancellationTokenSource();
                deadlines.Add((timeout, source));
                return source;
            },
        };
        var server = new PushServer(store, options, handler);

        var task = server.SendBatchAsync(1, new[] { new PushCommand("a"), new PushCommand("b") });
        Assert.Contains("\"transactions\"", await server.HandlePushAsync(1, "u"));
        var deadline = Assert.Single(deadlines);
        Assert.Equal(TimeSpan.FromMinutes(2), deadline.Timeout);
        Assert.False(task.IsCompleted);

        deadline.Source.Cancel();

        await Assert.ThrowsAsync<TimeoutException>(() => task.WaitAsync(Timeout));
        Assert.Equal(0, await store.CountAsync(1, CancellationToken.None));
        Assert.False(await server.HandleResultAsync(1, "u", "{}"));
        Assert.Empty(handler.Completions);
    }

    [Fact]
    public async Task Caller_token_still_cancels_before_CommandTimeout()
    {
        var server = new PushServer(new InMemoryPushCommandStore(), new PushServerOptions { CommandTimeout = TimeSpan.FromHours(1) });
        using var cts = new CancellationTokenSource();
        var task = server.SendAsync(1, new PushCommand("x"), cts.Token);

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task.WaitAsync(Timeout));
        Assert.Equal(0, await server.GetPendingCountAsync(1));
    }

    [Fact]
    public async Task Result_before_CommandTimeout_completes_normally()
    {
        var server = new PushServer(new InMemoryPushCommandStore(), new PushServerOptions { CommandTimeout = TimeSpan.FromHours(1) });
        var task = server.SendAsync(1, new PushCommand("x"));

        await server.HandlePushAsync(1, "u");
        Assert.True(await server.HandleResultAsync(1, "u", "{}"));

        Assert.True((await task.WaitAsync(Timeout)).Success);
    }

    // ---- CommandTtl ----

    [Fact]
    public async Task Expired_pending_command_is_discarded_and_the_next_one_is_delivered()
    {
        var clock = new PushFakeClock();
        var logs = new PushLogCapture();
        var handler = new PushRecordingHandler();
        var store = new InMemoryPushCommandStore(clock.Now);
        var server = new PushServer(store, new PushServerOptions { Clock = clock.Now, LoggerFactory = logs, CommandTimeout = null }, handler);

        var oldSend = server.SendAsync(1, new PushCommand("old-send"));
        var oldEnqueued = await server.EnqueueAsync(1, new PushCommand("old-enqueue"));
        clock.Advance(TimeSpan.FromHours(25));
        var fresh = server.SendAsync(1, new PushCommand("fresh"));

        Assert.Contains("\"endpoint\":\"fresh\"", await server.HandlePushAsync(1, "u"));
        Assert.True(await server.HandleResultAsync(1, "u", "{}"));

        var ex = await Assert.ThrowsAsync<ControlIdException>(() => oldSend.WaitAsync(Timeout));
        Assert.Contains("expirou", ex.Message);
        var completion = Assert.Single(handler.Completions);
        Assert.Equal(oldEnqueued, completion.CommandId);
        Assert.Contains("expirou", completion.Error!);
        Assert.True((await fresh.WaitAsync(Timeout)).Success);
        Assert.Equal(2, logs.WithId(1102).Count);
        Assert.Equal(0, await server.GetPendingCountAsync(1));
    }

    [Fact]
    public async Task Command_within_ttl_is_delivered()
    {
        var clock = new PushFakeClock();
        var server = new PushServer(new InMemoryPushCommandStore(clock.Now), new PushServerOptions { Clock = clock.Now, CommandTtl = TimeSpan.FromHours(1) });
        await server.EnqueueAsync(1, new PushCommand("x"));
        clock.Advance(TimeSpan.FromHours(1));

        Assert.Contains("\"endpoint\":\"x\"", await server.HandlePushAsync(1, "u"));
    }

    [Fact]
    public async Task Null_ttl_never_expires()
    {
        var clock = new PushFakeClock();
        var server = new PushServer(new InMemoryPushCommandStore(clock.Now), new PushServerOptions { Clock = clock.Now, CommandTtl = null });
        await server.EnqueueAsync(1, new PushCommand("x"));
        clock.Advance(TimeSpan.FromDays(3650));

        Assert.Contains("\"endpoint\":\"x\"", await server.HandlePushAsync(1, "u"));
    }

    // ---- MaxPendingPerDevice ----

    [Fact]
    public async Task Full_queue_throws_PushQueueFullException()
    {
        var server = new PushServer(new InMemoryPushCommandStore(), new PushServerOptions { MaxPendingPerDevice = 2 });
        await server.EnqueueAsync(1, new PushCommand("a"));
        var waiting = server.SendAsync(1, new PushCommand("b"));

        var enqueue = await Assert.ThrowsAsync<PushQueueFullException>(() => server.EnqueueAsync(1, new PushCommand("c")));
        var send = await Assert.ThrowsAsync<PushQueueFullException>(() => server.SendAsync(1, new PushCommand("c")));
        await Assert.ThrowsAsync<PushQueueFullException>(() => server.SendBatchAsync(1, new[] { new PushCommand("c") }));

        Assert.Equal(1, enqueue.DeviceId);
        Assert.Equal(2, enqueue.Limit);
        Assert.IsAssignableFrom<ControlIdException>(send);
        Assert.Equal(1, server.WaiterCount);
        Assert.Equal(2, await server.GetPendingCountAsync(1));

        // Outro equipamento tem a própria fila; e a fila volta a aceitar quando anda.
        await server.EnqueueAsync(2, new PushCommand("other"));
        await server.HandlePushAsync(1, "u");
        await server.HandleResultAsync(1, "u", "{}");
        await server.EnqueueAsync(1, new PushCommand("c"));
        Assert.False(waiting.IsCompleted);
    }

    [Fact]
    public async Task Concurrent_enqueues_never_exceed_the_limit()
    {
        var server = new PushServer(new InMemoryPushCommandStore(), new PushServerOptions { MaxPendingPerDevice = 10 });

        var attempts = await Task.WhenAll(Enumerable.Range(0, 50).Select(async i =>
        {
            await Task.Yield();
            try
            {
                await server.EnqueueAsync(1, new PushCommand("c" + i));
                return true;
            }
            catch (PushQueueFullException)
            {
                return false;
            }
        }));

        Assert.Equal(10, attempts.Count(ok => ok));
        Assert.Equal(10, await server.GetPendingCountAsync(1));
    }

    [Fact]
    public async Task Zero_MaxPendingPerDevice_means_no_limit()
    {
        var server = new PushServer(new InMemoryPushCommandStore(), new PushServerOptions { MaxPendingPerDevice = 0 });

        for (var i = 0; i < 1100; i++)
        {
            await server.EnqueueAsync(1, new PushCommand("c"));
        }

        Assert.Equal(1100, await server.GetPendingCountAsync(1));
    }

    // ---- RequireUuid ----

    [Fact]
    public async Task RequireUuid_rejects_push_and_result_without_uuid()
    {
        var logs = new PushLogCapture();
        var server = new PushServer(new InMemoryPushCommandStore(), new PushServerOptions { RequireUuid = true, LoggerFactory = logs });
        var task = server.SendAsync(1, new PushCommand("x"));

        Assert.Equal(string.Empty, await server.HandlePushAsync(1, null));
        Assert.Equal(string.Empty, await server.HandlePushAsync(1, ""));
        Assert.Equal(1, await server.GetPendingCountAsync(1));

        Assert.Contains("\"endpoint\":\"x\"", await server.HandlePushAsync(1, "u"));
        Assert.False(await server.HandleResultAsync(1, null, "{}"));
        Assert.False(task.IsCompleted);

        // O uuid também vale quando vem no corpo.
        Assert.True(await server.HandleResultAsync(1, null, """{"uuid":"u","response":{}}"""));
        Assert.True((await task.WaitAsync(Timeout)).Success);
        Assert.Equal(3, logs.WithId(1105).Count);
    }

    [Fact]
    public async Task Without_RequireUuid_a_result_without_uuid_still_matches()
    {
        var server = new PushServer(new InMemoryPushCommandStore(), new PushServerOptions());
        var task = server.SendAsync(1, new PushCommand("x"));
        await server.HandlePushAsync(1, null);

        Assert.True(await server.HandleResultAsync(1, null, "{}"));
        await task.WaitAsync(Timeout);
    }

    // ---- Cancelamento ----

    [Fact]
    public async Task Cancelling_releases_the_waiter_even_when_the_store_is_stuck()
    {
        var stuck = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new PushControllableStore { BeforeRemove = (_, _) => stuck.Task };
        var server = new PushServer(store);
        using var cts = new CancellationTokenSource();
        var task = server.SendAsync(1, new PushCommand("x"), cts.Token);

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task.WaitAsync(Timeout));
        Assert.False(store.RemoveFinished.IsCompleted);

        stuck.SetResult(true);
        await store.RemoveFinished.WaitAsync(Timeout);
        Assert.Equal(0, await server.GetPendingCountAsync(1));
        Assert.Equal(0, server.WaiterCount);
    }

    [Fact]
    public async Task Cancelling_releases_the_waiter_while_the_device_gate_is_busy()
    {
        var takeStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseTake = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new PushControllableStore
        {
            BeforeTake = _ =>
            {
                takeStarted.TrySetResult(true);
                return releaseTake.Task;
            },
        };
        var server = new PushServer(store);
        using var cts = new CancellationTokenSource();
        var task = server.SendAsync(1, new PushCommand("x"), cts.Token);
        var push = server.HandlePushAsync(1, "u");
        await takeStarted.Task.WaitAsync(Timeout);

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task.WaitAsync(Timeout));
        releaseTake.SetResult(true);
        await push.WaitAsync(Timeout);
        await store.RemoveFinished.WaitAsync(Timeout);
        Assert.Equal(0, await server.GetPendingCountAsync(1));
        Assert.False(await server.HandleResultAsync(1, "u", "{}"));
    }

    [Fact]
    public async Task Failure_to_remove_a_cancelled_command_is_logged_as_1106()
    {
        var logs = new PushLogCapture();
        var store = new PushControllableStore { BeforeRemove = (_, _) => throw new InvalidOperationException("banco fora") };
        var server = new PushServer(store, new PushServerOptions { LoggerFactory = logs });
        using var cts = new CancellationTokenSource();
        var task = server.SendAsync(1, new PushCommand("x"), cts.Token);

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task.WaitAsync(Timeout));
        await store.RemoveFinished.WaitAsync(Timeout);
        var log = Assert.Single(logs.WithId(1106));
        Assert.Equal(LogLevel.Warning, log.Level);
        Assert.IsType<InvalidOperationException>(log.Exception);
    }

    [Fact]
    public async Task Failed_add_is_compensated_before_rethrowing()
    {
        var store = new PushControllableStore { AfterAdd = (_, _) => throw new InvalidOperationException("commit falhou") };
        var server = new PushServer(store);

        await Assert.ThrowsAsync<InvalidOperationException>(() => server.EnqueueAsync(1, new PushCommand("x")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => server.SendAsync(1, new PushCommand("y")));

        Assert.Equal(0, await store.CountAsync(1, CancellationToken.None));
        Assert.Equal(0, server.WaiterCount);
    }

    [Fact]
    public async Task Orphan_of_a_cancelled_send_found_by_the_purge_does_not_reach_the_handler()
    {
        var clock = new PushFakeClock();
        var stuck = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new PushControllableMaintenanceStore(new InMemoryPushCommandStore(clock.Now)) { BeforeRemove = (_, _) => stuck.Task };
        var handler = new PushRecordingHandler();
        var server = new PushServer(store, new PushServerOptions { Clock = clock.Now }, handler);
        using var cts = new CancellationTokenSource();
        var task = server.SendAsync(1, new PushCommand("x"), cts.Token);
        await server.HandlePushAsync(1, "u");

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task.WaitAsync(Timeout));
        clock.Advance(TimeSpan.FromHours(1));

        Assert.Equal(1, await server.PurgeExpiredAsync());
        Assert.Empty(handler.Completions);
        stuck.SetResult(true);
        await store.RemoveFinished.WaitAsync(Timeout);
    }

    // ---- PurgeExpiredAsync ----

    [Fact]
    public async Task Purge_fails_expired_pending_waiters_and_calls_the_handler()
    {
        var clock = new PushFakeClock();
        var logs = new PushLogCapture();
        var handler = new PushRecordingHandler();
        var server = new PushServer(
            new InMemoryPushCommandStore(clock.Now),
            new PushServerOptions { Clock = clock.Now, CommandTtl = TimeSpan.FromHours(1), CommandTimeout = null, LoggerFactory = logs },
            handler);
        var waiting = server.SendAsync(1, new PushCommand("a"));
        var enqueued = await server.EnqueueAsync(2, new PushCommand("b"));
        clock.Advance(TimeSpan.FromMinutes(90));
        var fresh = await server.EnqueueAsync(2, new PushCommand("c"));

        Assert.Equal(2, await server.PurgeExpiredAsync());

        var ex = await Assert.ThrowsAsync<ControlIdException>(() => waiting.WaitAsync(Timeout));
        Assert.Contains("expirou", ex.Message);
        var completion = Assert.Single(handler.Completions);
        Assert.Equal(enqueued, completion.CommandId);
        Assert.Equal(2, completion.DeviceId);
        Assert.NotNull(completion.Error);
        Assert.Empty(completion.Results);
        Assert.Equal(0, await server.GetPendingCountAsync(1));
        Assert.Equal(1, await server.GetPendingCountAsync(2));
        Assert.Contains("\"endpoint\":\"c\"", await server.HandlePushAsync(2, "u"));
        Assert.NotEqual(Guid.Empty, fresh);

        var purged = Assert.Single(logs.WithId(1107));
        Assert.Equal(LogLevel.Information, purged.Level);
        Assert.Contains("2", purged.Message);
        Assert.Equal(2, logs.WithId(1102).Count);
    }

    [Fact]
    public async Task Purge_fails_in_flight_commands_past_InFlightTimeout()
    {
        var clock = new PushFakeClock();
        var handler = new PushRecordingHandler();
        var server = new PushServer(
            new InMemoryPushCommandStore(clock.Now),
            new PushServerOptions { Clock = clock.Now, InFlightTimeout = TimeSpan.FromMinutes(10), CommandTimeout = null },
            handler);
        var waiting = server.SendAsync(1, new PushCommand("a"));
        var enqueued = await server.EnqueueAsync(2, new PushCommand("b"));
        await server.HandlePushAsync(1, "u1");
        await server.HandlePushAsync(2, "u2");

        clock.Advance(TimeSpan.FromMinutes(10));
        Assert.Equal(0, await server.PurgeExpiredAsync());

        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(2, await server.PurgeExpiredAsync());

        await Assert.ThrowsAsync<ControlIdException>(() => waiting.WaitAsync(Timeout));
        Assert.Equal(enqueued, Assert.Single(handler.Completions).CommandId);
        Assert.False(await server.HandleResultAsync(1, "u1", "{}"));
        Assert.Equal(0, await server.GetPendingCountAsync(2));
    }

    [Fact]
    public async Task Purge_with_nothing_expired_returns_zero_and_does_not_log()
    {
        var clock = new PushFakeClock();
        var logs = new PushLogCapture();
        var server = new PushServer(new InMemoryPushCommandStore(clock.Now), new PushServerOptions { Clock = clock.Now, LoggerFactory = logs });
        await server.EnqueueAsync(1, new PushCommand("a"));

        Assert.Equal(0, await server.PurgeExpiredAsync());
        Assert.Empty(logs.WithId(1107));
        Assert.Equal(1, await server.GetPendingCountAsync(1));
    }

    [Fact]
    public async Task Purge_without_maintenance_or_with_both_limits_off_returns_zero()
    {
        var clock = new PushFakeClock();
        var withoutMaintenance = new PushServer(new PushControllableStore(new InMemoryPushCommandStore(clock.Now)), new PushServerOptions { Clock = clock.Now });
        await withoutMaintenance.EnqueueAsync(1, new PushCommand("a"));
        var limitsOff = new PushServer(new InMemoryPushCommandStore(clock.Now), new PushServerOptions { Clock = clock.Now, CommandTtl = null, InFlightTimeout = null });
        await limitsOff.EnqueueAsync(1, new PushCommand("a"));
        clock.Advance(TimeSpan.FromDays(30));

        Assert.Equal(0, await withoutMaintenance.PurgeExpiredAsync());
        Assert.Equal(0, await limitsOff.PurgeExpiredAsync());
        Assert.Equal(1, await withoutMaintenance.GetPendingCountAsync(1));
        Assert.Equal(1, await limitsOff.GetPendingCountAsync(1));
    }

    // ---- Memória limitada ----

    [Fact]
    public async Task Ten_thousand_distinct_device_ids_do_not_grow_the_server()
    {
        var logs = new PushLogCapture();
        var store = new InMemoryPushCommandStore();
        var server = new PushServer(store, new PushServerOptions { LoggerFactory = logs });

        for (long device = 0; device < 10_050; device++)
        {
            Assert.Equal(string.Empty, await server.HandlePushAsync(device, "u"));
            Assert.False(await server.HandleResultAsync(device, "u", "{}"));
        }

        Assert.Equal(10_000, server.TrackedDeviceCount);
        Assert.NotNull(server.GetLastSeen(0));
        Assert.NotNull(server.GetLastSeen(9_999));
        Assert.Null(server.GetLastSeen(10_000));
        Assert.Equal(0, store.DeviceCount);
        Assert.Equal(0, server.WaiterCount);
        var log = Assert.Single(logs.WithId(1104));
        Assert.Contains("10000", log.Message);

        // Quem já era acompanhado continua sendo atualizado.
        var before = server.GetLastSeen(5);
        await server.HandlePushAsync(5, "u");
        Assert.True(server.GetLastSeen(5) >= before);
    }

    [Fact]
    public async Task MaxTrackedDevices_is_configurable()
    {
        var server = new PushServer(new InMemoryPushCommandStore(), new PushServerOptions { MaxTrackedDevices = 3 });

        await Task.WhenAll(Enumerable.Range(0, 100).Select(i => server.HandlePushAsync(i, "u")));

        Assert.Equal(3, server.TrackedDeviceCount);
        Assert.Equal(3, Enumerable.Range(0, 100).Count(i => server.GetLastSeen(i) is not null));
    }

    [Fact]
    public async Task Store_drops_queues_that_become_empty()
    {
        var store = new InMemoryPushCommandStore();
        var server = new PushServer(store);

        for (long device = 1; device <= 300; device++)
        {
            await server.EnqueueAsync(device, new PushCommand("x"));
        }

        Assert.Equal(300, store.DeviceCount);
        for (long device = 1; device <= 300; device++)
        {
            await server.HandlePushAsync(device, "u");
            Assert.True(await server.HandleResultAsync(device, "u", "{}"));
        }

        Assert.Equal(0, store.DeviceCount);

        // Remoção e órfão também liberam a fila.
        var removed = await server.EnqueueAsync(1, new PushCommand("a"));
        Assert.True(await store.RemoveAsync(removed, CancellationToken.None));
        await server.EnqueueAsync(2, new PushCommand("b"));
        await server.HandlePushAsync(2, "u1");
        await server.HandlePushAsync(2, "u2");
        Assert.Equal(0, store.DeviceCount);
    }

    [Fact]
    public async Task Store_add_racing_with_queue_cleanup_never_loses_an_envelope()
    {
        var store = new InMemoryPushCommandStore();

        for (var round = 0; round < 200; round++)
        {
            var envelope = new PushEnvelope(Guid.NewGuid(), 1, false, new[] { "x" }, "{}", DateTimeOffset.UtcNow);
            var other = new PushEnvelope(Guid.NewGuid(), 1, false, new[] { "y" }, "{}", DateTimeOffset.UtcNow);
            await store.AddAsync(envelope, CancellationToken.None);

            // Esvaziar a fila (descarta) e incluir outro envio ao mesmo tempo.
            await Task.WhenAll(
                Task.Run(() => store.RemoveAsync(envelope.Id, CancellationToken.None)),
                Task.Run(() => store.AddAsync(other, CancellationToken.None)));

            Assert.Equal(1, await store.CountAsync(1, CancellationToken.None));
            Assert.Equal(other.Id, (await store.TakeNextAsync(1, "u", CancellationToken.None)).Next!.Id);
            Assert.NotNull(await store.CompleteAsync(1, "u", CancellationToken.None));
        }

        Assert.Equal(0, store.DeviceCount);
    }

    [Fact]
    public async Task Devices_sharing_a_gate_stripe_are_still_served_independently()
    {
        const long a = 1;
        const long b = a + PushServer.GateCount;
        Assert.Equal(PushServer.GateIndex(a), PushServer.GateIndex(b));
        var server = new PushServer();

        var sendA = server.SendAsync(a, new PushCommand("a"));
        var sendB = server.SendAsync(b, new PushCommand("b"));

        Assert.Contains("\"endpoint\":\"b\"", await server.HandlePushAsync(b, "ub"));
        Assert.Contains("\"endpoint\":\"a\"", await server.HandlePushAsync(a, "ua"));
        Assert.True(await server.HandleResultAsync(a, "ua", "{}"));
        Assert.True(await server.HandleResultAsync(b, "ub", "{}"));
        Assert.Equal("a", (await sendA.WaitAsync(Timeout)).Endpoint);
        Assert.Equal("b", (await sendB.WaitAsync(Timeout)).Endpoint);
    }

    [Fact]
    public void Gate_index_covers_negative_and_large_ids()
    {
        foreach (var id in new[] { long.MinValue, -1L, 0L, 255L, 256L, long.MaxValue })
        {
            Assert.InRange(PushServer.GateIndex(id), 0, PushServer.GateCount - 1);
        }
    }

    // ---- Opções ----

    [Fact]
    public void Default_options_match_the_contract()
    {
        var options = new PushServerOptions();

        Assert.Equal(TimeSpan.FromMinutes(5), options.CommandTimeout);
        Assert.Equal(TimeSpan.FromHours(24), options.CommandTtl);
        Assert.Equal(TimeSpan.FromMinutes(10), options.InFlightTimeout);
        Assert.Equal(1000, options.MaxPendingPerDevice);
        Assert.Equal(10_000, options.MaxTrackedDevices);
        Assert.False(options.RequireUuid);
        Assert.Equal(TimeSpan.FromSeconds(30), options.ResultHandlerTimeout);
        Assert.Equal(TimeSpan.FromMinutes(1), options.PurgeInterval);
        Assert.Null(options.LoggerFactory);
    }

    [Theory]
    [InlineData(nameof(PushServerOptions.CommandTimeout))]
    [InlineData(nameof(PushServerOptions.CommandTtl))]
    [InlineData(nameof(PushServerOptions.InFlightTimeout))]
    [InlineData(nameof(PushServerOptions.MaxPendingPerDevice))]
    [InlineData(nameof(PushServerOptions.MaxTrackedDevices))]
    [InlineData(nameof(PushServerOptions.ResultHandlerTimeout))]
    [InlineData(nameof(PushServerOptions.PurgeInterval))]
    public void Invalid_options_are_rejected(string property)
    {
        var options = new PushServerOptions();
        switch (property)
        {
            case nameof(PushServerOptions.CommandTimeout): options.CommandTimeout = TimeSpan.Zero; break;
            case nameof(PushServerOptions.CommandTtl): options.CommandTtl = TimeSpan.FromSeconds(-1); break;
            case nameof(PushServerOptions.InFlightTimeout): options.InFlightTimeout = TimeSpan.Zero; break;
            case nameof(PushServerOptions.MaxPendingPerDevice): options.MaxPendingPerDevice = -1; break;
            case nameof(PushServerOptions.MaxTrackedDevices): options.MaxTrackedDevices = 0; break;
            case nameof(PushServerOptions.ResultHandlerTimeout): options.ResultHandlerTimeout = TimeSpan.Zero; break;
            case nameof(PushServerOptions.PurgeInterval): options.PurgeInterval = TimeSpan.Zero; break;
        }

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new PushServer(new InMemoryPushCommandStore(), options));
        Assert.Equal(property, ex.ParamName);
    }

    [Fact]
    public void Null_options_or_store_are_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new PushServer(new InMemoryPushCommandStore(), (PushServerOptions)null!));
        Assert.Throws<ArgumentNullException>(() => new PushServer(null!, new PushServerOptions()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PushServer(new InMemoryPushCommandStore(), new PushServerOptions { CommandTimeout = TimeSpan.FromDays(30) }));
    }

    [Fact]
    public async Task Options_are_read_once_at_construction()
    {
        var options = new PushServerOptions { MaxPendingPerDevice = 1 };
        var server = new PushServer(new InMemoryPushCommandStore(), options);
        options.MaxPendingPerDevice = 0;

        await server.EnqueueAsync(1, new PushCommand("a"));
        await Assert.ThrowsAsync<PushQueueFullException>(() => server.EnqueueAsync(1, new PushCommand("b")));
    }

    // ---- Métrica ----

    [Fact]
    public async Task Commands_metric_counts_each_outcome()
    {
        using var metrics = new PushMetricCapture();
        var clock = new PushFakeClock();
        var server = new PushServer(
            new InMemoryPushCommandStore(clock.Now),
            new PushServerOptions { Clock = clock.Now, MaxPendingPerDevice = 3, CommandTtl = TimeSpan.FromHours(1) },
            new PushRecordingHandler());

        // enqueued + delivered + completed
        var ok = server.SendAsync(1, new PushCommand("ok"));
        await server.HandlePushAsync(1, "u1");
        await server.HandleResultAsync(1, "u1", "{}");
        await ok.WaitAsync(Timeout);

        // orphaned
        await server.EnqueueAsync(1, new PushCommand("orphan"));
        await server.HandlePushAsync(1, "u2");
        await server.HandlePushAsync(1, "u3");

        // expired
        await server.EnqueueAsync(1, new PushCommand("old"));
        clock.Advance(TimeSpan.FromHours(2));
        await server.HandlePushAsync(1, "u4");

        // cancelled
        using (var cts = new CancellationTokenSource())
        {
            var cancelled = server.SendAsync(1, new PushCommand("cancel"), cts.Token);
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled.WaitAsync(Timeout));
        }

        // rejected
        await server.EnqueueAsync(2, new PushCommand("a"));
        await server.EnqueueAsync(2, new PushCommand("b"));
        await server.EnqueueAsync(2, new PushCommand("c"));
        await Assert.ThrowsAsync<PushQueueFullException>(() => server.EnqueueAsync(2, new PushCommand("d")));

        // timeout
        var quick = new PushServer(new InMemoryPushCommandStore(), new PushServerOptions { CommandTimeout = Short });
        await Assert.ThrowsAsync<TimeoutException>(() => quick.SendAsync(3, new PushCommand("slow")).WaitAsync(Timeout));

        Assert.Equal(8, metrics.Count("enqueued"));
        Assert.Equal(2, metrics.Count("delivered"));
        Assert.Equal(1, metrics.Count("completed"));
        Assert.Equal(1, metrics.Count("orphaned"));
        Assert.Equal(1, metrics.Count("expired"));
        Assert.Equal(1, metrics.Count("cancelled"));
        Assert.Equal(1, metrics.Count("rejected"));
        Assert.Equal(1, metrics.Count("timeout"));
    }
}
