using System.Net;
using System.Text.Json;
using ControlId.Access;
using ControlId.Access.Configuration;
using ControlId.Access.Online;

namespace ControlId.Tests;

/// <summary>Comportamentos observados num iDFace real (firmware 6.9.12) que a documentação não descreve.</summary>
public class IdFaceFirmwareTests
{
    private static readonly ControlIdDeviceOptions Options = new() { BaseAddress = new Uri("http://10.0.0.5") };

    private static FakeDeviceHandler LoggedDevice() => new FakeDeviceHandler().OnJson("login.fcgi", """{"session":"s"}""");

    private const string MissingCatraTimeout =
        """{"error":"Node or attribute not found. Node path: config->general->param[name=catra_timeout]","code":1}""";

    [Fact]
    public async Task Get_section_drops_parameters_the_firmware_does_not_have_and_retries()
    {
        var device = LoggedDevice()
            .On("get_configuration.fcgi", HttpStatusCode.BadRequest, MissingCatraTimeout)
            .OnJson("get_configuration.fcgi", """{"general":{"beep_enabled":"1"}}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var general = await client.GetConfigurationSectionAsync<GeneralSettings>();

        Assert.True(general.BeepEnabled);
        Assert.Null(general.CatraTimeout);
        var requests = device.RequestsTo("get_configuration.fcgi").Select(r => FieldsOf(r.Body, "general")).ToList();
        Assert.Equal(2, requests.Count);
        Assert.Contains("catra_timeout", requests[0]);
        Assert.DoesNotContain("catra_timeout", requests[1]);
        Assert.Equal(requests[0].Count - 1, requests[1].Count);

        // O cliente lembra do parâmetro inexistente: a próxima leitura já vai sem ele, numa requisição só.
        await client.GetConfigurationSectionAsync<GeneralSettings>();
        Assert.Equal(3, device.RequestsTo("get_configuration.fcgi").Count());
        Assert.DoesNotContain("catra_timeout", FieldsOf(device.RequestsTo("get_configuration.fcgi").Last().Body, "general"));
    }

    [Fact]
    public async Task Missing_parameter_inside_a_module_is_dropped_and_section_without_fields_is_left_out()
    {
        var device = LoggedDevice()
            .On("get_configuration.fcgi", HttpStatusCode.BadRequest,
                """{"error":"Node or attribute not found. Node path: config->modules->module[name=monitor]->param[name=alive_interval]","code":1}""")
            .OnJson("get_configuration.fcgi", """{"general":{"beep_enabled":"0"}}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var values = await client.GetConfigurationAsync(new Dictionary<string, IEnumerable<string>>
        {
            ["general"] = new[] { "beep_enabled" },
            ["monitor"] = new[] { "alive_interval" },
        });

        Assert.Equal("0", values["general"]["beep_enabled"]);
        Assert.False(values.ContainsKey("monitor"));
        Assert.Equal("""{"general":["beep_enabled"]}""", device.RequestsTo("get_configuration.fcgi").Last().Body);
    }

    [Fact]
    public async Task Other_configuration_errors_are_not_swallowed()
    {
        var device = LoggedDevice().On("get_configuration.fcgi", HttpStatusCode.BadRequest, """{"error":"Invalid session","code":1}""");
        using var client = new AccessClient(Options, device.CreateClient());

        await Assert.ThrowsAsync<ControlIdException>(() => client.GetConfigurationSectionAsync<GeneralSettings>());
        Assert.Single(device.RequestsTo("get_configuration.fcgi"));
    }

    [Fact]
    public async Task Push_remote_address_is_text()
    {
        var device = LoggedDevice().OnJson(
            "get_configuration.fcgi", """{"push_server":{"push_remote_address":"192.168.0.20:8080","push_request_period":"60","push_request_timeout":"15000"}}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var push = await client.GetConfigurationSectionAsync<PushServerSettings>();

        Assert.Equal("192.168.0.20:8080", push.PushRemoteAddress);
        Assert.Equal(60, push.PushRequestPeriod);
    }

    [Fact]
    public async Task Face_template_extraction_without_success_field_counts_as_success()
    {
        var device = LoggedDevice()
            .OnJson("face_template_extract.fcgi", """{"face_template":"QUJD"}""")
            .OnJson("user_test_image.fcgi", """{"success":false,"errors":[{"code":2,"message":"Face not detected"}]}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var extracted = await client.ExtractFaceTemplateAsync(new byte[] { 1, 2, 3 });
        var rejected = await client.TestUserImageAsync(new byte[] { 1, 2, 3 });

        Assert.True(extracted.Success);
        Assert.Equal("QUJD", extracted.FaceTemplate);
        Assert.False(rejected.Success);
        Assert.False(new FaceImageResult { Errors = { new FaceImageError { Code = 2 } } }.Success);
    }

    [Fact]
    public async Task Remote_authorization_fills_the_members_the_firmware_requires()
    {
        var device = LoggedDevice().OnJson("remote_user_authorization.fcgi", "{}");
        using var client = new AccessClient(Options, device.CreateClient());
        var authorization = OnlineAuthorization.NotIdentified("Teste");

        await client.AuthorizeRemotelyAsync(authorization);

        // Corpo aceito por um iDFace 6.9.12; sem qualquer um destes membros ele responde "Invalid member".
        Assert.Equal(
            """{"event":3,"user_id":0,"user_name":"","user_image":false,"portal_id":1,"actions":[],"message":"Teste"}""",
            device.RequestsTo("remote_user_authorization.fcgi").Single().Body);
        Assert.Null(authorization.UserId); // o objeto do chamador não muda
        Assert.Null(authorization.Actions);
    }

    [Fact]
    public async Task Remote_authorization_keeps_informed_values()
    {
        var device = LoggedDevice().OnJson("remote_user_authorization.fcgi", "{}");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.AuthorizeRemotelyAsync(new OnlineAuthorization
        {
            Event = AccessEventType.AccessGranted, UserId = 6, UserName = "Ana", UserImage = true, PortalId = 3, Actions = new() { DeviceAction.OpenDoor(1) },
        });

        Assert.Equal(
            """{"event":7,"user_id":6,"user_name":"Ana","user_image":true,"portal_id":3,"actions":[{"action":"door","parameters":"door=1"}]}""",
            device.RequestsTo("remote_user_authorization.fcgi").Single().Body);
    }

    [Fact]
    public async Task Online_mode_outside_an_ihm_does_not_send_ihm_enterprise_mode()
    {
        var device = LoggedDevice()
            .OnJson("load_objects.fcgi", """{"devices":[{"id":12,"name":"x","ip":"10.0.0.1:5000"}]}""")
            .OnJson("set_configuration.fcgi", "{}");
        using var client = new AccessClient(Options, device.CreateClient());

        await client.ConfigureOnlineModeAsync(new OnlineModeConfiguration { ServerAddress = "10.0.0.1:5000", Mode = OnlineMode.Enterprise });

        Assert.Equal(
            """{"online_client":{"server_id":"12"},"general":{"online":"1","local_identification":"0"}}""",
            device.RequestsTo("set_configuration.fcgi").Single().Body);
    }

    [Fact]
    public async Task Holiday_after_2038_is_rejected_before_calling_the_device()
    {
        var device = LoggedDevice().OnJson("create_objects.fcgi", """{"ids":[1]}""");
        using var client = new AccessClient(Options, device.CreateClient());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.CreateHolidayAsync(AccessHoliday.FullDay("Natal", new DateTime(2099, 12, 25), TimeSpan.FromHours(-3))));
        Assert.Empty(device.RequestsTo("create_objects.fcgi"));

        Assert.Equal(1, await client.CreateHolidayAsync(AccessHoliday.FullDay("Natal", new DateTime(2030, 12, 25), TimeSpan.FromHours(-3))));
    }

    [Fact]
    public async Task User_type_zero_is_read_as_null_so_the_user_can_be_written_back()
    {
        var device = LoggedDevice()
            .OnJson("load_objects.fcgi", """{"users":[{"id":2032,"name":"Vania","user_type_id":0},{"id":7,"name":"Visitante","user_type_id":1}]}""")
            .OnJson("modify_objects.fcgi", """{"changes":1}""");
        using var client = new AccessClient(Options, device.CreateClient());

        var users = await client.GetUsersAsync();
        Assert.Null(users[0].UserTypeId);
        Assert.Equal(1, users[1].UserTypeId);

        // Gravar user_type_id 0 viola a chave estrangeira no equipamento ("FOREIGN KEY constraint failed").
        await client.UpdateObjectAsync(users[0]);
        Assert.Equal(
            """{"object":"users","values":{"id":2032,"name":"Vania"},"where":{"users":{"id":2032}}}""",
            device.RequestsTo("modify_objects.fcgi").Single().Body);
    }

    private static List<string> FieldsOf(string body, string section)
    {
        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty(section).EnumerateArray().Select(e => e.GetString()!).ToList();
    }
}
