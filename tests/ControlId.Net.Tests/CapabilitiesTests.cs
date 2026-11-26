using ControlId.Access;
using ControlId.Internal;

namespace ControlId.Tests;

public class CapabilitiesTests
{
    private static readonly ControlIdDeviceOptions Options = new() { BaseAddress = new Uri("http://10.0.0.5") };

    private static FakeDeviceHandler Device(string version, string extra = "") =>
        new FakeDeviceHandler()
            .OnJson("login.fcgi", """{"session":"s"}""")
            .OnJson("system_information.fcgi", "{\"version\":\"" + version + "\",\"serial\":\"X\"" + extra + "}");

    [Fact]
    public async Task Capabilities_of_a_known_firmware_list_the_missing_commands()
    {
        var device = Device("6.9.12", ",\"model\":\"iDFace\"");
        using var client = new AccessClient(Options, device.CreateClient());

        var capabilities = await client.GetCapabilitiesAsync();

        Assert.Equal("6.9.12", capabilities.Firmware);
        Assert.Equal("iDFace", capabilities.Model);
        Assert.Contains("template_sync_init", capabilities.UnsupportedCommands);
        Assert.False(capabilities.Supports("template_sync_init"));
        Assert.False(capabilities.Supports("template_sync_init.fcgi"));
        Assert.True(capabilities.Supports("load_objects"));
        Assert.Equal("6.9.12", client.Transport.Firmware);
        Assert.Single(device.RequestsTo("system_information.fcgi"));
    }

    [Fact]
    public async Task Calling_a_missing_command_on_a_known_firmware_throws_without_a_request()
    {
        var device = Device("6.9.12");
        using var client = new AccessClient(Options, device.CreateClient());
        await client.GetCapabilitiesAsync();
        var before = device.Requests.Count;

        var error = await Assert.ThrowsAsync<ControlIdNotSupportedException>(() => client.StartTemplateSyncAsync());

        Assert.Equal("template_sync_init", error.Command);
        Assert.Equal("6.9.12", error.Firmware);
        Assert.Equal(before, device.Requests.Count);
        Assert.Empty(device.RequestsTo("template_sync_init.fcgi"));
    }

    [Fact]
    public async Task Firmware_6_13_1_marks_only_its_own_missing_commands()
    {
        var device = Device("6.13.1");
        using var client = new AccessClient(Options, device.CreateClient());

        var capabilities = await client.GetCapabilitiesAsync();

        Assert.False(capabilities.Supports("get_catra_info"));
        Assert.True(capabilities.Supports("template_sync_init"));
        Assert.True(client.Transport.IsKnownUnsupported("get_catra_info"));
        Assert.False(client.Transport.IsKnownUnsupported("template_sync_init"));
    }

    [Theory]
    [InlineData("7.0.1")]
    [InlineData("6.9.123")]
    [InlineData("")]
    public async Task Unknown_firmware_has_no_restrictions_and_marks_nothing(string version)
    {
        var device = Device(version);
        using var client = new AccessClient(Options, device.CreateClient());

        var capabilities = await client.GetCapabilitiesAsync();

        Assert.Empty(capabilities.UnsupportedCommands);
        Assert.True(capabilities.Supports("template_sync_init"));
        Assert.False(client.Transport.IsKnownUnsupported("template_sync_init"));
    }

    [Fact]
    public async Task Firmware_with_a_suffix_still_matches_the_matrix()
    {
        var device = Device("6.9.12-rc1");
        using var client = new AccessClient(Options, device.CreateClient());

        var capabilities = await client.GetCapabilitiesAsync();

        Assert.False(capabilities.Supports("remote_led_control"));
    }

    [Fact]
    public void Supports_requires_a_command()
    {
        var capabilities = new DeviceCapabilities(null, null, Array.Empty<string>());

        Assert.Throws<ArgumentException>(() => capabilities.Supports(" "));
        Assert.Throws<ArgumentNullException>(() => new DeviceCapabilities(null, null, null!));
    }

    [Fact]
    public void Matrix_has_no_duplicates_and_only_bare_command_names()
    {
        foreach (var (_, commands) in FirmwareCapabilityMatrix.All)
        {
            Assert.Equal(commands.Length, commands.Distinct(StringComparer.Ordinal).Count());
            Assert.All(commands, c => Assert.Equal(Telemetry.CommandName(c), c));
        }
    }
}
