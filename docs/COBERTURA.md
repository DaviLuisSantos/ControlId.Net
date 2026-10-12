# Cobertura da documentação da API de acesso

Levantamento feito em 2026-10-12 sobre as **74 páginas** da [documentação oficial da linha de acesso](https://www.controlid.com.br/docs/access-api-pt/). Cada endpoint, objeto, evento e seção de configuração
documentados tem um método, modelo ou rota tipados no pacote.

| Item | Cobertura |
| --- | --- |
| Endpoints chamados no equipamento | 85 de 85 |
| Rotas que o equipamento chama no seu servidor (Monitor, Push, modo online) | todas, no `ControlId.Net.AspNetCore` |
| Objetos (`load/create/modify/destroy_objects`) | 37 de 37, com modelo e CRUD tipado |
| Seções de configuração | 34 seções, 245 parâmetros |

A lista de endpoints abaixo é a mesma do teste `DocumentationCoverageTests`, que chama cada método contra um
equipamento simulado e confere o endpoint e o método HTTP. Ao surgir um endpoint novo na documentação,
acrescente-o ao teste.

> O REP iDClass não entra aqui: a documentação dele não é pública (o site responde 403).

## Endpoints chamados no equipamento

### Sessão

| Endpoint | HTTP | Método |
| --- | --- | --- |
| `login.fcgi` | POST | `LoginAsync()` |
| `logout.fcgi` | POST | `LogoutAsync()` |
| `session_is_valid.fcgi` | POST | `IsSessionValidAsync()` |
| `change_login.fcgi` | POST | `ChangeLoginAsync("novo", "senha")` |

### Objetos

| Endpoint | HTTP | Método |
| --- | --- | --- |
| `load_objects.fcgi` | POST | `GetUsersAsync()` |
| `create_objects.fcgi` | POST | `CreateObjectAsync(new AccessArea { Name = "x" })` |
| `modify_objects.fcgi` | POST | `UpdateObjectAsync(new AccessArea { Id = 1, Name = "x" })` |
| `create_or_modify_objects.fcgi` | POST | `CreateOrModifyObjectsAsync("users", new[] { new AccessUser { Id = 1 } })` |
| `destroy_objects.fcgi` | POST | `DeleteObjectAsync<AccessArea>(1)` |
| `report_generate.fcgi` | POST | `GenerateReportAsync(new ReportRequest("users"))` |
| `export_afd.fcgi` | POST | `ExportAfdAsync()` |
| `export_audit_logs.fcgi` | POST | `ExportAuditLogsAsync()` |
| `template_sync_init.fcgi` | POST | `StartTemplateSyncAsync()` |
| `template_sync_end.fcgi` | POST | `EndTemplateSyncAsync()` |

### Ações

| Endpoint | HTTP | Método |
| --- | --- | --- |
| `remote_user_authorization.fcgi` | POST | `AuthorizeRemotelyAsync(OnlineAuthorization.Grant(1, "x"))` |
| `execute_actions.fcgi` | POST | `OpenDoorAsync()` |
| `door_state.fcgi` | POST | `GetDoorStatesAsync()` |
| `remote_enroll.fcgi` | POST | `StartRemoteEnrollAsync(new RemoteEnrollRequest(RemoteEnrollType.Card))` |
| `cancel_remote_enroll.fcgi` | POST | `CancelRemoteEnrollAsync()` |
| `message_to_screen.fcgi` | POST | `ShowMessageAsync("x")` |
| `buzzer_buzz.fcgi` | POST | `BuzzAsync(TimeSpan.FromSeconds(1))` |

### Reconhecimento facial e fotos

| Endpoint | HTTP | Método |
| --- | --- | --- |
| `user_set_image.fcgi` | POST | `SetUserImageAsync(1, Bytes)` |
| `user_set_image_list.fcgi` | POST | `SetUserImagesAsync(new[] { new UserImage(1, Bytes) })` |
| `user_test_image.fcgi` | POST | `TestUserImageAsync(Bytes)` |
| `user_get_image.fcgi` | GET | `GetUserImageAsync(1)` |
| `user_get_image_list.fcgi` | POST | `GetUserImagesAsync(new long[] { 1 })` |
| `user_list_images.fcgi` | POST | `GetUserIdsWithImageAsync()` |
| `user_destroy_image.fcgi` | POST | `DeleteUserImageAsync(1)` |
| `face_template_extract.fcgi` | POST | `ExtractFaceTemplateAsync(Bytes)` |
| `face_template_extract_list.fcgi` | POST | `ExtractFaceTemplatesAsync(new[] { new FaceTemplateImage(1, Bytes) })` |
| `face_template_enroll.fcgi` | POST | `EnrollFaceTemplatesAsync(new[] { new FaceTemplateEnrollment(1, "t") })` |
| `face_template_destroy.fcgi` | POST | `DeleteAllFaceTemplatesAsync()` |
| `save_screenshot.fcgi` | POST | `CaptureCameraAsync()` |

### Logotipo e modo propaganda

| Endpoint | HTTP | Método |
| --- | --- | --- |
| `logo.fcgi` | POST | `GetLogoAsync()` |
| `logo_change.fcgi` | POST | `SetLogoAsync(Bytes)` |
| `logo_destroy.fcgi` | POST | `DeleteLogoAsync()` |
| `send_video.fcgi` | POST | `UploadAdvertisingVideoAsync(Bytes)` |
| `set_custom_video.fcgi` | POST | `SetAdvertisingModeAsync(true)` |
| `remove_custom_video.fcgi` | POST | `RemoveAdvertisingVideoAsync()` |

### Hardware

| Endpoint | HTTP | Método |
| --- | --- | --- |
| `gpio_state.fcgi` | POST | `GetGpioStateAsync(0)` |
| `led_rgb_refresh.fcgi` | POST | `RefreshLedAsync()` |
| `remote_led_control.fcgi` | POST | `SetTurnstileLedAsync(TurnstileLedEvent.Idle)` |
| `alarm_status.fcgi` | POST | `GetAlarmStatusAsync()` |

### Configurações

| Endpoint | HTTP | Método |
| --- | --- | --- |
| `get_configuration.fcgi` | POST | `GetConfigurationSectionAsync<GeneralSettings>()` |
| `set_configuration.fcgi` | POST | `SetConfigurationSectionAsync(new GeneralSettings { BeepEnabled = true })` |
| `set_network_interlock.fcgi` | POST | `SetNetworkInterlockAsync(true, false, false)` |
| `connection_test.fcgi` | GET | `TestInternetConnectionAsync()` |
| `ping_test.fcgi` | POST | `PingAsync("x")` |
| `nslookup_test.fcgi` | POST | `NsLookupAsync("x", "8.8.8.8")` |

### Sistema

| Endpoint | HTTP | Método |
| --- | --- | --- |
| `user_hash_password.fcgi` | POST | `HashPasswordAsync("x")` |
| `system_information.fcgi` | POST | `GetSystemInformationAsync()` |
| `set_system_network.fcgi` | POST | `SetNetworkAsync(new NetworkSettings("10.0.0.2", "255.0.0.0", "10.0.0.1"))` |
| `ssl_certificate_change.fcgi` | POST | `UploadSslCertificateAsync(Bytes)` |
| `set_vpn_information.fcgi` | POST | `SetVpnAsync(new VpnSettings())` |
| `set_vpn_file.fcgi` | POST | `UploadVpnFileAsync(Bytes, VpnFileType.Config)` |
| `get_vpn_file.fcgi` | GET | `GetVpnExampleFilesAsync()` |
| `get_vpn_information.fcgi` | GET | `GetVpnInformationAsync()` |
| `get_vpn_status.fcgi` | GET | `GetVpnStatusAsync()` |
| `has_vpn_file.fcgi` | GET | `HasVpnFileAsync()` |
| `configure_802_1X.fcgi` | POST | `Configure8021XAsync(true)` |
| `set_osdp_installation_mode.fcgi` | POST | `SetOsdpInstallationModeAsync(true)` |
| `set_system_time.fcgi` | POST | `SetSystemTimeAsync(new DateTime(2026, 1, 1))` |
| `reset_to_factory_default.fcgi` | POST | `ResetToFactoryDefaultAsync(true)` |
| `delete_admins.fcgi` | POST | `DeleteAllAdministratorsAsync()` |
| `reboot_recovery.fcgi` | POST | `RebootToUpdateModeAsync()` |
| `reboot.fcgi` | POST | `RebootAsync()` |
| `reboot` | GET | `RebootWithoutSessionAsync()` |
| `export_objects.fcgi` | POST | `ExportObjectsAsync(new[] { new ExportObjectRequest("users") })` |
| `is_valid_biometry.fcgi` | POST | `IsValidBiometryAsync("x")` |

### Particularidades de produto

| Endpoint | HTTP | Método |
| --- | --- | --- |
| `upgrade_ten_thousand_face_templates.fcgi` | POST | `ApplyFaceLicenseAsync(FaceLicense.Pro10k, "x")` |
| `upgrade_fifteen_thousand_face_templates.fcgi` | POST | `ApplyFaceLicenseAsync(FaceLicense.Pro15k, "x")` |
| `upgrade_thirty_thousand_face_templates.fcgi` | POST | `ApplyFaceLicenseAsync(FaceLicense.Pro30k, "x")` |
| `upgrade_fifty_thousand_face_templates.fcgi` | POST | `ApplyFaceLicenseAsync(FaceLicense.Pro50k, "x")` |
| `upgrade_hundred_thousand_face_templates.fcgi` | POST | `ApplyFaceLicenseAsync(FaceLicense.Pro100k, "x")` |
| `idflex_upgrade_enterprise.fcgi` | POST | `UpgradeToEnterpriseAsync("x")` |
| `make_sip_call.fcgi` | POST | `MakeSipCallAsync("100")` |
| `finalize_sip_call.fcgi` | POST | `FinalizeSipCallAsync()` |
| `get_sip_status.fcgi` | POST | `GetSipStatusAsync()` |
| `set_pjsip_audio_message.fcgi` | POST | `UploadSipCallAudioAsync(Bytes)` |
| `get_pjsip_audio_message.fcgi` | POST | `GetSipCallAudioAsync()` |
| `has_pjsip_audio_message.fcgi` | POST | `HasSipCallAudioAsync()` |
| `set_audio_access_message.fcgi` | POST | `UploadAccessAudioAsync(AccessAudioEvent.Authorized, Bytes)` |
| `get_audio_access_message.fcgi` | POST | `GetAccessAudioAsync(AccessAudioEvent.Authorized)` |
| `has_audio_access_messages.fcgi` | POST | `GetAccessAudioStatusAsync()` |
| `change_idcloud_code.fcgi` | GET | `ChangeIdCloudCodeAsync()` |

## Rotas que o equipamento chama no seu servidor

| Funcionalidade | Rotas | No pacote |
| --- | --- | --- |
| Monitor | `api/notifications/{dao, door, secbox, catra_event, operation_mode, device_is_alive, access_photo, usb_drive, template, user_image, face_template, card, pin, password}` | `MonitorNotifications.Parse`, `MapControlIdMonitor` |
| Push | `GET /push`, `POST /result` (comando avulso ou `transactions`) | `PushServer`, `MapControlIdPush` |
| Modo online: identificação | `new_user_identified`, `new_card`, `new_qrcode`, `new_uhf_tag`, `new_user_id_and_password`, `new_biometric_image`, `new_biometric_template` | `OnlineEvents.Parse`, `MapControlIdOnline` |
| Modo online: cadastro remoto | `card_create`, `fingerprint_create`, `template_create`, `face_create`, `pin_create`, `password_create` | `OnlineEvents.ParseEnroll`, `IOnlineAccessHandler.OnRemoteEnrollAsync` |
| Modo online: outros | `new_rex_log`, `GET user_get_image`, `device_is_alive`, `session_is_valid` | `MapControlIdOnline` |
| Streaming | `rtsp://…/main_stream` (não é HTTP) | `GetRtspStreamUri` |

## Objetos

Todos os modelos têm `[ControlIdObject]` e funcionam com `GetObjectsAsync<T>`, `CreateObjectAsync<T>`,
`UpdateObjectAsync<T>` e `DeleteObjectAsync<T>`. As tabelas de associação usam `LinkAsync`/`UnlinkAsync` com `AccessLink`.

| Objeto | Modelo |
| --- | --- |
| `users`, `change_logs` | `AccessUser`, `ChangeLog` |
| `templates`, `cards`, `qrcodes`, `uhf_tags`, `pins` | `AccessTemplate`, `AccessCard`, `AccessQrCode`, `AccessUhfTag`, `AccessPin` |
| `alarm_zones`, `alarm_zone_time_zones`, `timed_alarms`, `alarm_logs` | `AlarmZone`, `AccessLink.AlarmZoneTimeZone`, `TimedAlarm`, `AlarmLog` |
| `user_roles` | `UserRole`, `SetAdministratorAsync` |
| `groups`, `user_groups` | `AccessGroup`, `AccessLink.UserGroup` |
| `scheduled_unlocks`, `scheduled_unlock_access_rules` | `ScheduledUnlock`, `AccessLink.ScheduledUnlockAccessRule` |
| `actions`, `portal_actions` | `ActionScript`, `AccessLink.PortalAction` |
| `areas`, `portals`, `area_access_rules`, `portal_access_rules` | `AccessArea`, `AccessPortal`, `AccessLink.AreaAccessRule`, `AccessLink.PortalAccessRule` |
| `access_rules`, `group_access_rules`, `user_access_rules`, `access_rule_time_zones` | `AccessRule`, `AccessLink.*` |
| `time_zones`, `time_spans`, `holidays` | `AccessTimeZone`, `AccessTimeSpan`, `AccessHoliday` |
| `contingency_cards`, `contingency_card_access_rules` | `ContingencyCard`, `AccessLink.ContingencyCardAccessRule` |
| `access_logs`, `access_log_access_rules`, `access_events` | `AccessLog`, `AccessLogAccessRule` / `GetAccessLogRuleIdsAsync`, `AccessDeviceEvent` |
| `devices`, `catra_infos`, `log_types`, `sec_boxs` | `AccessDevice`, `TurnstileInfo`, `LogType`, `SecBox` |
| `contacts`, `custom_thresholds`, `network_interlocking_rules` | `SipContact`, `CustomThreshold`, `NetworkInterlockingRule` |

## Seções de configuração

Classes em `ControlId.Access.Configuration`, usadas com `GetConfigurationSectionAsync<T>()` e
`SetConfigurationSection(s)Async`. Tipos conforme a documentação; flags 0/1 viram `bool`; parâmetros que só aparecem
em exemplos, sem descrição, ficam como `string`.

| Seção | Classe | Parâmetros |
| --- | --- | --- |
| `alarm` | `AlarmSettings` | 14 |
| `barras` | `BarrasSettings` | 9 |
| `bio_id` | `BioIdSettings` | 1 |
| `buzzer` | `BuzzerSettings` | 5 |
| `camera_overlay` | `CameraOverlaySettings` | 2 |
| `card_readerN` | `CardReaderSettings` | 2 |
| `catra` | `CatraSettings` | 4 |
| `energy` (nome inferido) | `EnergySettings` | 5 |
| `enroller` | `EnrollerSettings` | 1 |
| `face_id` | `FaceIdSettings` | 7 |
| `face_module` | `FaceModuleSettings` | 2 |
| `general` | `GeneralSettings` | 39 |
| `gpio` | `GpioSettings` | 4 |
| `hid` (nome inferido) | `HidSettings` | 7 |
| `identifier` | `IdentifierSettings` | 18 |
| `led_white` | `LedWhiteSettings` | 1 |
| `mifare` | `MifareSettings` | 5 |
| `monitor` | `MonitorSettings` | 6 |
| `ntp` | `NtpSettings` | 2 |
| `online_client` | `OnlineClientSettings` | 7 |
| `onvif` | `OnvifSettings` | 14 |
| `opening_times` | `OpeningTimesSettings` | 4 |
| `osdp` (nome inferido) | `OsdpSettings` | 7 |
| `pjsip` | `PjsipSettings` | 32 |
| `push_server` | `PushServerSettings` | 3 |
| `rfid` (nome inferido) | `RfidSettings` | 2 |
| `rs485` (nome inferido) | `Rs485Settings` | 3 |
| `sec_box` | `SecBoxSettings` | 17 |
| `sec_boxs` | `SecBoxsSettings` | 2 |
| `snmp_agent` | `SnmpAgentSettings` | 1 |
| `uhf` | `UhfSettings` | 10 |
| `video_player` | `VideoPlayerSettings` | 6 |
| `video_stream` | `VideoStreamSettings` | 1 |
| `w_out0` | `WOut0Settings` | 2 |

## Divergências da documentação e como foram tratadas

| Onde | Divergência | Decisão |
| --- | --- | --- |
| `remote_enroll` | Tabela diz `msg`; descrição e exemplo dizem `message` | `message` |
| `door_state` | Título diz `door_state`; exemplo usa `doors_state` | `door_state` |
| `alarm_status` | `stop` tem padrão `true`, e a consulta "não tem parâmetros" | A consulta envia `stop: false` |
| `set_custom_video` | Tabela diz `custom_video_enable` (bool); exemplo usa `custom_video_enabled: "1"` | Exemplo |
| `face_template_enroll` | Tabela diz `face_tempaltes`; exemplo usa `face_templates` | `face_templates` |
| `face_template_destroy` | Título sem `.fcgi`; exemplo com `.fcgi` | Com `.fcgi` |
| `user_list_images` | GET numa página, POST na outra | Sem timestamp: POST; com `get_timestamp`: GET, como nos exemplos |
| `transactions` (Push em lote) | `transactionid` string no envio, número no resultado; `transaction_results` vs. `transactions_results` | Envia string; aceita os dois nomes e tipos |
| `logo` | Slots "de 1 a 8", mas `id=5` é "a 6ª posição" | Repassa o número recebido |
| `user_set_image_list` | Limite de 1 MB numa página e 2 MB na outra | Documentado nos dois valores |
| Eventos de acesso | Lista dos 15 tipos sem números | Numerados de 1 a 15 pela ordem (3, 7 e 11 confirmados no texto) |
| `alarm_logs.event` | Lista sem números | Mantido como `int` |
| Configuração | Módulos de Energia, RS485, RFID, HID e OSDP sem nome | Nome inferido do título e marcado nas classes |
| `session_is_valid` (servidor) | Resposta não documentada | `{"session_is_valid": true}` |

## A validar em equipamento real

- Os nomes de módulo inferidos (Energia, RS485, RFID, HID, OSDP) e os tipos dos parâmetros sem descrição.
- As decisões da tabela acima, em especial `message`, `door_state`, a numeração dos eventos e o slot do logotipo.
- O formato do resultado de `export_objects` e do corpo de `template_create` (não descritos).
