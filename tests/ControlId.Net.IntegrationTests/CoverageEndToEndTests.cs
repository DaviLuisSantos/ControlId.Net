using System.Text;
using ControlId.Access;

namespace ControlId.Net.IntegrationTests;

/// <summary>Os métodos de cobertura da Access API (hardware, tabelas customizadas, diagnóstico e senhas) contra o simulador.</summary>
public sealed class CoverageEndToEndTests
{
    [Fact]
    public async Task Hardware_reads_answer_in_the_documented_shape()
    {
        await using var sim = await RunningSimulator.StartAsync(o => o.SeedSampleData = false);
        using var device = sim.CreateClient();

        var io = await device.GetIoStateAsync();
        Assert.NotEmpty(io.Gpios);
        Assert.NotEmpty(io.Relays);
        Assert.False(await device.IsIdentifyingAsync());
        Assert.Equal(0, (await device.GetTurnstileCountersAsync()).TotalTurns);
        Assert.NotEmpty(await device.GetLastSnapshotAsync());
        Assert.False(string.IsNullOrEmpty(await device.GetRfidModuleAsync()));
        Assert.Equal(26, (await device.GetHidLastCardAsync()).Bits);
        Assert.Equal(26, (await device.GetLastWiegandReadAsync()).CardReadNumBits);
        Assert.Equal(0, await device.CountFaceTemplatesAsync());
    }

    [Fact]
    public async Task Matches_face_and_fingerprint_templates()
    {
        await using var sim = await RunningSimulator.StartAsync(o => o.SeedSampleData = false);
        using var device = sim.CreateClient();
        var userId = await device.CreateUserAsync(new AccessUser { Name = "Maria" });

        var template = Convert.ToBase64String(new byte[1024]);
        var hit = await device.MatchFaceTemplateAsync(template, userId);
        Assert.True(hit.Match);
        Assert.Equal(userId, hit.Info!.MatchUserId);
        Assert.False((await device.MatchFaceTemplateAsync(template)).Match);

        var result = await device.MatchFingerprintTemplatesAsync(new[] { new byte[] { 1, 2, 3 }, new byte[] { 4, 5 } });
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task Manages_custom_tables_and_fields()
    {
        await using var sim = await RunningSimulator.StartAsync(o => o.SeedSampleData = false);
        using var device = sim.CreateClient();

        var tableId = await device.CreateCustomTableAsync(
            "visitas", "Visitas", new[] { new CustomFieldDefinition { ColumnName = "empresa", Name = "Empresa", Type = CustomFieldType.Text } });
        Assert.True(tableId > 0);

        var fieldId = await device.AddCustomFieldAsync("visitas", new CustomFieldDefinition { ColumnName = "placa", Name = "Placa", Type = CustomFieldType.Text });
        await device.UpdateCustomFieldAsync(fieldId, new CustomFieldDefinition { ColumnName = "placa", Name = "Placa do veículo", Type = CustomFieldType.Text });

        var custom = await device.GetObjectMetadataAsync(ObjectMetadataFilter.Custom);
        var table = Assert.Single(custom);
        Assert.Equal("visitas", table.Key);
        Assert.Contains("empresa", table.Value.Fields.Keys);
        Assert.Contains("placa", table.Value.Fields.Keys);
        Assert.Contains("users", (await device.GetObjectMetadataAsync(ObjectMetadataFilter.Default)).Keys);
        Assert.True((await device.GetObjectMetadataAsync()).Count >= 2);

        var metadataCsv = Encoding.UTF8.GetString(await device.ExportCustomTablesMetadataCsvAsync());
        Assert.Contains("visitas", metadataCsv);

        Assert.Equal(new[] { fieldId }, await device.RemoveCustomFieldsAsync(DataLossConfirmation.Confirmed, new[] { fieldId }));
        await device.DeleteCustomTablesAsync(DataLossConfirmation.Confirmed, new[] { tableId });
        Assert.Empty(await device.GetObjectMetadataAsync(ObjectMetadataFilter.Custom));
    }

    [Fact]
    public async Task Exports_an_object_as_csv()
    {
        await using var sim = await RunningSimulator.StartAsync(o => o.SeedSampleData = false);
        using var device = sim.CreateClient();
        await device.CreateUserAsync(new AccessUser { Name = "Maria", Registration = "1020" });

        var csv = Encoding.UTF8.GetString(await device.ExportObjectCsvAsync("users", new[] { "name", "registration" }));

        Assert.StartsWith("name,registration", csv);
        Assert.Contains("Maria,1020", csv);
    }

    [Fact]
    public async Task Configures_and_reads_the_ntp_servers()
    {
        await using var sim = await RunningSimulator.StartAsync(o => o.SeedSampleData = false);
        using var device = sim.CreateClient();

        await device.SetNtpServerAsync("a.ntp.br", "b.ntp.br");

        var servers = await device.GetNtpServerAsync();
        Assert.Equal("a.ntp.br", servers.Server1);
        Assert.Equal("b.ntp.br", servers.Server2);
        Assert.True(await device.GetNtpServerStatusAsync());
    }

    [Fact]
    public async Task Reads_network_diagnostics()
    {
        await using var sim = await RunningSimulator.StartAsync(o => o.SeedSampleData = false);
        using var device = sim.CreateClient();

        Assert.True((await device.CheckPortAvailabilityAsync(8000, 8010)).IsAvailable);
        Assert.False(string.IsNullOrEmpty(await device.GetVpnIpAsync()));
        Assert.Contains("CERTIFICATE", await device.GetSslCertificateAsync());
        Assert.Equal(Ieee8021XStatus.Disabled, await device.Get8021XStatusAsync());
        Assert.Equal(Ieee8021XStatus.Disabled, await device.Get8021XStatusAsync(NetworkInterfaceKind.Wireless));
    }

    [Theory]
    [InlineData(DeviceLog.AccessControl)]
    [InlineData(DeviceLog.OpenVpn)]
    [InlineData(DeviceLog.Wpa)]
    [InlineData(DeviceLog.WpaSupplicant)]
    public async Task Reads_each_device_log(DeviceLog log)
    {
        await using var sim = await RunningSimulator.StartAsync(o => o.SeedSampleData = false);
        using var device = sim.CreateClient();

        Assert.False(string.IsNullOrWhiteSpace(await device.GetDeviceLogAsync(log)));
    }

    [Fact]
    public async Task Manages_the_master_and_update_mode_passwords()
    {
        await using var sim = await RunningSimulator.StartAsync(o => o.SeedSampleData = false);
        using var device = sim.CreateClient();

        await device.SetMasterPasswordAsync("senha-mestra");
        var info = await device.GetMasterPasswordInfoAsync();
        Assert.False(string.IsNullOrEmpty(info.Hash));
        Assert.False(string.IsNullOrEmpty(info.Salt));

        Assert.False(await device.GetUpdateModePasswordStatusAsync());
        await device.SetUpdateModePasswordAsync("123456");
        Assert.True(await device.GetUpdateModePasswordStatusAsync());
        await device.RemoveUpdateModePasswordAsync();
        Assert.False(await device.GetUpdateModePasswordStatusAsync());
    }
}
