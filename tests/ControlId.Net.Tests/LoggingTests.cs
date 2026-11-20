using System.Net;
using ControlId.Access;
using ControlId.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ControlId.Tests;

public class LoggingTests
{
    private const string Password = "s3nh4-ultra-secreta";
    private const string SessionToken = "tok3n-d4-s3ssao";

    private static ControlIdDeviceOptions Options(MemoryLoggerFactory logs, int readRetries = 0) => new()
    {
        BaseAddress = new Uri("http://10.0.0.9:8080"),
        Login = "operador",
        Password = Password,
        ReadRetries = readRetries,
        RetryDelay = TimeSpan.Zero,
        LoggerFactory = logs,
    };

    private static FakeDeviceHandler Device() => new FakeDeviceHandler().OnJson("login.fcgi", $$"""{"session":"{{SessionToken}}"}""");

    [Fact]
    public async Task Logs_a_successful_login_at_debug_with_the_device_and_category()
    {
        var logs = new MemoryLoggerFactory();
        var device = Device().OnJson("load_objects.fcgi", """{"users":[]}""");
        using var client = new AccessClient(Options(logs), device.CreateClient());

        await client.GetUsersAsync();

        var entry = Assert.Single(logs.Entries, e => e.EventId == 1001);
        Assert.Equal(LogLevel.Debug, entry.Level);
        Assert.Equal(ControlIdTelemetry.LoggerCategory, entry.Category);
        Assert.Contains("10.0.0.9:8080", entry.Message);
    }

    [Fact]
    public async Task Logs_a_refused_login_as_a_warning_with_the_status()
    {
        var logs = new MemoryLoggerFactory();
        var device = new FakeDeviceHandler().On("login.fcgi", HttpStatusCode.Unauthorized, """{"error":"Invalid login","code":1}""");
        using var client = new AccessClient(Options(logs), device.CreateClient());

        await Assert.ThrowsAsync<ControlIdAuthenticationException>(() => client.GetUsersAsync());

        var entry = Assert.Single(logs.Entries, e => e.EventId == 1002);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("401", entry.Message);
        Assert.Contains("10.0.0.9:8080", entry.Message);
    }

    [Fact]
    public async Task Logs_an_expired_session_with_the_command()
    {
        var logs = new MemoryLoggerFactory();
        var device = new FakeDeviceHandler()
            .OnJson("login.fcgi", $$"""{"session":"{{SessionToken}}"}""")
            .On("load_objects.fcgi", HttpStatusCode.Unauthorized)
            .OnJson("load_objects.fcgi", """{"users":[]}""");
        using var client = new AccessClient(Options(logs), device.CreateClient());

        await client.GetUsersAsync();

        var entry = Assert.Single(logs.Entries, e => e.EventId == 1003);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("load_objects", entry.Message);
        Assert.Contains("10.0.0.9:8080", entry.Message);
    }

    [Fact]
    public async Task Logs_each_retry_with_attempt_and_reason()
    {
        var logs = new MemoryLoggerFactory();
        var device = Device()
            .On("load_objects.fcgi", HttpStatusCode.ServiceUnavailable)
            .On("load_objects.fcgi", HttpStatusCode.BadGateway)
            .OnJson("load_objects.fcgi", """{"users":[]}""");
        using var client = new AccessClient(Options(logs, readRetries: 3), device.CreateClient());

        await client.GetUsersAsync();

        var retries = logs.Entries.Where(e => e.EventId == 1004).ToList();
        Assert.Equal(2, retries.Count);
        Assert.All(retries, e => Assert.Equal(LogLevel.Warning, e.Level));
        Assert.Contains("503", retries[0].Message);
        Assert.Contains("502", retries[1].Message);
        Assert.All(retries, e => Assert.Contains("load_objects", e.Message));
    }

