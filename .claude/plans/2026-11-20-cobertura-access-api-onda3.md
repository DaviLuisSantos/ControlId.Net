# Plano: Cobertura da Access API, rodada 2 (Onda 3 do plano: 53 endpoints específicos de produto)
Slug: cobertura-access-api-onda3 | Criado: 2026-11-20
Verificação final: `dotnet build ControlId.Net.sln -c Release -warnaserror --no-incremental && dotnet test tests/ControlId.Net.Tests -c Release --no-build -f net8.0 && dotnet test tests/ControlId.Net.Tests -c Release --no-build -f net10.0 && dotnet test tests/ControlId.Net.Tests -c Release --no-build -f net48 && dotnet test tests/ControlId.Net.IntegrationTests -c Release --no-build && dotnet pack ControlId.Net.sln -c Release --no-build -o artifacts/plano`
Sujos antes: nenhum

Contexto: Onda 3 de `docs/plano-cobertura-access-api.md` (tabela "Onda 3"). Os endpoints vêm de `tests/ControlId.Net.Tests/Cobertura/pendentes.txt`, menos os 11 da Onda 4 (`hid_*`, `*oem*`), que ficam pendentes. Fonte dos campos: as páginas em `docs/controlid-api/paginas/**` citadas na tabela do plano. Padrões iguais aos da rodada 1 (`.claude/plans/2026-11-20-cobertura-access-api.md`).

## Contratos

Regras gerais (todas as tarefas):
- Código novo em arquivos parciais próprios: `src/ControlId.Net/Access/IAccessClient.<Area>.cs` e `AccessClient.<Area>.cs`. Modelos em `Models/<Area>Models.cs`, um arquivo de teste por tarefa. Reutilize os helpers de `AccessClient.cs` (`Required`, `GetJsonAsync<T>`, `ValidateImage`, `DataLoss.Require`) e de `AccessClient.Diagnostics.cs` (`GetTextAsync`). Helper privado novo leva o nome da área como prefixo, para não colidir entre parciais.
- Nomes de tipo que já existem e não podem ser redefinidos: `SecBox`, `NetworkInterfaceKind` (reutilizar para wired/wireless), `ScreenFrame`, `DeviceLog`, `FaceLicense`.
- Estilo: `<summary>` em português começando por `<c>endpoint.fcgi</c>`; produto/firmware no summary; `<param>` em todos os parâmetros ou em nenhum (CS1573); `cref` sem ambiguidade (CS0419).
- Não alterar assinatura pública existente.
- Destrutivo (troca/reset de chave, esquecer rede, apagar mídia em massa) → `DataLossConfirmation` + `DataLoss.Require`.
- Segredos (`psk`, `scbk`, chave privada EAP, segredo do dual sided, senha do `request_redirect`, senha de licença): nunca em exceção nem log; mascarados no `ToString` dos modelos.
- Operação assíncrona no equipamento (calibração, firmware do SecBox): método que inicia + método de status + `Wait...Async(TimeSpan? pollInterval, CancellationToken)` que sonda até terminar.
- Executores NÃO editam: `PublicAPI.*.txt`, `CompatibilitySuppressions.xml`, `Internal/ReadRetry.cs`, `DocumentationCoverageTests.cs`, `docs/COBERTURA.md`, `CHANGELOG.md`, `docs/plano-cobertura-access-api.md`, `Cobertura/*.txt`, `tools/ControlId.Net.Simulator/**`, `tests/ControlId.Net.IntegrationTests/**`. Isso é da Onda 2 (T6/T7).
- Testes: xUnit + `FakeDeviceHandler`; nos uploads binários, conferir `ContentType` e query. O corpo é capturado como string.

## Ondas
- [x] Onda 1: T1 ✓, T2 ✓, T3 ✓, T4 ✓, T5 ✓ | verificação: `dotnet build ControlId.Net.sln -c Release -warnaserror --no-incremental "-p:MSBuildWarningsNotAsErrors=NU1900%3BRS0016%3BRS0017" && dotnet test tests/ControlId.Net.Tests -c Release --no-build -f net8.0 --filter "FullyQualifiedName!~EndpointCoverageGuard"`
- [x] Onda 2: T6 ✓, T7 ✓ | verificação: a verificação final

