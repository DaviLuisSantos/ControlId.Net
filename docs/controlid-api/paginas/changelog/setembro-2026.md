<!-- fonte: https://www.controlid.com.br/docs/access-api-pt/changelog/setembro-2026/ -->

# Atualizações — setembro de 2026

Esta página lista tudo o que foi adicionado a esta documentação na versão de setembro de 2026. O
conteúdo novo é sinalizado ao longo da documentação com um selo
NEW! na página ou seção afetada, e as linhas de tabela e parâmetros
recém-adicionados aparecem destacados em amarelo.

Todas as entradas abaixo são aditivas: nenhum endpoint ou parâmetro foi removido.

## Novos endpoints

### Reconhecimento facial

| Endpoint | Disponibilidade | Página |
| --- | --- | --- |
| `POST /face_template_extract_list.fcgi` | A partir do iDFace V6.x.x | [Operações de Template Facial](../reconhecimento-facial/operacoes-template-facial.md#extrair-templates-faciais-em-lote) |
| `POST /face_template_enroll.fcgi` | A partir do iDFace V6.x.x | [Operações de Template Facial](../reconhecimento-facial/operacoes-template-facial.md#cadastrar-template-facial) |
| `POST /face_template_destroy.fcgi` | A partir do iDFace V6.x.x | [Operações de Template Facial](../reconhecimento-facial/operacoes-template-facial.md#remover-template-facial) |
| `GET /face_template_count_distinct.fcgi` | A partir do iDFace V6.x.x | [Operações de Template Facial](../reconhecimento-facial/operacoes-template-facial.md#contar-templates-cadastrados) |
| `POST /remote_camera_calibration.fcgi` | Apenas iDFace V6.x.x | [Calibração remota da câmera](../reconhecimento-facial/calibracao-da-camera.md#post-remote_camera_calibrationfcgi) |
| `POST /cancel_remote_camera_calibration.fcgi` | Apenas iDFace V6.x.x | [Calibração remota da câmera](../reconhecimento-facial/calibracao-da-camera.md#post-cancel_remote_camera_calibrationfcgi) |
| `POST /camera_calibration_status.fcgi` | Apenas iDFace V6.x.x | [Calibração remota da câmera](../reconhecimento-facial/calibracao-da-camera.md#post-camera_calibration_statusfcgi) |

### Wi-Fi

Disponível apenas em **equipamentos da linha iDFace e iDFace Max com firmware V8.x.x**. O suporte
para **equipamentos da linha iDFace Max com firmware V7.x.x** por meio de um dongle Wi-Fi USB está
previsto.

| Endpoint | Finalidade | Página |
| --- | --- | --- |
| `POST /set_wifi_source.fcgi` | Seleciona o adaptador Wi-Fi (interno, USB ou desabilitado) | [Configurações de Rede](../sistema/configuracoes-de-rede.md#wi-fi) |
| `POST /wifi_scan.fcgi` | Busca pontos de acesso ao alcance | [Configurações de Rede](../sistema/configuracoes-de-rede.md#wi-fi) |
| `POST /set_wifi_settings.fcgi` | Conecta a uma rede | [Configurações de Rede](../sistema/configuracoes-de-rede.md#wi-fi) |
| `POST /get_wifi_data.fcgi` | Retorna a configuração sem fio e o estado da conexão | [Configurações de Rede](../sistema/configuracoes-de-rede.md#wi-fi) |
| `POST /forget_wifi_connected_network.fcgi` | Desconecta e apaga as credenciais armazenadas | [Configurações de Rede](../sistema/configuracoes-de-rede.md#wi-fi) |
| `POST /check_ip_conflict.fcgi` | Verifica conflito de IP entre as interfaces cabeada e sem fio | [Configurações de Rede](../sistema/configuracoes-de-rede.md#wi-fi) |
| `POST /eap_802_1X_certificate_change_wifi.fcgi` | Envia o certificado CA EAP-PEAP para a interface sem fio | [Certificados EAP](../sistema/certificados-eap.md) |

### Gerenciamento de chaves do leitor HID

Disponível apenas em **equipamentos da linha iDFace Max com módulo OMNIKEY Reader Core**. Os sete endpoints
estão documentados na nova página
[Gerenciamento de Chaves do Leitor HID](../hardware/gerenciamento-de-chaves-hid.md).

| Endpoint | Finalidade |
| --- | --- |
| `POST /hid_inject_default_kek.fcgi` | Substitui a Key Encryption Key de fábrica do leitor |
| `POST /hid_inject_custom_kek.fcgi` | Injeta uma KEK intermediária em uma referência de chave |
| `POST /hid_inject_media_key.fcgi` | Injeta a chave final do cartão (por exemplo, uma chave de setor MIFARE) |
| `POST /hid_get_key_info.fcgi` | Informa o tipo e o identificador da chave em uma referência |
| `POST /hid_set_oid.fcgi` | Escreve um parâmetro de configuração do OMNIKEY por OID |
| `POST /hid_get_oid.fcgi` | Lê um parâmetro de configuração do OMNIKEY por OID |
| `POST /hid_reset_config.fcgi` | Redefine a configuração do leitor, ou restaura um arquivo `config.orco` |

### Logs

| Endpoint | Disponibilidade | Página |
| --- | --- | --- |
| `GET /get_openvpn_log.fcgi` | Todas as versões | [Ferramentas de Diagnóstico](../configuracoes/ferramentas-diagnostico.md#obter-log-do-openvpn) |

### Sistema

| Endpoint | Disponibilidade | Página |
| --- | --- | --- |
| `POST /recovery_password.fcgi` | Apenas iDFace Max V7.x.x | [Senha do modo de update](../sistema/senha-do-modo-de-update.md#post-recovery_passwordfcgi) |
| `POST /get_oem_downgrade_approved_versions.fcgi` | Apenas iDFace Max V7.x.x | [Firmware aprovado pelo OEM](../sistema/firmware-aprovado-oem.md#post-get_oem_downgrade_approved_versionsfcgi) |
| `POST /set_oem_approved_fw.fcgi` | Apenas iDFace Max V7.x.x | [Firmware aprovado pelo OEM](../sistema/firmware-aprovado-oem.md#post-set_oem_approved_fwfcgi) |
| `POST /set_dual_sided_secret.fcgi` | Apenas linha iDAccess | [Dual sided do SecBox](../sistema/dual-sided-secbox.md#post-set_dual_sided_secretfcgi) |
| `POST /get_dual_sided_status.fcgi` | Apenas linha iDAccess | [Dual sided do SecBox](../sistema/dual-sided-secbox.md#post-get_dual_sided_statusfcgi) |
| `POST /dual_sided_query.fcgi` | Apenas linha iDAccess | [Dual sided do SecBox](../sistema/dual-sided-secbox.md#post-dual_sided_queryfcgi) |

### Hardware

| Endpoint | Disponibilidade | Página |
| --- | --- | --- |
| `POST /io_state.fcgi` | Apenas iDFace Max V7.x.x | [Diagnóstico de entradas e saídas](../hardware/diagnostico-de-entradas-e-saidas.md#post-io_statefcgi) |

## Novas páginas

| Página | Conteúdo |
| --- | --- |
| [Gerenciamento de Chaves do Leitor HID](../hardware/gerenciamento-de-chaves-hid.md) | A hierarquia de chaves do OMNIKEY, os modos de injeção plain e wrapped, e os sete endpoints de chave/OID |
| [Senha do modo de update](../sistema/senha-do-modo-de-update.md) | Definir, remover e consultar a senha do modo de update |
| [Firmware aprovado pelo OEM](../sistema/firmware-aprovado-oem.md) | Versões aprovadas para downgrade OEM e definição da versão de firmware aprovada |
| [Diagnóstico de entradas e saídas](../hardware/diagnostico-de-entradas-e-saidas.md) | Estado de todos os GPIOs, relés e SecBoxes em uma única chamada |
| [Dual sided do SecBox](../sistema/dual-sided-secbox.md) | Configuração do dual sided, segredo compartilhado, estado e a rota de consulta entre os pares |
| [Calibração remota da câmera](../reconhecimento-facial/calibracao-da-camera.md) | Iniciar, cancelar e acompanhar a calibração da câmera |

## Novos parâmetros de configuração

Todos em [Parâmetros de Configuração](../configuracoes/parametros-configuracao.md).

### `hid_reader`

| Parâmetro | Descrição |
| --- | --- |
| **card_process** | Como o leitor processa o dado do cartão: `PACS_ONLY`, `CSN_ONLY`, `CSN_MIFARE`, `CUSTOM_ONLY` ou `CUSTOM_PACS` |
| **custom_use_index** | Qual índice da lista de dados customizados do leitor é tratado como o dado válido do cartão |
| **toc_crypt_mode** | Modo de criptografia Template on Card (ToC) |
| **toc_crypt_key** | Chave de criptografia do equipamento usada no modo ToC |
| **toc_site_key** | Site key usada no modo ToC |

### `OSDP`

| Parâmetro | Descrição |
| --- | --- |
| **remote_validation_enabled** | Habilita o fluxo de validação remota pelo protocolo OSDP nativo, disponível apenas quando **out_mode** é `ANY_CARD` |
| **remote_validation_timeout_ms** | Janela de tempo para a resposta da validação remota. Padrão: `5000` |

### `identifier`

| Parâmetro | Descrição |
| --- | --- |
| **two_person_auth_enabled** | Habilita globalmente a regra de autenticação de 2 pessoas |

## Novos campos de objetos

Todos em [Lista de Objetos](../objetos/lista-de-objetos.md).

| Objeto | Campo | Descrição |
| --- | --- | --- |
| *cards* | **wiegand_format_id** | Formato Wiegand do cartão, referenciando o objeto *wiegand_modes* |
| *cards* | **credential_type** | Tipo de credencial: cartão, credencial facial ou QR Code |
| *groups* | **identification_mode** | Modo de identificação usado pelos usuários do grupo: 1:N, 1:1, Template no Cartão, ou o padrão do equipamento |

## Novos objetos

| Objeto | Descrição |
| --- | --- |
| [*wiegand_modes*](../objetos/lista-de-objetos.md#wiegand_modes) | Tabela de consulta (somente leitura) dos formatos Wiegand suportados pelo equipamento, incluindo layout de bits e configuração de paridade |
| [*group_compatibility*](../objetos/lista-de-objetos.md#group_compatibility) | Lista, por grupo, dos grupos aceitos como "segunda pessoa" na regra de autenticação de 2 pessoas |

## Novos campos de resposta

| Endpoint | Campo | Descrição |
| --- | --- | --- |
| `GET /get_hid_last_card.fcgi` | **custom** | String hexadecimal do bloco de dados customizados do último cartão lido |
| `POST /remote_enroll.fcgi` | **num_bits** | Número de bits lidos do cartão durante o cadastro remoto, usado para inferir o formato Wiegand |

## Parâmetros recém-documentados

Parâmetros que a API já aceitava, mas que não constavam na documentação.

| Endpoint | Parâmetros |
| --- | --- |
| `POST /configure_802_1X.fcgi` | **eap_auth** (PEAP, TTLS ou TLS), **uses_certificate**, **interface** |
| `GET /get_802_1X_status.fcgi` | **interface** |
| `POST /set_system_network.fcgi` | **interface**, **dhcp_enabled**, **ten_mbps** |
| `POST /set_network_interlock.fcgi` | **interlock_enabled**, **api_bypass_enabled**, **rex_bypass_enabled** |
| `POST /export_audit_logs.fcgi` | **boot**, **push** — o relatório cobre 9 categorias de log de eventos, e não 7 |
| `POST /export_afd.fcgi` | **limit**, **offset** |
| `POST /reset_to_factory_default.fcgi` | **keep_all_config** |
| `POST /save_screenshot.fcgi` | **format_type** (`png` ou `base64`); **frame_type** também aceita `screen` e `display` |
| `POST /user_set_image.fcgi` | **bypass_quality_score** |
| `POST /user_set_image_list.fcgi` | **bypass_quality_score** |

## Correções

| Onde | Correção |
| --- | --- |
| `POST /set_system_network.fcgi` | Os parâmetros de DNS se chamam **primary_dns** e **secondary_dns**. Estavam documentados como *dns_primary* e *dns_secondary*, nomes que o equipamento ignora. |
| `POST /system_information.fcgi` | Mesma correção nos campos de resposta: **primary_dns** e **secondary_dns**. |
| `POST /face_template_enroll.fcgi` | O parâmetro **match** tem padrão `true`, e não `false`. Faces duplicadas são recusadas, a menos que ele seja explicitamente definido como `false`. |
| [Operações de Template Facial](../reconhecimento-facial/operacoes-template-facial.md) | Disponibilidade corrigida para **equipamentos da linha iDFace com firmware V6.x.x** em diante (antes constava V7.x.x). |
| *sec_boxs* **out_mode** | Descrição dos valores atualizada: `"ID"` para transmissão do user_id, `"CARD"` (padrão) para cartões autorizados, `"RELAY_CARD"` para qualquer cartão. |
| `GET /device_rfid_module.fcgi` | Esclarecido que o campo **module** retorna `"HID"` em equipamentos construídos em torno do OMNIKEY Reader Core. |

## Definições esclarecidas

| Onde | Esclarecimento |
| --- | --- |
| *cards* **value** | Equipamentos com o módulo OMNIKEY Reader Core (iDFace Max HID) combinam o código de área e o número do cartão usando a largura em bits do formato Wiegand configurado (`2^N`), e não a divisão fixa por `2^32` usada pelos leitores Mifare/ASK. Inclui tabela de larguras por formato. |
| `OSDP` **out_mode** | Valores documentados por nome — `ID`, `AUTH_CARD` (o novo padrão de fábrica), `ANY_CARD` (Passthrough) e `USER_CARD` (legado) — com o comportamento de cada um. |
| `POST /face_template_match.fcgi` | **match_score** e **match_similarity** são saídas independentes do verificador, e não um par bruto/normalizado; ambos os limiares devem ser ajustados em conjunto. |
| `POST /hid_get_key_info.fcgi` | **key_id** só é preenchido para chaves injetadas via `hid_inject_default_kek`; os outros endpoints de injeção não conseguem defini-lo. |
