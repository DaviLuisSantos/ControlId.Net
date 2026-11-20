using ControlId.Access;

namespace ControlId.Net.IntegrationTests;

/// <summary>O <see cref="IAccessProvisioningClient"/> (chaves do leitor HID e código OEM, ADR 0024) contra o simulador.</summary>
public sealed class CoverageRound3EndToEndTests
{
    private const string Kek = "1111111111111111111111111111111111111111111111111111111111111111";

    private static async Task<(RunningSimulator Sim, AccessClient Device)> StartAsync()
    {
        var sim = await RunningSimulator.StartAsync(o => o.SeedSampleData = false);
        return (sim, sim.CreateClient());
    }

    [Fact]
    public async Task Injects_hid_keys_and_reads_their_info()
    {
        var (sim, device) = await StartAsync();
        await using var _ = sim;
        using var __ = device;
        var provisioning = device.CreateProvisioningClient();

        Assert.True(await provisioning.InjectHidDefaultKekAsync(Kek, Kek, "4445464B45593031"));
        Assert.True(await provisioning.InjectHidCustomKekAsync("03F001", "03F000", Kek));
        Assert.True(await provisioning.InjectHidMediaKeyAsync("030101", "03F001", "FFFFFFFFFFFF", Kek));

        var defaultKek = await provisioning.GetHidKeyInfoAsync("03F000");
        Assert.True(defaultKek.IsKek);
        Assert.Equal("4445464B45593031", defaultKek.KeyId);
        Assert.Equal(string.Empty, (await provisioning.GetHidKeyInfoAsync("03F001")).KeyId);
        Assert.True((await provisioning.GetHidKeyInfoAsync("030101")).IsMediaKey);
        await Assert.ThrowsAsync<ControlIdException>(() => provisioning.GetHidKeyInfoAsync("0A0A0A"));
    }

    [Fact]
    public async Task Writes_and_reads_hid_oids_and_resets_the_reader()
    {
        var (sim, device) = await StartAsync();
        await using var _ = sim;
        using var __ = device;
        var provisioning = device.CreateProvisioningClient();

        Assert.True(await provisioning.SetHidOidAsync("1.1.1.2.3.5", true));
        Assert.True(await provisioning.SetHidOidAsync("1.1.1.2.3.6", 42));
        Assert.True(await provisioning.SetHidOidAsync("1.1.1.2.3.1", new byte[] { 0xA2, 0x61 }));
        Assert.True((await provisioning.GetHidOidAsync("1.1.1.2.3.5")).Boolean);
        Assert.Equal(42, (await provisioning.GetHidOidAsync("1.1.1.2.3.6")).Integer);
        Assert.Equal("A261", (await provisioning.GetHidOidAsync("1.1.1.2.3.1")).Hex);

        Assert.True((await provisioning.ResetHidConfigAsync(DataLossConfirmation.Confirmed)).IsResetting);
        await Assert.ThrowsAsync<ControlIdException>(() => provisioning.ResetHidConfigAsync(DataLossConfirmation.Confirmed));
        Assert.True((await provisioning.WaitHidConfigResetAsync(TimeSpan.FromMilliseconds(10))).IsDone);

        Assert.True((await provisioning.RestoreHidConfigAsync(DataLossConfirmation.Confirmed, new byte[] { 1, 2, 3 })).IsResetting);
        Assert.True((await provisioning.GetHidConfigResetStatusAsync()).IsDone);
    }

    [Fact]
    public async Task Changes_the_oem_code_and_approved_firmware()
    {
        var (sim, device) = await StartAsync();
        await using var _ = sim;
        using var __ = device;
        var provisioning = device.CreateProvisioningClient();

        Assert.Equal("CID", await provisioning.GetOemCodeAsync());
        await provisioning.SetOemCodeAsync("MYOEM", "ABCDE12345");
        Assert.Equal("MYOEM", await provisioning.GetOemCodeAsync());

        Assert.Equal(new[] { "7.12.0", "7.13.2" }, await provisioning.GetOemDowngradeApprovedVersionsAsync());
        await provisioning.SetOemApprovedFirmwareAsync("MYOEM", "ABCDE12345", "7.13.2");
        await provisioning.ClearOemApprovedFirmwareAsync("MYOEM", "ABCDE12345");
        await Assert.ThrowsAsync<ControlIdException>(() => provisioning.SetOemApprovedFirmwareAsync("OTHER", "ABCDE12345", "7.13.2"));
    }
}
