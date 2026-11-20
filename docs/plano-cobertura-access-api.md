# Plano de cobertura da Access API — ControlId.Net 0.9.0-alpha.1 (2026-11-20)

Comparação entre a documentação oficial da Control iD (dump em [`controlid-api/`](controlid-api/README.md), gerado por
`tools/controlid-docs-dump/dump_controlid_docs.py`) e o código de `main` em `64f0163`.

## Como foi medido

- **Documentado:** 204 endpoints `*.fcgi` distintos, somando as 96 páginas do site e as 434 requisições da coleção
  Postman (`controlid-api/endpoints.json`).
- **Coberto:** o nome do endpoint (com ou sem `.fcgi`) aparece como string literal em `src/`. Isso conta também as rotas
  que o equipamento chama no servidor (Monitor, Online, Push), que o código guarda sem o sufixo.
- **Resultado:** **102 cobertos, 102 faltando.** Todos os endpoints Access usados pela biblioteca estão documentados.
  Os 36 endpoints a mais que aparecem em `src/` (`get_afd`, `add_users`, `set_coil_paper`...) são da API REP (iDClass),
  fora deste escopo.
- A medição é textual. Ela não confere se os parâmetros de um endpoint coberto estão completos; a Onda 0 trata disso
  para tudo o que o changelog de setembro/2026 mudou.

Prioridade: **P1** corrigir já · **P2** próxima versão · **P3** quando houver demanda ou equipamento para testar.

## Resumo

| Onda | Conteúdo | Endpoints novos | Prioridade |
|---|---|---|---|
| 0 | Bug do DNS, parâmetros e campos novos em métodos e modelos existentes | 0 | P1/P2 |
| 1 | Callbacks Online que o equipamento envia e a biblioteca ainda não interpreta | 5 | P2 |
| 2 | Uso geral: biometria, schema customizado, rede/NTP/logs, leitura de hardware, senhas | 31 | P2 |
| 3 | Específicos de produto: Wi-Fi, EAP, calibração, mídia de tela, SecBox, OSDP, licenças | 53 | P3 |
| 4 | Sensíveis: chaves do leitor HID e OEM, só com opt-in explícito | 11 | P3 |
| — | Não cobrir: rota entre equipamentos e teste de fábrica | 2 | — |
| 5 | Guarda de cobertura no CI para não perder novos endpoints | — | P2 |

**Situação em 2026-11-20:** Ondas 0, 1, 2 e 5 feitas (cobertura em `docs/COBERTURA.md`; a guarda `EndpointCoverageGuardTests` e a lista `tests/ControlId.Net.Tests/Cobertura/pendentes.txt` seguem com as Ondas 3 e 4). Faltam as Ondas 3 e 4.

## Regras para todas as ondas

- Cada método novo entra em `IAccessClient`, `AccessClient` e `PublicAPI.Unshipped.txt`. Mudar a interface quebra
  implementações de terceiros; aceitável na série alpha, mas registrar no CHANGELOG.
- Destrutivos (apagam dados, trocam chaves, reiniciam) recebem a sobrecarga com `DataLossConfirmation`, como
  `ResetToFactoryDefaultAsync` e `DeleteAllUserImagesAsync` já fazem.
- Segredos (`master_password`, `recovery_password`, `set_dual_sided_secret`, `osdp_scbk`, `osdp_set_secure_key`,
  `set_wifi_settings.psk`, chaves `hid_*`, `set_oem_code.license`) nunca vão para log, telemetria nem mensagem de
  exceção, e não entram no retry de leitura (ADR 0013).
- Endpoints restritos a produto ou firmware dizem isso no `<summary>`, por exemplo "Apenas iDFace Max V7.x.x".
- Cada endpoint novo ganha rota no `tools/ControlId.Net.Simulator` e teste de ponta a ponta, seguindo a Onda 3 da
  auditoria.
- Upload binário usa `PostBytesAsync` com `application/octet-stream`; download usa `GetBytesAsync`/`PostForBytesAsync`.

---

## Onda 0 — Correções em código existente (concluída em 2026-11-20)

### 0.1 (P1) DNS ignorado pelo equipamento
- `src/ControlId.Net/Access/Models/DeviceCommandModels.cs:53-57` serializa `dns_primary` e `dns_secondary`. O changelog
  de setembro/2026 corrigiu a documentação: os nomes são **`primary_dns`** e **`secondary_dns`**, e o equipamento ignora
  os antigos. Hoje `SetNetworkAsync` não altera o DNS, e não há erro nenhum.
