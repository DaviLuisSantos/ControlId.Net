using ControlId.Access;

namespace ControlId.Net.IntegrationTests;

/// <summary>Os métodos da rodada 2 de cobertura da Access API (Wi-Fi, mídia da tela, SecBox, OSDP, calibração e provisionamento) contra o simulador.</summary>
public sealed class CoverageRound2EndToEndTests
{
    private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 0x01, 0x02 };

    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private static readonly byte[] Certificate = { 0x2D, 0x2D, 0x2D, 0x2D, 0x2D };

    private static async Task<(RunningSimulator Sim, AccessClient Device)> StartAsync()
    {
        var sim = await RunningSimulator.StartAsync(o => o.SeedSampleData = false);
        return (sim, sim.CreateClient());
    }

    [Fact]
    public async Task Connects_and_forgets_a_wifi_network()
    {
        var (sim, device) = await StartAsync();
        await using var _ = sim;
        using var __ = device;

        await device.SetWifiSourceAsync(WifiSource.Internal);
        var networks = await device.ScanWifiAsync();
        var network = Assert.Single(networks);
        Assert.Equal("Simulador", network.Ssid);

        Assert.Equal(WifiConnectionResult.NetworkNotFound, await device.SetWifiSettingsAsync("Outra", "senha-qualquer"));
        Assert.Equal(WifiConnectionResult.Connected, await device.SetWifiSettingsAsync(network.Ssid!, "senha", network.Bssid));

        var data = await device.GetWifiDataAsync();
        Assert.Equal(WifiSource.Internal, data.SourceKind);
        Assert.True(data.IsConnected);
        Assert.Equal("Simulador", data.ConnectedSsid);
        Assert.NotEmpty(data.AvailableAdapters);
        Assert.Equal(NetworkInterfaceKind.Wired, data.PriorityInterface);

        await device.ForgetWifiConnectedNetworkAsync(DataLossConfirmation.Confirmed);
        Assert.False((await device.GetWifiDataAsync()).IsConnected);

        await device.SetWifiSourceAsync(WifiSource.Disabled);
        Assert.Equal(WifiSource.Disabled, (await device.GetWifiDataAsync()).SourceKind);
    }

    [Fact]
    public async Task Checks_ip_conflict_on_both_interfaces()
    {
        var (sim, device) = await StartAsync();
        await using var _ = sim;
        using var __ = device;

        Assert.False(await device.CheckIpConflictAsync(NetworkInterfaceKind.Wired, "192.168.0.50"));
        Assert.True(await device.CheckIpConflictAsync(NetworkInterfaceKind.Wireless, "10.0.0.1"));
    }

    [Fact]
    public async Task Uploads_eap_certificates_and_reads_the_csr()
    {
        var (sim, device) = await StartAsync();
        await using var _ = sim;
        using var __ = device;

        await device.UploadEapCaCertificateAsync(Certificate);
        await device.UploadEapCaCertificateWifiAsync(Certificate);
        await device.UploadEapTlsClientCertificateAsync(Certificate);
        await device.UploadEapTlsPrivateKeyAsync(Certificate);
        await device.PersistEapTlsPrivateKeyAsync();

        Assert.Contains("CERTIFICATE REQUEST", await device.GetEapTlsCsrAsync());
        Assert.Contains("CERTIFICATE REQUEST", await device.GetEapTlsCsrAsync("segredo"));
    }

    [Fact]
    public async Task Manages_slideshow_images()
    {
        var (sim, device) = await StartAsync();
        await using var _ = sim;
        using var __ = device;

        await device.SendSlideshowImageAsync("um.jpg", Jpeg);
        await device.SendSlideshowImageAsync("dois.jpg", Jpeg);

        var list = await device.GetSlideshowImagesAsync();
        Assert.Equal(new[] { "dois.jpg", "um.jpg" }, list.Filenames);
        Assert.True(list.MaxFiles > 0);
        Assert.Equal(Jpeg, await device.GetSlideshowImageAsync("um.jpg"));

        await device.DeleteSlideshowImageAsync("um.jpg");
        Assert.Equal(new[] { "dois.jpg" }, (await device.GetSlideshowImagesAsync()).Filenames);

        await device.RemoveAllSlideshowImagesAsync(DataLossConfirmation.Confirmed);
        Assert.Empty((await device.GetSlideshowImagesAsync()).Filenames);
    }

    [Fact]
    public async Task Manages_kiosk_icons()
    {
        var (sim, device) = await StartAsync();
        await using var _ = sim;
        using var __ = device;

        await device.SetKioskIconAsync("porta.png", Png);

        var list = await device.GetKioskIconsAsync();
        Assert.Equal(new[] { "porta.png" }, list.Filenames);
        Assert.Equal(Png, await device.GetKioskIconAsync("porta.png"));

        await device.DeleteKioskIconAsync("porta.png");
        Assert.Empty((await device.GetKioskIconsAsync()).Filenames);
    }

    [Fact]
    public async Task Manages_the_phone_icon_the_streaming_logo_and_sip_conference()
    {
        var (sim, device) = await StartAsync();
        await using var _ = sim;
        using var __ = device;

        await device.SetPhoneIconAsync(Png);
        Assert.Equal(Png, await device.GetPhoneIconAsync());
        await device.RemovePhoneIconAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => device.GetPhoneIconAsync());

        await device.SetStreamingLogoAsync(Png);
        Assert.Equal(Png, await device.GetStreamingLogoAsync());
        await device.RemoveStreamingLogoAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => device.GetStreamingLogoAsync());

        await device.MakeSipConferenceAsync();
        await device.MakeSipConferenceAsync("1001");
    }

    [Fact]
    public async Task Reads_the_secbox_and_updates_its_firmware()
    {
        var (sim, device) = await StartAsync();
        await using var _ = sim;
        using var __ = device;

        Assert.True(await device.DeviceHasSecBoxAsync());
        Assert.True(await device.IsSecBoxActiveAsync());
        Assert.False(string.IsNullOrEmpty(await device.GetSecBoxSerialNumberAsync()));
        Assert.True(await device.GetSecBoxFirmwareVersionAsync() > 0);

        Assert.True(await device.UpdateSecBoxFirmwareAsync());
        Assert.True((await device.GetSecBoxFirmwareUpdateStatusAsync()).IsFinished);
        var finished = await device.WaitSecBoxFirmwareUpdateAsync(TimeSpan.FromMilliseconds(10));
        Assert.True(finished.IsFinished);
    }

    [Fact]
    public async Task Changes_the_secbox_crypto_key()
    {
        var (sim, device) = await StartAsync();
        await using var _ = sim;
        using var __ = device;

        Assert.True((await device.ChangeSecBoxCryptoKeyToDefaultAsync(DataLossConfirmation.Confirmed)).Succeeded);
        Assert.True((await device.ChangeSecBoxCryptoKeyToRandomAsync(DataLossConfirmation.Confirmed)).Succeeded);
        Assert.True((await device.ResetSecBoxCryptoKeyAsync(DataLossConfirmation.Confirmed)).Succeeded);
    }

    [Fact]
    public async Task Configures_the_dual_sided_secret()
    {
        var (sim, device) = await StartAsync();
        await using var _ = sim;
        using var __ = device;

        Assert.False((await device.GetDualSidedStatusAsync()).SecretConfigured);

        await device.SetDualSidedSecretAsync(new string('a', 64));

        Assert.True((await device.GetDualSidedStatusAsync()).SecretConfigured);
    }

    [Fact]
    public async Task Manages_osdp_keys_and_reads_its_status()
    {
        var (sim, device) = await StartAsync();
        await using var _ = sim;
        using var __ = device;

        Assert.False(await device.GetOsdpInstallationModeAsync());
        await device.SetOsdpInstallationModeAsync(true);
        Assert.True(await device.GetOsdpInstallationModeAsync());

        await device.SetOsdpScbkAsync("0102030405060708090A0B0C0D0E0F10");
        Assert.Equal("0102030405060708090A0B0C0D0E0F10", (await device.GetOsdpScbkAsync()).Scbk);

        var status = await device.GetOsdpConnectionStatusAsync(1);
        Assert.Equal(1, status[1]);
        Assert.Equal(0, status[2]);

        var capabilities = await device.GetOsdpOutputCapabilitiesAsync(1);
        Assert.Equal(2, capabilities.NumOutputs);

        await device.SetOsdpSecureKeyAsync(1, 1, "0102030405060708090A0B0C0D0E0F10");
        await device.SetOsdpSecureTypeAsync(1, 1, OsdpSecureType.SecureOnly);
        await device.RefreshOsdpAsync();
    }

    [Fact]
    public async Task Calibrates_the_camera()
    {
        var (sim, device) = await StartAsync();
        await using var _ = sim;
        using var __ = device;

        Assert.False((await device.GetCameraCalibrationStatusAsync()).Calibrated);

        await device.StartCameraCalibrationAsync(30);
        var finished = await device.WaitCameraCalibrationAsync(TimeSpan.FromMilliseconds(10));
        Assert.False(finished.Running);
        Assert.True(finished.Calibrated);

        await device.CancelCameraCalibrationAsync();
        Assert.False((await device.GetCameraCalibrationStatusAsync()).Calibrated);
    }

    [Fact]
    public async Task Applies_licenses_and_the_pro_operation_mode()
    {
        var (sim, device) = await StartAsync();
        await using var _ = sim;
        using var __ = device;

        await device.UpgradeFingerprintCapacityAsync("licenca");
        foreach (var license in Enum.GetValues<FaceLicense>())
        {
            await device.ApplyFaceLicenseAsync(license, "licenca");
        }

        await device.SetProOperationModeAndRebootAsync("20k");
    }

    [Fact]
    public async Task Redirects_a_request_to_another_device()
    {
        var (sim, device) = await StartAsync();
        await using var _ = sim;
        using var __ = device;

        var byId = await device.RedirectRequestAsync(RedirectTarget.ByDeviceId(7), "system_information");
        Assert.Contains("system_information", byId);

        var byAddress = await device.RedirectRequestAsync(
            RedirectTarget.ByAddress("192.168.0.20", "admin", "admin"),
            "get_ntp_server",
            query: new Dictionary<string, string> { ["extra"] = "1" });
        Assert.Contains("get_ntp_server", byAddress);
    }
}
