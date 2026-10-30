using System.Text;
using System.Text.Json;
using ControlId.Rep;
using ControlId.Rep.Afd;

namespace ControlId.Tests;

/// <summary>Regras da documentação da API do REP iDClass (v4.0.1) para os comandos além de usuários básicos e AFD.</summary>
public class RepClientPhase2Tests
{
    private static ControlIdDeviceOptions Options(int maxConcurrentRequests = 4) => new()
    {
        BaseAddress = new Uri("https://10.0.0.9"),
        AcceptAnyServerCertificate = true,
        MaxConcurrentRequests = maxConcurrentRequests,
    };

    private static FakeDeviceHandler Rep() => new FakeDeviceHandler().OnJson("login.fcgi", """{"session":"rep"}""");

    private static RepClient Client(FakeDeviceHandler device, RepMode mode = RepMode.Portaria671, int maxConcurrentRequests = 4) =>
        new(Options(maxConcurrentRequests), mode, device.CreateClient());

    private static (string Query, string Body) Single(FakeDeviceHandler device, string endpoint)
    {
        var request = Assert.Single(device.RequestsTo(endpoint));
        return (request.Uri.Query, request.Body);
    }

    // ---------------------------------------------------------------- Sessão

    [Fact]
    public async Task Change_login_sends_confirmation_and_next_login_uses_new_credentials()
    {
        var device = Rep().OnJson("change_login.fcgi", "").OnJson("login.fcgi", """{"session":"nova"}""").OnJson("count_users.fcgi", """{"count":1}""");
        using var client = Client(device);

        await client.ChangeLoginAsync("ponto", "1234");
        await client.LogoutAsync(); // a sessão cai para todos depois da troca
        await client.CountUsersAsync();

        Assert.Equal("""{"login":"ponto","password":"1234","confirmation":"1234"}""", Single(device, "change_login.fcgi").Body);
        Assert.Equal("""{"login":"ponto","password":"1234"}""", device.RequestsTo("login.fcgi").Last().Body);
    }

    [Fact]
    public async Task Session_is_valid_without_session_does_not_call_the_rep()
    {
        var device = Rep();
        using var client = Client(device);

        Assert.False(await client.IsSessionValidAsync());
        Assert.Empty(device.Requests);
    }

    // ---------------------------------------------------------------- Usuários

    [Fact]
    public async Task Add_users_with_do_match_sends_photo_in_base64()
    {
        var device = Rep().OnJson("add_users.fcgi", "");
        using var client = Client(device);

        await client.AddUsersAsync(new[] { new RepUser { Cpf = 2233445548, Name = "Mauro", ImageTimestamp = 1712930699, Image = new byte[] { 0xFF, 0xD8 } } }, doMatch: false);

        var (query, body) = Single(device, "add_users.fcgi");
        Assert.Equal("?session=rep&mode=671", query);
        Assert.Equal("""{"do_match":false,"users":[{"cpf":2233445548,"name":"Mauro","image_timestamp":1712930699,"image":"/9g="}]}""", body);
    }

    [Fact]
    public async Task Update_users_sends_only_filled_fields()
    {
        var device = Rep().OnJson("update_users.fcgi", "");
        using var client = Client(device);

        await client.UpdateUsersAsync(new[]
        {
            new RepUserUpdate { Cpf = 98278123892, NewCpf = 12345678909, RemoveFaces = true },
            new RepUserUpdate { Cpf = 2233445548, RemoveTemplates = true },
        }, doMatch: false);

        Assert.Equal(
            """{"users":[{"cpf":98278123892,"new_cpf":12345678909,"remove_faces":true},{"cpf":2233445548,"remove_templates":true}],"do_match":false}""",
            Single(device, "update_users.fcgi").Body);
    }

    [Theory]
    [InlineData(RepMode.Portaria671, "users_cpf", "?session=rep&mode=671")]
    [InlineData(RepMode.Legacy, "users_pis", "?session=rep")]
    public async Task Users_by_identifier_use_the_key_of_the_mode_and_pages_of_100(RepMode mode, string key, string query)
    {
        var device = Rep().OnJson("load_users.fcgi", """{"count":2,"users":[{"cpf":1,"name":"A"}]}""");
        using var client = Client(device, mode);

        var users = await client.GetUsersAsync(Enumerable.Range(1, 150).Select(i => (long)i));

        var requests = device.RequestsTo("load_users.fcgi").ToList();
        Assert.Equal(2, requests.Count);
        Assert.All(requests, r => Assert.Equal(query, r.Uri.Query));
        using var first = JsonDocument.Parse(requests[0].Body);
        Assert.Equal(100, first.RootElement.GetProperty(key).GetArrayLength());
        Assert.Equal(2, users.Count);
    }

