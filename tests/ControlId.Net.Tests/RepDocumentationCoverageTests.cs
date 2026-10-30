using ControlId.Rep;

namespace ControlId.Tests;

/// <summary>
/// Cada comando da documentação da API do REP iDClass (v4.0.1, https://www.controlid.com.br/suporte/api_idclass_latest.html)
/// tem um método: chama todos contra um REP simulado e confere o endpoint e o método HTTP. Ao surgir um comando novo na
/// documentação, acrescente-o aqui e em docs/COBERTURA.md.
/// </summary>
public class RepDocumentationCoverageTests
{
    private static readonly byte[] Bytes = { 1, 2, 3 };

    public static TheoryData<string, Func<IRepClient, Task>> Endpoints => new()
    {
        // Sessão
        { "login.fcgi", c => c.LoginAsync() },
        { "logout.fcgi", async c => { await c.LoginAsync(); await c.LogoutAsync(); } },
        { "session_is_valid.fcgi", async c => { await c.LoginAsync(); await c.IsSessionValidAsync(); } },
        { "change_login.fcgi", c => c.ChangeLoginAsync("novo", "senha") },

        // Usuários
        { "add_users.fcgi", c => c.AddUsersAsync(new[] { new RepUser { Cpf = 1 } }) },
        { "count_users.fcgi", c => c.CountUsersAsync() },
        { "load_users.fcgi", c => c.LoadUsersAsync() },
        { "remove_users.fcgi", c => c.RemoveUsersAsync(new long[] { 1 }) },
        { "update_users.fcgi", c => c.UpdateUsersAsync(new[] { new RepUserUpdate { Cpf = 1, Name = "x" } }) },
        { "import_users_csv.fcgi", c => c.ImportUsersCsvAsync(Bytes) },
        { "export_users_csv.fcgi", c => c.ExportUsersCsvAsync() },
        { "remove_admins.fcgi", c => c.RemoveAdminsAsync(DataLossConfirmation.Confirmed) },

        // Empregador
        { "edit_company.fcgi", c => c.SetCompanyAsync(new RepCompany { Name = "x" }) },
        { "load_company.fcgi", c => c.GetCompanyAsync() },

        // Sistema
        { "get_system_information.fcgi", c => c.GetSystemInformationAsync() },
        { "get_about.fcgi", c => c.GetAboutAsync() },
        { "set_idcloud.fcgi", c => c.SetIdCloudEnabledAsync(true) },
        { "get_idcloud.fcgi", c => c.GetIdCloudEnabledAsync() },
        { "set_system_date_time.fcgi", c => c.SetDateTimeAsync(new DateTime(2026, 1, 1)) },
        { "get_system_date_time.fcgi", c => c.GetDateTimeAsync() },
        { "set_animation_screen.fcgi", c => c.SetAnimationEnabledAsync(true) },
        { "get_afd.fcgi", c => c.GetAfdAsync() },
        { "set_buzzer_beep.fcgi", c => c.SetBeepEnabledAsync(true) },
        { "get_public_key.fcgi", c => c.GetPublicKeyAsync() },
        { "get_system_network.fcgi", c => c.GetNetworkAsync() },
        { "set_system_network.fcgi", c => c.SetNetworkAsync(new RepNetworkSettings { UseDhcp = true }) },
        { "set_coil_paper.fcgi", c => c.SetCoilPaperAsync(200) },
        { "get_coil_paper.fcgi", c => c.GetCoilPaperAsync() },
        { "set_ticket_size.fcgi", c => c.SetEconomicReceiptEnabledAsync(true) },
        { "set_identification_type.fcgi", c => c.SetOneToOneIdentificationAsync(true) },
        { "set_system_daylight_saving_time.fcgi", c => c.SetDaylightSavingTimeAsync(new RepDaylightSavingTime(new DateTime(2026, 11, 1), new DateTime(2027, 2, 20))) },
        { "get_system_daylight_saving_time.fcgi", c => c.GetDaylightSavingTimeAsync() },
        { "get_info.fcgi", c => c.GetInfoAsync() },
        { "get_system_configuration.fcgi", c => c.GetSystemConfigurationAsync() },

        // Biometria
        { "template_extract.fcgi", c => c.ExtractTemplateAsync(Bytes, 260, 300) },
        { "template_merge.fcgi", c => c.MergeTemplatesAsync(1, new[] { "a", "b", "c" }) },

        // Facial
        { "set_facial_module_config.fcgi", c => c.SetFacialModuleConfigAsync(new RepFacialModuleConfig()) },
        { "get_facial_module_config.fcgi", c => c.GetFacialModuleConfigAsync() },
        { "set_facial_limit.fcgi", c => c.SetFacialLimitAsync("x") },
        { "get_about_facial.fcgi", c => c.GetAboutFacialAsync() },
        { "facial_enroller_state.fcgi", c => c.GetFacialEnrollerStateAsync() },
        { "remote_enroll.fcgi", c => c.StartRemoteEnrollAsync(1) },
        { "cancel_remote_enroll.fcgi", c => c.CancelRemoteEnrollAsync() },
        { "user_set_image.fcgi", c => c.SetUserImageAsync(1, Bytes) },
        { "user_get_image.fcgi", c => c.GetUserImageAsync(1) },
        { "user_destroy_image.fcgi", c => c.DeleteUserImageAsync(1) },
        { "user_import_image.fcgi", c => c.ImportUserImageAsync(1, Bytes) },
    };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Every_documented_command_has_a_method(string endpoint, Func<IRepClient, Task> call)
    {
        var device = new FakeDeviceHandler()
            .OnJson("login.fcgi", """{"session":"s"}""")
            .OnJson("logout.fcgi", "")
            .OnJson("session_is_valid.fcgi", """{"session_is_valid":true}""")
            .OnJson("get_system_date_time.fcgi", """{"day":1,"month":1,"year":2026,"hour":0,"minute":0,"second":0}""")
            .OnJson("get_afd.fcgi", "")
            .OnJson(endpoint, "{}");
        using var client = new RepClient(
            new ControlIdDeviceOptions { BaseAddress = new Uri("https://10.0.0.9"), AcceptAnyServerCertificate = true }, RepMode.Portaria671, device.CreateClient());

        await call(client);

        var request = Assert.Single(device.RequestsTo(endpoint));
        Assert.Equal(HttpMethod.Post, request.Method);
    }

    [Fact]
    public void The_documentation_has_47_commands()
    {
        // 45 com exemplo de URL, mais load_company e get_system_network, que só aparecem descritos.
        Assert.Equal(47, Endpoints.Count());
    }
}
