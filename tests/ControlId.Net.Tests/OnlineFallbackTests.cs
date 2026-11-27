#if NET8_0_OR_GREATER
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ControlId.Access;
using ControlId.Access.Online;
using ControlId.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ControlId.Tests;

/// <summary>Modo online: o handler que falha, devolve null ou demora não derruba o equipamento em um 500 (issues #51 e #52).</summary>
[Collection("OnlineFallbackMetrics")] // o contador de fallback é global ao processo
public sealed class OnlineFallbackTests : IAsyncDisposable
{
    private WebApplication? _app;

    private async Task<HttpClient> StartAsync(
        Func<OnlineIdentificationEvent, CancellationToken, Task<OnlineAuthorization>> authorize,
        Action<ControlIdOnlineOptions>? configure = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControlIdOnline(authorize);
        if (configure is not null)
        {
            builder.Services.Configure(configure);
        }

        _app = builder.Build();
        _app.MapControlIdOnline("/api").AllowAnonymousControlIdCallbacks();
        await _app.StartAsync();
        return _app.GetTestClient();
    }

    private static FormUrlEncodedContent Card() =>
        new(new[] { new KeyValuePair<string, string>("device_id", "1"), new KeyValuePair<string, string>("card_value", "123") });

    private static async Task<JsonElement> ResultAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("result").Clone();
    }

    private static void AssertDefaultFallback(JsonElement result)
    {
        Assert.Equal((int)AccessEventType.AccessDenied, result.GetProperty("event").GetInt32());
        Assert.Equal("Sistema indisponível", result.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Handler_that_throws_answers_200_with_the_default_fallback()
    {
        var http = await StartAsync((_, _) => throw new InvalidOperationException("banco fora do ar"));

        var response = await http.PostAsync("/api/new_card.fcgi", Card());

        AssertDefaultFallback(await ResultAsync(response));
    }

    [Fact]
    public async Task Handler_that_faults_asynchronously_answers_200_with_the_fallback()
    {
        var http = await StartAsync(async (_, _) =>
        {
            await Task.Yield();
            throw new TimeoutException("consulta lenta");
        });

        var response = await http.PostAsync("/api/new_card.fcgi", Card());

        AssertDefaultFallback(await ResultAsync(response));
    }

    [Fact]
    public async Task Handler_that_returns_null_answers_200_with_the_fallback()
    {
        var http = await StartAsync((_, _) => Task.FromResult<OnlineAuthorization>(null!));

        var response = await http.PostAsync("/api/new_card.fcgi", Card());

        AssertDefaultFallback(await ResultAsync(response));
    }

    [Fact]
    public async Task Slow_handler_is_cancelled_at_the_deadline_and_the_fallback_is_answered_in_time()
    {
        var tokenCancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var http = await StartAsync(
            async (_, token) =>
            {
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                }
                catch (OperationCanceledException)
                {
                    tokenCancelled.TrySetResult(true);
                    throw;
                }

                return OnlineAuthorization.Grant(1, "x");
            },
            o => o.DecisionTimeout = TimeSpan.FromMilliseconds(200));

        var stopwatch = Stopwatch.StartNew();
        var response = await http.PostAsync("/api/new_card.fcgi", Card());
        stopwatch.Stop();

        AssertDefaultFallback(await ResultAsync(response));
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"A resposta levou {stopwatch.Elapsed}.");
        Assert.True(await tokenCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task Handler_that_ignores_the_token_still_gets_the_fallback_at_the_deadline()
    {
        var http = await StartAsync(
            async (_, _) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(30));
                return OnlineAuthorization.Grant(1, "x");
            },
            o => o.DecisionTimeout = TimeSpan.FromMilliseconds(200));

        var stopwatch = Stopwatch.StartNew();
        var response = await http.PostAsync("/api/new_card.fcgi", Card());
        stopwatch.Stop();

        AssertDefaultFallback(await ResultAsync(response));
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"A resposta levou {stopwatch.Elapsed}.");
    }

    [Fact]
    public async Task Handler_inside_the_deadline_answers_its_own_decision()
    {
        var http = await StartAsync((_, _) => Task.FromResult(OnlineAuthorization.Grant(6, "Neal Caffrey", DeviceAction.OpenDoor(1))));

        var result = await ResultAsync(await http.PostAsync("/api/new_card.fcgi", Card()));

        Assert.Equal((int)AccessEventType.AccessGranted, result.GetProperty("event").GetInt32());
        Assert.Equal(6, result.GetProperty("user_id").GetInt64());
    }

    [Fact]
    public async Task Infinite_deadline_waits_for_the_handler()
    {
        var http = await StartAsync(
            async (_, _) =>
            {
                await Task.Delay(300);
                return OnlineAuthorization.Grant(6, "Neal Caffrey");
            },
            o => o.DecisionTimeout = Timeout.InfiniteTimeSpan);

        var result = await ResultAsync(await http.PostAsync("/api/new_card.fcgi", Card()));

        Assert.Equal((int)AccessEventType.AccessGranted, result.GetProperty("event").GetInt32());
    }

    [Fact]
    public async Task Fallback_authorization_is_configurable()
    {
        var http = await StartAsync(
            (_, _) => throw new InvalidOperationException("falha"),
            o => o.FallbackAuthorization = OnlineAuthorization.NotIdentified("Tente de novo"));

        var result = await ResultAsync(await http.PostAsync("/api/new_card.fcgi", Card()));

        Assert.Equal((int)AccessEventType.NotIdentified, result.GetProperty("event").GetInt32());
        Assert.Equal("Tente de novo", result.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Request_that_cannot_be_parsed_answers_the_fallback_without_running_the_handler()
    {
        var called = 0;
        var http = await StartAsync((_, _) =>
        {
            Interlocked.Increment(ref called);
            return Task.FromResult(OnlineAuthorization.Grant(1, "x"));
        });
        var content = new ByteArrayContent(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        content.Headers.ContentType = new MediaTypeHeaderValue("multipart/form-data");

        var response = await http.PostAsync("/api/new_card.fcgi?device_id=1", content);

        AssertDefaultFallback(await ResultAsync(response));
        Assert.Equal(0, called);
    }

    [Fact]
    public async Task Fallback_counter_carries_the_reason_tag()
    {
        var reasons = new ConcurrentQueue<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Name == "controlid.server.online.fallbacks")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "controlid.online.fallback.reason" && tag.Value is string reason)
                {
                    reasons.Enqueue(reason);
                }
            }
        });
        listener.Start();

        var mode = 0;
        var http = await StartAsync(
            (_, token) => Interlocked.Increment(ref mode) switch
            {
                1 => throw new InvalidOperationException("falha"),
                2 => Task.FromResult<OnlineAuthorization>(null!),
                _ => Task.Delay(Timeout.InfiniteTimeSpan, token).ContinueWith(_ => OnlineAuthorization.Deny(), TaskScheduler.Default),
            },
            o => o.DecisionTimeout = TimeSpan.FromMilliseconds(200));

        await http.PostAsync("/api/new_card.fcgi", Card());
        await http.PostAsync("/api/new_card.fcgi", Card());
        await http.PostAsync("/api/new_card.fcgi", Card());

        Assert.Equal(new[] { "exception", "null", "timeout" }, reasons.ToArray());
    }

    [Fact]
    public void Options_have_the_documented_defaults_and_validate_values()
    {
        var options = new ControlIdOnlineOptions();

        Assert.Equal(TimeSpan.FromSeconds(3), options.DecisionTimeout);
        Assert.Equal(AccessEventType.AccessDenied, options.FallbackAuthorization.Event);
        Assert.Equal("Sistema indisponível", options.FallbackAuthorization.Message);
        Assert.Throws<ArgumentNullException>(() => options.FallbackAuthorization = null!);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.DecisionTimeout = TimeSpan.Zero);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.DecisionTimeout = TimeSpan.FromSeconds(-2));
        options.DecisionTimeout = Timeout.InfiniteTimeSpan;
        Assert.Equal(Timeout.InfiniteTimeSpan, options.DecisionTimeout);
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}
#endif