    [Fact]
    public async Task Logs_a_device_error_with_status_code_and_message()
    {
        var logs = new MemoryLoggerFactory();
        var device = Device().On("create_objects.fcgi", HttpStatusCode.BadRequest, """{"error":"constraint failed","code":5}""");
        using var client = new AccessClient(Options(logs), device.CreateClient());

        await Assert.ThrowsAsync<ControlIdException>(() => client.CreateUserAsync(new AccessUser { Name = "Ana" }));

        var entry = Assert.Single(logs.Entries, e => e.EventId == 1005);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("create_objects", entry.Message);
        Assert.Contains("400", entry.Message);
        Assert.Contains("5", entry.Message);
        Assert.Contains("constraint failed", entry.Message);
    }

    [Fact]
    public async Task Logs_a_communication_failure_with_the_exception()
    {
        var logs = new MemoryLoggerFactory();
        using var http = new HttpClient(new ThrowingHandler());
        using var client = new AccessClient(Options(logs), http);

        await Assert.ThrowsAsync<ControlIdException>(() => client.GetUsersAsync());

        var entry = Assert.Single(logs.Entries, e => e.EventId == 1006);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.IsType<HttpRequestException>(entry.Exception);
        Assert.Contains("login", entry.Message);
    }

    [Fact]
    public async Task Never_logs_the_session_login_password_or_body()
    {
        var logs = new MemoryLoggerFactory();
        var device = new FakeDeviceHandler()
            .OnJson("login.fcgi", $$"""{"session":"{{SessionToken}}"}""")
            .On("load_objects.fcgi", HttpStatusCode.Unauthorized)
            .On("load_objects.fcgi", HttpStatusCode.ServiceUnavailable, $$"""{"error":"falha","code":7,"detalhe":"{{SessionToken}}"}""");
        using var client = new AccessClient(Options(logs, readRetries: 1), device.CreateClient());

        await Assert.ThrowsAnyAsync<ControlIdException>(() => client.GetUsersAsync());
        await client.LogoutAsync();

        Assert.NotEmpty(logs.Entries);
        foreach (var entry in logs.Entries)
        {
            var text = entry.Message + entry.Exception;
            Assert.DoesNotContain(SessionToken, text);
            Assert.DoesNotContain(Password, text);
            Assert.DoesNotContain("operador", text);
            Assert.DoesNotContain("detalhe", text);
        }
    }

    [Fact]
    public async Task A_logout_without_network_is_logged_at_debug_and_does_not_throw()
    {
        var logs = new MemoryLoggerFactory();
        var device = Device().OnJson("load_objects.fcgi", """{"users":[]}""");
        var handler = new LogoutFailingHandler(device, new HttpRequestException("sem rede"));
        using var client = new AccessClient(Options(logs), new HttpClient(handler));
        await client.GetUsersAsync();

        await client.LogoutAsync();

        Assert.Null(client.Session);
        var entry = Assert.Single(logs.Entries, e => e.EventId == 1012);
        Assert.Equal(LogLevel.Debug, entry.Level);
        Assert.Contains("10.0.0.9:8080", entry.Message);
        Assert.DoesNotContain(SessionToken, entry.Message);
        Assert.Null(entry.Exception);
    }

    [Fact]
    public async Task A_logout_timeout_is_logged_and_does_not_throw()
    {
        var logs = new MemoryLoggerFactory();
        var device = Device().OnJson("load_objects.fcgi", """{"users":[]}""");
        var handler = new LogoutFailingHandler(device, new TaskCanceledException("HttpClient.Timeout"));
        using var client = new AccessClient(Options(logs), new HttpClient(handler));
        await client.GetUsersAsync();

        await client.LogoutAsync();

        Assert.Contains("timeout", Assert.Single(logs.Entries, e => e.EventId == 1012).Message);
    }

