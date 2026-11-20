using ControlId.Access;

namespace ControlId.Tests;

public class SecBoxDeviceTests
{
    private const string ValidSecret = "3f1c9a7be2d04856a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718";

    private static readonly ControlIdDeviceOptions Options = new()
    {
        BaseAddress = new Uri("http://10.0.0.5"),
        Login = "admin",
        Password = "admin",
    };

    private static FakeDeviceHandler LoggedDevice() =>
        new FakeDeviceHandler().OnJson("login.fcgi", """{"session":"s"}""");

    private static FakeDeviceHandler LoggedDevice(string endpoint, string response) =>
        LoggedDevice().OnJson(endpoint, response);

    [Fact]
    public async Task Device_has_secbox_is_a_get_and_parses_has_secbox()
    {
        var device = LoggedDevice("device_has_secbox.fcgi", """{"has_secbox":true}""");
        using var client = new AccessClient(Options, device.CreateClient());

        Assert.True(await client.DeviceHasSecBoxAsync());
        Assert.Equal(HttpMethod.Get, device.RequestsTo("device_has_secbox.fcgi").Single().Method);
    }

    [Fact]
    public async Task Secbox_is_active_is_a_get_and_parses_is_active()
    {
        var device = LoggedDevice("secbox_is_active.fcgi", """{"isActive":true}""");
        using var client = new AccessClient(Options, device.CreateClient());

        Assert.True(await client.IsSecBoxActiveAsync());
        Assert.Equal(HttpMethod.Get, device.RequestsTo("secbox_is_active.fcgi").Single().Method);
    }

    [Fact]
    public async Task Secbox_serial_number_is_a_get_and_returns_the_serial()
    {
        var device = LoggedDevice("secbox_serial_number.fcgi", """{"serial":"0A1B2C3D4E5F"}""");
        using var client = new AccessClient(Options, device.CreateClient());

        Assert.Equal("0A1B2C3D4E5F", await client.GetSecBoxSerialNumberAsync());
        Assert.Equal(HttpMethod.Get, device.RequestsTo("secbox_serial_number.fcgi").Single().Method);
    }

    [Fact]
    public async Task Secbox_firmware_version_is_a_get_and_returns_fw_version()
    {
        var device = LoggedDevice("update_secbox_firmware_version.fcgi", """{"fw_version":3}""");
        using var client = new AccessClient(Options, device.CreateClient());

        Assert.Equal(3, await client.GetSecBoxFirmwareVersionAsync());
        Assert.Equal(HttpMethod.Get, device.RequestsTo("update_secbox_firmware_version.fcgi").Single().Method);
    }

    [Fact]
    public async Task Update_secbox_firmware_is_a_post_and_returns_is_updated()
    {
        var device = LoggedDevice("update_secbox_firmware.fcgi", """{"isUpdated":true}""");
        using var client = new AccessClient(Options, device.CreateClient());

        Assert.True(await client.UpdateSecBoxFirmwareAsync());
        Assert.Equal(HttpMethod.Post, device.RequestsTo("update_secbox_firmware.fcgi").Single().Method);
    }