    [Fact]
    public async Task Enumerate_users_reads_pages_on_demand()
    {
        static FakeDeviceHandler TwoPages() => Rep()
            .OnJson("load_users.fcgi", JsonSerializer.Serialize(new { count = 150, users = Enumerable.Range(1, 100).Select(i => new { cpf = i }) }))
            .OnJson("load_users.fcgi", JsonSerializer.Serialize(new { count = 150, users = Enumerable.Range(101, 50).Select(i => new { cpf = i }) }));

        var partial = TwoPages();
        using (var client = Client(partial))
        {
            Assert.Equal(10, (await client.EnumerateUsersAsync().CollectFirstAsync(10)).Count);
        }

        var full = TwoPages();
        using (var client = Client(full))
        {
            Assert.Equal(150, (await client.EnumerateUsersAsync().CollectAsync()).Count);
        }

        Assert.Single(partial.RequestsTo("load_users.fcgi")); // só a primeira página foi buscada
        Assert.Equal(2, full.RequestsTo("load_users.fcgi").Count());
    }

    [Fact]
    public async Task Csv_import_sends_binary_and_reads_the_result()
    {
        var device = Rep().OnJson("import_users_csv.fcgi", """{"imported_users_num":2,"imported_templates_num":3,"error_in_rows":[4]}""");
        using var client = Client(device);

        var result = await client.ImportUsersCsvAsync(Encoding.UTF8.GetBytes("1;Ana;0;;;;;;\n"));

        var request = Assert.Single(device.RequestsTo("import_users_csv.fcgi"));
        Assert.Equal("application/octet-stream", request.ContentType);
        Assert.Equal("?session=rep&mode=671", request.Uri.Query);
        Assert.Equal(2, result.ImportedUsers);
        Assert.Equal(3, result.ImportedTemplates);
        Assert.Equal(new[] { 4 }, result.ErrorRows);
    }

    [Fact]
    public async Task Csv_export_keeps_latin1_accents()
    {
        var device = Rep().OnBytes("export_users_csv.fcgi", Encoding.GetEncoding("iso-8859-1").GetBytes("12345678909;João;0\n"), "text/plain");
        using var client = Client(device);

        Assert.Equal("12345678909;João;0\n", await client.ExportUsersCsvAsync());
    }

    [Fact]
    public async Task Remove_admins_requires_confirmation()
    {
        var device = Rep().OnJson("remove_admins.fcgi", "");
        using var client = Client(device);

        await Assert.ThrowsAsync<ArgumentException>(() => client.RemoveAdminsAsync(default));
        Assert.Empty(device.RequestsTo("remove_admins.fcgi"));
    }

    // ---------------------------------------------------------------- Empregador

    [Fact]
    public async Task Company_reads_document_as_number_or_text()
    {
        var device = Rep()
            .OnJson("load_company.fcgi", """{"company":{"name":"Control iD","address":"Rua Hungria 888","cpf":45623245623,"tipo_doc":1,"cpf_cnpj":89765478985464,"cei":123456789874}}""")
            .OnJson("load_company.fcgi", """{"company":{"tipo_doc":1,"cpf_cnpj":"12.ABC.345/01DE-35"}}""");
        using var client = Client(device);

        var old = await client.GetCompanyAsync();
        var alphanumeric = await client.GetCompanyAsync();

        Assert.Equal("89765478985464", old.Document);
        Assert.Equal(RepCompanyDocumentType.Cnpj, old.DocumentType);
        Assert.Equal(123456789874, old.Cei);
        Assert.Equal(45623245623, old.ResponsibleCpf);
        Assert.Equal("12.ABC.345/01DE-35", alphanumeric.Document);
    }

    [Theory]
    [InlineData("89.765.478/9854-64", "89765478985464")]
    [InlineData("12.ABC.345/01DE-35", "\"12.ABC.345/01DE-35\"")]
    public async Task Company_document_goes_as_number_when_numeric_and_as_text_when_alphanumeric(string document, string expected)
    {
        var device = Rep().OnJson("edit_company.fcgi", "");
        using var client = Client(device);

        await client.SetCompanyAsync(new RepCompany { Name = "Control iD", DocumentType = RepCompanyDocumentType.Cnpj, Document = document });

        Assert.Equal($$$"""{"company":{"name":"Control iD","tipo_doc":1,"cpf_cnpj":{{{expected}}}}}""", Single(device, "edit_company.fcgi").Body);
    }