## T1
- O que: Wi-Fi (`set_wifi_source`, `wifi_scan`, `set_wifi_settings`, `get_wifi_data`, `forget_wifi_connected_network`, `check_ip_conflict`) e certificados EAP (`eap_802_1X_certificate_change`, `eap_802_1X_certificate_change_wifi`, `eap_tls_802_1X_certificate_change`, `eap_tls_802_1X_private_key_change`, `eap_tls_802_1X_private_key_persist`, `get_eap_tls_csr_file`). Fontes: `sistema/configuracoes-de-rede.md` (#wi-fi) e `sistema/certificados-eap.md`. `psk` mascarado; esquecer a rede usa `DataLossConfirmation`.
- Arquivos: src/ControlId.Net/Access/IAccessClient.Wifi.cs, src/ControlId.Net/Access/AccessClient.Wifi.cs, src/ControlId.Net/Access/Models/WifiModels.cs, tests/ControlId.Net.Tests/WifiEapTests.cs (todos novos)
- Depende de: —
- Pronto quando: 12 endpoints com teste de requisição e parse, mais um teste de que o `psk` não aparece no `ToString`.
- modelo: sonnet | esforço: high

## T2
- O que: mídia de tela: slideshow (`send_slideshow_images`, `get_slideshow_images_list`, `get_slideshow_image`, `delete_slideshow_image`, `remove_slideshow_images`), quiosque (`set_kiosk_icon`, `get_kiosk_icon`, `delete_kiosk_icon`, `get_kiosk_icons_list`), interfonia (`set_phone_icon`, `get_phone_icon`, `remove_phone_icon`, `make_sip_conference`) e streaming (`set_streaming_logo`, `get_streaming_logo`, `remove_streaming_logo`). Fontes: `fotos-e-logotipo/gerenciar-slideshow.md`, `particularidade-dos-produtos/{modo-quiosque,interfonia-sip-idface,streaming-idface}.md`. Seguir o padrão de `SetLogoAsync`/`GetLogoAsync`; validar formato e tamanho conforme a doc.
- Arquivos: src/ControlId.Net/Access/IAccessClient.ScreenMedia.cs, src/ControlId.Net/Access/AccessClient.ScreenMedia.cs, src/ControlId.Net/Access/Models/ScreenMediaModels.cs, tests/ControlId.Net.Tests/ScreenMediaTests.cs (todos novos)
- Depende de: —
- Pronto quando: 16 endpoints com teste.
- modelo: sonnet | esforço: medium

## T3
- O que: SecBox (`device_has_secbox`, `secbox_is_active`, `secbox_serial_number`, `update_secbox_firmware`, `update_secbox_firmware_status`, `update_secbox_firmware_version`, `change_crypto_key_default`, `change_crypto_key_random`, `reset_crypto_key`) e dual sided (`set_dual_sided_secret`, `get_dual_sided_status`). Fontes: `sistema/secbox.md`, `sistema/alterar-chave-de-seguranca-secbox.md`, `sistema/dual-sided-secbox.md`. Troca/reset de chave com `DataLossConfirmation`; firmware com `WaitSecBoxFirmwareUpdateAsync`; segredo do dual sided só de escrita (sessão de admin, citada no summary).
- Arquivos: src/ControlId.Net/Access/IAccessClient.SecBoxDevice.cs, src/ControlId.Net/Access/AccessClient.SecBoxDevice.cs, src/ControlId.Net/Access/Models/SecBoxDeviceModels.cs, tests/ControlId.Net.Tests/SecBoxDeviceTests.cs (todos novos)
- Depende de: —
- Pronto quando: 11 endpoints com teste; teste do `Wait...` (termina, respeita o cancelamento) e da exigência de confirmação.
- modelo: sonnet | esforço: high

## T4
- O que: OSDP (`get_osdp_installation_mode`, `osdp_get_connection_status`, `osdp_get_output_capabilities`, `osdp_refresh`, `osdp_scbk`, `osdp_set_secure_key`, `osdp_set_secure_type`) e calibração da câmera (`remote_camera_calibration`, `cancel_remote_camera_calibration`, `camera_calibration_status` + `WaitCameraCalibrationAsync`). Fontes: `sistema/configuracoes-de-rede.md` (OSDP) e `reconhecimento-facial/calibracao-da-camera.md`. `scbk` é segredo. A doc escreve `nun_outputs` (sic): mapear o nome literal e registrar isso num comentário.
- Arquivos: src/ControlId.Net/Access/IAccessClient.Osdp.cs, src/ControlId.Net/Access/AccessClient.Osdp.cs, src/ControlId.Net/Access/IAccessClient.CameraCalibration.cs, src/ControlId.Net/Access/AccessClient.CameraCalibration.cs, src/ControlId.Net/Access/Models/OsdpModels.cs, tests/ControlId.Net.Tests/OsdpCalibrationTests.cs (todos novos)
- Depende de: —
- Pronto quando: 10 endpoints com teste, incluindo o `Wait...`.
- modelo: sonnet | esforço: medium

## T5
- O que: licenças e modo: `FaceLicense.Pro20k` (no fim do enum) → `upgrade_twenty_thousand_face_templates.fcgi` no switch de `ApplyFaceLicenseAsync`; novo `UpgradeFingerprintCapacityAsync(string password)` → `upgrade_ten_thousand_templates`; `SetProOperationModeAndRebootAsync(string mode)` → `set_pro_operation_mode_and_reboot` (valores da doc: iDFace 10k/15k/30k, iDFace Max 20k/50k/100k; o summary avisa que reinicia). Redirecionamento: `RedirectRequestAsync` → `request_redirect` com os dois modos da doc (por `deviceId` ou por endereço+login+senha) e um parâmetro `command`; o summary avisa que a credencial do destino vai na query string. Fontes: `particularidade-dos-produtos/{update-idface,update-idface-max,update-idflex-idaccess-nano}.md`, `sistema/redirecionar-requisicao.md`.
- Arquivos: src/ControlId.Net/Access/AccessClient.cs, src/ControlId.Net/Access/Models/NetworkModels.cs, src/ControlId.Net/Access/IAccessClient.Provisioning.cs, src/ControlId.Net/Access/AccessClient.Provisioning.cs, src/ControlId.Net/Access/Models/ProvisioningModels.cs, tests/ControlId.Net.Tests/ProvisioningTests.cs (novos, exceto os dois primeiros)
- Depende de: —
- Pronto quando: 4 endpoints com teste; `Pro20k` testado; a senha do redirect não aparece em exceção.
- modelo: sonnet | esforço: medium

## T6
- O que: integração. (1) `PublicAPI.Unshipped.txt` (RS0016; conferir com build `--no-incremental`). (2) Regerar `src/ControlId.Net/CompatibilitySuppressions.xml` com `dotnet pack src/ControlId.Net/ControlId.Net.csproj -c Release -p:GenerateCompatibilitySuppressionFile=true`, mantendo os comentários existentes e comentando cada grupo novo (CP0006 dos membros novos de `IAccessClient`; o novo membro de `FaceLicense`, se acusado). (3) `ReadRetry`: leituras puras novas (`get_wifi_data`, `check_ip_conflict`, `camera_calibration_status`, `get_slideshow_images_list`, `get_slideshow_image`, `get_kiosk_icon`, `get_kiosk_icons_list`, `get_phone_icon`, `get_streaming_logo`, `device_has_secbox`, `secbox_is_active`, `secbox_serial_number`, `update_secbox_firmware_status`, `update_secbox_firmware_version`, `get_dual_sided_status`, `get_osdp_installation_mode`, `osdp_get_connection_status`, `osdp_get_output_capabilities`). (4) Linhas em `DocumentationCoverageTests.Calls`. (5) Tirar de `pendentes.txt` os 53 implementados; devem sobrar só os 11 da Onda 4. (6) `docs/COBERTURA.md`, `CHANGELOG.md [Unreleased]` (Added; Changed: `IAccessClient` ganhou membros) e marcar a Onda 3 em `docs/plano-cobertura-access-api.md`. Pode rodar build, pack e o filtro `EndpointCoverageGuard`.
- Arquivos: src/ControlId.Net/PublicAPI.Unshipped.txt, src/ControlId.Net/CompatibilitySuppressions.xml, src/ControlId.Net/Internal/ReadRetry.cs, tests/ControlId.Net.Tests/DocumentationCoverageTests.cs, tests/ControlId.Net.Tests/Cobertura/pendentes.txt, docs/COBERTURA.md, CHANGELOG.md, docs/plano-cobertura-access-api.md
- Depende de: T1–T5
- Pronto quando: build `-warnaserror` sem supressão, `pack` e guarda passam.
- modelo: sonnet | esforço: medium

## T7
- O que: rotas no `Commands` de `tools/ControlId.Net.Simulator/AccessApi.cs` para os 53 endpoints (respostas no formato da doc; estado em `DeviceState` só onde for trivial: mídia, NTP, Wi-Fi). As operações assíncronas terminam na primeira sondagem de status. Testes de ponta a ponta para cada método novo, contra `RunningSimulator`.
- Arquivos: tools/ControlId.Net.Simulator/AccessApi.cs, tools/ControlId.Net.Simulator/DeviceState.cs, tests/ControlId.Net.IntegrationTests/CoverageRound2EndToEndTests.cs (novo)
- Depende de: T1–T5
- Pronto quando: `dotnet test tests/ControlId.Net.IntegrationTests --filter CoverageRound2` passa, com cada método novo coberto.
- modelo: sonnet | esforço: medium

## Bloqueios
(vazio)

## Correções do orquestrador
- Onda 1: CA2263 em `AccessClient.Osdp.cs` (`Enum.IsDefined` trocado por padrão explícito, compatível com
  netstandard2.0) e CA1859 em `AccessClient.Wifi.cs` (helper passa a devolver `Task<string>`).