    [Fact]
    public async Task Secbox_firmware_update_status_is_a_get_and_parses_progress()
    {
        var device = LoggedDevice("update_secbox_firmware_status.fcgi", """{"isFinished":false,"bitsWritten":4096}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var status = await client.GetSecBoxFirmwareUpdateStatusAsync();

        Assert.False(status.IsFinished);
        Assert.Equal(4096, status.BitsWritten);
        Assert.Equal(HttpMethod.Get, device.RequestsTo("update_secbox_firmware_status.fcgi").Single().Method);
    }

    [Fact]
    public async Task Wait_secbox_firmware_update_polls_until_finished()
    {
        var device = LoggedDevice()
            .OnJson("update_secbox_firmware_status.fcgi", """{"isFinished":false,"bitsWritten":1024}""")
            .OnJson("update_secbox_firmware_status.fcgi", """{"isFinished":false,"bitsWritten":2048}""")
            .OnJson("update_secbox_firmware_status.fcgi", """{"isFinished":true,"bitsWritten":4096}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var status = await client.WaitSecBoxFirmwareUpdateAsync(TimeSpan.FromMilliseconds(1));

        Assert.True(status.IsFinished);
        Assert.Equal(4096, status.BitsWritten);
        Assert.Equal(3, device.RequestsTo("update_secbox_firmware_status.fcgi").Count());
    }

    [Fact]
    public async Task Wait_secbox_firmware_update_respects_cancellation()
    {
        var device = LoggedDevice("update_secbox_firmware_status.fcgi", """{"isFinished":false,"bitsWritten":1}""");
        using var client = new AccessClient(Options, device.CreateClient());
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.WaitSecBoxFirmwareUpdateAsync(TimeSpan.FromMilliseconds(5), cts.Token));

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.WaitSecBoxFirmwareUpdateAsync(TimeSpan.FromMilliseconds(5), cancelled.Token));
    }

    [Fact]
    public async Task Wait_secbox_firmware_update_rejects_a_non_positive_interval()
    {
        using var client = new AccessClient(Options, LoggedDevice().CreateClient());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.WaitSecBoxFirmwareUpdateAsync(TimeSpan.Zero));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.WaitSecBoxFirmwareUpdateAsync(TimeSpan.FromSeconds(-1)));
    }

    [Theory]
    [InlineData("default")]
    [InlineData("random")]
    [InlineData("reset")]
    public async Task Crypto_key_operations_post_and_parse_status_key(string operation)
    {
        var endpoint = operation switch
        {
            "default" => "change_crypto_key_default.fcgi",
            "random" => "change_crypto_key_random.fcgi",
            _ => "reset_crypto_key.fcgi",
        };
        var device = LoggedDevice(endpoint, """{"Status Key":"00"}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var result = operation switch
        {
            "default" => await client.ChangeSecBoxCryptoKeyToDefaultAsync(DataLossConfirmation.Confirmed),
            "random" => await client.ChangeSecBoxCryptoKeyToRandomAsync(DataLossConfirmation.Confirmed),
            _ => await client.ResetSecBoxCryptoKeyAsync(DataLossConfirmation.Confirmed),
        };

        Assert.Equal("00", result.StatusKey);
        Assert.True(result.Succeeded);
        Assert.Equal(HttpMethod.Post, device.RequestsTo(endpoint).Single().Method);
    }

    [Fact]
    public async Task Crypto_key_failure_code_is_reported_as_not_succeeded()
    {
        var device = LoggedDevice("reset_crypto_key.fcgi", """{"Status Key":"01"}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var result = await client.ResetSecBoxCryptoKeyAsync(DataLossConfirmation.Confirmed);

        Assert.Equal("01", result.StatusKey);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Crypto_key_operations_require_confirmation_and_send_nothing_without_it()
    {
        var device = LoggedDevice("change_crypto_key_default.fcgi", """{"Status Key":"00"}""")
            .OnJson("change_crypto_key_random.fcgi", """{"Status Key":"00"}""")
            .OnJson("reset_crypto_key.fcgi", """{"Status Key":"00"}""");
        using var client = new AccessClient(Options, device.CreateClient());

        await Assert.ThrowsAsync<ArgumentException>(() => client.ChangeSecBoxCryptoKeyToDefaultAsync(DataLossConfirmation.None));
        await Assert.ThrowsAsync<ArgumentException>(() => client.ChangeSecBoxCryptoKeyToRandomAsync(DataLossConfirmation.None));
        await Assert.ThrowsAsync<ArgumentException>(() => client.ResetSecBoxCryptoKeyAsync(default));

        Assert.Empty(device.Requests);
    }

    [Fact]
    public async Task Set_dual_sided_secret_posts_the_secret()
    {
        var device = LoggedDevice("set_dual_sided_secret.fcgi", "{}");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.SetDualSidedSecretAsync(ValidSecret);

        var request = device.RequestsTo("set_dual_sided_secret.fcgi").Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal($$"""{"secret":"{{ValidSecret}}"}""", request.Body);
    }

    [Fact]
    public async Task Set_dual_sided_secret_accepts_uppercase_hex()
    {
        var device = LoggedDevice("set_dual_sided_secret.fcgi", "{}");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.SetDualSidedSecretAsync(ValidSecret.ToUpperInvariant());

        Assert.Single(device.RequestsTo("set_dual_sided_secret.fcgi"));
    }

    [Fact]
    public async Task Set_dual_sided_secret_validates_without_leaking_the_value()
    {
        var device = LoggedDevice("set_dual_sided_secret.fcgi", "{}");
        using var client = new AccessClient(Options, device.CreateClient());
        var tooShort = ValidSecret.Substring(0, 62);
        var notHex = ValidSecret.Substring(0, 63) + "z";

        await Assert.ThrowsAsync<ArgumentNullException>(() => client.SetDualSidedSecretAsync(null!));
        var empty = await Assert.ThrowsAsync<ArgumentException>(() => client.SetDualSidedSecretAsync(""));
        var shortError = await Assert.ThrowsAsync<ArgumentException>(() => client.SetDualSidedSecretAsync(tooShort));
        var hexError = await Assert.ThrowsAsync<ArgumentException>(() => client.SetDualSidedSecretAsync(notHex));

        Assert.DoesNotContain(tooShort, shortError.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(notHex, hexError.Message, StringComparison.Ordinal);
        Assert.NotNull(empty.Message);
        Assert.Empty(device.RequestsTo("set_dual_sided_secret.fcgi"));
    }

    [Fact]
    public async Task Get_dual_sided_status_is_a_post_and_parses_the_flags()
    {
        var device = LoggedDevice("get_dual_sided_status.fcgi", """{"secret_configured":true,"door1_peer_address_valid":false}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var status = await client.GetDualSidedStatusAsync();

        Assert.True(status.SecretConfigured);
        Assert.False(status.Door1PeerAddressValid);
        Assert.Equal(HttpMethod.Post, device.RequestsTo("get_dual_sided_status.fcgi").Single().Method);
    }
}
