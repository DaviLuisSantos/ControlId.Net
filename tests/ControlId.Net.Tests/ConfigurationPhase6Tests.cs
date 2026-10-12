using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using ControlId.Access;
using ControlId.Access.Configuration;

namespace ControlId.Tests;

public class ConfigurationPhase6Tests
{
    private static readonly ControlIdDeviceOptions Options = new() { BaseAddress = new Uri("http://10.0.0.5") };

    private static FakeDeviceHandler LoggedDevice() => new FakeDeviceHandler().OnJson("login.fcgi", """{"session":"s"}""");

    [Fact]
    public async Task Pjsip_example_from_documentation()
    {
        var device = LoggedDevice().OnJson("set_configuration.fcgi", "{}");
        using var client = new AccessClient(Options, device.CreateClient());

        // Exemplo "Conexão SIP" da documentação da interfonia.
        await client.SetConfigurationSectionAsync(new PjsipSettings
        {
            Enabled = true,
            ServerIp = "meu_servidor",
            ServerPort = 5060,
            ServerOutboundPort = 10000,
            ServerOutboundPortRange = 1000,
            NumericBranchEnabled = true,
            Branch = "987",
            Login = "987",
            Password = "123456",
            PeerToPeerEnabled = false,
            AudioCodecsEnabled = "PCMU/8000/1;PCMA/8000/1",
        });

        using var body = JsonDocument.Parse(device.RequestsTo("set_configuration.fcgi").Single().Body);
        var pjsip = body.RootElement.GetProperty("pjsip");
        var expected = new Dictionary<string, string>
        {
            ["enabled"] = "1", ["server_ip"] = "meu_servidor", ["server_port"] = "5060", ["server_outbound_port"] = "10000",
            ["server_outbound_port_range"] = "1000", ["numeric_branch_enabled"] = "1", ["branch"] = "987", ["login"] = "987",
            ["password"] = "123456", ["peer_to_peer_enabled"] = "0", ["audio_codecs_enabled"] = "PCMU/8000/1;PCMA/8000/1",
        };
        Assert.Equal(expected.Count, pjsip.EnumerateObject().Count()); // nulos não são enviados
        foreach (var pair in expected)
        {
            Assert.Equal(pair.Value, pjsip.GetProperty(pair.Key).GetString());
        }
    }

    [Fact]
    public async Task Several_sections_in_one_call_and_indexed_card_reader()
    {
        var device = LoggedDevice().OnJson("set_configuration.fcgi", "{}");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.SetConfigurationSectionsAsync(new ConfigurationSection[]
        {
            new GeneralSettings { BeepEnabled = true, CatraTimeout = 3000 },
            new OnvifSettings { RtspEnabled = 1, RtspPort = 556 }, // a documentação declara rtsp_enabled como int
            new CardReaderSettings(1) { Mode = "WIN" },
        });

        Assert.Equal(
            """{"general":{"beep_enabled":"1","catra_timeout":"3000"},"onvif":{"rtsp_enabled":"1","rtsp_port":"556"},"card_reader1":{"mode":"WIN"}}""",
            device.RequestsTo("set_configuration.fcgi").Single().Body);
    }

    [Fact]
    public async Task Get_section_requests_all_fields_and_parses_types()
    {
        var device = LoggedDevice().OnJson(
            "get_configuration.fcgi", """{"general":{"beep_enabled":"1","catra_timeout":"5000","language":"pt_BR","online":"x"}}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var general = await client.GetConfigurationSectionAsync<GeneralSettings>();

        Assert.True(general.BeepEnabled);
        Assert.Equal(5000, general.CatraTimeout);
        Assert.Equal("pt_BR", general.Language);
        Assert.Null(general.Online);     // valor fora do formato: fica null, sem derrubar a leitura
        Assert.Null(general.SshEnabled); // não devolvido pelo equipamento

        using var body = JsonDocument.Parse(device.RequestsTo("get_configuration.fcgi").Single().Body);
        var requested = body.RootElement.GetProperty("general").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("beep_enabled", requested);
        Assert.Contains("web_server_enabled", requested);
        Assert.Equal(typeof(GeneralSettings).GetProperties().Count(p => p.GetCustomAttribute<JsonPropertyNameAttribute>() is not null), requested.Count);
    }

    [Fact]
    public async Task Empty_section_is_rejected_before_sending()
    {
        var device = LoggedDevice();
        using var client = new AccessClient(Options, device.CreateClient());

        await Assert.ThrowsAsync<ArgumentException>(() => client.SetConfigurationSectionAsync(new AlarmSettings()));
        Assert.Empty(device.Requests);
    }

    [Fact]
    public void Every_generated_section_has_a_name_and_round_trips()
    {
        var sections = typeof(ConfigurationSection).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(ConfigurationSection)) && !t.IsAbstract)
            .ToList();

        Assert.True(sections.Count >= 30, $"esperava as seções da documentação, achou {sections.Count}");
        foreach (var type in sections)
        {
            var section = (ConfigurationSection)Activator.CreateInstance(type, type == typeof(CardReaderSettings) ? new object[] { 0 } : Array.Empty<object>())!;
            Assert.False(string.IsNullOrEmpty(section.SectionName), type.Name);

            var properties = type.GetProperties().Where(p => p.GetCustomAttribute<JsonPropertyNameAttribute>() is not null).ToList();
            Assert.NotEmpty(properties);

            // Preenche cada propriedade com um valor do seu tipo, envia e lê de volta.
            var values = new Dictionary<string, string>();
            foreach (var p in properties)
            {
                var key = p.GetCustomAttribute<JsonPropertyNameAttribute>()!.Name;
                var target = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
                (object value, string text) = target == typeof(bool) ? (true, "1")
                    : target == typeof(int) ? (7, "7")
                    : target == typeof(long) ? (7L, "7")
                    : target == typeof(double) ? (1.5, "1.5")
                    : ((object)"x", "x");
                p.SetValue(section, value);
                values[key] = text;
            }

            Assert.Equal(values, ConfigurationSectionMapper.ToValues(section));

            var copy = (ConfigurationSection)Activator.CreateInstance(type, type == typeof(CardReaderSettings) ? new object[] { 0 } : Array.Empty<object>())!;
            ConfigurationSectionMapper.Populate(copy, values);
            Assert.Equal(values, ConfigurationSectionMapper.ToValues(copy));
        }
    }
}
