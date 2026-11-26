using System.Text.Json;
using ControlId.Access;
using ControlId.Access.Configuration;

namespace ControlId.Tests;

public class CoverageWave03Tests
{
    private static readonly ControlIdDeviceOptions Options = new() { BaseAddress = new Uri("http://10.0.0.5") };

    private static FakeDeviceHandler LoggedDevice() => new FakeDeviceHandler().OnJson("login.fcgi", """{"session":"s"}""");

    [Fact]
    public async Task Cards_and_groups_read_the_new_fields()
    {
        var device = LoggedDevice()
            .OnJson("load_objects.fcgi", """{"cards":[{"id":1,"value":123,"user_id":7,"wiegand_format_id":3,"credential_type":2}]}""");
        using var client = new AccessClient(Options, device.CreateClient());
        var card = Assert.Single(await client.GetObjectsAsync<AccessCard>());
        Assert.Equal(3, card.WiegandFormatId);
        Assert.Equal(2, card.CredentialType);

        var groupDevice = LoggedDevice()
            .OnJson("load_objects.fcgi", """{"groups":[{"id":1,"name":"A","identification_mode":2},{"id":2,"name":"B","identification_mode":null}]}""");
        using var groupClient = new AccessClient(Options, groupDevice.CreateClient());
        var groups = await groupClient.GetObjectsAsync<AccessGroup>();
        Assert.Equal(2, groups[0].IdentificationMode);
        Assert.Null(groups[1].IdentificationMode);
    }

    [Fact]
    public async Task Wiegand_modes_and_group_compatibility_deserialize()
    {
        var device = LoggedDevice()
            .OnJson("load_objects.fcgi", """{"wiegand_modes":[{"id":1,"name":"26","size":26,"format":"F[2:9], C[10:25]","parities":"E(1)[2:13]","parities_calculation_order":"1, 26","inverted_facility":0,"inverted_card":1}]}""");
        using var client = new AccessClient(Options, device.CreateClient());
        var mode = Assert.Single(await client.GetObjectsAsync<WiegandMode>());
        Assert.Equal(26, mode.Size);
        Assert.Equal("F[2:9], C[10:25]", mode.Format);
        Assert.Equal("1, 26", mode.ParitiesCalculationOrder);
        Assert.Equal(1, mode.InvertedCard);

        var compat = LoggedDevice()
            .OnJson("load_objects.fcgi", """{"group_compatibility":[{"group_id":1,"compatible_group_id":2}]}""");
        using var compatClient = new AccessClient(Options, compat.CreateClient());
        var link = Assert.Single(await compatClient.GetObjectsAsync<GroupCompatibility>());
        Assert.Equal(1, link.GroupId);
        Assert.Equal(2, link.CompatibleGroupId);

        // Associação não tem chave própria: remover/alterar é recusado antes de enviar.
        await Assert.ThrowsAsync<InvalidOperationException>(() => compatClient.DeleteObjectAsync<GroupCompatibility>(1));
    }

    [Fact]
    public async Task New_configuration_keys_are_mapped()
    {
        var device = LoggedDevice().OnJson("set_configuration.fcgi", "{}");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.SetConfigurationSectionsAsync(new ConfigurationSection[]
        {
            new HidSettings { CardProcess = 3, CustomUseIndex = 1, TocCryptMode = 1, TocCryptKey = "k", TocSiteKey = "s" },
            new OsdpSettings { RemoteValidationEnabled = true, RemoteValidationTimeoutMs = 5000 },
            new IdentifierSettings { TwoPersonAuthEnabled = false },
        });

        using var body = JsonDocument.Parse(device.RequestsTo("set_configuration.fcgi").Single().Body);
        var hid = body.RootElement.GetProperty("hid_reader");
        Assert.Equal("3", hid.GetProperty("card_process").GetString());
        Assert.Equal("1", hid.GetProperty("custom_use_index").GetString());
        Assert.Equal("1", hid.GetProperty("toc_crypt_mode").GetString());
        Assert.Equal("k", hid.GetProperty("toc_crypt_key").GetString());
        Assert.Equal("s", hid.GetProperty("toc_site_key").GetString());
        var osdp = body.RootElement.GetProperty("osdp");
        Assert.Equal("1", osdp.GetProperty("remote_validation_enabled").GetString());
        Assert.Equal("5000", osdp.GetProperty("remote_validation_timeout_ms").GetString());
        Assert.Equal("0", body.RootElement.GetProperty("identifier").GetProperty("two_person_auth_enabled").GetString());
    }
}