- Fazer: trocar os `JsonPropertyName` (as propriedades C# continuam iguais), teste de serialização e nota no CHANGELOG
  como correção de bug.

### 0.2 (P2) Parâmetros recém-documentados em métodos existentes

| Método | Endpoint | O que falta |
|---|---|---|
| `SetNetworkAsync` (`NetworkSettings`) | `set_system_network` | `interface`, `dhcp_enabled`, `ten_mbps` |
| `Configure8021XAsync` | `configure_802_1X` | `eap_auth` (PEAP/TTLS/TLS), `uses_certificate`, `interface`. Hoje só PEAP com `inner_auth` |
| `ResetToFactoryDefaultAsync` | `reset_to_factory_default` | `keep_all_config` (apaga só dados de usuário), `keep_osdp_info` |
| `ExportAuditLogsAsync` (`AuditLogCategories`) | `export_audit_logs` | categorias `Boot` e `Push`: hoje há 7 flags e o equipamento tem 9. Atualizar `All` |
| `ExportAfdAsync` | `export_afd` | paginação `limit`/`offset` |
| `CaptureCameraAsync` | `save_screenshot` | `format_type` (`png`/`base64`) e `frame_type` `screen`/`display`. Avaliar um `CaptureScreenAsync` separado |
| `SetUserImageAsync`, `SetUserImagesAsync` | `user_set_image(_list)` | `bypass_quality_score` |
| `RemoteEnrollAsync` (resultado) | `remote_enroll` | campo de resposta `num_bits` (formato Wiegand inferido) |

- `EnrollFaceTemplatesAsync(rejectExistingFaces = false)` sempre envia `match=false`, mas o padrão do equipamento é
  `true` (a documentação estava errada e foi corrigida). O comportamento atual aceita faces duplicadas sem o chamador
  pedir. Decidir: mudar o padrão para `true` (mudança de comportamento, com CHANGELOG) ou manter e documentar. A
  recomendação é mudar, para seguir o equipamento.

### 0.3 (P2) Objetos e configuração

- `cards`: `wiegand_format_id`, `credential_type`. `groups`: `identification_mode` (1:N, 1:1, template no cartão,
  padrão do equipamento).
- Objetos novos: `wiegand_modes` (somente leitura, layout de bits e paridade) e `group_compatibility` (regra de
  autenticação de 2 pessoas). Criar os modelos com `[ControlIdObject]` e os métodos `Get...Async`.
- `DeviceSettings`: `hid_reader` ganha `card_process`, `custom_use_index`, `toc_crypt_mode`, `toc_crypt_key`,
  `toc_site_key`; `osdp` ganha `remote_validation_enabled`, `remote_validation_timeout_ms`; `identifier` ganha
  `two_person_auth_enabled`. `toc_crypt_key` e `toc_site_key` são segredos: aplicar a regra de log.

---

## Onda 1 — Callbacks Online (P2, 5 rotas) (concluída em 2026-11-20)

O equipamento chama essas rotas no servidor. Elas entram em `OnlineEvents` (`src/ControlId.Net/Access/Online/OnlineEvents.cs`)
e no mapeamento do `ControlId.Net.AspNetCore`, com o mesmo `RequireControlIdToken` das demais.

| Rota | Quando chega | Onde encaixar |
|---|---|---|
| `new_pin` | identificação por PIN | `IdentificationEndpoints` + `PinEvent` |
| `new_face_template` | identificação facial com template (`template`, `templateFF`) | `IdentificationEndpoints` + evento novo |
| `face_template_create` | cadastro remoto de face com `enroller.return_face_template=1` (V6 a V8); senão vem `face_create` | `EnrollEndpoints`, tipo `Face` com template no evento |
| `tag_create` | cadastro remoto de tag (só iDAccess) | `EnrollEndpoints` + `RemoteEnrollType.Tag` |
| `new_user_enrolled` | autocadastro facial no modo quiosque (V7/V8), traz imagem e template | evento novo fora dos dois grupos, ou um `ParseKioskEnrollment` |

Conferir em `eventos-de-identificacao-online.md` o formato de resposta que cada rota espera (`result`), porque
`new_pin` e `new_face_template` precisam devolver a autorização como as outras identificações.

---

## Onda 2 — Uso geral (P2, 31 endpoints) (concluída em 2026-11-20)

### 2.1 Biometria (3)
| Endpoint | Método proposto | Nota |
|---|---|---|
| `face_template_match` | `MatchFaceTemplateAsync` | 1:1 com `user_id` ou 1:N. `match_score` e `match_similarity` são saídas independentes |
| `face_template_count_distinct` | `CountFaceTemplatesAsync` | iDFace V6+ |
| `template_match` | `MatchFingerprintTemplatesAsync` | query `temp_num`/`sizeN` + corpo binário concatenado; validar a soma dos tamanhos antes de enviar |

### 2.2 Schema de objetos customizados (8)
| Endpoint | Método proposto |
|---|---|
| `object_metadata` | `GetObjectMetadataAsync` (`filter`, `fields`, `joins`) |
| `object_add` / `object_remove` | `CreateCustomTableAsync` / `DeleteCustomTablesAsync` |
| `object_add_field` / `object_update_field` / `object_remove_fields` | `AddCustomFieldAsync` / `UpdateCustomFieldAsync` / `RemoveCustomFieldsAsync` |
| `export_object` | `ExportObjectCsvAsync(object, columns)` |
| `export_custom_tables_metadata` | `ExportCustomTablesMetadataCsvAsync` |

Modelar `CustomFieldDefinition` (tipo, `constraint`, `unique`, `default_value`, `foreign_key {object, field}`).
Remover tabela ou campo apaga dados, então usa `DataLossConfirmation`.

### 2.3 Rede, NTP e diagnóstico (11)
| Endpoint | Método proposto |
|---|---|
| `get_ntp_server` / `set_ntp_server` / `get_ntp_server_status` | `GetNtpServersAsync` / `SetNtpServersAsync` / `GetNtpStatusAsync` |
| `check_port_availability` | `CheckPortAvailabilityAsync(initial, final)` |
| `get_vpn_ip` | `GetVpnIpAsync` (junto dos métodos de VPN existentes) |
| `get_ssl_certificate` | `GetSslCertificateAsync` (par de `UploadSslCertificateAsync`) |
| `get_802_1X_status` | `Get8021XStatusAsync(interface)` |
| `get_ac_log`, `get_openvpn_log`, `get_wpa_log`, `get_wpasupplicant_log` | `GetSystemLogAsync(DeviceLog kind)`: um método com enum, já que todos devolvem texto |

`get_wpa_log` existe só na V7 e `get_wpasupplicant_log` na iDAccess, V6 e V8. O enum documenta isso, e a biblioteca não
tenta adivinhar a versão do firmware.

### 2.4 Leitura de hardware e estado (7)
| Endpoint | Método proposto | Nota |
|---|---|---|
| `io_state` | `GetIoStateAsync` | GPIOs, relés e SecBoxes numa chamada (iDFace Max V7) |
| `is_identifying` | `IsIdentifyingAsync` | |
| `get_catra_info` | `GetTurnstileCountersAsync` | giros por sentido |
| `get_last_snapshot` | `GetLastSnapshotAsync` | `image_base64` → `byte[]` |
| `device_rfid_module` | `GetRfidModuleAsync` | `"HID"` em OMNIKEY |
| `get_hid_last_card` | `GetHidLastCardAsync` | `csn`, `pac`, `type`, `bits`, `custom` |
| `get_wiegand_card_read` | `GetLastWiegandReadAsync` | |

### 2.5 Senhas de sistema (2)
| Endpoint | Método proposto | Nota |
|---|---|---|
| `master_password` | `SetMasterPasswordAsync` / `GetMasterPasswordInfoAsync` | sem `password` devolve só `hash`/`salt` |
| `recovery_password` | `SetUpdateModePasswordAsync` / `RemoveUpdateModePasswordAsync` / `GetUpdateModePasswordStatusAsync` | sessão de administrador; iDFace Max V7 |

---

## Onda 3 — Específicos de produto (P3, 53 endpoints)

Cada grupo pode virar um PR. A ordem sugerida vai de mais clientes afetados para menos.

| Grupo | Endpoints | Disponibilidade | Nota de desenho |
|---|---|---|---|
| Wi-Fi (6) | `set_wifi_source`, `wifi_scan`, `set_wifi_settings`, `get_wifi_data`, `forget_wifi_connected_network`, `check_ip_conflict` | iDFace / iDFace Max V8 | `psk` é segredo; `get_wifi_data` devolve `psk`, então mascarar no `ToString` do modelo |
| Certificados EAP (6) | `eap_802_1X_certificate_change(_wifi)`, `eap_tls_802_1X_certificate_change`, `eap_tls_802_1X_private_key_change`, `eap_tls_802_1X_private_key_persist`, `get_eap_tls_csr_file` | — | completa a Onda 0.2 do 802.1X (TLS); chave privada é segredo |
| Calibração da câmera (3) | `remote_camera_calibration`, `cancel_remote_camera_calibration`, `camera_calibration_status` | iDFace V6 | a chamada retorna antes do fim; oferecer `WaitCameraCalibrationAsync` com polling e `CancellationToken` |
| Slideshow (5) | `send_slideshow_images`, `get_slideshow_images_list`, `get_slideshow_image`, `delete_slideshow_image`, `remove_slideshow_images` | — | JPG; seguir o padrão de `SetLogoAsync` |
| Ícones do quiosque (4) | `set_kiosk_icon`, `get_kiosk_icon`, `delete_kiosk_icon`, `get_kiosk_icons_list` | modo quiosque | png/jpg/svg; respeitar `max_files` |
| Interfonia (4) | `set_phone_icon`, `get_phone_icon`, `remove_phone_icon`, `make_sip_conference` | iDFace SIP | junto de `MakeSipCallAsync` |
| Logo do streaming (3) | `set_streaming_logo`, `get_streaming_logo`, `remove_streaming_logo` | iDFace | PNG |
| SecBox (9) | `device_has_secbox`, `secbox_is_active`, `secbox_serial_number`, `update_secbox_firmware(_status/_version)`, `change_crypto_key_default`, `change_crypto_key_random`, `reset_crypto_key` | — | firmware com polling de `isFinished`/`bitsWritten`; troca de chave usa `DataLossConfirmation` |
| Dual sided (2) | `set_dual_sided_secret`, `get_dual_sided_status` | iDAccess | admin; o segredo nunca volta |
| OSDP (7) | `get_osdp_installation_mode`, `osdp_get_connection_status`, `osdp_get_output_capabilities`, `osdp_refresh`, `osdp_scbk`, `osdp_set_secure_key`, `osdp_set_secure_type` | — | completa `SetOsdpInstallationModeAsync`; `scbk` é segredo. A doc escreve `nun_outputs` (sic): conferir no equipamento |
| Licenças e modo (3) | `upgrade_twenty_thousand_face_templates`, `upgrade_ten_thousand_templates`, `set_pro_operation_mode_and_reboot` | iDFace Max / iDFlex / iDAccess Nano | `FaceLicense.Pro20k` falta no enum; o upgrade de digitais é método à parte; o último reinicia o equipamento |
| Redirecionamento (1) | `request_redirect` | — | `RedirectAsync(command, deviceId \| address+login+password)`; a credencial do destino vai na query string, então documentar o risco |

---

## Onda 4 — Sensíveis, com opt-in (P3, 11 endpoints)

Um erro aqui pode deixar leitores ou equipamentos inutilizáveis em campo. A proposta é um cliente separado, por exemplo
`AccessClient.Provisioning` ou um `IAccessProvisioningClient` no DI, que só existe quando o integrador pede.

- **Chaves do leitor HID (7, iDFace Max com OMNIKEY Reader Core):** `hid_inject_default_kek`, `hid_inject_custom_kek`,
  `hid_inject_media_key`, `hid_get_key_info`, `hid_set_oid`, `hid_get_oid`, `hid_reset_config`. Modos plain e wrapped
  (KWP), referências em hexadecimal. `hid_set_oid` infere o tipo pelo tipo JSON do valor, o que pede tipagem cuidadosa
  (`bool`/`int`/`byte[]`). Testar com um leitor real antes de publicar.
- **OEM (4):** `get_oem_code`, `set_oem_code`, `get_oem_downgrade_approved_versions`, `set_oem_approved_fw`. Exigem
  licença e só interessam a fabricantes OEM.

## Não cobrir

| Endpoint | Motivo |
|---|---|
| `dual_sided_query` | rota entre dois equipamentos, assinada com o segredo compartilhado; a doc diz que não é para integrações |
| `tests.fcgi` | coleta de testes de fábrica, só na coleção Postman, sem página na documentação |

Os dois continuam acessíveis por `SendRawAsync`. Registrar a decisão num ADR curto, junto com a Onda 4.

---

## Onda 5 — Guarda de cobertura (P2) (concluída em 2026-11-20)

- Copiar `docs/controlid-api/endpoints.json` para um teste (ou ler direto do repositório) que falha quando um endpoint
  documentado não está nem em `src/` nem numa lista explícita `pendentes.txt`/`nao-cobrir.txt`. A lista de pendentes
  começa com os itens deste plano e encolhe a cada onda.
- Rodar o dump numa action mensal (`workflow_dispatch` + `schedule`) que abre PR quando a documentação mudar. O
  changelog da Control iD é aditivo e com selo NEW!, então o diff do PR já mostra o que entrou.

## Ordem sugerida

1. **0.1** sozinho, já (bug silencioso, uma linha mais o teste).
2. **Onda 5** antes das demais, para que cada onda seguinte prove que reduziu a lista de pendentes.
3. **0.2/0.3** e **Onda 1** no mesmo ciclo: mexem em modelos e eventos que já existem.
4. **Onda 2** em quatro PRs (2.1+2.4, 2.2, 2.3, 2.5).
5. **Onda 3** por grupo, conforme demanda ou equipamento disponível; **Onda 4** só com hardware para teste.
