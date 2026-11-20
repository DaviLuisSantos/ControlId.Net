# Plano: Cobertura da Access API (0.1, Onda 5, 0.2/0.3, Onda 1, Onda 2)
Slug: cobertura-access-api | Criado: 2026-11-20
Verificação final: `dotnet build ControlId.Net.sln -c Release -warnaserror && dotnet test tests/ControlId.Net.Tests -c Release --no-build -f net8.0 && dotnet test tests/ControlId.Net.Tests -c Release --no-build -f net10.0 && dotnet test tests/ControlId.Net.Tests -c Release --no-build -f net48 && dotnet test tests/ControlId.Net.IntegrationTests -c Release --no-build && dotnet pack ControlId.Net.sln -c Release --no-build -o artifacts/plano`
Sujos antes: ?? docs/controlid-api/, ?? docs/plano-cobertura-access-api.md, ?? tools/controlid-docs-dump/

Contexto: executa `docs/plano-cobertura-access-api.md`. Fonte de verdade dos endpoints: `docs/controlid-api/paginas/**` e `docs/controlid-api/postman/*.md` (o executor lê a página do endpoint para nomes e tipos de campo). Decisão do usuário: `EnrollFaceTemplatesAsync` passa a ter `rejectExistingFaces = true` por padrão.

## Contratos

Regras gerais (todas as tarefas):
- `AccessClient` e `IAccessClient` viram `partial` (T1). Código novo da Onda 2 vai em arquivos parciais próprios: `src/ControlId.Net/Access/IAccessClient.<Area>.cs` e `src/ControlId.Net/Access/AccessClient.<Area>.cs`. Helpers privados de `AccessClient.cs` (`Required`, `GetJsonAsync<T>`, `ValidateImage`, `DataLoss.Require`, `_transport`) são reutilizados, não duplicados.
- Estilo: `<summary>` em português começando por `<c>endpoint.fcgi</c>`; `/// <inheritdoc/>` na implementação; restrição de produto/firmware no summary ("Apenas iDFace Max V7.x.x").
- Transporte (`src/ControlId.Net/Internal/ControlIdTransport.cs`): JSON tipado `PostAsync<T>`; texto `PostRawAsync`; download `PostForBytesAsync`/`GetBytesAsync`; upload `PostBytesAsync(endpoint, bytes, "application/octet-stream", ct, query)`; GET com JSON via `GetJsonAsync<T>` (`AccessClient.cs:1629`).
- Não alterar assinatura pública existente: adicionar sobrecarga (RS0026/27 já suprimidos em `src/Directory.Build.props`).
- Destrutivo → parâmetro `DataLossConfirmation confirmation` + `DataLoss.Require(...)`.
- Segredos (senha, psk, chaves): nunca em mensagem de exceção nem log; não entram em `ReadRetry`.
- Executores NÃO editam: `PublicAPI.*.txt`, `Internal/ReadRetry.cs`, `DocumentationCoverageTests.cs`, `docs/COBERTURA.md`, `CHANGELOG.md`, `tools/ControlId.Net.Simulator/**`, `tests/ControlId.Net.Tests/Cobertura/*.txt`. Isso é da Onda 3 (T10/T11).
- Testes unitários: xUnit + `FakeDeviceHandler` (`OnJson`, `OnBytes`, `RequestsTo(...).Single().Body`), um arquivo novo por tarefa.

