# Changelog

Formato baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/) e [SemVer](https://semver.org/lang/pt-BR/).

## [Unreleased]

### Fixed
- `DaoNotification.GetInsertedAccessLogs()` e `ObjectChange.ValuesAs<T>()` lançavam exceção com os eventos mais comuns: pelo Monitor, o iDFace manda `user_id: ""` em rosto não identificado e `confidence: ""` em cartão negado. A leitura do Monitor passa a tratar `""` em campo numérico como vazio (`null`); antes, um servidor como o de exemplo respondia 500 e a notificação se perdia.

### Documentation
- Cadastro remoto de PIN não funciona no iDFace 6.9.12 (responde `UNKNOWN`), anotado em [docs/COBERTURA.md](docs/COBERTURA.md).

## [0.4.0-alpha.1] - 2026-10-13

Correções encontradas testando num iDFace real (firmware 6.9.12).

### Fixed
- `GetConfigurationSectionAsync<T>` e `GetConfigurationAsync` falhavam na seção inteira quando o modelo não tinha um dos parâmetros (o firmware recusa a leitura toda: no iDFace, 25 das 34 seções, entre elas `general` e `monitor`). Agora o parâmetro apontado no erro sai da lista, a leitura é repetida e ele fica `null`/fora do resultado; o cliente lembra dos parâmetros inexistentes e não os pede de novo.
- `UpdateSecBoxAsync` era recusado ("integer expected"): `enabled`, `door_sensor_enabled` e `door_sensor_idle` passam a ser enviados como 0/1.
- `ExtractFaceTemplateAsync` devolvia `Success = false` mesmo extraindo o template, porque o iDFace não manda `success` nessa resposta. Sem o campo, `FaceImageResult.Success` agora vale a ausência de erros.
- `AuthorizeRemotelyAsync` era recusado sem usuário (ex.: `OnlineAuthorization.NotIdentified()`): o firmware exige `user_id`, `portal_id`, `user_name`, `user_image` e `actions`. Os que faltarem vão como 0, portal 1, vazio, `false` e lista vazia, sem alterar o objeto recebido.
- `ConfigureOnlineModeAsync` falhava fora das IHMs (o iDFace não tem `general.ihm_enterprise_mode`, e o `set_configuration` era recusado depois de aplicar parte dos valores). `ihm_enterprise_mode` só é enviado com `OnlineModeConfiguration.IsIhm`.
- `CreateHolidayAsync` lança `ArgumentOutOfRangeException` para datas depois de 19/01/2038, que o equipamento recusa (guarda os segundos em 32 bits).
- `AccessUser.UserTypeId` lê 0 como `null`: o equipamento devolve 0 quando o campo está vazio, mas recusa gravar 0 (chave estrangeira para `user_types`), então regravar um usuário lido falhava.

### Changed
- `PushServerSettings.PushRemoteAddress` passa de `int?` para `string?` (o valor é `ip:porta`; antes a leitura sempre dava `null`).

### Documentation
- Comportamentos observados no iDFace 6.9.12 em [docs/COBERTURA.md](docs/COBERTURA.md): `set_configuration` não atômico, áudio de acesso só em WAV, Push em lote não aceito, reinícios depois de exportar o log de auditoria e a lista do que o modelo não tem.

## [0.3.0-alpha.1] - 2026-10-12

Bases grandes e vários equipamentos: limite de requisições por equipamento, leitura paginada e operações em lote.

### Added
- `ControlIdDeviceOptions.MaxConcurrentRequests` (e em `ControlIdRepOptions`): limite de requisições simultâneas por equipamento, padrão 4, login incluído. As chamadas além do limite esperam a vez. O limite de conexões do `HttpClientHandler` criado pelo pacote (e pelo pacote de DI) acompanha o valor, então o comportamento é o mesmo no .NET Framework e no .NET Core.
- Leitura paginada com `IAsyncEnumerable`: `EnumerateUsersAsync` e `EnumerateAccessLogsAsync` (páginas por ID, estáveis com inserções e remoções durante a leitura), `EnumerateObjectsAsync<T>` (modelos tipados, ordenados pela chave) e `EnumerateObjectsAsync<T>(LoadObjectsRequest)` (qualquer objeto, por `limit`/`offset`). Página padrão de 500 registros (`AccessClient.DefaultPageSize`); a próxima página só é buscada quando a anterior é consumida.
- `ControlIdDevices.RunAsync`: a mesma operação em vários equipamentos em paralelo, com limite de equipamentos simultâneos (`maxParallelism`, padrão 8) e um `DeviceResult`/`DeviceResult<T>` por equipamento (nome, sucesso, valor, erro e tempo). A falha de um equipamento não interrompe os demais.
- `RunOnAccessDevicesAsync` e `RunOnRepDevicesAsync` em `IControlIdClientFactory` (pacote de DI), para todos os equipamentos registrados ou só os informados.
- Dependência de `Microsoft.Bcl.AsyncInterfaces` no alvo netstandard2.0 (para `IAsyncEnumerable`).

### Changed
- Até aqui as requisições a um equipamento não tinham limite no .NET Core (e ficavam em 2 conexões no .NET Framework). Agora o padrão é 4 simultâneas por equipamento; use `MaxConcurrentRequests` para ajustar.

## [0.2.0-alpha.1] - 2026-10-12

Cobertura completa da documentação pública da API de acesso (veja [docs/COBERTURA.md](docs/COBERTURA.md)).

### Changed
- `SetUserImageAsync` e `SetUserImagesAsync` passam a enviar `timestamp` (obrigatório na API) e `match`, e devolvem o resultado do cadastro facial (`FaceImageResult`). `UserImage` ganha `Timestamp` e `PushCommand.SetUserImage`, os mesmos parâmetros.

### Added
- Modelos para todos os objetos documentados que faltavam: `qrcodes`, `uhf_tags`, `pins`, `contingency_cards`, `alarm_zones`, `timed_alarms`, `user_roles`, `scheduled_unlocks`, `actions` (`ActionScript`), `devices`, `catra_infos`, `log_types`, `contacts`, `custom_thresholds`, `network_interlocking_rules`, `change_logs` e `access_log_access_rules`.
- CRUD tipado genérico: `[ControlIdObject]` nos modelos e `GetObjectsAsync<T>`/`CreateObjectAsync<T>`/`UpdateObjectAsync<T>`/`DeleteObjectAsync<T>`.
- QR Codes, tags UHF e PINs (`Get/Create...Async`), `SetAdministratorAsync`, `GetChangeLogsAsync` e `GetAccessLogRuleIdsAsync`.
- Vínculos `AccessLink.PortalAction`, `ScheduledUnlockAccessRule`, `AlarmZoneTimeZone` e `ContingencyCardAccessRule`.
- `AccessUser.ImageTimestamp` e `AccessUser.LastAccess`.
- Estado e comandos do equipamento: `GetDoorStatesAsync` (`door_state`), `GetAlarmStatusAsync`/`StopAlarmAsync` (`alarm_status`, com `AlarmCause` também em `AlarmLog.CauseType`), `RebootWithoutSessionAsync` (`GET /reboot`), `SetTurnstileLedAsync` (`remote_led_control`, iDBlock Next) e `ExecuteActionsWithStatusAsync` (resultado do intertravamento).
- `DeviceAction.OpenTurnstileCollector` (`open_collector`) e `DeviceAction.ActivateTurnstileRelay` (`relay=1|2`).
- Modo online: `ComponentId`/`ComponentName` (iDBlock Next).
- Reconhecimento facial: `TestUserImageAsync`, `GetUserImageWithTimestampAsync`, `GetUserImagesAsync` (`user_get_image_list`), `GetUserImageInfoAsync`, `DeleteUserImagesAsync`, `DeleteDanglingUserImagesAsync`, templates faciais (`ExtractFaceTemplate(s)Async`, `EnrollFaceTemplatesAsync`, `DeleteFaceTemplatesAsync`, `DeleteAllFaceTemplatesAsync`), `CaptureCameraAsync` (`save_screenshot`), `FaceImageResult` com notas e `FaceImageErrorCode`.
- `StartTemplateSyncAsync`/`EndTemplateSyncAsync` (carga de bases grandes) e `ExportObjectsAsync` (`export_objects`).
- Mídia do iDFace: modo propaganda (`UploadAdvertisingVideoAsync`, `SetAdvertisingModeAsync`, `RemoveAdvertisingVideoAsync`), mensagens sonoras (`UploadAccessAudioAsync`, `GetAccessAudioAsync`, `GetAccessAudioStatusAsync`), interfonia SIP (`MakeSipCallAsync`, `FinalizeSipCallAsync`, `GetSipStatusAsync`, som de chamada) e `GetRtspStreamUri`. Vídeo e áudio são enviados em partes de até 2 MB automaticamente.
- Rede e sistema: diagnóstico (`TestInternetConnectionAsync`, `PingAsync`, `NsLookupAsync`), `SetNetworkInterlockAsync`, `IsValidBiometryAsync`, OpenVPN (`SetVpnAsync`, `GetVpnInformationAsync`, `GetVpnStatusAsync`, `UploadVpnFileAsync`, `HasVpnFileAsync`, `GetVpnExampleFilesAsync`), `UploadSslCertificateAsync`, `Configure8021XAsync`, `SetOsdpInstallationModeAsync`, `ChangeIdCloudCodeAsync` e `PushConfiguration.IdCloud()`.
- `docs/COBERTURA.md` com a matriz de cobertura da documentação e `DocumentationCoverageTests`, que chama o método de cada um dos 85 endpoints documentados e confere endpoint e método HTTP.
- Configurações tipadas: 34 seções (`GeneralSettings`, `CatraSettings`, `AlarmSettings`, `IdentifierSettings`, `PjsipSettings`, `OnvifSettings`, `SecBoxSettings`...) com cerca de 245 parâmetros documentados, geradas a partir da documentação, e `Get/SetConfigurationSectionAsync`/`SetConfigurationSectionsAsync`. Os nomes dos módulos de Energia, RS485, RFID, HID e OSDP não constam da documentação e foram inferidos dos títulos.
- Licenças: `ApplyFaceLicenseAsync` (iDFace Pro 10k/15k/30k, iDFace Max 50k/100k) e `UpgradeToEnterpriseAsync` (iDFlex/iDAccess Nano).

## [0.1.0-alpha.2] - 2026-10-10

### Changed
- Autor e copyright dos pacotes e da licença passam a ser "Davi Luis".

## [0.1.0-alpha.1] - 2026-10-10

### Fixed
- `AccessLog` tinha `AccessRuleId` (`access_rule_id`), campo que não existe em `access_logs` e nunca vinha preenchido. Trocado pelos campos documentados: `IdentificationRuleId`, `QrCodeValue`, `UhfTag`, `PinValue`, `Confidence`, `Mask` e `LogTypeId`.
- `RepClient.GetAfd*` decodificava o AFD como UTF-8 quando o REP não informava o charset, corrompendo nomes acentuados; agora usa ISO-8859-1 (codificação das portarias) quando os bytes não são UTF-8 válido.

### Changed
- `FingerprintImage` renomeado para `CapturedImage` (também usado na face do modo online).

### Added
- Transporte HTTP com login automático, cache de sessão e re-login em 401.
- `AccessClient` (iDAccess / iDFlex / iDFace): objetos genéricos (`load/create/modify/destroy_objects`), usuários, logs de acesso, abertura de porta/SecBox/catraca, informações do sistema e data/hora.
- `AccessClient`: cartões, digitais (`templates`), grupos, regras de acesso, horários (`time_zones`/`time_spans`) e portais tipados; associações via `LinkAsync`/`UnlinkAsync` com `AccessLink`.
- `AccessClient`: fotos de usuário (`user_set_image`, `user_set_image_list`, `user_get_image`, `user_list_images`, `user_destroy_image`).
- Monitor: `MonitorNotifications.Parse` com notificações tipadas (`dao`, `door`, `secbox`, `catra_event`, `operation_mode`, `device_is_alive`, `access_photo`, `usb_drive`) e `ConfigureMonitorAsync`.
- Push: `PushServer` (fila por equipamento, `HandlePush`/`HandleResult`), `PushCommand`, `PushDevice` com operações tipadas e `ConfigurePushAsync`.
- Push em lote: `PushServer.SendBatchAsync`/`PushDevice.SendBatchAsync` (objeto `transactions`), com um `PushResult` por comando.
- Pacote `ControlId.Net.DependencyInjection`: `AddControlIdAccess`/`AddControlIdRep` (padrão ou por nome, por delegate ou `IConfiguration`), `AddControlIdAccessDevices`/`AddControlIdRepDevices` para vários equipamentos no `appsettings.json` e `IControlIdClientFactory`.
- Pacote `ControlId.Net.AspNetCore`: `AddControlIdPush`, `AddControlIdMonitor` (handler tipado ou delegate), `MapControlIdPush` e `MapControlIdMonitor`.
- Sample `ControlId.Net.Server` (ASP.NET Core) com Push e Monitor.
- Objetos tipados: áreas (`AccessArea`, `AccessLink.AreaAccessRule`), feriados (`AccessHoliday`), eventos de porta/SecBox/catraca (`AccessDeviceEvent`), logs de alarme (`AlarmLog`) e SecBox (`GetSecBoxAsync`/`UpdateSecBoxAsync`).
- Exportações: `GenerateReportAsync` (`report_generate`), `ExportAfdAsync`/`ExportAfdFileAsync` (`export_afd` do equipamento de acesso) e `ExportAuditLogsAsync` (`export_audit_logs`).
- Modo online: resultados do cadastro remoto (`card_create`, `face_create`, `fingerprint_create`, `template_create`, `pin_create`, `password_create`) via `OnlineEvents.ParseEnroll` e `IOnlineAccessHandler.OnRemoteEnrollAsync` no `MapControlIdOnline`; `RemoteEnrollResult.Face`.
- Comandos tipados no `AccessClient`: `ShowMessageAsync`/`ClearMessageAsync`, `BuzzAsync`, `ChangeLoginAsync` (atualiza as credenciais do próprio cliente), `HashPasswordAsync`, `RebootAsync`, `RebootToUpdateModeAsync`, `ResetToFactoryDefaultAsync`, `DeleteAllAdministratorsAsync`, `SetNetworkAsync`, logotipo (`Get`/`Set`/`Delete`/`ShowLogoAsync`), `GetGpioStateAsync`, `RefreshLedAsync`, `SetUsbPortEnabledAsync` e `CreateOrModifyObjectsAsync`; `PushCommand.ShowMessage`/`Buzz`/`Reboot`.
- Modo online (Pro/Enterprise): `ConfigureOnlineModeAsync`/`DisableOnlineModeAsync`, eventos tipados (`OnlineEvents.Parse`), `OnlineAuthorization` (Grant/Deny/NotIdentified), `AuthorizeRemotelyAsync` e, no ASP.NET Core, `AddControlIdOnline` + `MapControlIdOnline` (identificação, botoeira, foto do usuário e `device_is_alive`).
- `AccessEventType` (códigos de evento) e `AccessLog.EventType`; `DeviceAction.OpenDoor`/`OpenSecBox`/`ReleaseTurnstile`.
- Cadastro remoto: `RemoteEnrollAsync` (síncrono, cancela no equipamento em timeout/cancelamento), `StartRemoteEnrollAsync` (resultado via Monitor como `RemoteEnrollNotification`), `CancelRemoteEnrollAsync` e `PushCommand.RemoteEnroll`.
- `IControlIdClient` (base de `IAccessClient` e `IRepClient`): `SendAsync<T>`, `SendRawAsync`, `SendBytesAsync` e `DownloadAsync` para chamar qualquer endpoint `.fcgi` com sessão e re-login automáticos.
- `AccessClient.GetConfigurationAsync` (`get_configuration.fcgi`), por seção ou várias seções de uma vez.
- `AccessClient.SetConfigurationAsync` para `set_configuration.fcgi`.
- Parser do AFD (`ControlId.Rep.Afd`): leiautes das Portarias 671/2021 e 1510/2009, todos os tipos de registro (1 a 7, trailer e assinatura), CRC-16/KERMIT, validação do trailer e do NSR, leitura em streaming e detecção de encoding.
- `RepClient.GetAfdFileFromNsrAsync`/`GetAfdFileFromDateAsync`/`GetAfdFileAsync` devolvem o AFD interpretado.
- `RepClient` (REP iDClass): about, informações do sistema, data/hora, usuários (PIS/CPF) e download de AFD (incluindo modo Portaria 671).

[Unreleased]: https://github.com/DaviLuisSantos/ControlId.Net/compare/v0.4.0-alpha.1...HEAD
[0.4.0-alpha.1]: https://github.com/DaviLuisSantos/ControlId.Net/compare/v0.3.0-alpha.1...v0.4.0-alpha.1
[0.3.0-alpha.1]: https://github.com/DaviLuisSantos/ControlId.Net/compare/v0.2.0-alpha.1...v0.3.0-alpha.1
[0.2.0-alpha.1]: https://github.com/DaviLuisSantos/ControlId.Net/compare/v0.1.0-alpha.2...v0.2.0-alpha.1
[0.1.0-alpha.2]: https://github.com/DaviLuisSantos/ControlId.Net/compare/v0.1.0-alpha.1...v0.1.0-alpha.2
[0.1.0-alpha.1]: https://github.com/DaviLuisSantos/ControlId.Net/releases/tag/v0.1.0-alpha.1