    [Fact]
    public void Accept_any_certificate_without_a_fingerprint_warns_once_at_creation()
    {
        var logs = new MemoryLoggerFactory();
        var options = Options(logs);
        options.BaseAddress = new Uri("https://10.0.0.9");
        options.AcceptAnyServerCertificate = true;

        using (new Rep.RepClient(options))
        {
        }

        var entry = Assert.Single(logs.Entries, e => e.EventId == 1013);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("10.0.0.9", entry.Message);
        Assert.Contains("ServerCertificateSha256", entry.Message);

        // Com o HttpClient de fora (caminho do DI) também avisa.
        using (new AccessClient(options, new HttpClient(new ThrowingHandler())))
        {
        }

        Assert.Equal(2, logs.Entries.Count(e => e.EventId == 1013));
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, "ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789")]
    [InlineData(false, "ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789")]
    public void No_certificate_warning_when_validated_or_pinned(bool acceptAny, string? sha256)
    {
        var logs = new MemoryLoggerFactory();
        var options = Options(logs);
        options.BaseAddress = new Uri("https://10.0.0.9");
        options.AcceptAnyServerCertificate = acceptAny;
        options.ServerCertificateSha256 = sha256;

        using (new Rep.RepClient(options))
        {
        }

        Assert.DoesNotContain(logs.Entries, e => e.EventId == 1013);
    }

    [Fact]
    public async Task Accept_any_certificate_registered_by_di_also_warns()
    {
        var logs = new MemoryLoggerFactory();
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(logs);
        services.AddControlIdRep(o =>
        {
            o.BaseAddress = new Uri("https://10.0.0.3");
            o.AcceptAnyServerCertificate = true;
        });
        await using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IControlIdClientFactory>().GetRepClient();

        Assert.Contains("10.0.0.3", Assert.Single(logs.Entries, e => e.EventId == 1013).Message);
    }

    [Fact]
    public async Task Without_a_logger_factory_the_client_works_and_logs_nothing()
    {
        var device = Device().OnJson("load_objects.fcgi", """{"users":[]}""");
        var options = Options(new MemoryLoggerFactory());
        options.LoggerFactory = null;
        using var client = new AccessClient(options, device.CreateClient());

        Assert.Empty(await client.GetUsersAsync());
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("sem rede");
    }

    // Repassa ao equipamento falso, mas o logout falha com a exceção dada.
    private sealed class LogoutFailingHandler : DelegatingHandler
    {
        private readonly Exception _failure;

        public LogoutFailingHandler(HttpMessageHandler inner, Exception failure)
            : base(inner) => _failure = failure;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            request.RequestUri!.AbsolutePath.EndsWith("/logout.fcgi", StringComparison.Ordinal)
                ? throw _failure
                : base.SendAsync(request, cancellationToken);
    }

    private sealed class LogEntry
    {
        public LogEntry(string category, LogLevel level, int eventId, string message, Exception? exception)
        {
            Category = category;
            Level = level;
            EventId = eventId;
            Message = message;
            Exception = exception;
        }

        public string Category { get; }

        public LogLevel Level { get; }

        public int EventId { get; }

        public string Message { get; }

        public Exception? Exception { get; }
    }

    private sealed class MemoryLoggerFactory : ILoggerFactory
    {
        private readonly object _gate = new();
        private readonly List<LogEntry> _entries = new();

        public IReadOnlyList<LogEntry> Entries
        {
            get
            {
                lock (_gate)
                {
                    return _entries.ToList();
                }
            }
        }

        public ILogger CreateLogger(string categoryName) => new MemoryLogger(this, categoryName);

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public void Dispose()
        {
        }

        private sealed class MemoryLogger : ILogger
        {
            private readonly MemoryLoggerFactory _owner;
            private readonly string _category;

            public MemoryLogger(MemoryLoggerFactory owner, string category)
            {
                _owner = owner;
                _category = category;
            }

            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (_owner._gate)
                {
                    _owner._entries.Add(new LogEntry(_category, logLevel, eventId.Id, formatter(state, exception), exception));
                }
            }
        }
    }
}