Assinaturas novas (T3, Onda 0.2), em `IAccessClient.cs`/`AccessClient.cs`:
```csharp
// NetworkSettings (+): string? Interface ["interface"], bool? DhcpEnabled ["dhcp_enabled"], bool? TenMbps ["ten_mbps"]
public enum Ieee8021XEapMethod { Peap, Ttls, Tls }                       // Models/NetworkModels.cs
public sealed class Ieee8021XSettings { bool Enabled; string? Login; string? Password;
  Ieee8021XInnerAuthentication InnerAuthentication = MsChapV2; Ieee8021XEapMethod? EapMethod; bool? UsesCertificate; string? Interface; }
Task Configure8021XAsync(Ieee8021XSettings settings, CancellationToken cancellationToken = default);
public sealed class FactoryResetOptions { bool KeepNetworkSettings; bool KeepAllConfiguration; bool KeepOsdpSettings; } // Models/DeviceCommandModels.cs
Task ResetToFactoryDefaultAsync(DataLossConfirmation confirmation, FactoryResetOptions options, CancellationToken cancellationToken = default);
// AuditLogCategories (+): Boot = 128, Push = 256; All inclui os 9
public sealed class AfdExportOptions { long? InitialNsr; DateTime? InitialDate; bool Portaria671 = true; int? Limit; int? Offset; } // Models/ReportRequest.cs
Task<string> ExportAfdAsync(AfdExportOptions options, CancellationToken cancellationToken = default);
public enum ScreenFrame { Screen, Display }
Task<byte[]> CaptureScreenAsync(ScreenFrame frame = ScreenFrame.Screen, CancellationToken cancellationToken = default); // save_screenshot, format_type "png"
public sealed class UserImageUploadOptions { bool RejectExistingFace = true; bool BypassQualityScore; } // Models/UserImage.cs
Task<FaceImageResult> SetUserImageAsync(long userId, byte[] image, UserImageUploadOptions options, DateTimeOffset? timestamp = null, CancellationToken cancellationToken = default); // tipo de retorno = o da sobrecarga atual
Task<...> SetUserImagesAsync(IEnumerable<UserImage> images, UserImageUploadOptions options, CancellationToken cancellationToken = default);
// EnrollFaceTemplatesAsync: default rejectExistingFaces = true
```
As sobrecargas antigas encaminham para as novas.

Online (T5): `OnlineIdentificationKind` + `Pin`, `FaceTemplate` (no fim). `PinEvent` (`Pin`), `FaceTemplateEvent` (`Template`, `TemplateFF`, `TemplateMF`, `TemplateMM`) entram em `IdentificationEndpoints`/`Parse`. `RemoteEnrollType` + `Tag` (no fim; `RemoteEnrollRequest.ToBody` lança `ArgumentException` explicando que tag só chega por `tag_create`). `RemoteEnrollResult` + `Registration` ["registration"], `TagValue` ["tag_value"], `NumBits` ["num_bits"]. `face_template_create`→Face e `tag_create`→Tag em `EnrollEndpoints`/`ParseEnroll`. `KioskEnrollmentEvent` (`UserId`, `DeviceId`, `UserImage` byte[]?, `FaceTemplate`, `ImageTimestamp`) + `OnlineEvents.ParseKioskEnrollment(string json)`. `IOnlineAccessHandler.OnKioskEnrollmentAsync(KioskEnrollmentEvent, CancellationToken)` com implementação padrão vazia; rota `new_user_enrolled.fcgi` em `MapControlIdOnline`, com a mesma guarda de token e `SameDevice` do cadastro, responde 200 vazio.

Onda 2: nomes de método conforme `docs/plano-cobertura-access-api.md` §2.1 a §2.5. Logs: `enum DeviceLog { AccessControl, OpenVpn, Wpa, WpaSupplicant }` + `Task<string> GetDeviceLogAsync(DeviceLog log, CancellationToken)`.

Leituras para `ReadRetry` (T10): `face_template_count_distinct`, `face_template_match`, `template_match`, `object_metadata`, `export_object`, `export_custom_tables_metadata`, `get_ntp_server`, `get_ntp_server_status`, `check_port_availability`, `get_vpn_ip`, `get_ssl_certificate`, `get_802_1X_status`, `get_ac_log`, `get_openvpn_log`, `get_wpa_log`, `get_wpasupplicant_log`, `io_state`, `is_identifying`, `get_catra_info`, `get_last_snapshot`, `device_rfid_module`, `get_hid_last_card`, `get_wiegand_card_read`.

Guarda de cobertura (T2): `tests/ControlId.Net.Tests/EndpointCoverageGuardTests.cs` acha a raiz subindo de `AppContext.BaseDirectory` até `ControlId.Net.sln`, lê `docs/controlid-api/endpoints.json` e coleta os literais de string em `src/**/*.cs` (normalizando `/` inicial e `.fcgi`). O teste falha se um endpoint não estiver coberto nem listado em `tests/ControlId.Net.Tests/Cobertura/pendentes.txt` ou `nao-cobrir.txt`, e também se um item listado já estiver coberto. Formato dos arquivos: um `nome.fcgi` por linha, `#` comenta.

