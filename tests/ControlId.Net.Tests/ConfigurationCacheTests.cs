using System.Net;
using System.Text.Json;
using ControlId.Access;
using ControlId.Access.Configuration;
using Microsoft.Extensions.Logging;

namespace ControlId.Tests;

/// <summary>Lista de parâmetros de configuração que o firmware não tem: lembrada por sessão, limpa em login novo ou a pedido.</summary>
public class ConfigurationCacheTests
{
    private const string MissingCatraTimeout =
        """{"error":"Node or attribute not found. Node path: config->general->param[name=catra_timeout]","code":1}""";

    private static ControlIdDeviceOptions Options(ILoggerFactory? logs = null) => new()
    {
        BaseAddress = new Uri("http://10.0.0.5"),
        LoggerFactory = logs,
    };

    private static IReadOnlyDictionary<string, IEnumerable<string>> Fields() =>
        new Dictionary<string, IEnumerable<string>> { ["general"] = new[] { "beep_enabled", "catra_timeout" } };

    private static List<string> Requested(FakeDeviceHandler device) =>
        device.RequestsTo("get_configuration.fcgi")
            .Select(r =>
            {
                using var json = JsonDocument.Parse(r.Body);
                return string.Join(",", json.RootElement.GetProperty("general").EnumerateArray().Select(e => e.GetString()));
            })
            .ToList();

    [Fact]
    public async Task Remembered_within_the_same_session()
    {
        var device = new FakeDeviceHandler()
            .OnJson("login.fcgi", """{"session":"s1"}""")
            .On("get_configuration.fcgi", HttpStatusCode.BadRequest, MissingCatraTimeout)
            .OnJson("get_configuration.fcgi", """{"general":{"beep_enabled":"1"}}""");
        using var client = new AccessClient(Options(), device.CreateClient());

        await client.GetConfigurationAsync(Fields());
        await client.GetConfigurationAsync(Fields());

        Assert.Equal(new[] { "beep_enabled,catra_timeout", "beep_enabled", "beep_enabled" }, Requested(device));
    }

    [Fact]
    public async Task A_new_login_after_the_device_restarts_asks_again()
    {
        var device = new FakeDeviceHandler()
            .OnJson("login.fcgi", """{"session":"s1"}""")
            .On("get_configuration.fcgi", HttpStatusCode.BadRequest, MissingCatraTimeout)
            .OnJson("get_configuration.fcgi", """{"general":{"beep_enabled":"1"}}""")
            // O equipamento reiniciou (sessão perdida) e voltou com um firmware que tem o parâmetro.
            .On("get_configuration.fcgi", HttpStatusCode.Unauthorized)
            .OnJson("get_configuration.fcgi", """{"general":{"beep_enabled":"1"}}""")
            .OnJson("get_configuration.fcgi", """{"general":{"beep_enabled":"1","catra_timeout":"3000"}}""");
        using var client = new AccessClient(Options(), device.CreateClient());

        await client.GetConfigurationAsync(Fields());
        await client.GetConfigurationAsync(Fields()); // 401 → relogin; ainda sem o parâmetro (a lista era da sessão antiga)
        var values = await client.GetConfigurationAsync(Fields());

        Assert.Equal(
            new[] { "beep_enabled,catra_timeout", "beep_enabled", "beep_enabled", "beep_enabled", "beep_enabled,catra_timeout" },
            Requested(device));
        Assert.Equal("3000", values["general"]["catra_timeout"]);
        Assert.Equal(2, device.RequestsTo("login.fcgi").Count());
    }

    [Fact]
    public async Task An_explicit_login_clears_the_list()
    {
        var device = new FakeDeviceHandler()
            .OnJson("login.fcgi", """{"session":"s1"}""")
            .On("get_configuration.fcgi", HttpStatusCode.BadRequest, MissingCatraTimeout)
            .OnJson("get_configuration.fcgi", """{"general":{"beep_enabled":"1"}}""")
            .OnJson("get_configuration.fcgi", """{"general":{"beep_enabled":"1","catra_timeout":"3000"}}""");
        using var client = new AccessClient(Options(), device.CreateClient());

        await client.GetConfigurationAsync(Fields());
        await client.LogoutAsync();
        await client.LoginAsync();
        await client.GetConfigurationAsync(Fields());

        Assert.Equal(new[] { "beep_enabled,catra_timeout", "beep_enabled", "beep_enabled,catra_timeout" }, Requested(device));
    }

    [Fact]
    public async Task Clear_configuration_field_cache_asks_again_in_the_same_session()
    {
        var device = new FakeDeviceHandler()
            .OnJson("login.fcgi", """{"session":"s1"}""")
            .On("get_configuration.fcgi", HttpStatusCode.BadRequest, MissingCatraTimeout)
            .OnJson("get_configuration.fcgi", """{"general":{"beep_enabled":"1"}}""")
            .OnJson("get_configuration.fcgi", """{"general":{"beep_enabled":"1","catra_timeout":"3000"}}""");
        using var client = new AccessClient(Options(), device.CreateClient());

        await client.GetConfigurationAsync(Fields());
        client.ClearConfigurationFieldCache();
        var values = await client.GetConfigurationSectionAsync<GeneralSettings>();

        Assert.Equal(3000, values.CatraTimeout);
        Assert.Contains("catra_timeout", Requested(device).Last());
        Assert.Single(device.RequestsTo("login.fcgi"));
    }

    [Fact]
    public async Task A_dropped_field_is_logged_once_without_values()
    {
        var logs = new MemoryLogs();
        var device = new FakeDeviceHandler()
            .OnJson("login.fcgi", """{"session":"segredo-da-sessao"}""")
            .On("get_configuration.fcgi", HttpStatusCode.BadRequest, MissingCatraTimeout)
            .OnJson("get_configuration.fcgi", """{"general":{"beep_enabled":"valor-secreto"}}""");
        using var client = new AccessClient(Options(logs), device.CreateClient());

        await client.GetConfigurationAsync(Fields());
        await client.GetConfigurationAsync(Fields());

        var entry = Assert.Single(logs.Entries, e => e.EventId == 1010);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("10.0.0.5", entry.Message);
        Assert.Contains("general.catra_timeout", entry.Message);
        Assert.DoesNotContain("valor-secreto", entry.Message);
        Assert.DoesNotContain("segredo-da-sessao", entry.Message);
    }

    internal sealed class MemoryLogs : ILoggerFactory, ILogger
    {
        private readonly object _gate = new();
        private readonly List<(int EventId, LogLevel Level, string Message)> _entries = new();

        public IReadOnlyList<(int EventId, LogLevel Level, string Message)> Entries
        {
            get
            {
                lock (_gate)
                {
                    return _entries.ToList();
                }
            }
        }

        public ILogger CreateLogger(string categoryName) => this;

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (_gate)
            {
                _entries.Add((eventId.Id, logLevel, formatter(state, exception)));
            }
        }

        public void Dispose()
        {
        }
    }
}
