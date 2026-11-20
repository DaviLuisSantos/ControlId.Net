using System.Net;
using ControlId.Access;
using ControlId.Internal;

namespace ControlId.Tests;

/// <summary>"Invalid command", "Invalid table" e 404 viram <see cref="ControlIdNotSupportedException"/>; o cache evita repetir o comando recusado.</summary>
public class NotSupportedTests
{
    private static ControlIdDeviceOptions Options() => new() { BaseAddress = new Uri("http://10.0.0.9") };

    private static FakeDeviceHandler Device() => new FakeDeviceHandler().OnJson("login.fcgi", """{"session":"s"}""");

    [Fact]
    public async Task Invalid_command_throws_NotSupported_and_the_second_call_sends_nothing()
    {
        var device = Device().On("load_objects.fcgi", HttpStatusCode.BadRequest, """{"error":"Invalid command","code":1}""");
        using var client = new AccessClient(Options(), device.CreateClient());

        var first = await Assert.ThrowsAsync<ControlIdNotSupportedException>(() => client.GetUsersAsync());
        var second = await Assert.ThrowsAsync<ControlIdNotSupportedException>(() => client.GetUsersAsync());

        Assert.Equal("load_objects", first.Command);
        Assert.Equal("Invalid command", first.DeviceMessage);
        Assert.Equal(HttpStatusCode.BadRequest, first.StatusCode);
        Assert.Equal("load_objects", second.Command);
        Assert.Single(device.RequestsTo("load_objects.fcgi"));
    }

    [Fact]
    public async Task Http_404_throws_NotSupported_and_marks_the_command()
    {
        var device = Device().On("load_objects.fcgi", HttpStatusCode.NotFound);
        using var client = new AccessClient(Options(), device.CreateClient());

        var first = await Assert.ThrowsAsync<ControlIdNotSupportedException>(() => client.GetUsersAsync());
        await Assert.ThrowsAsync<ControlIdNotSupportedException>(() => client.GetUsersAsync());

        Assert.Equal(HttpStatusCode.NotFound, first.StatusCode);
        Assert.Single(device.RequestsTo("load_objects.fcgi"));
    }

    [Fact]
    public async Task Invalid_table_throws_NotSupported_without_marking_the_command()
    {
        var device = Device().On("load_objects.fcgi", HttpStatusCode.BadRequest, """{"error":"Invalid table","code":1}""");
        using var client = new AccessClient(Options(), device.CreateClient());

        await Assert.ThrowsAsync<ControlIdNotSupportedException>(() => client.GetUsersAsync());
        await Assert.ThrowsAsync<ControlIdNotSupportedException>(() => client.GetUsersAsync());

        // Outra tabela do mesmo comando pode existir: cada chamada vai ao equipamento.
        Assert.Equal(2, device.RequestsTo("load_objects.fcgi").Count());
    }

    [Fact]
    public async Task A_marked_command_does_not_block_other_commands()
    {
        var device = Device()
            .On("load_objects.fcgi", HttpStatusCode.NotFound)
            .OnJson("system_information.fcgi", """{"uptime":{"days":0,"hours":1,"minutes":2,"seconds":3}}""");
        using var transport = new ControlIdTransport(Options(), device.CreateClient());

        await Assert.ThrowsAsync<ControlIdNotSupportedException>(() => transport.PostRawAsync("load_objects.fcgi", null, CancellationToken.None));

        Assert.True(transport.IsKnownUnsupported("load_objects"));
        Assert.False(transport.IsKnownUnsupported("system_information"));
        await transport.PostRawAsync("system_information.fcgi", null, CancellationToken.None);
        Assert.Single(device.RequestsTo("system_information.fcgi"));
    }

    [Fact]
    public async Task A_command_marked_by_hand_fails_without_any_request()
    {
        var device = Device().OnJson("load_objects.fcgi", "{}");
        using var transport = new ControlIdTransport(Options(), device.CreateClient());
        transport.MarkUnsupported("load_objects");

        await Assert.ThrowsAsync<ControlIdNotSupportedException>(() => transport.PostRawAsync("load_objects.fcgi", null, CancellationToken.None));

        Assert.Empty(device.Requests);
    }

    [Fact]
    public async Task The_exception_carries_the_known_firmware()
    {
        var device = Device().On("load_objects.fcgi", HttpStatusCode.BadRequest, """{"error":"Invalid command","code":1}""");
        using var transport = new ControlIdTransport(Options(), device.CreateClient());
        transport.Firmware = "6.9.12";

        var first = await Assert.ThrowsAsync<ControlIdNotSupportedException>(() => transport.PostRawAsync("load_objects.fcgi", null, CancellationToken.None));
        var second = await Assert.ThrowsAsync<ControlIdNotSupportedException>(() => transport.PostRawAsync("load_objects.fcgi", null, CancellationToken.None));

        Assert.Equal("6.9.12", first.Firmware);
        Assert.Equal("6.9.12", second.Firmware);
        Assert.Contains("6.9.12", first.Message, StringComparison.Ordinal);
        Assert.Contains("load_objects", first.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_404_on_login_is_a_plain_device_error_and_does_not_mark_login()
    {
        var device = new FakeDeviceHandler().On("login.fcgi", HttpStatusCode.NotFound);
        using var transport = new ControlIdTransport(Options(), device.CreateClient());

        var error = await Assert.ThrowsAsync<ControlIdDeviceErrorException>(() => transport.LoginAsync(CancellationToken.None));

        Assert.False(transport.IsKnownUnsupported("login"));
        Assert.Equal(HttpStatusCode.NotFound, error.StatusCode);
    }

    [Fact]
    public async Task NotSupported_is_a_DeviceError_and_a_ControlIdException()
    {
        var device = Device().On("load_objects.fcgi", HttpStatusCode.BadRequest, """{"error":"Invalid command","code":1}""");
        using var client = new AccessClient(Options(), device.CreateClient());

        var error = await Assert.ThrowsAnyAsync<ControlIdException>(() => client.GetUsersAsync());

        Assert.IsAssignableFrom<ControlIdDeviceErrorException>(error);
    }

    [Fact]
    public async Task A_regular_device_error_is_not_NotSupported()
    {
        var device = Device().On("load_objects.fcgi", HttpStatusCode.BadRequest, """{"error":"Invalid door","code":7}""");
        using var client = new AccessClient(Options(), device.CreateClient());

        await Assert.ThrowsAsync<ControlIdDeviceErrorException>(() => client.GetUsersAsync());

        Assert.Equal(2, device.Requests.Count); // login + um comando; nada ficou marcado
        await Assert.ThrowsAsync<ControlIdDeviceErrorException>(() => client.GetUsersAsync());
        Assert.Equal(2, device.RequestsTo("load_objects.fcgi").Count());
    }
}