## Ondas
- [x] Onda 0: T1 ✓ | verificação: `dotnet build ControlId.Net.sln -c Release -warnaserror && dotnet test tests/ControlId.Net.Tests -c Release --no-build -f net8.0`
- [x] Onda 1: T2 ✓, T3 ✓, T4 ✓, T5 ✓ | verificação: `dotnet build ControlId.Net.sln -c Release -warnaserror "-p:MSBuildWarningsNotAsErrors=NU1900%3BRS0016%3BRS0017" && dotnet test tests/ControlId.Net.Tests -c Release --no-build -f net8.0 --filter "FullyQualifiedName!~EndpointCoverageGuard"`
- [x] Onda 2: T6 ✓, T7 ✓, T8 ✓, T9 ✓ | verificação: igual à Onda 1
- [x] Onda 3: T10 ✓, T11 ✓ | verificação: a verificação final

## T1
- O que: tornar `AccessClient` e `IAccessClient` `partial` (só a palavra-chave). Corrigir DNS: `JsonPropertyName` `dns_primary`→`primary_dns` e `dns_secondary`→`secondary_dns` (`Models/DeviceCommandModels.cs:53-57`). Ajustar/adicionar teste de serialização do `set_system_network`.
- Arquivos: src/ControlId.Net/Access/AccessClient.cs, src/ControlId.Net/Access/IAccessClient.cs, src/ControlId.Net/Access/Models/DeviceCommandModels.cs, tests/ControlId.Net.Tests/DeviceCommandsTests.cs
- Depende de: —
- Pronto quando: o corpo de `SetNetworkAsync` sai com `primary_dns`/`secondary_dns`; o teste cobre isso.
- modelo: sonnet | esforço: low

## T2
- O que: guarda de cobertura (Contratos) + `pendentes.txt` com os 102 faltantes atuais (agrupados por onda do plano, em comentários) + `nao-cobrir.txt` (`dual_sided_query.fcgi`, `tests.fcgi`). Workflow mensal `.github/workflows/controlid-docs.yml` (`schedule` mensal + `workflow_dispatch`): setup-python, `pip install -r tools/controlid-docs-dump/requirements.txt`, roda o script e abre PR com `peter-evans/create-pull-request` em versão fixa. Para conferir a lista, rode só este teste: `dotnet test tests/ControlId.Net.Tests -f net8.0 --filter EndpointCoverageGuard`.
- Arquivos: tests/ControlId.Net.Tests/EndpointCoverageGuardTests.cs, tests/ControlId.Net.Tests/Cobertura/pendentes.txt, tests/ControlId.Net.Tests/Cobertura/nao-cobrir.txt, .github/workflows/controlid-docs.yml
- Depende de: T1
- Pronto quando: o teste passa contra `src/` atual; uma remoção de linha em pendentes.txt o faz falhar.
- modelo: sonnet | esforço: medium

## T3
- O que: Onda 0.2, assinaturas T3 dos Contratos. Os parâmetros saem das páginas `sistema/configuracoes-de-rede.md`, `sistema/redefinir-as-configuracoes.md`, `objetos/exportar-relatorio.md`, `reconhecimento-facial/captura-de-camera.md`, `fotos-e-logotipo/gerenciar-fotos-de-usuarios.md` e `reconhecimento-facial/operacoes-template-facial.md`. Atualizar os testes que fixam o comportamento antigo (`AccessReportsTests.cs:182` com 9 chaves; teste de `face_template_enroll` com `match` padrão).
- Arquivos: src/ControlId.Net/Access/IAccessClient.cs, src/ControlId.Net/Access/AccessClient.cs, src/ControlId.Net/Access/Models/DeviceCommandModels.cs, src/ControlId.Net/Access/Models/NetworkModels.cs, src/ControlId.Net/Access/Models/ReportRequest.cs, src/ControlId.Net/Access/Models/UserImage.cs, tests/ControlId.Net.Tests/AccessReportsTests.cs, tests/ControlId.Net.Tests/FacePhase3Tests.cs, tests/ControlId.Net.Tests/CoverageWave02Tests.cs (novo)
- Depende de: T1
- Pronto quando: cada item tem teste do corpo/query enviado; as sobrecargas antigas mantêm o comportamento, exceto o padrão de `EnrollFaceTemplatesAsync`.
- modelo: sonnet | esforço: medium

