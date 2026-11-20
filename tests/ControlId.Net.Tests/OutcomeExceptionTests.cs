using System.Net;
using System.Net.Sockets;
using System.Text;
using ControlId.Access;

namespace ControlId.Tests;

/// <summary>Cada tipo de falha do transporte vira a exceção do seu desfecho (leitura x escrita, conexão, timeout, erro do equipamento).</summary>
public class OutcomeExceptionTests
{
    private const string Users = """{"users":[{"id":1,"name":"Ana"}]}""";

    private static ControlIdDeviceOptions Options(int readRetries = 0) => new()
    {
        BaseAddress = new Uri("http://10.0.0.9"),
        ReadRetries = readRetries,
        RetryDelay = TimeSpan.Zero,
    };

    private static HttpRequestException Refused() =>
        new("Conexão recusada.", new SocketException((int)SocketError.ConnectionRefused));

    private static HttpRequestException Dropped() =>
        new("Conexão encerrada.", new IOException("Connection reset by peer."));

    private static TaskCanceledException TimedOut() => new("O prazo do cliente venceu.");

    [Fact]
    public async Task Read_that_cannot_connect_throws_ConnectionException()
    {
        var device = new ScriptedDevice(_ => throw Refused());
        using var client = new AccessClient(Options(), new HttpClient(device));

        var error = await Assert.ThrowsAsync<ControlIdConnectionException>(() => client.GetUsersAsync());

        Assert.IsType<HttpRequestException>(error.InnerException);
        Assert.Null(error.StatusCode);
        Assert.Contains("Falha de comunicação", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Read_whose_connection_drops_throws_ConnectionException()
    {
        // Em leitura não há dúvida de desfecho: nada foi alterado no equipamento.
        var device = new ScriptedDevice(_ => throw Dropped());
        using var client = new AccessClient(Options(), new HttpClient(device));

        await Assert.ThrowsAsync<ControlIdConnectionException>(() => client.GetUsersAsync());
    }

    [Fact]
    public async Task Read_that_times_out_throws_TimeoutException()
    {
        var device = new ScriptedDevice(_ => throw TimedOut());
        using var client = new AccessClient(Options(), new HttpClient(device));

        var error = await Assert.ThrowsAsync<ControlIdTimeoutException>(() => client.GetUsersAsync());

        Assert.IsType<TaskCanceledException>(error.InnerException);
        Assert.Contains("Timeout ao chamar", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Write_that_times_out_throws_OutcomeUnknown_with_TimedOut()
    {
        var device = new ScriptedDevice(_ => throw TimedOut());
        using var client = new AccessClient(Options(), new HttpClient(device));

        var error = await Assert.ThrowsAsync<ControlIdOutcomeUnknownException>(() => client.OpenDoorAsync(1));

        Assert.True(error.TimedOut);
        Assert.Equal("execute_actions", error.Command);
        Assert.IsType<TaskCanceledException>(error.InnerException);
    }

    [Fact]
    public async Task Write_whose_connection_drops_throws_OutcomeUnknown_without_TimedOut()
    {
        var device = new ScriptedDevice(_ => throw Dropped());
        using var client = new AccessClient(Options(), new HttpClient(device));

        var error = await Assert.ThrowsAsync<ControlIdOutcomeUnknownException>(() => client.OpenDoorAsync(1));

        Assert.False(error.TimedOut);
        Assert.Equal("execute_actions", error.Command);
        Assert.IsType<HttpRequestException>(error.InnerException);
    }

    [Fact]
    public async Task Write_that_never_connected_throws_ConnectionException()
    {
        // A recusa de conexão prova que o comando não saiu: é seguro repetir.
        var device = new ScriptedDevice(_ => throw Refused());
        using var client = new AccessClient(Options(), new HttpClient(device));

        await Assert.ThrowsAsync<ControlIdConnectionException>(() => client.OpenDoorAsync(1));
    }

    [Fact]
    public async Task Timeout_types_differ_between_read_and_write()
    {
        var device = new ScriptedDevice(_ => throw TimedOut());
        using var client = new AccessClient(Options(), new HttpClient(device));

        var read = await Record.ExceptionAsync(() => client.GetUsersAsync());
        var write = await Record.ExceptionAsync(() => client.OpenDoorAsync(1));

        Assert.IsType<ControlIdTimeoutException>(read);
        Assert.IsType<ControlIdOutcomeUnknownException>(write);
    }

    [Fact]
    public async Task Device_error_throws_DeviceErrorException_with_the_device_fields()
    {
        var device = new FakeDeviceHandler()
            .OnJson("login.fcgi", """{"session":"s"}""")
            .On("load_objects.fcgi", HttpStatusCode.BadRequest, """{"error":"Invalid door","code":7}""");
        using var client = new AccessClient(Options(), device.CreateClient());

        var error = await Assert.ThrowsAsync<ControlIdDeviceErrorException>(() => client.GetUsersAsync());

        Assert.Equal(HttpStatusCode.BadRequest, error.StatusCode);
        Assert.Equal(7, error.DeviceErrorCode);
        Assert.Equal("Invalid door", error.DeviceMessage);
        Assert.Contains("Invalid door", error.ResponseBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Read_retries_still_apply_to_connection_and_timeout_failures()
    {
        var attempts = 0;
        var device = new ScriptedDevice(_ =>
        {
            attempts++;
            return attempts switch
            {
                1 => throw Refused(),
                2 => throw TimedOut(),
                _ => Json(Users),
            };
        });
        using var client = new AccessClient(Options(readRetries: 2), new HttpClient(device));

        var users = await client.GetUsersAsync();

        Assert.Equal("Ana", Assert.Single(users).Name);
        Assert.Equal(3, device.Commands);
    }

    [Fact]
    public async Task Read_retries_give_up_with_the_last_failure_type()
    {
        var device = new ScriptedDevice(_ => throw TimedOut());
        using var client = new AccessClient(Options(readRetries: 1), new HttpClient(device));

        await Assert.ThrowsAsync<ControlIdTimeoutException>(() => client.GetUsersAsync());

        Assert.Equal(2, device.Commands);
    }

    [Fact]
    public async Task Write_is_never_retried_after_a_timeout()
    {
        var device = new ScriptedDevice(_ => throw TimedOut());
        using var client = new AccessClient(Options(readRetries: 3), new HttpClient(device));

        await Assert.ThrowsAsync<ControlIdOutcomeUnknownException>(() => client.OpenDoorAsync(1));

        Assert.Equal(1, device.Commands);
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    // Responde o login e delega os demais comandos ao roteiro do teste.
    private sealed class ScriptedDevice : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _command;
        private int _commands;

        public ScriptedDevice(Func<HttpRequestMessage, HttpResponseMessage> command) => _command = command;

        public int Commands => Volatile.Read(ref _commands);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/login.fcgi", StringComparison.Ordinal))
            {
                return Task.FromResult(Json("""{"session":"s"}"""));
            }

            Interlocked.Increment(ref _commands);
            return Task.FromResult(_command(request));
        }
    }
}