    // ---------------------------------------------------------------- Sistema

    [Fact]
    public async Task Network_reads_uppercase_dns_and_the_dhcp_typo_and_always_sends_dhcp()
    {
        var device = Rep()
            .OnJson("get_system_network.fcgi", """{"ip":"192.168.2.185","netmask":"255.255.128.0","gateway":"192.168.2.1","DNS":"8.8.8.8","port":443,"use_dhpc":true}""")
            .OnJson("set_system_network.fcgi", "");
        using var client = Client(device);

        var network = await client.GetNetworkAsync();
        await client.SetNetworkAsync(new RepNetworkSettings { Ip = "192.168.0.10", Netmask = "255.255.255.0", Gateway = "192.168.0.1", Dns = "8.8.8.8", Port = 443 });

        Assert.Equal("8.8.8.8", network.Dns);
        Assert.True(network.UseDhcp);
        Assert.Equal(443, network.Port);
        Assert.Equal(
            """{"ip":"192.168.0.10","netmask":"255.255.255.0","gateway":"192.168.0.1","dns":"8.8.8.8","port":443,"use_dhcp":false}""",
            Single(device, "set_system_network.fcgi").Body);
    }

    [Fact]
    public async Task Daylight_saving_time_round_trip_and_zeros_mean_none()
    {
        var device = Rep()
            .OnJson("get_system_daylight_saving_time.fcgi", """{"start_date_time":{"year":2026,"month":11,"day":1},"end_date_time":{"year":2027,"month":2,"day":20}}""")
            .OnJson("get_system_daylight_saving_time.fcgi", """{"start_date_time":{"year":0,"month":0,"day":0},"end_date_time":{"year":0,"month":0,"day":0}}""")
            .OnJson("set_system_daylight_saving_time.fcgi", "");
        using var client = Client(device);

        var period = await client.GetDaylightSavingTimeAsync();
        var none = await client.GetDaylightSavingTimeAsync();
        await client.SetDaylightSavingTimeAsync(new RepDaylightSavingTime(new DateTime(2016, 3, 1), new DateTime(2016, 4, 1)));

        Assert.Equal(new DateTime(2026, 11, 1), period!.Start);
        Assert.Equal(new DateTime(2027, 2, 20), period.End);
        Assert.Null(none);
        Assert.Equal(
            """{"start_date_time":{"year":2016,"month":3,"day":1},"end_date_time":{"year":2016,"month":4,"day":1}}""",
            Single(device, "set_system_daylight_saving_time.fcgi").Body);
    }

    [Fact]
    public async Task Date_time_with_time_zone_uses_mode_671()
    {
        var device = Rep()
            .OnJson("get_system_date_time.fcgi", """{"day":10,"month":3,"year":2023,"hour":21,"minute":30,"second":0,"timezone":"-0300"}""")
            .OnJson("set_system_date_time.fcgi", "");
        using var client = Client(device);

        var now = await client.GetDateTimeOffsetAsync();
        await client.SetDateTimeAsync(new DateTimeOffset(2023, 3, 10, 21, 30, 0, TimeSpan.FromHours(-3)));

        Assert.Equal(new DateTimeOffset(2023, 3, 10, 21, 30, 0, TimeSpan.FromHours(-3)), now);
        var (query, body) = Single(device, "set_system_date_time.fcgi");
        Assert.Equal("?session=rep&mode=671", query);
        Assert.Equal("""{"day":10,"month":3,"year":2023,"hour":21,"minute":30,"second":0,"timezone":"-0300"}""", body);
    }

