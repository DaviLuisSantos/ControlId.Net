using System.Net;
using System.Text;
using ControlId.Access;
using Microsoft.Extensions.Logging;

namespace ControlId.Tests;

/// <summary>
/// Dados pessoais (nome, cartão, foto, PIN e senha) não podem aparecer em logs, spans, métricas, nem em
/// <c>Message</c> e <c>ResponseBody</c> das exceções com <see cref="ExceptionResponseBody.ErrorOnly"/>.
/// </summary>
public class PersonalDataLeakTests
{
    private const string Host = "10.77.18.1";
    private const string Name = "Fulano Sigiloso Pereira";
    private const long Card = 987654321;
    private const string Pin = "482913";
    private const string UserPassword = "senhaDoUsuario!77";
    private const string DevicePassword = "senha-do-admin-xyz";
    private const string Session = "sessao-secreta-abc";

    private static readonly byte[] Photo = TestImages.Jpeg(640, 480, 2048);

    private static string[] Secrets() =>
    [
        Name,
        "Pereira",
        Card.ToString(System.Globalization.CultureInfo.InvariantCulture),
        Pin,
        UserPassword,
        DevicePassword,
        Session,
        Convert.ToBase64String(Photo, 0, 24),
    ];

    private static FakeDeviceHandler Device() => new FakeDeviceHandler().OnJson("login.fcgi", $$"""{"session":"{{Session}}"}""");

    private static async Task<List<Exception>> ExerciseAsync(AccessClient client)
    {
        var failures = new List<Exception>();

        async Task Run(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (ControlIdException ex)
            {
                failures.Add(ex);
            }
        }

        await Run(() => client.CreateUserAsync(new AccessUser { Name = Name, Password = UserPassword, Registration = "r1" }));
        await Run(() => client.CreateCardAsync(new AccessCard { Value = Card, UserId = 1 }));
        await Run(() => client.CreatePinAsync(new AccessPin { Value = Pin, UserId = 1 }));
        await Run(() => client.SetUserImageAsync(1, Photo));
        return failures;
    }

