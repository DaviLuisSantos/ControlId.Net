using System.Net;
using ControlId.Access;

namespace ControlId.Tests;

public class SystemPasswordsTests
{
    private static readonly ControlIdDeviceOptions Options = new() { BaseAddress = new Uri("http://10.0.0.5") };

    private static FakeDeviceHandler LoggedDevice() => new FakeDeviceHandler().OnJson("login.fcgi", """{"session":"s"}""");

    [Fact]
    public async Task Master_password_with_password_sends_only_the_password()
    {
        var device = LoggedDevice().OnJson("master_password.fcgi", "");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.SetMasterPasswordAsync("novasenha123");

        Assert.Equal("""{"password":"novasenha123"}""", device.RequestsTo("master_password.fcgi").Single().Body);
    }

    [Fact]
    public async Task Master_password_without_password_reads_hash_and_salt()
    {
        var device = LoggedDevice().OnJson("master_password.fcgi", """{"hash":"ab5d13ce","salt":"y2a2B"}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var info = await client.GetMasterPasswordInfoAsync();

        Assert.Equal("{}", device.RequestsTo("master_password.fcgi").Single().Body);
        Assert.Equal("ab5d13ce", info.Hash);
        Assert.Equal("y2a2B", info.Salt);
    }

    [Fact]
    public async Task Master_password_info_does_not_print_hash_or_salt()
    {
        var device = LoggedDevice().OnJson("master_password.fcgi", """{"hash":"ab5d13ce","salt":"y2a2B"}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var text = (await client.GetMasterPasswordInfoAsync()).ToString();

        Assert.DoesNotContain("ab5d13ce", text);
        Assert.DoesNotContain("y2a2B", text);
    }

    [Fact]
    public async Task Master_password_rejects_null_and_empty_without_calling_the_device()
    {
        var device = LoggedDevice();
        using var client = new AccessClient(Options, device.CreateClient());

        await Assert.ThrowsAsync<ArgumentNullException>(() => client.SetMasterPasswordAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => client.SetMasterPasswordAsync(string.Empty));

        Assert.Empty(device.RequestsTo("master_password.fcgi"));
    }

    [Fact]
    public async Task Update_mode_password_with_password_sends_only_the_password()
    {
        var device = LoggedDevice().OnJson("recovery_password.fcgi", """{"enabled":true}""");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.SetUpdateModePasswordAsync("482913");

        Assert.Equal("""{"password":"482913"}""", device.RequestsTo("recovery_password.fcgi").Single().Body);
    }

    [Fact]
    public async Task Update_mode_password_removal_sends_enabled_false()
    {
        var device = LoggedDevice().OnJson("recovery_password.fcgi", """{"enabled":false}""");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.RemoveUpdateModePasswordAsync();

        Assert.Equal("""{"enabled":false}""", device.RequestsTo("recovery_password.fcgi").Single().Body);
    }

    [Theory]
    [InlineData("""{"enabled":true}""", true)]
    [InlineData("""{"enabled":false}""", false)]
    public async Task Update_mode_password_status_sends_empty_body_and_reads_enabled(string response, bool expected)
    {
        var device = LoggedDevice().OnJson("recovery_password.fcgi", response);
        using var client = new AccessClient(Options, device.CreateClient());

        var enabled = await client.GetUpdateModePasswordStatusAsync();

        Assert.Equal("{}", device.RequestsTo("recovery_password.fcgi").Single().Body);
        Assert.Equal(expected, enabled);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12345a")]
    [InlineData("12 456")]
    [InlineData("001818")]
    public async Task Update_mode_password_rejects_invalid_values_without_leaking_them(string password)
    {
        var device = LoggedDevice();
        using var client = new AccessClient(Options, device.CreateClient());

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => client.SetUpdateModePasswordAsync(password));

        if (password.Length > 0 && password != "001818")
        {
            Assert.DoesNotContain(password, ex.Message);
        }

        Assert.Equal("password", ex.ParamName);
        Assert.Empty(device.RequestsTo("recovery_password.fcgi"));
    }

    [Fact]
    public async Task Update_mode_password_rejects_null()
    {
        var device = LoggedDevice();
        using var client = new AccessClient(Options, device.CreateClient());

        await Assert.ThrowsAsync<ArgumentNullException>(() => client.SetUpdateModePasswordAsync(null!));
        Assert.Empty(device.RequestsTo("recovery_password.fcgi"));
    }

    [Fact]
    public async Task Update_mode_password_does_not_leak_in_the_device_error()
    {
        const string password = "482913";
        var device = LoggedDevice().On(
            "recovery_password.fcgi",
            HttpStatusCode.BadRequest,
            """{"error":"Invalid access level for command recovery_password","code":1}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var ex = await Assert.ThrowsAsync<ControlIdException>(() => client.SetUpdateModePasswordAsync(password));

        Assert.Contains("Invalid access level", ex.Message);
        Assert.DoesNotContain(password, ex.Message);
        Assert.DoesNotContain(password, ex.ToString());
        Assert.DoesNotContain(password, ex.ResponseBody ?? string.Empty);
    }

    [Fact]
    public async Task Master_password_does_not_leak_in_the_device_error()
    {
        const string password = "senha-super-secreta";
        var device = LoggedDevice().On("master_password.fcgi", HttpStatusCode.BadRequest, """{"error":"Invalid master password","code":2}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var ex = await Assert.ThrowsAsync<ControlIdException>(() => client.SetMasterPasswordAsync(password));

        Assert.DoesNotContain(password, ex.Message);
        Assert.DoesNotContain(password, ex.ToString());
    }
}
