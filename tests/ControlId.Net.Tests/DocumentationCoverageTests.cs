using System.Net;
using System.Text;
using ControlId.Access;
using ControlId.Access.Configuration;
using ControlId.Access.Online;

namespace ControlId.Tests;

/// <summary>
/// Garante que cada endpoint que a documentação da API de acesso descreve (do lado do equipamento) tem um método
/// tipado que realmente o chama. A lista abaixo é o inventário da documentação (veja docs/COBERTURA.md); ao incluir
/// um endpoint novo da documentação, acrescente aqui a chamada correspondente.
/// </summary>
public class DocumentationCoverageTests
{
    private static readonly byte[] Bytes = { 1, 2, 3 };

    public static TheoryData<string, string> Endpoints()
    {
        var data = new TheoryData<string, string>();
        foreach (var (endpoint, method, _) in Calls)
        {
            data.Add(endpoint, method);
        }

        return data;
    }

    // endpoint documentado, método HTTP, chamada tipada
    private static readonly (string Endpoint, string Method, Func<AccessClient, Task> Call)[] Calls =
    {
        // Sessão
        ("login.fcgi", "POST", c => c.LoginAsync()),
        ("logout.fcgi", "POST", async c => { await c.LoginAsync(); await c.LogoutAsync(); }),
        ("session_is_valid.fcgi", "POST", async c => { await c.LoginAsync(); await c.IsSessionValidAsync(); }),
        ("change_login.fcgi", "POST", c => c.ChangeLoginAsync("novo", "senha")),

        // Objetos
        ("load_objects.fcgi", "POST", c => c.GetUsersAsync()),
        ("create_objects.fcgi", "POST", c => c.CreateObjectAsync(new AccessArea { Name = "x" })),
        ("modify_objects.fcgi", "POST", c => c.UpdateObjectAsync(new AccessArea { Id = 1, Name = "x" })),
        ("create_or_modify_objects.fcgi", "POST", c => c.CreateOrModifyObjectsAsync("users", new[] { new AccessUser { Id = 1 } })),
        ("destroy_objects.fcgi", "POST", c => c.DeleteObjectAsync<AccessArea>(1)),
        ("report_generate.fcgi", "POST", c => c.GenerateReportAsync(new ReportRequest("users"))),
        ("export_afd.fcgi", "POST", c => c.ExportAfdAsync()),
        ("export_audit_logs.fcgi", "POST", c => c.ExportAuditLogsAsync()),
        ("template_sync_init.fcgi", "POST", c => c.StartTemplateSyncAsync()),
        ("template_sync_end.fcgi", "POST", c => c.EndTemplateSyncAsync()),

        // Ações
        ("remote_user_authorization.fcgi", "POST", c => c.AuthorizeRemotelyAsync(OnlineAuthorization.Grant(1, "x"))),
        ("execute_actions.fcgi", "POST", c => c.OpenDoorAsync()),
        ("door_state.fcgi", "POST", c => c.GetDoorStatesAsync()),
        ("remote_enroll.fcgi", "POST", c => c.StartRemoteEnrollAsync(new RemoteEnrollRequest(RemoteEnrollType.Card))),
        ("cancel_remote_enroll.fcgi", "POST", c => c.CancelRemoteEnrollAsync()),
        ("message_to_screen.fcgi", "POST", c => c.ShowMessageAsync("x")),
        ("buzzer_buzz.fcgi", "POST", c => c.BuzzAsync(TimeSpan.FromSeconds(1))),

        // Reconhecimento facial e fotos
        ("user_set_image.fcgi", "POST", c => c.SetUserImageAsync(1, Bytes)),
        ("user_set_image_list.fcgi", "POST", c => c.SetUserImagesAsync(new[] { new UserImage(1, Bytes) })),
        ("user_test_image.fcgi", "POST", c => c.TestUserImageAsync(Bytes)),
        ("user_get_image.fcgi", "GET", c => c.GetUserImageAsync(1)),
        ("user_get_image_list.fcgi", "POST", c => c.GetUserImagesAsync(new long[] { 1 })),
        ("user_list_images.fcgi", "POST", c => c.GetUserIdsWithImageAsync()),
        ("user_destroy_image.fcgi", "POST", c => c.DeleteUserImageAsync(1)),
        ("face_template_extract.fcgi", "POST", c => c.ExtractFaceTemplateAsync(Bytes)),
        ("face_template_extract_list.fcgi", "POST", c => c.ExtractFaceTemplatesAsync(new[] { new FaceTemplateImage(1, Bytes) })),
        ("face_template_enroll.fcgi", "POST", c => c.EnrollFaceTemplatesAsync(new[] { new FaceTemplateEnrollment(1, "t") })),
        ("face_template_destroy.fcgi", "POST", c => c.DeleteAllFaceTemplatesAsync()),
        ("save_screenshot.fcgi", "POST", c => c.CaptureCameraAsync()),

        // Logotipo e modo propaganda
        ("logo.fcgi", "POST", c => c.GetLogoAsync()),
        ("logo_change.fcgi", "POST", c => c.SetLogoAsync(Bytes)),
        ("logo_destroy.fcgi", "POST", c => c.DeleteLogoAsync()),
        ("send_video.fcgi", "POST", c => c.UploadAdvertisingVideoAsync(Bytes)),
        ("set_custom_video.fcgi", "POST", c => c.SetAdvertisingModeAsync(true)),
        ("remove_custom_video.fcgi", "POST", c => c.RemoveAdvertisingVideoAsync()),

        // Hardware
        ("gpio_state.fcgi", "POST", c => c.GetGpioStateAsync(0)),
        ("led_rgb_refresh.fcgi", "POST", c => c.RefreshLedAsync()),
        ("remote_led_control.fcgi", "POST", c => c.SetTurnstileLedAsync(TurnstileLedEvent.Idle)),
        ("alarm_status.fcgi", "POST", c => c.GetAlarmStatusAsync()),

        // Configurações
        ("get_configuration.fcgi", "POST", c => c.GetConfigurationSectionAsync<GeneralSettings>()),
        ("set_configuration.fcgi", "POST", c => c.SetConfigurationSectionAsync(new GeneralSettings { BeepEnabled = true })),
        ("set_network_interlock.fcgi", "POST", c => c.SetNetworkInterlockAsync(true, false, false)),
        ("connection_test.fcgi", "GET", c => c.TestInternetConnectionAsync()),
        ("ping_test.fcgi", "POST", c => c.PingAsync("x")),
        ("nslookup_test.fcgi", "POST", c => c.NsLookupAsync("x", "8.8.8.8")),

        // Sistema
        ("user_hash_password.fcgi", "POST", c => c.HashPasswordAsync("x")),
        ("system_information.fcgi", "POST", c => c.GetSystemInformationAsync()),
        ("set_system_network.fcgi", "POST", c => c.SetNetworkAsync(new NetworkSettings("10.0.0.2", "255.0.0.0", "10.0.0.1"))),
        ("ssl_certificate_change.fcgi", "POST", c => c.UploadSslCertificateAsync(Bytes)),
        ("set_vpn_information.fcgi", "POST", c => c.SetVpnAsync(new VpnSettings())),
        ("set_vpn_file.fcgi", "POST", c => c.UploadVpnFileAsync(Bytes, VpnFileType.Config)),
        ("get_vpn_file.fcgi", "GET", c => c.GetVpnExampleFilesAsync()),
        ("get_vpn_information.fcgi", "GET", c => c.GetVpnInformationAsync()),
        ("get_vpn_status.fcgi", "GET", c => c.GetVpnStatusAsync()),
        ("has_vpn_file.fcgi", "GET", c => c.HasVpnFileAsync()),
        ("configure_802_1X.fcgi", "POST", c => c.Configure8021XAsync(true)),
        ("set_osdp_installation_mode.fcgi", "POST", c => c.SetOsdpInstallationModeAsync(true)),
        ("set_system_time.fcgi", "POST", c => c.SetSystemTimeAsync(new DateTime(2026, 1, 1))),
        ("reset_to_factory_default.fcgi", "POST", c => c.ResetToFactoryDefaultAsync(true)),
        ("delete_admins.fcgi", "POST", c => c.DeleteAllAdministratorsAsync()),
        ("reboot_recovery.fcgi", "POST", c => c.RebootToUpdateModeAsync()),
        ("reboot.fcgi", "POST", c => c.RebootAsync()),
        ("reboot", "GET", c => c.RebootWithoutSessionAsync()),
        ("export_objects.fcgi", "POST", c => c.ExportObjectsAsync(new[] { new ExportObjectRequest("users") })),
        ("is_valid_biometry.fcgi", "POST", c => c.IsValidBiometryAsync("x")),

        // Particularidades de produto
        ("upgrade_ten_thousand_face_templates.fcgi", "POST", c => c.ApplyFaceLicenseAsync(FaceLicense.Pro10k, "x")),
        ("upgrade_fifteen_thousand_face_templates.fcgi", "POST", c => c.ApplyFaceLicenseAsync(FaceLicense.Pro15k, "x")),
        ("upgrade_thirty_thousand_face_templates.fcgi", "POST", c => c.ApplyFaceLicenseAsync(FaceLicense.Pro30k, "x")),
        ("upgrade_fifty_thousand_face_templates.fcgi", "POST", c => c.ApplyFaceLicenseAsync(FaceLicense.Pro50k, "x")),
        ("upgrade_hundred_thousand_face_templates.fcgi", "POST", c => c.ApplyFaceLicenseAsync(FaceLicense.Pro100k, "x")),
        ("idflex_upgrade_enterprise.fcgi", "POST", c => c.UpgradeToEnterpriseAsync("x")),
        ("make_sip_call.fcgi", "POST", c => c.MakeSipCallAsync("100")),
        ("finalize_sip_call.fcgi", "POST", c => c.FinalizeSipCallAsync()),
        ("get_sip_status.fcgi", "POST", c => c.GetSipStatusAsync()),
        ("set_pjsip_audio_message.fcgi", "POST", c => c.UploadSipCallAudioAsync(Bytes)),
        ("get_pjsip_audio_message.fcgi", "POST", c => c.GetSipCallAudioAsync()),
        ("has_pjsip_audio_message.fcgi", "POST", c => c.HasSipCallAudioAsync()),
        ("set_audio_access_message.fcgi", "POST", c => c.UploadAccessAudioAsync(AccessAudioEvent.Authorized, Bytes)),
        ("get_audio_access_message.fcgi", "POST", c => c.GetAccessAudioAsync(AccessAudioEvent.Authorized)),
        ("has_audio_access_messages.fcgi", "POST", c => c.GetAccessAudioStatusAsync()),
        ("change_idcloud_code.fcgi", "GET", c => c.ChangeIdCloudCodeAsync()),
    };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Documented_endpoint_has_a_typed_method(string endpoint, string method)
    {
        var device = new AnyEndpointDevice();
        using var client = new AccessClient(new ControlIdDeviceOptions { BaseAddress = new Uri("http://10.0.0.5") }, new HttpClient(device));

        var call = Calls.Single(c => c.Endpoint == endpoint).Call;
        try
        {
            await call(client);
        }
        catch (ControlIdException)
        {
            // A resposta genérica "{}" pode não satisfazer o método (ex.: sem ID criado); importa a requisição feita.
        }

        Assert.Contains(device.Requests, r => r.Path == endpoint && r.Method == method);
    }

    [Fact]
    public void Inventory_has_no_duplicates()
    {
        Assert.Equal(Calls.Length, Calls.Select(c => c.Endpoint).Distinct().Count());
    }

    /// <summary>Responde 200 com "{}" a qualquer endpoint (e uma sessão no login).</summary>
    private sealed class AnyEndpointDevice : HttpMessageHandler
    {
        public List<(string Path, string Method)> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath.TrimStart('/');
            lock (Requests)
            {
                Requests.Add((path, request.Method.Method));
            }

            var body = path == "login.fcgi" ? """{"session":"s"}""" : "{}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