    [Fact]
    public async Task Time_zone_requires_portaria_671_and_quarter_hours()
    {
        using var legacy = Client(Rep(), RepMode.Legacy);
        using var client = Client(Rep());

        await Assert.ThrowsAsync<InvalidOperationException>(() => legacy.GetDateTimeOffsetAsync());
        await Assert.ThrowsAsync<ArgumentException>(() => client.SetDateTimeAsync(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromMinutes(-200))));
        Assert.Equal("+0545", RepClient.FormatTimeZone(new TimeSpan(5, 45, 0)));
        Assert.Equal(new TimeSpan(-9, -30, 0), RepClient.ParseTimeZone("-0930"));
    }

    [Fact]
    public async Task Simple_settings_send_the_documented_fields()
    {
        var device = Rep()
            .OnJson("set_buzzer_beep.fcgi", "").OnJson("set_animation_screen.fcgi", "").OnJson("set_ticket_size.fcgi", "")
            .OnJson("set_identification_type.fcgi", "").OnJson("set_idcloud.fcgi", "").OnJson("set_coil_paper.fcgi", "")
            .OnJson("get_idcloud.fcgi", """{"enable":true}""").OnJson("get_coil_paper.fcgi", """{"coil_paper":180}""")
            .OnJson("get_system_configuration.fcgi", """{"beep_enabled":true,"animation_enabled":false,"economic_receipt_enabled":true,"one_to_one_enabled":false}""");
        using var client = Client(device);

        await client.SetBeepEnabledAsync(true);
        await client.SetAnimationEnabledAsync(false);
        await client.SetEconomicReceiptEnabledAsync(true);
        await client.SetOneToOneIdentificationAsync(true);
        await client.SetIdCloudEnabledAsync(true);
        await client.SetCoilPaperAsync(200);
        var config = await client.GetSystemConfigurationAsync();

        Assert.Equal("""{"enabled":true}""", Single(device, "set_buzzer_beep.fcgi").Body);
        Assert.Equal("""{"enabled":false}""", Single(device, "set_animation_screen.fcgi").Body);
        Assert.Equal("""{"economic_receipt_enabled":true}""", Single(device, "set_ticket_size.fcgi").Body);
        Assert.Equal("""{"one_to_one_enabled":true}""", Single(device, "set_identification_type.fcgi").Body);
        Assert.Equal("""{"enable":true}""", Single(device, "set_idcloud.fcgi").Body);
        Assert.Equal("""{"coil_paper":200}""", Single(device, "set_coil_paper.fcgi").Body);
        Assert.True(await client.GetIdCloudEnabledAsync());
        Assert.Equal(180, await client.GetCoilPaperAsync());
        Assert.True(config.BeepEnabled && config.EconomicReceiptEnabled && !config.AnimationEnabled && !config.OneToOneEnabled);
    }

    [Fact]
    public void Api_revision_is_the_firmware_version_in_hexadecimal()
    {
        Assert.Equal(104, new RepAbout { FirmwareVersion = 260 }.ApiRevision);
        Assert.Equal(101, new RepAbout { FirmwareVersion = 257 }.ApiRevision);
    }

    // ---------------------------------------------------------------- AFD

    [Fact]
    public async Task Afd_query_sends_header_top_and_rejects_nsr_with_date()
    {
        var device = Rep().OnJson("get_afd.fcgi", "x");
        using var client = Client(device);

        await client.GetAfdAsync(new RepAfdQuery { InitialNsr = 14, HeaderTop = true });

        Assert.Equal("""{"initial_nsr":14,"headerTop":true}""", Single(device, "get_afd.fcgi").Body);
        await Assert.ThrowsAsync<ArgumentException>(() => client.GetAfdAsync(new RepAfdQuery { InitialNsr = 1, InitialDate = DateTime.Today }));
    }

    [Fact]
    public async Task Afd_records_are_streamed_and_decoded_as_latin1()
    {
        // Registro tipo 5 (inclusão de empregado) da Portaria 1510, com nome acentuado.
        const string afd = "0000000015" + "100120261200" + "I" + "012345678901" + "José da Conceição                                   " + "\r\n";
        var device = Rep().OnBytes("get_afd.fcgi", Encoding.GetEncoding("iso-8859-1").GetBytes(afd), "application/octet-stream");
        using var client = Client(device);

        var records = await client.EnumerateAfdRecordsAsync().CollectAsync();

        var record = Assert.Single(records);
        Assert.Equal(1, record.LineNumber);
        Assert.Contains("José da Conceição", record.Line);
    }

    [Fact]
    public async Task Afd_stream_holds_a_request_slot_until_disposed()
    {
        var device = Rep().OnJson("get_afd.fcgi", "0000000011\r\n").OnJson("count_users.fcgi", """{"count":3}""");
        using var client = Client(device, maxConcurrentRequests: 1);
        await client.LoginAsync();

        var stream = await client.OpenAfdStreamAsync();
        var blocked = client.CountUsersAsync();
        await Task.Delay(200);
        Assert.False(blocked.IsCompleted); // a única vaga está com o stream aberto

        stream.Dispose();
        Assert.Equal(3, await blocked.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    // ---------------------------------------------------------------- Biometria

    [Fact]
    public async Task Template_extract_sends_bitmap_with_size_in_the_query()
    {
        var device = Rep().OnJson("template_extract.fcgi", """{"quality":88,"template":"base64"}""");
        using var client = Client(device);

        var result = await client.ExtractTemplateAsync(new byte[] { 1, 2 }, 260, 320);

        var request = Assert.Single(device.RequestsTo("template_extract.fcgi"));
        Assert.Equal("?session=rep&width=260&height=320", request.Uri.Query);
        Assert.Equal("application/octet-stream", request.ContentType);
        Assert.Equal(88, result.Quality);
        Assert.Equal("base64", result.Template);
    }

    [Theory]
    [InlineData(RepMode.Portaria671, """{"templates":["a","b","c"],"user_cpf":12341234123,"new_templates":[],"remove":false}""")]
    [InlineData(RepMode.Legacy, """{"templates":["a","b","c"],"user_pis":12341234123,"new_templates":[],"remove":false}""")]
    public async Task Template_merge_identifies_the_user_by_the_mode(RepMode mode, string expected)
    {
        var device = Rep().OnJson("template_merge.fcgi", """{"template":"final"}""");
        using var client = Client(device, mode);

        var result = await client.MergeTemplatesAsync(12341234123, new[] { "a", "b", "c" });

        Assert.Equal(expected, Single(device, "template_merge.fcgi").Body);
        Assert.Equal("final", result.Template);
    }

    // ---------------------------------------------------------------- Facial

    [Fact]
    public async Task Facial_module_config_is_validated_and_sent_complete()
    {
        var device = Rep().OnJson("set_facial_module_config.fcgi", "");
        using var client = Client(device);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.SetFacialModuleConfigAsync(new RepFacialModuleConfig { IdentificationDistance = 20 }));
        await client.SetFacialModuleConfigAsync(new RepFacialModuleConfig { IdentificationDistance = 120, LedsBrightness = 80, LedActivation = RepLedActivation.Always });

        Assert.Equal(
            """{"identification_distance":120,"leds_brightness":80,"led_activation_ambient_brightness":3}""",
            Single(device, "set_facial_module_config.fcgi").Body);
    }

    [Fact]
    public async Task Facial_commands_always_send_mode_671_even_in_legacy_mode()
    {
        var device = Rep()
            .OnJson("remote_enroll.fcgi", "").OnJson("user_destroy_image.fcgi", "").OnJson("user_import_image.fcgi", "")
            .OnBytes("user_get_image.fcgi", new byte[] { 0xFF, 0xD8, 0xFF }, "application/octet-stream");
        using var client = Client(device, RepMode.Legacy);

        await client.StartRemoteEnrollAsync(49885326820);
        var photo = await client.GetUserImageAsync(48192834820);
        await client.DeleteUserImageAsync(48192834820);
        await client.ImportUserImageAsync(48192834820, new byte[] { 0xFF, 0xD8 });

        Assert.Equal(("?session=rep&mode=671", """{"cpf":49885326820}"""), Single(device, "remote_enroll.fcgi"));
        Assert.Equal(("?session=rep&mode=671", """{"cpf":48192834820,"raw":true}"""), Single(device, "user_get_image.fcgi"));
        Assert.Equal(new byte[] { 0xFF, 0xD8, 0xFF }, photo);
        Assert.Equal(("?session=rep&mode=671", """{"cpf":48192834820}"""), Single(device, "user_destroy_image.fcgi"));
        Assert.Equal(("?session=rep&cpf=48192834820&mode=671", """{"image":"/9g="}"""), Single(device, "user_import_image.fcgi"));
    }

    [Fact]
    public async Task User_photo_goes_as_binary_with_the_documented_query()
    {
        var device = Rep().OnJson("user_set_image.fcgi", "");
        using var client = Client(device);

        await client.SetUserImageAsync(48192834820, new byte[] { 0xFF, 0xD8 }, DateTimeOffset.FromUnixTimeSeconds(712930658), doMatch: true);

        var request = Assert.Single(device.RequestsTo("user_set_image.fcgi"));
        Assert.Equal("?session=rep&do_match=true&cpf=48192834820&image_timestamp=712930658&raw=true&mode=671", request.Uri.Query);
        Assert.Equal("application/octet-stream", request.ContentType);
    }

    [Fact]
    public async Task Facial_limit_sends_ten_thousand()
    {
        var device = Rep().OnJson("set_facial_limit.fcgi", "");
        using var client = Client(device);

        await client.SetFacialLimitAsync("segredo");

        Assert.Equal("""{"facial_limit":"10000","password":"segredo"}""", Single(device, "set_facial_limit.fcgi").Body);
    }
}