## T4
- O que: Onda 0.3, sem métodos novos (o acesso usa `GetObjectsAsync<T>`). `AccessCard` + `WiegandFormatId` ["wiegand_format_id"], `CredentialType` ["credential_type"]; `AccessGroup` + `IdentificationMode` ["identification_mode"]. Modelos novos `[ControlIdObject("wiegand_modes")] WiegandMode` e `[ControlIdObject("group_compatibility")] GroupCompatibility`, com campos de `objetos/lista-de-objetos.md` (`Key` correto; null se for associação). `DeviceSettings`: `HidSettings` + `card_process`, `custom_use_index`, `toc_crypt_mode`, `toc_crypt_key`, `toc_site_key`; `OsdpSettings` + `remote_validation_enabled`, `remote_validation_timeout_ms`; `IdentifierSettings` + `two_person_auth_enabled` (tipos de `configuracoes/parametros-configuracao.md`).
- Arquivos: src/ControlId.Net/Access/Models/AccessCard.cs, src/ControlId.Net/Access/Models/AccessGroup.cs, src/ControlId.Net/Access/Models/WiegandMode.cs (novo), src/ControlId.Net/Access/Models/GroupCompatibility.cs (novo), src/ControlId.Net/Access/Configuration/DeviceSettings.cs, tests/ControlId.Net.Tests/CoverageWave03Tests.cs (novo)
- Depende de: T1
- Pronto quando: há teste de desserialização dos objetos e de mapeamento das chaves de configuração.
- modelo: sonnet | esforço: medium

## T5
- O que: Onda 1, os 5 callbacks Online (Contratos/Online). Campos em `modos-de-operacao/eventos-de-identificacao-online.md`. Toda rota adicionada às listas precisa do caso correspondente no switch, por causa dos `!` em `ControlIdOnlineEndpoints.cs:149,190`.
- Arquivos: src/ControlId.Net/Access/Online/OnlineEvents.cs, src/ControlId.Net/Access/Models/RemoteEnroll.cs, src/ControlId.Net.AspNetCore/IOnlineAccessHandler.cs, src/ControlId.Net.AspNetCore/ControlIdOnlineEndpoints.cs, tests/ControlId.Net.Tests/OnlineNewRoutesTests.cs (novo)
- Depende de: T1
- Pronto quando: há testes de `Parse`/`ParseEnroll`/`ParseKioskEnrollment` e testes TestHost das 5 rotas (identificação devolve `result`; token exigido; `device_id` divergente → 403).
- modelo: sonnet | esforço: high

## T6
- O que: Onda 2.1 + 2.4, biometria (`face_template_match`, `face_template_count_distinct`, `template_match`) e hardware (`io_state`, `is_identifying`, `get_catra_info`, `get_last_snapshot`, `device_rfid_module`, `get_hid_last_card`, `get_wiegand_card_read`). `template_match`: query `temp_num`/`sizeN` + corpo concatenado, validando a soma.
- Arquivos: src/ControlId.Net/Access/IAccessClient.Hardware.cs, src/ControlId.Net/Access/AccessClient.Hardware.cs, src/ControlId.Net/Access/Models/HardwareModels.cs, tests/ControlId.Net.Tests/HardwareCoverageTests.cs (todos novos)
- Depende de: T1
- Pronto quando: 10 métodos com teste de requisição e de parse.
- modelo: sonnet | esforço: medium

## T7
- O que: Onda 2.2, schema customizado (`object_metadata`, `object_add`, `object_remove`, `object_add_field`, `object_update_field`, `object_remove_fields`, `export_object`, `export_custom_tables_metadata`). `CustomFieldDefinition` com `ForeignKey {Object, Field}`. Remover tabela/campo usa `DataLossConfirmation`. Fonte: `objetos/schema-objeto-customizado.md`, `objetos/exportar-relatorio.md`.
- Arquivos: src/ControlId.Net/Access/IAccessClient.CustomSchema.cs, src/ControlId.Net/Access/AccessClient.CustomSchema.cs, src/ControlId.Net/Access/Models/CustomSchemaModels.cs, tests/ControlId.Net.Tests/CustomSchemaTests.cs (todos novos)
- Depende de: T1
- Pronto quando: 8 endpoints com teste.
- modelo: sonnet | esforço: medium