    [Fact]
    public async Task Successful_commands_leak_nothing_to_logs_spans_or_metrics()
    {
        using var telemetry = new TelemetryTests.TelemetryCapture(Host);
        var logs = new CapturingLoggerFactory();
        var device = Device()
            .OnJson("create_objects.fcgi", """{"ids":[1]}""")
            .OnJson("user_set_image.fcgi", """{"success":true,"user_id":1}""");
        var options = telemetry.Options();
        options.Password = DevicePassword;
        options.LoggerFactory = logs;
        using var client = new AccessClient(options, device.CreateClient());

        var failures = await ExerciseAsync(client);

        Assert.Empty(failures);
        AssertNoSecrets(logs, telemetry);
        Assert.NotEmpty(telemetry.Activities);
        Assert.NotEmpty(telemetry.Measurements);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Device_error_that_echoes_the_request_body_leaks_nothing(HttpStatusCode status)
    {
        using var telemetry = new TelemetryTests.TelemetryCapture(Host);
        var logs = new CapturingLoggerFactory();

        // O equipamento devolve, sem o formato de erro, o que recebeu (nome, cartão, PIN, senha e foto).
        var echo = "{\"echo\":{\"name\":\"" + Name + "\",\"password\":\"" + UserPassword + "\",\"card\":" + Card + ",\"pin\":\"" + Pin + "\",\"image\":\"" + Convert.ToBase64String(Photo) + "\"}}";
        var device = Device()
            .On("create_objects.fcgi", status, echo)
            .On("user_set_image.fcgi", status, echo);
        var options = telemetry.Options();
        options.Password = DevicePassword;
        options.LoggerFactory = logs;
        using var client = new AccessClient(options, device.CreateClient());

        var failures = await ExerciseAsync(client);

        Assert.Equal(4, failures.Count);
        foreach (var ex in failures.Cast<ControlIdException>())
        {
            Assert.Null(ex.ResponseBody);
            AssertClean(ex.Message);
            AssertClean(ex.ToString());
        }

        AssertNoSecrets(logs, telemetry);
    }

    [Fact]
    public async Task Device_error_in_the_json_error_format_keeps_only_that_body()
    {
        using var telemetry = new TelemetryTests.TelemetryCapture(Host);
        var logs = new CapturingLoggerFactory();
        var device = Device()
            .On("create_objects.fcgi", HttpStatusCode.BadRequest, """{"error":"Invalid value","code":3}""")
            .On("user_set_image.fcgi", HttpStatusCode.BadRequest, """{"error":"Invalid image","code":4}""");
        var options = telemetry.Options();
        options.Password = DevicePassword;
        options.LoggerFactory = logs;
        using var client = new AccessClient(options, device.CreateClient());

        var failures = await ExerciseAsync(client);

        Assert.Equal(4, failures.Count);
        foreach (var ex in failures.Cast<ControlIdException>())
        {
            Assert.Equal(ExceptionResponseBody.ErrorOnly, options.ExceptionResponseBody);
            Assert.StartsWith("{\"error\":", ex.ResponseBody, StringComparison.Ordinal);
            AssertClean(ex.Message);
            AssertClean(ex.ResponseBody);
            AssertClean(ex.ToString());
        }

        AssertNoSecrets(logs, telemetry);
    }

    [Fact]
    public async Task Unreadable_response_body_is_not_kept_in_the_exception()
    {
        var device = Device().On("create_objects.fcgi", HttpStatusCode.BadGateway, $"<html>{Name} {UserPassword} {Pin}</html>");
        using var client = new AccessClient(
            new ControlIdDeviceOptions { BaseAddress = new Uri("http://" + Host), Login = "admin", Password = DevicePassword, ReadRetries = 0 },
            device.CreateClient());

        var ex = await Assert.ThrowsAnyAsync<ControlIdException>(() => client.CreateUserAsync(new AccessUser { Name = Name, Password = UserPassword }));

        Assert.Null(ex.ResponseBody);
        AssertClean(ex.Message);
        AssertClean(ex.ToString());
    }

    private static void AssertClean(string? text)
    {
        if (text is null)
        {
            return;
        }

        foreach (var secret in Secrets())
        {
            Assert.DoesNotContain(secret, text, StringComparison.Ordinal);
        }
    }

    private static void AssertNoSecrets(CapturingLoggerFactory logs, TelemetryTests.TelemetryCapture telemetry)
    {
        Assert.NotEmpty(logs.Lines);
        foreach (var line in logs.Lines)
        {
            AssertClean(line);
        }

        foreach (var activity in telemetry.Activities)
        {
            AssertClean(activity.DisplayName);
            AssertClean(activity.OperationName);
            AssertClean(activity.StatusDescription);
            foreach (var tag in activity.TagObjects)
            {
                AssertClean(tag.Key);
                AssertClean(Convert.ToString(tag.Value, System.Globalization.CultureInfo.InvariantCulture));
            }

            foreach (var item in activity.Events)
            {
                AssertClean(item.Name);
                foreach (var tag in item.Tags)
                {
                    AssertClean(Convert.ToString(tag.Value, System.Globalization.CultureInfo.InvariantCulture));
                }
            }
        }

        foreach (var measurement in telemetry.Measurements)
        {
            AssertClean(measurement.Instrument);
            foreach (var tag in measurement.Tags)
            {
                AssertClean(tag.Key);
                AssertClean(Convert.ToString(tag.Value, System.Globalization.CultureInfo.InvariantCulture));
            }
        }
    }

    /// <summary>Guarda cada registro como texto: mensagem formatada, pares do estado, escopo e exceção.</summary>
    private sealed class CapturingLoggerFactory : ILoggerFactory
    {
        private readonly object _gate = new();
        private readonly List<string> _lines = new();

        public IReadOnlyList<string> Lines
        {
            get
            {
                lock (_gate)
                {
                    return _lines.ToList();
                }
            }
        }

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(CapturingLoggerFactory owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
            {
                owner.Add(Describe(state));
                return null;
            }

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var text = new StringBuilder();
                text.Append(formatter(state, exception)).Append(' ').Append(Describe(state));
                if (exception is not null)
                {
                    text.Append(' ').Append(exception);
                }

                owner.Add(text.ToString());
            }

            private static string Describe<TState>(TState state)
            {
                if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
                {
                    return string.Join(" ", pairs.Select(p => p.Key + "=" + Convert.ToString(p.Value, System.Globalization.CultureInfo.InvariantCulture)));
                }

                return Convert.ToString(state, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
            }
        }

        private void Add(string line)
        {
            lock (_gate)
            {
                _lines.Add(line);
            }
        }
    }
}
