# Changelog

Formato baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/) e [SemVer](https://semver.org/lang/pt-BR/).

## [Unreleased]

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

[Unreleased]: https://github.com/DaviLuisSantos/ControlId.Net/compare/v0.1.0-alpha.2...HEAD
[0.1.0-alpha.2]: https://github.com/DaviLuisSantos/ControlId.Net/compare/v0.1.0-alpha.1...v0.1.0-alpha.2
[0.1.0-alpha.1]: https://github.com/DaviLuisSantos/ControlId.Net/releases/tag/v0.1.0-alpha.1