## T8
- O que: Onda 2.3, rede, NTP e diagnóstico (`get_ntp_server`, `set_ntp_server`, `get_ntp_server_status`, `check_port_availability`, `get_vpn_ip`, `get_ssl_certificate`, `get_802_1X_status`, e `get_ac_log`/`get_openvpn_log`/`get_wpa_log`/`get_wpasupplicant_log` via `GetDeviceLogAsync(DeviceLog)`).
- Arquivos: src/ControlId.Net/Access/IAccessClient.Diagnostics.cs, src/ControlId.Net/Access/AccessClient.Diagnostics.cs, src/ControlId.Net/Access/Models/DiagnosticsModels.cs, tests/ControlId.Net.Tests/DiagnosticsCoverageTests.cs (todos novos)
- Depende de: T1
- Pronto quando: 11 endpoints com teste (o método GET/POST segue a página).
- modelo: sonnet | esforço: medium

## T9
- O que: Onda 2.5, senhas de sistema: `master_password` (`SetMasterPasswordAsync`, `GetMasterPasswordInfoAsync`) e `recovery_password` (`SetUpdateModePasswordAsync`, `RemoveUpdateModePasswordAsync`, `GetUpdateModePasswordStatusAsync`). Fonte: `sistema/reforco-de-seguranca.md`, `sistema/senha-do-modo-de-update.md`. A senha nunca aparece em exceção nem em `ToString`.
- Arquivos: src/ControlId.Net/Access/IAccessClient.SystemPasswords.cs, src/ControlId.Net/Access/AccessClient.SystemPasswords.cs, src/ControlId.Net/Access/Models/SystemPasswordModels.cs, tests/ControlId.Net.Tests/SystemPasswordsTests.cs (todos novos)
- Depende de: T1
- Pronto quando: há testes das variações de parâmetros e um teste de que a senha não vaza na mensagem de erro.
- modelo: sonnet | esforço: high

## T10
- O que: integração. (1) `PublicAPI.Unshipped.txt` de ControlId.Net e ControlId.Net.AspNetCore, via `dotnet format analyzers <csproj> --diagnostics RS0016` ou à mão a partir dos avisos (pode rodar build). (2) Leituras da lista dos Contratos em `ReadRetry.cs`. (3) Linhas novas em `DocumentationCoverageTests.Calls` para cada endpoint de cliente novo. (4) Tirar de `pendentes.txt` os 36 implementados. (5) `docs/COBERTURA.md`. (6) `CHANGELOG.md [Unreleased]`: Fixed (DNS), Changed (padrão `match`, `AuditLogCategories.All`, `IAccessClient` ganhou membros), Added. (7) Marcar as ondas feitas em `docs/plano-cobertura-access-api.md`.
- Arquivos: src/ControlId.Net/PublicAPI.Unshipped.txt, src/ControlId.Net.AspNetCore/PublicAPI.Unshipped.txt, src/ControlId.Net/Internal/ReadRetry.cs, tests/ControlId.Net.Tests/DocumentationCoverageTests.cs, tests/ControlId.Net.Tests/Cobertura/pendentes.txt, docs/COBERTURA.md, CHANGELOG.md, docs/plano-cobertura-access-api.md
- Depende de: T2–T9
- Pronto quando: `dotnet build -c Release -warnaserror` passa sem supressão e a guarda passa.
- modelo: sonnet | esforço: medium

## T11
- O que: simulador + ponta a ponta. Rotas no dicionário `Commands` de `AccessApi.cs` para os 31 endpoints de cliente da Onda 2 (respostas fixas no formato da doc; estado em `DeviceState` só se for trivial). Testes em arquivo novo, chamando os métodos de T6–T9 contra `RunningSimulator`.
- Arquivos: tools/ControlId.Net.Simulator/AccessApi.cs, tools/ControlId.Net.Simulator/DeviceState.cs, tests/ControlId.Net.IntegrationTests/CoverageEndToEndTests.cs (novo)
- Depende de: T6–T9
- Pronto quando: cada método novo da Onda 2 tem pelo menos um teste de ponta a ponta passando.
- modelo: sonnet | esforço: medium

## Bloqueios
(vazio)

## Correções do orquestrador
- Onda 1: `cref` ambíguo de `ExportAfdAsync` em `IAccessClient.cs:371` (CS0419), desambiguado.
- Onda 2: `<param name="cancellationToken">` faltando em `IAccessClient.SystemPasswords.cs` (CS1573).
- Verificação final: o `pack` falhou na validação de pacote (CP0006 nos membros novos de `IAccessClient` e CP0011 em
  `AuditLogCategories.All`). Supressões regeneradas em `src/ControlId.Net/CompatibilitySuppressions.xml`, com o motivo de
  cada grupo, conforme o ADR 0006.
