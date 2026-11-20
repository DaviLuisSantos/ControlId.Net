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
    private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 1 };
    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 1 };

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
        ("doors_state.fcgi", "POST", c => c.GetDoorStatesAsync()),
        ("remote_enroll.fcgi", "POST", c => c.StartRemoteEnrollAsync(new RemoteEnrollRequest(RemoteEnrollType.Card))),
        ("cancel_remote_enroll.fcgi", "POST", c => c.CancelRemoteEnrollAsync()),
        ("enroller_state.fcgi", "POST", c => c.GetEnrollerStateAsync()),
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
        ("face_template_destroy.fcgi", "POST", c => c.DeleteAllFaceTemplatesAsync(DataLossConfirmation.Confirmed)),
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
        ("reset_to_factory_default.fcgi", "POST", c => c.ResetToFactoryDefaultAsync(DataLossConfirmation.Confirmed, true)),
        ("delete_admins.fcgi", "POST", c => c.DeleteAllAdministratorsAsync(DataLossConfirmation.Confirmed)),
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
        // Biometria
        ("face_template_match.fcgi", "POST", c => c.MatchFaceTemplateAsync(Convert.ToBase64String(new byte[1024]))),
        ("face_template_count_distinct.fcgi", "GET", c => c.CountFaceTemplatesAsync()),
        ("template_match.fcgi", "POST", c => c.MatchFingerprintTemplatesAsync(new[] { Bytes })),

        // Schema de objetos customizados
        ("object_metadata.fcgi", "POST", c => c.GetObjectMetadataAsync()),
        ("object_add.fcgi", "POST", c => c.CreateCustomTableAsync("pets", "Pets", new[] { new CustomFieldDefinition { ColumnName = "nome", Name = "Nome" } })),
        ("object_remove.fcgi", "POST", c => c.DeleteCustomTablesAsync(DataLossConfirmation.Confirmed, new long[] { 1 })),
        ("object_add_field.fcgi", "POST", c => c.AddCustomFieldAsync("pets", new CustomFieldDefinition { ColumnName = "cor", Name = "Cor" })),
        ("object_update_field.fcgi", "POST", c => c.UpdateCustomFieldAsync(1, new CustomFieldDefinition { ColumnName = "cor", Name = "Cor" })),
        ("object_remove_fields.fcgi", "POST", c => c.RemoveCustomFieldsAsync(DataLossConfirmation.Confirmed, new long[] { 1 })),
        ("export_object.fcgi", "POST", c => c.ExportObjectCsvAsync("users")),
        ("export_custom_tables_metadata.fcgi", "GET", c => c.ExportCustomTablesMetadataCsvAsync()),

        // Rede, NTP e diagnóstico
        ("get_ntp_server.fcgi", "GET", c => c.GetNtpServerAsync()),
        ("set_ntp_server.fcgi", "POST", c => c.SetNtpServerAsync("pool.ntp.org")),
        ("get_ntp_server_status.fcgi", "GET", c => c.GetNtpServerStatusAsync()),
        ("check_port_availability.fcgi", "POST", c => c.CheckPortAvailabilityAsync(8000, 8010)),
        ("get_vpn_ip.fcgi", "GET", c => c.GetVpnIpAsync()),
        ("get_ssl_certificate.fcgi", "GET", c => c.GetSslCertificateAsync()),
        ("get_802_1X_status.fcgi", "GET", c => c.Get8021XStatusAsync()),
        ("get_ac_log.fcgi", "GET", c => c.GetDeviceLogAsync(DeviceLog.AccessControl)),
        ("get_openvpn_log.fcgi", "GET", c => c.GetDeviceLogAsync(DeviceLog.OpenVpn)),
        ("get_wpa_log.fcgi", "GET", c => c.GetDeviceLogAsync(DeviceLog.Wpa)),
        ("get_wpasupplicant_log.fcgi", "GET", c => c.GetDeviceLogAsync(DeviceLog.WpaSupplicant)),

        // Leitura de hardware e estado
        ("io_state.fcgi", "POST", c => c.GetIoStateAsync()),
        ("is_identifying.fcgi", "GET", c => c.IsIdentifyingAsync()),
        ("get_catra_info.fcgi", "GET", c => c.GetTurnstileCountersAsync()),
        ("get_last_snapshot.fcgi", "GET", c => c.GetLastSnapshotAsync()),
        ("device_rfid_module.fcgi", "GET", c => c.GetRfidModuleAsync()),
        ("get_hid_last_card.fcgi", "GET", c => c.GetHidLastCardAsync()),
        ("get_wiegand_card_read.fcgi", "GET", c => c.GetLastWiegandReadAsync()),

        // Senhas de sistema
        ("master_password.fcgi", "POST", c => c.SetMasterPasswordAsync("senha-mestra")),
        ("recovery_password.fcgi", "POST", c => c.SetUpdateModePasswordAsync("123456")),

        // Onda 3: Wi-Fi e certificados EAP
        ("set_wifi_source.fcgi", "POST", c => c.SetWifiSourceAsync(WifiSource.Internal)),
        ("wifi_scan.fcgi", "POST", c => c.ScanWifiAsync()),
        ("set_wifi_settings.fcgi", "POST", c => c.SetWifiSettingsAsync("rede", "senha")),
        ("get_wifi_data.fcgi", "POST", c => c.GetWifiDataAsync()),
        ("forget_wifi_connected_network.fcgi", "POST", c => c.ForgetWifiConnectedNetworkAsync(DataLossConfirmation.Confirmed)),
        ("check_ip_conflict.fcgi", "POST", c => c.CheckIpConflictAsync(NetworkInterfaceKind.Wired, "10.0.0.9")),
        ("eap_802_1X_certificate_change.fcgi", "POST", c => c.UploadEapCaCertificateAsync(Bytes)),
        ("eap_802_1X_certificate_change_wifi.fcgi", "POST", c => c.UploadEapCaCertificateWifiAsync(Bytes)),
        ("eap_tls_802_1X_certificate_change.fcgi", "POST", c => c.UploadEapTlsClientCertificateAsync(Bytes)),
        ("eap_tls_802_1X_private_key_change.fcgi", "POST", c => c.UploadEapTlsPrivateKeyAsync(Bytes)),
        ("eap_tls_802_1X_private_key_persist.fcgi", "POST", c => c.PersistEapTlsPrivateKeyAsync()),
        ("get_eap_tls_csr_file.fcgi", "POST", c => c.GetEapTlsCsrAsync()),

        // Onda 3: mídia de tela
        ("send_slideshow_images.fcgi", "POST", c => c.SendSlideshowImageAsync("a.jpg", Jpeg)),
        ("get_slideshow_images_list.fcgi", "GET", c => c.GetSlideshowImagesAsync()),
        ("get_slideshow_image.fcgi", "GET", c => c.GetSlideshowImageAsync("a.jpg")),
        ("delete_slideshow_image.fcgi", "POST", c => c.DeleteSlideshowImageAsync("a.jpg")),
        ("remove_slideshow_images.fcgi", "POST", c => c.RemoveAllSlideshowImagesAsync(DataLossConfirmation.Confirmed)),
        ("set_kiosk_icon.fcgi", "POST", c => c.SetKioskIconAsync("a.png", Png)),
        ("get_kiosk_icon.fcgi", "GET", c => c.GetKioskIconAsync("a.png")),
        ("delete_kiosk_icon.fcgi", "POST", c => c.DeleteKioskIconAsync("a.png")),
        ("get_kiosk_icons_list.fcgi", "GET", c => c.GetKioskIconsAsync()),
        ("set_phone_icon.fcgi", "POST", c => c.SetPhoneIconAsync(Png)),
        ("get_phone_icon.fcgi", "GET", c => c.GetPhoneIconAsync()),
        ("remove_phone_icon.fcgi", "POST", c => c.RemovePhoneIconAsync()),
        ("make_sip_conference.fcgi", "POST", c => c.MakeSipConferenceAsync()),
        ("set_streaming_logo.fcgi", "POST", c => c.SetStreamingLogoAsync(Png)),
        ("get_streaming_logo.fcgi", "GET", c => c.GetStreamingLogoAsync()),
        ("remove_streaming_logo.fcgi", "POST", c => c.RemoveStreamingLogoAsync()),

        // Onda 3: SecBox e dual sided
        ("device_has_secbox.fcgi", "GET", c => c.DeviceHasSecBoxAsync()),
        ("secbox_is_active.fcgi", "GET", c => c.IsSecBoxActiveAsync()),
        ("secbox_serial_number.fcgi", "GET", c => c.GetSecBoxSerialNumberAsync()),
        ("update_secbox_firmware_version.fcgi", "GET", c => c.GetSecBoxFirmwareVersionAsync()),
        ("update_secbox_firmware.fcgi", "POST", c => c.UpdateSecBoxFirmwareAsync()),
        ("update_secbox_firmware_status.fcgi", "GET", c => c.GetSecBoxFirmwareUpdateStatusAsync()),
        ("change_crypto_key_default.fcgi", "POST", c => c.ChangeSecBoxCryptoKeyToDefaultAsync(DataLossConfirmation.Confirmed)),
        ("change_crypto_key_random.fcgi", "POST", c => c.ChangeSecBoxCryptoKeyToRandomAsync(DataLossConfirmation.Confirmed)),
        ("reset_crypto_key.fcgi", "POST", c => c.ResetSecBoxCryptoKeyAsync(DataLossConfirmation.Confirmed)),
        ("set_dual_sided_secret.fcgi", "POST", c => c.SetDualSidedSecretAsync(new string('a', 64))),
        ("get_dual_sided_status.fcgi", "POST", c => c.GetDualSidedStatusAsync()),

        // Onda 3: OSDP e calibração da câmera
        ("get_osdp_installation_mode.fcgi", "GET", c => c.GetOsdpInstallationModeAsync()),
        ("osdp_scbk.fcgi", "POST", c => c.GetOsdpScbkAsync()),
        ("osdp_get_connection_status.fcgi", "POST", c => c.GetOsdpConnectionStatusAsync(1)),
        ("osdp_get_output_capabilities.fcgi", "POST", c => c.GetOsdpOutputCapabilitiesAsync(1)),
        ("osdp_set_secure_key.fcgi", "POST", c => c.SetOsdpSecureKeyAsync(1, 1, new string('a', 32))),
        ("osdp_set_secure_type.fcgi", "POST", c => c.SetOsdpSecureTypeAsync(1, 1, OsdpSecureType.SecureOnly)),
        ("osdp_refresh.fcgi", "POST", c => c.RefreshOsdpAsync()),
        ("remote_camera_calibration.fcgi", "POST", c => c.StartCameraCalibrationAsync()),
        ("cancel_remote_camera_calibration.fcgi", "POST", c => c.CancelCameraCalibrationAsync()),
        ("camera_calibration_status.fcgi", "POST", c => c.GetCameraCalibrationStatusAsync()),

        // Onda 3: licenças, modo Pro e redirecionamento
        ("upgrade_twenty_thousand_face_templates.fcgi", "POST", c => c.ApplyFaceLicenseAsync(FaceLicense.Pro20k, "x")),
        ("upgrade_ten_thousand_templates.fcgi", "POST", c => c.UpgradeFingerprintCapacityAsync("x")),
        ("set_pro_operation_mode_and_reboot.fcgi", "POST", c => c.SetProOperationModeAndRebootAsync("10k")),
        ("request_redirect.fcgi", "POST", c => c.RedirectRequestAsync(RedirectTarget.ByDeviceId(1), "get_configuration")),

        // Onda 4: cliente de provisionamento (ADR 0024)
        ("hid_inject_default_kek.fcgi", "POST", c => c.CreateProvisioningClient().InjectHidDefaultKekAsync("AA")),
        ("hid_inject_custom_kek.fcgi", "POST", c => c.CreateProvisioningClient().InjectHidCustomKekAsync("03F001", "03F000", "AA")),
        ("hid_inject_media_key.fcgi", "POST", c => c.CreateProvisioningClient().InjectHidMediaKeyAsync("030101", "03F001", "AA")),
        ("hid_get_key_info.fcgi", "POST", c => c.CreateProvisioningClient().GetHidKeyInfoAsync("03F000")),
        ("hid_set_oid.fcgi", "POST", c => c.CreateProvisioningClient().SetHidOidAsync("1.1.1.2.3.5", true)),
        ("hid_get_oid.fcgi", "POST", c => c.CreateProvisioningClient().GetHidOidAsync("1.1.1.2.3.5")),
        ("hid_reset_config.fcgi", "POST", c => c.CreateProvisioningClient().GetHidConfigResetStatusAsync()),
        ("get_oem_code.fcgi", "GET", c => c.CreateProvisioningClient().GetOemCodeAsync()),
        ("set_oem_code.fcgi", "POST", c => c.CreateProvisioningClient().SetOemCodeAsync("MYOEM", "x")),
        ("get_oem_downgrade_approved_versions.fcgi", "POST", c => c.CreateProvisioningClient().GetOemDowngradeApprovedVersionsAsync()),
        ("set_oem_approved_fw.fcgi", "POST", c => c.CreateProvisioningClient().SetOemApprovedFirmwareAsync("MYOEM", "x", "7.14.0")),
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
