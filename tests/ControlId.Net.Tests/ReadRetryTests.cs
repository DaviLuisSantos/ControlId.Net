using System.Net;
using ControlId.Access;
using ControlId.Internal;
using ControlId.Rep;

namespace ControlId.Tests;

public class ReadRetryTests
{
    private static ControlIdDeviceOptions Options(int readRetries) => new()
    {
        BaseAddress = new Uri("http://10.0.0.9"),
        ReadRetries = readRetries,
        RetryDelay = TimeSpan.Zero,
    };

    private static FakeDeviceHandler Device() => new FakeDeviceHandler().OnJson("login.fcgi", """{"session":"s"}""");

    [Fact]
    public async Task Retries_a_read_after_a_transient_http_error()
    {
        var device = Device()
            .On("load_objects.fcgi", HttpStatusCode.ServiceUnavailable)
            .OnJson("load_objects.fcgi", """{"users":[{"id":1,"name":"Ana"}]}""");
        using var client = new AccessClient(Options(readRetries: 2), device.CreateClient());

        var users = await client.GetUsersAsync();

        Assert.Equal("Ana", Assert.Single(users).Name);
        Assert.Equal(2, device.RequestsTo("load_objects.fcgi").Count());
    }

    [Fact]
    public async Task Never_retries_a_command_that_changes_the_device()
    {
        // Abrir a porta duas vezes não é aceitável, mesmo com o equipamento devolvendo 503.
        var device = Device().On("execute_actions.fcgi", HttpStatusCode.ServiceUnavailable);
        using var client = new AccessClient(Options(readRetries: 3), device.CreateClient());

        await Assert.ThrowsAsync<ControlIdException>(() => client.OpenDoorAsync(1));

        Assert.Single(device.RequestsTo("execute_actions.fcgi"));
    }

    [Fact]
    public async Task Gives_up_after_the_configured_retries()
    {
        var device = Device().On("load_objects.fcgi", HttpStatusCode.BadGateway);
        using var client = new AccessClient(Options(readRetries: 2), device.CreateClient());

        var error = await Assert.ThrowsAsync<ControlIdException>(() => client.GetUsersAsync());

        Assert.Equal(HttpStatusCode.BadGateway, error.StatusCode);
        Assert.Equal(3, device.RequestsTo("load_objects.fcgi").Count());
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task Does_not_retry_errors_that_would_repeat(HttpStatusCode status)
    {
        var device = Device().On("load_objects.fcgi", status, """{"error":"Invalid command","code":1}""");
        using var client = new AccessClient(Options(readRetries: 3), device.CreateClient());

        await Assert.ThrowsAsync<ControlIdException>(() => client.GetUsersAsync());

        Assert.Single(device.RequestsTo("load_objects.fcgi"));
    }

    [Fact]
    public async Task Is_off_by_default()
    {
        var device = Device().On("load_objects.fcgi", HttpStatusCode.ServiceUnavailable);
        using var client = new AccessClient(new ControlIdDeviceOptions { BaseAddress = new Uri("http://10.0.0.9") }, device.CreateClient());

        await Assert.ThrowsAsync<ControlIdException>(() => client.GetUsersAsync());

        Assert.Single(device.RequestsTo("load_objects.fcgi"));
    }

    [Fact]
    public async Task Retries_a_read_after_a_network_failure()
    {
        var handler = new FlakyHandler(failures: 1);
        using var client = new AccessClient(Options(readRetries: 1), new HttpClient(handler));

        Assert.Empty(await client.GetUsersAsync());
        Assert.Equal(2, handler.LoadAttempts);
    }

    [Fact]
    public async Task Retries_a_streamed_read_before_the_body_arrives()
    {
        var device = new FakeDeviceHandler()
            .OnJson("login.fcgi", """{"session":"rep"}""")
            .On("get_afd.fcgi", HttpStatusCode.ServiceUnavailable)
            .OnBytes("get_afd.fcgi", "0000000011\r\n"u8.ToArray(), "text/plain");
        using var client = new RepClient(Options(readRetries: 1), RepMode.Portaria671, device.CreateClient());

        using var stream = await client.OpenAfdStreamAsync();
        using var reader = new StreamReader(stream);

        Assert.Equal("0000000011", (await reader.ReadLineAsync())?.Trim());
        Assert.Equal(2, device.RequestsTo("get_afd.fcgi").Count());
    }

    [Fact]
    public async Task Stops_waiting_when_the_caller_cancels()
    {
        // Cancela depois que o equipamento respondeu 503, durante a espera de 30 s antes da nova tentativa.
        using var cts = new CancellationTokenSource();
        var handler = new CancelOn503Handler(cts);
        var options = Options(readRetries: 3);
        options.RetryDelay = TimeSpan.FromSeconds(30);
        using var client = new AccessClient(options, new HttpClient(handler));
        var started = DateTime.UtcNow;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetUsersAsync(cts.Token));

        Assert.Equal(1, handler.LoadAttempts);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(10));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(ControlIdDeviceOptions.MaxReadRetries + 1)]
    public void Rejects_an_invalid_number_of_retries(int retries)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AccessClient(new ControlIdDeviceOptions
        {
            BaseAddress = new Uri("http://10.0.0.9"),
            ReadRetries = retries,
        }));
    }

    [Fact]
    public void Doubles_the_delay_with_jitter()
    {
        for (var i = 0; i < 100; i++)
        {
            var first = ReadRetry.Delay(TimeSpan.FromMilliseconds(100), 1).TotalMilliseconds;
            var third = ReadRetry.Delay(TimeSpan.FromMilliseconds(100), 3).TotalMilliseconds;

            Assert.InRange(first, 80, 120);
            Assert.InRange(third, 320, 480);
        }
    }

    [Theory]
    [InlineData("load_objects", true)]
    [InlineData("get_afd", true)]
    [InlineData("system_information", true)]
    [InlineData("create_objects", false)]
    [InlineData("execute_actions", false)]
    [InlineData("remote_enroll", false)]
    [InlineData("set_configuration", false)]
    [InlineData("alarm_status", false)] // lê ou desliga o alarme, conforme o corpo
    public void Only_known_reads_are_retried(string command, bool readOnly) =>
        Assert.Equal(readOnly, ReadRetry.IsReadOnly(command));

    /// <summary>Responde 503 em <c>load_objects</c> e pede o cancelamento logo em seguida.</summary>
    private sealed class CancelOn503Handler(CancellationTokenSource cts) : HttpMessageHandler
    {
        public int LoadAttempts { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("login.fcgi", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"session":"s"}""") });
            }

            LoadAttempts++;
            cts.CancelAfter(TimeSpan.FromMilliseconds(50));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent(string.Empty) });
        }
    }

    /// <summary>Falha na rede nas primeiras <c>load_objects</c>, depois responde.</summary>
    private sealed class FlakyHandler(int failures) : HttpMessageHandler
    {
        private int _failures = failures;

        public int LoadAttempts { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("login.fcgi", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"session":"s"}""") });
            }

            LoadAttempts++;
            if (_failures-- > 0)
            {
                throw new HttpRequestException("Conexão recusada.");
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"users":[]}""") });
        }
    }
}
