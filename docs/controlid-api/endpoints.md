# Catálogo de endpoints da Access API

204 endpoints `*.fcgi` distintos, extraídos das páginas do site e da coleção Postman.

| Endpoint | Métodos (Postman) | Requisições no Postman | Páginas do site |
|---|---|---|---|
| `alarm_status.fcgi` | POST | Alarme / Desativar alarme<br>Alarme / Obter status do alarme | [alarme/introducao-ao-alarme](paginas/alarme/introducao-ao-alarme.md) |
| `buzzer_buzz.fcgi` | POST | Ações / Acionamento remoto buzzer | [acoes/acionamento_remoto_buzzer](paginas/acoes/acionamento_remoto_buzzer.md) |
| `camera_calibration_status.fcgi` | — | — | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[reconhecimento-facial/calibracao-da-camera](paginas/reconhecimento-facial/calibracao-da-camera.md) |
| `cancel_remote_camera_calibration.fcgi` | — | — | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[reconhecimento-facial/calibracao-da-camera](paginas/reconhecimento-facial/calibracao-da-camera.md) |
| `cancel_remote_enroll.fcgi` | POST | Registro / Cancelar registro remoto | [acoes/cadastro-remoto-biometria-facial-cartao](paginas/acoes/cadastro-remoto-biometria-facial-cartao.md) |
| `card_create.fcgi` | — | — | [acoes/cadastro-remoto-biometria-facial-cartao](paginas/acoes/cadastro-remoto-biometria-facial-cartao.md) |
| `change_crypto_key_default.fcgi` | — | — | [sistema/alterar-chave-de-seguranca-secbox](paginas/sistema/alterar-chave-de-seguranca-secbox.md) |
| `change_crypto_key_random.fcgi` | — | — | [sistema/alterar-chave-de-seguranca-secbox](paginas/sistema/alterar-chave-de-seguranca-secbox.md) |
| `change_idcloud_code.fcgi` | GET | Push / Alterar código de verificação | [modo-push/idcloud](paginas/modo-push/idcloud.md) |
| `change_login.fcgi` | POST | Gerenciamento de sessão / Alterar login e senha | [gerenciamento-secao/alterar-usuario-e-senha-de-login](paginas/gerenciamento-secao/alterar-usuario-e-senha-de-login.md) |
| `check_ip_conflict.fcgi` | — | — | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[sistema/configuracoes-de-rede](paginas/sistema/configuracoes-de-rede.md) |
| `check_port_availability.fcgi` | — | — | [configuracoes/verificar-disponibilidade-de-porta](paginas/configuracoes/verificar-disponibilidade-de-porta.md) |
| `configure_802_1X.fcgi` | POST | Sistema / Habilitar 802.1X | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[sistema/configuracoes-de-rede](paginas/sistema/configuracoes-de-rede.md) |
| `connection_test.fcgi` | — | — | [configuracoes/ferramentas-diagnostico](paginas/configuracoes/ferramentas-diagnostico.md) |
| `create_objects.fcgi` | POST | Objetos / Regras de acesso / Criar regras de acesso<br>Objetos / Regras de acesso time zones / Criar regra de acesso time zones<br>Objetos / Cartões / Criar cartões<br>Objetos / QR Codes / Criar qrcodes<br>Objetos / Tabelas customizadas / Criar tabelas customizadas<br>Objetos / Colunas customizadas / Criar colunas customizadas<br>… +14 | [modos-de-operacao/configurar-modo-online](paginas/modos-de-operacao/configurar-modo-online.md)<br>[objetos/carregar-objetos](paginas/objetos/carregar-objetos.md)<br>[objetos/criar-objetos](paginas/objetos/criar-objetos.md)<br>[particularidade-dos-produtos/qr-code](paginas/particularidade-dos-produtos/qr-code.md)<br>[reconhecimento-facial/cadastro-facial](paginas/reconhecimento-facial/cadastro-facial.md) |
| `create_or_modify_objects.fcgi` | POST | Objetos / Usuários / Criar ou modificar usuários | [objetos/criar-ou-modificar-objetos](paginas/objetos/criar-ou-modificar-objetos.md) |
| `delete_admins.fcgi` | POST | Sistema / Remover todos administradores | [sistema/remover-administradores](paginas/sistema/remover-administradores.md) |
| `delete_kiosk_icon.fcgi` | — | — | [particularidade-dos-produtos/modo-quiosque](paginas/particularidade-dos-produtos/modo-quiosque.md) |
| `delete_slideshow_image.fcgi` | — | — | [fotos-e-logotipo/gerenciar-slideshow](paginas/fotos-e-logotipo/gerenciar-slideshow.md)<br>[particularidade-dos-produtos/slideshow](paginas/particularidade-dos-produtos/slideshow.md) |
| `destroy_objects.fcgi` | POST | Objetos / Cartões / Deletar cartões<br>Objetos / QR Codes / Deletar qrcodes<br>Objetos / Grupos / Deletar grupos<br>Objetos / Liberações agendadas / Deletar liberações agendadas<br>Objetos / Templates / Deletar templates<br>Objetos / Grupos de usuários / Deletar grupo de usuários<br>… +2 | [objetos/destruir-objetos](paginas/objetos/destruir-objetos.md) |
| `device_has_secbox.fcgi` | — | — | [sistema/secbox](paginas/sistema/secbox.md) |
| `device_is_alive.fcgi` | — | — | [modos-de-operacao/configurar-modo-online](paginas/modos-de-operacao/configurar-modo-online.md)<br>[modos-de-operacao/eventos-de-identificacao-online](paginas/modos-de-operacao/eventos-de-identificacao-online.md) |
| `device_rfid_module.fcgi` | — | — | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[hardware/modulo-rfid](paginas/hardware/modulo-rfid.md) |
| `doors_state.fcgi` | POST | Ações / Obter estado atual das portas | [acoes/abertura-remota-porta-e-catraca](paginas/acoes/abertura-remota-porta-e-catraca.md) |
| `dual_sided_query.fcgi` | — | — | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[sistema/dual-sided-secbox](paginas/sistema/dual-sided-secbox.md) |
| `eap_802_1X_certificate_change.fcgi` | — | — | [sistema/certificados-eap](paginas/sistema/certificados-eap.md) |
| `eap_802_1X_certificate_change_wifi.fcgi` | — | — | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[sistema/certificados-eap](paginas/sistema/certificados-eap.md) |
| `eap_tls_802_1X_certificate_change.fcgi` | — | — | [sistema/certificados-eap](paginas/sistema/certificados-eap.md) |
| `eap_tls_802_1X_private_key_change.fcgi` | — | — | [sistema/certificados-eap](paginas/sistema/certificados-eap.md) |
| `eap_tls_802_1X_private_key_persist.fcgi` | — | — | [sistema/certificados-eap](paginas/sistema/certificados-eap.md) |
| `enroller_state.fcgi` | POST | Registro / Obter estado atual do registro | [acoes/estado-do-enrollment](paginas/acoes/estado-do-enrollment.md) |
| `execute_actions.fcgi` | POST | Ações / Abrir secbox (padrão)<br>Ações / Abrir secbox (3 segundos)<br>Ações / Abrir porta 1 (padrão)<br>Ações / Abrir porta 1 (3 segundos)<br>Ações / Abrir todas portas (padrão)<br>Ações / Abrir todas portas (3 segundos)<br>… +10 | [acoes/abertura-remota-porta-e-catraca](paginas/acoes/abertura-remota-porta-e-catraca.md)<br>[primeiros-passos/exemplos](paginas/primeiros-passos/exemplos.md) |
| `export_afd.fcgi` | POST | Objetos / Relatórios / Relatório AFD<br>Objetos / Relatórios / Relatório AFD com formato 671<br>Objetos / Relatórios / Relatório AFD com filtro de data inicial<br>Objetos / Relatórios / Relatório AFD com filtro NSR | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[objetos/exportar-relatorio](paginas/objetos/exportar-relatorio.md) |
| `export_audit_logs.fcgi` | POST | Sistema / Obter relatório de logs de auditoria para todos os filtros | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[objetos/exportar-relatorio](paginas/objetos/exportar-relatorio.md) |
| `export_custom_tables_metadata.fcgi` | — | — | [objetos/exportar-relatorio](paginas/objetos/exportar-relatorio.md) |
| `export_object.fcgi` | — | — | [objetos/exportar-relatorio](paginas/objetos/exportar-relatorio.md) |
| `export_objects.fcgi` | POST | Sistema / Exportar objetos | [objetos/exportar-relatorio](paginas/objetos/exportar-relatorio.md)<br>[sistema/sincronizacao](paginas/sistema/sincronizacao.md) |
| `face_create.fcgi` | — | — | [acoes/cadastro-remoto-biometria-facial-cartao](paginas/acoes/cadastro-remoto-biometria-facial-cartao.md)<br>[modos-de-operacao/eventos-de-identificacao-online](paginas/modos-de-operacao/eventos-de-identificacao-online.md) |
| `face_template_count_distinct.fcgi` | — | — | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[reconhecimento-facial/operacoes-template-facial](paginas/reconhecimento-facial/operacoes-template-facial.md) |
| `face_template_create.fcgi` | — | — | [modos-de-operacao/eventos-de-identificacao-online](paginas/modos-de-operacao/eventos-de-identificacao-online.md) |
| `face_template_destroy.fcgi` | — | — | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[reconhecimento-facial/operacoes-template-facial](paginas/reconhecimento-facial/operacoes-template-facial.md) |
| `face_template_enroll.fcgi` | — | — | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[reconhecimento-facial/operacoes-template-facial](paginas/reconhecimento-facial/operacoes-template-facial.md) |
| `face_template_extract.fcgi` | — | — | [reconhecimento-facial/operacoes-template-facial](paginas/reconhecimento-facial/operacoes-template-facial.md) |
| `face_template_extract_list.fcgi` | — | — | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[reconhecimento-facial/operacoes-template-facial](paginas/reconhecimento-facial/operacoes-template-facial.md) |
| `face_template_match.fcgi` | — | — | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[reconhecimento-facial/operacoes-template-facial](paginas/reconhecimento-facial/operacoes-template-facial.md) |
| `finalize_sip_call.fcgi` | POST | Interfonia SIP / Finalizar chamada SIP | [particularidade-dos-produtos/interfonia-sip-idface](paginas/particularidade-dos-produtos/interfonia-sip-idface.md) |
| `fingerprint_create.fcgi` | — | — | [acoes/cadastro-remoto-biometria-facial-cartao](paginas/acoes/cadastro-remoto-biometria-facial-cartao.md)<br>[modos-de-operacao/eventos-de-identificacao-online](paginas/modos-de-operacao/eventos-de-identificacao-online.md) |
| `forget_wifi_connected_network.fcgi` | — | — | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[sistema/configuracoes-de-rede](paginas/sistema/configuracoes-de-rede.md) |
| `get_802_1X_status.fcgi` | — | — | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[sistema/configuracoes-de-rede](paginas/sistema/configuracoes-de-rede.md) |
| `get_ac_log.fcgi` | POST | Sistema / Obter logs de sistema | [configuracoes/ferramentas-diagnostico](paginas/configuracoes/ferramentas-diagnostico.md) |
| `get_audio_access_message.fcgi` | POST | Configurações / Download de mensagem sonora customizada | [particularidade-dos-produtos/mensagens-sonoras-acesso-idface](paginas/particularidade-dos-produtos/mensagens-sonoras-acesso-idface.md) |
| `get_catra_info.fcgi` | GET | Cadastro facial / Obter informações da catraca | [reconhecimento-facial/informacoes-catraca](paginas/reconhecimento-facial/informacoes-catraca.md) |
| `get_configuration.fcgi` | POST | Configurações / Buscar todas configurações (iDAccess/iDFit)<br>Configurações / Buscar todas configurações (iDBlock)<br>Configurações / Buscar todas configurações (iDBox)<br>Configurações / Buscar todas configurações (iDFlex)<br>Configurações / Visualizar configurações do módulo mifare | [configuracoes/obter-configuracoes](paginas/configuracoes/obter-configuracoes.md) |
| `get_dual_sided_status.fcgi` | — | — | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[sistema/dual-sided-secbox](paginas/sistema/dual-sided-secbox.md) |
| `get_eap_tls_csr_file.fcgi` | — | — | [sistema/certificados-eap](paginas/sistema/certificados-eap.md) |
| `get_hid_last_card.fcgi` | — | — | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[hardware/modulo-rfid](paginas/hardware/modulo-rfid.md) |
| `get_kiosk_icon.fcgi` | — | — | [particularidade-dos-produtos/modo-quiosque](paginas/particularidade-dos-produtos/modo-quiosque.md) |
| `get_kiosk_icons_list.fcgi` | — | — | [particularidade-dos-produtos/modo-quiosque](paginas/particularidade-dos-produtos/modo-quiosque.md) |
| `get_last_snapshot.fcgi` | — | — | [reconhecimento-facial/captura-de-camera](paginas/reconhecimento-facial/captura-de-camera.md) |
| `get_ntp_server.fcgi` | — | — | [configuracoes/servidor-ntp](paginas/configuracoes/servidor-ntp.md) |
| `get_ntp_server_status.fcgi` | — | — | [configuracoes/servidor-ntp](paginas/configuracoes/servidor-ntp.md) |
| `get_oem_code.fcgi` | — | — | [sistema/obter-informacoes-do-sistema](paginas/sistema/obter-informacoes-do-sistema.md) |
| `get_oem_downgrade_approved_versions.fcgi` | — | — | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[sistema/firmware-aprovado-oem](paginas/sistema/firmware-aprovado-oem.md) |
| `get_openvpn_log.fcgi` | — | — | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[configuracoes/ferramentas-diagnostico](paginas/configuracoes/ferramentas-diagnostico.md) |
| `get_osdp_installation_mode.fcgi` | POST | Configurações / Status do Modo de Instalação | [sistema/configuracoes-de-rede](paginas/sistema/configuracoes-de-rede.md) |
| `get_phone_icon.fcgi` | — | — | [particularidade-dos-produtos/interfonia-sip-idface](paginas/particularidade-dos-produtos/interfonia-sip-idface.md) |
| `get_pjsip_audio_message.fcgi` | POST | Interfonia SIP / Download de som de chamada personalizado | [particularidade-dos-produtos/interfonia-sip-idface](paginas/particularidade-dos-produtos/interfonia-sip-idface.md) |
| `get_sip_status.fcgi` | POST | Interfonia SIP / Verificar status da chamada SIP | [particularidade-dos-produtos/interfonia-sip-idface](paginas/particularidade-dos-produtos/interfonia-sip-idface.md) |
| `get_slideshow_image.fcgi` | — | — | [fotos-e-logotipo/gerenciar-slideshow](paginas/fotos-e-logotipo/gerenciar-slideshow.md)<br>[particularidade-dos-produtos/slideshow](paginas/particularidade-dos-produtos/slideshow.md) |
| `get_slideshow_images_list.fcgi` | — | — | [fotos-e-logotipo/gerenciar-slideshow](paginas/fotos-e-logotipo/gerenciar-slideshow.md)<br>[particularidade-dos-produtos/slideshow](paginas/particularidade-dos-produtos/slideshow.md) |
| `get_ssl_certificate.fcgi` | — | — | [sistema/configuracoes-de-rede](paginas/sistema/configuracoes-de-rede.md) |
| `get_streaming_logo.fcgi` | — | — | [particularidade-dos-produtos/streaming-idface](paginas/particularidade-dos-produtos/streaming-idface.md) |
| `get_vpn_file.fcgi` | POST | Sistema / Receber arquivo de Exemplo OpenVPN | [sistema/configuracoes-de-rede](paginas/sistema/configuracoes-de-rede.md) |
| `get_vpn_information.fcgi` | POST | Sistema / Recebe as configurações de OpenVPN | [sistema/configuracoes-de-rede](paginas/sistema/configuracoes-de-rede.md) |
| `get_vpn_ip.fcgi` | — | — | [sistema/configuracoes-de-rede](paginas/sistema/configuracoes-de-rede.md) |
| `get_vpn_status.fcgi` | POST | Sistema / Recebe Status da conexão OpenVPN | [sistema/configuracoes-de-rede](paginas/sistema/configuracoes-de-rede.md) |
| `get_wiegand_card_read.fcgi` | — | — | [hardware/modulo-rfid](paginas/hardware/modulo-rfid.md) |
| `get_wifi_data.fcgi` | — | — | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[sistema/configuracoes-de-rede](paginas/sistema/configuracoes-de-rede.md) |
| `get_wpa_log.fcgi` | — | — | [configuracoes/ferramentas-diagnostico](paginas/configuracoes/ferramentas-diagnostico.md) |
| `get_wpasupplicant_log.fcgi` | — | — | [configuracoes/ferramentas-diagnostico](paginas/configuracoes/ferramentas-diagnostico.md) |
| `gpio_state.fcgi` | POST | Hardware / Buscar estado do GPIO | [hardware/ler-estado-de-gpio](paginas/hardware/ler-estado-de-gpio.md) |
| `has_audio_access_messages.fcgi` | POST | Configurações / Verificar áudios customizados de acesso presentes no dispositivo<br>Interfonia SIP / Verificar se existe som de chamada personalizado no dispositivo | [particularidade-dos-produtos/mensagens-sonoras-acesso-idface](paginas/particularidade-dos-produtos/mensagens-sonoras-acesso-idface.md) |
| `has_pjsip_audio_message.fcgi` | — | — | [particularidade-dos-produtos/interfonia-sip-idface](paginas/particularidade-dos-produtos/interfonia-sip-idface.md) |
| `has_vpn_file.fcgi` | POST | Sistema / Recebe a confirmação se já possui um arquivo de configuração | [sistema/configuracoes-de-rede](paginas/sistema/configuracoes-de-rede.md) |
| `hid_get_key_info.fcgi` | — | — | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[hardware/gerenciamento-de-chaves-hid](paginas/hardware/gerenciamento-de-chaves-hid.md) |
| `hid_get_oid.fcgi` | — | — | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[hardware/gerenciamento-de-chaves-hid](paginas/hardware/gerenciamento-de-chaves-hid.md) |
| `hid_inject_custom_kek.fcgi` | — | — | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[hardware/gerenciamento-de-chaves-hid](paginas/hardware/gerenciamento-de-chaves-hid.md) |
| `hid_inject_default_kek.fcgi` | — | — | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[hardware/gerenciamento-de-chaves-hid](paginas/hardware/gerenciamento-de-chaves-hid.md) |
| `hid_inject_media_key.fcgi` | — | — | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[hardware/gerenciamento-de-chaves-hid](paginas/hardware/gerenciamento-de-chaves-hid.md) |
| `hid_reset_config.fcgi` | — | — | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[hardware/gerenciamento-de-chaves-hid](paginas/hardware/gerenciamento-de-chaves-hid.md) |
| `hid_set_oid.fcgi` | — | — | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[hardware/gerenciamento-de-chaves-hid](paginas/hardware/gerenciamento-de-chaves-hid.md) |
| `idflex_upgrade_enterprise.fcgi` | — | — | [particularidade-dos-produtos/update-idflex-idaccess-nano](paginas/particularidade-dos-produtos/update-idflex-idaccess-nano.md) |
| `io_state.fcgi` | — | — | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[hardware/diagnostico-de-entradas-e-saidas](paginas/hardware/diagnostico-de-entradas-e-saidas.md) |
| `is_identifying.fcgi` | — | — | [reconhecimento-facial/configuracoes-reconhecimento-facial](paginas/reconhecimento-facial/configuracoes-reconhecimento-facial.md) |
| `is_valid_biometry.fcgi` | POST | Template / Verificar validade de template | [acoes/operacoes-template-digital](paginas/acoes/operacoes-template-digital.md)<br>[sistema/verificar-biometria](paginas/sistema/verificar-biometria.md) |
| `led_rgb_refresh.fcgi` | POST | Hardware / Recarregar LED RGB | [hardware/reler-configuracao-do-led](paginas/hardware/reler-configuracao-do-led.md) |
| `load_objects.fcgi` | POST | Objetos / Alarm logs / Carregar logs do alarm<br>Objetos / Regras de acesso / Carregar regras de acesso<br>Objetos / Regras de acesso time zones / Carregar regras de acesso time zones<br>Objetos / Ações / Carregar ações<br>Objetos / Logs de acesso / Carregar logs de acesso<br>Objetos / Cartões / Carregar cartões<br>… +22 | [objetos/carregar-objetos](paginas/objetos/carregar-objetos.md)<br>[primeiros-passos/exemplos](paginas/primeiros-passos/exemplos.md) |
| `login.fcgi` | POST | Gerenciamento de sessão / Login | [gerenciamento-secao/fazer-login](paginas/gerenciamento-secao/fazer-login.md)<br>[primeiros-passos/realizar-login](paginas/primeiros-passos/realizar-login.md) |
| `logo.fcgi` | POST | Fotos e Logotipo / Recuperar logo | [fotos-e-logotipo/gerenciar-logotipo](paginas/fotos-e-logotipo/gerenciar-logotipo.md) |
| `logo_change.fcgi` | POST | Fotos e Logotipo / Mudar Logo | [fotos-e-logotipo/gerenciar-logotipo](paginas/fotos-e-logotipo/gerenciar-logotipo.md) |
| `logo_destroy.fcgi` | POST | Fotos e Logotipo / Remover Logo<br>Fotos e Logotipo / Remover logo sem id | [fotos-e-logotipo/gerenciar-logotipo](paginas/fotos-e-logotipo/gerenciar-logotipo.md) |
| `logout.fcgi` | POST | Gerenciamento de sessão / Logout | [gerenciamento-secao/fazer-logout](paginas/gerenciamento-secao/fazer-logout.md) |
| `make_sip_call.fcgi` | POST | Interfonia SIP / Realizar chamada SIP | [particularidade-dos-produtos/interfonia-sip-idface](paginas/particularidade-dos-produtos/interfonia-sip-idface.md) |
| `make_sip_conference.fcgi` | — | — | [particularidade-dos-produtos/interfonia-sip-idface](paginas/particularidade-dos-produtos/interfonia-sip-idface.md) |
| `master_password.fcgi` | POST | Sistema / Alterar senha mestre | [sistema/reforco-de-seguranca](paginas/sistema/reforco-de-seguranca.md) |
| `message_to_screen.fcgi` | — | — | [acoes/apresentar_mensagem_tela](paginas/acoes/apresentar_mensagem_tela.md) |
| `modify_objects.fcgi` | POST | Objetos / Dispositivos / Atulaizar dispositivos<br>Objetos / Regras de acesso para grupos / Atualizar regras de acesso para grupos<br>Objetos / Regras de acesso para liberações agendadas / Atualizar regras de acesso para liberações agendadas<br>Objetos / Regras de acesso ao portal / Atualizar ações do portal<br>Objetos / Time spans / Atualizar time spans<br>Objetos / Usuários / Atualizar usuários<br>… +1 | [objetos/modificar-objetos](paginas/objetos/modificar-objetos.md) |
| `new_biometric_image.fcgi` | — | — | [modos-de-operacao/eventos-de-identificacao-online](paginas/modos-de-operacao/eventos-de-identificacao-online.md) |
| `new_biometric_template.fcgi` | — | — | [modos-de-operacao/eventos-de-identificacao-online](paginas/modos-de-operacao/eventos-de-identificacao-online.md) |
| `new_card.fcgi` | — | — | [modos-de-operacao/eventos-de-identificacao-online](paginas/modos-de-operacao/eventos-de-identificacao-online.md) |
| `new_face_template.fcgi` | — | — | [modos-de-operacao/eventos-de-identificacao-online](paginas/modos-de-operacao/eventos-de-identificacao-online.md) |
| `new_pin.fcgi` | — | — | [modos-de-operacao/eventos-de-identificacao-online](paginas/modos-de-operacao/eventos-de-identificacao-online.md) |
| `new_qrcode.fcgi` | — | — | [modos-de-operacao/eventos-de-identificacao-online](paginas/modos-de-operacao/eventos-de-identificacao-online.md) |
| `new_rex_log.fcgi` | — | — | [modos-de-operacao/eventos-de-identificacao-online](paginas/modos-de-operacao/eventos-de-identificacao-online.md) |
| `new_uhf_tag.fcgi` | — | — | [modos-de-operacao/eventos-de-identificacao-online](paginas/modos-de-operacao/eventos-de-identificacao-online.md) |
| `new_user_enrolled.fcgi` | — | — | [modos-de-operacao/eventos-de-identificacao-online](paginas/modos-de-operacao/eventos-de-identificacao-online.md) |
| `new_user_id_and_password.fcgi` | — | — | [modos-de-operacao/eventos-de-identificacao-online](paginas/modos-de-operacao/eventos-de-identificacao-online.md) |
| `new_user_identified.fcgi` | — | — | [modos-de-operacao/eventos-de-identificacao-online](paginas/modos-de-operacao/eventos-de-identificacao-online.md) |
| `nslookup_test.fcgi` | — | — | [configuracoes/ferramentas-diagnostico](paginas/configuracoes/ferramentas-diagnostico.md) |
| `object_add.fcgi` | — | — | [objetos/schema-objeto-customizado](paginas/objetos/schema-objeto-customizado.md) |
| `object_add_field.fcgi` | POST | Usuário / Adicionar campo customizado | [objetos/schema-objeto-customizado](paginas/objetos/schema-objeto-customizado.md) |
| `object_metadata.fcgi` | POST | Sistema / Obter metadados de objetos | [objetos/schema-objeto-customizado](paginas/objetos/schema-objeto-customizado.md) |
| `object_remove.fcgi` | — | — | [objetos/schema-objeto-customizado](paginas/objetos/schema-objeto-customizado.md) |
| `object_remove_fields.fcgi` | POST | Usuário / Apagar campo customizado | [objetos/schema-objeto-customizado](paginas/objetos/schema-objeto-customizado.md) |
| `object_update_field.fcgi` | — | — | [objetos/schema-objeto-customizado](paginas/objetos/schema-objeto-customizado.md) |
| `osdp_get_connection_status.fcgi` | — | — | [sistema/configuracoes-de-rede](paginas/sistema/configuracoes-de-rede.md) |
| `osdp_get_output_capabilities.fcgi` | — | — | [sistema/configuracoes-de-rede](paginas/sistema/configuracoes-de-rede.md) |
| `osdp_refresh.fcgi` | — | — | [sistema/configuracoes-de-rede](paginas/sistema/configuracoes-de-rede.md) |
| `osdp_scbk.fcgi` | — | — | [sistema/configuracoes-de-rede](paginas/sistema/configuracoes-de-rede.md) |
| `osdp_set_secure_key.fcgi` | — | — | [sistema/configuracoes-de-rede](paginas/sistema/configuracoes-de-rede.md) |
| `osdp_set_secure_type.fcgi` | — | — | [sistema/configuracoes-de-rede](paginas/sistema/configuracoes-de-rede.md) |
| `password_create.fcgi` | — | — | [acoes/cadastro-remoto-biometria-facial-cartao](paginas/acoes/cadastro-remoto-biometria-facial-cartao.md) |
| `pin_create.fcgi` | — | — | [acoes/cadastro-remoto-biometria-facial-cartao](paginas/acoes/cadastro-remoto-biometria-facial-cartao.md) |
| `ping_test.fcgi` | — | — | [configuracoes/ferramentas-diagnostico](paginas/configuracoes/ferramentas-diagnostico.md) |
| `reboot.fcgi` | POST | Sistema / Reiniciar | [sistema/reiniciar-equipamento](paginas/sistema/reiniciar-equipamento.md) |
| `reboot_recovery.fcgi` | POST | Sistema / Reiniciar em modo de recuperação | [sistema/reiniciar-em-modo-de-update](paginas/sistema/reiniciar-em-modo-de-update.md) |
| `recovery_password.fcgi` | — | — | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[sistema/senha-do-modo-de-update](paginas/sistema/senha-do-modo-de-update.md) |
| `remote_camera_calibration.fcgi` | — | — | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[reconhecimento-facial/calibracao-da-camera](paginas/reconhecimento-facial/calibracao-da-camera.md) |
| `remote_enroll.fcgi` | POST | Registro / Registrar dedo de pânico (sync)<br>Registro / Registrar dedo de pânico (async)<br>Registro / Registrar dedo (sync)<br>Registro / Registrar senha (sync)<br>Registro / Registrar PIN (sync)<br>Registro / Registrar PIN (async)<br>… +5 | [acoes/cadastro-remoto-biometria-facial-cartao](paginas/acoes/cadastro-remoto-biometria-facial-cartao.md)<br>[changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[monitor/introducao-ao-monitor](paginas/monitor/introducao-ao-monitor.md) |
| `remote_led_control.fcgi` | POST | Configurações / iDBlock Next / Configurar LEDs na catraca | [particularidade-dos-produtos/idblock-next](paginas/particularidade-dos-produtos/idblock-next.md) |
| `remote_user_authorization.fcgi` | — | — | [acoes/autorizacao_remota_acesso](paginas/acoes/autorizacao_remota_acesso.md) |
| `remove_custom_video.fcgi` | — | — | [fotos-e-logotipo/gerenciar-modo-propaganda](paginas/fotos-e-logotipo/gerenciar-modo-propaganda.md)<br>[particularidade-dos-produtos/streaming-idface](paginas/particularidade-dos-produtos/streaming-idface.md) |
| `remove_phone_icon.fcgi` | — | — | [particularidade-dos-produtos/interfonia-sip-idface](paginas/particularidade-dos-produtos/interfonia-sip-idface.md) |
| `remove_slideshow_images.fcgi` | — | — | [fotos-e-logotipo/gerenciar-slideshow](paginas/fotos-e-logotipo/gerenciar-slideshow.md)<br>[particularidade-dos-produtos/slideshow](paginas/particularidade-dos-produtos/slideshow.md) |
| `remove_streaming_logo.fcgi` | — | — | [particularidade-dos-produtos/streaming-idface](paginas/particularidade-dos-produtos/streaming-idface.md) |
| `report_generate.fcgi` | POST | Objetos / Relatórios / Gerar Relatório | [modo-ponto/funcionamento](paginas/modo-ponto/funcionamento.md)<br>[objetos/exportar-relatorio](paginas/objetos/exportar-relatorio.md) |
| `request_redirect.fcgi` | — | — | [sistema/redirecionar-requisicao](paginas/sistema/redirecionar-requisicao.md) |
| `reset_crypto_key.fcgi` | — | — | [sistema/alterar-chave-de-seguranca-secbox](paginas/sistema/alterar-chave-de-seguranca-secbox.md) |
| `reset_to_factory_default.fcgi` | POST | Sistema / Voltar todas configurações de fábrica<br>Sistema / Voltar configurações de fábrica exceto rede | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[sistema/redefinir-as-configuracoes](paginas/sistema/redefinir-as-configuracoes.md) |
| `save_screenshot.fcgi` | POST | Cadastro facial / Captura de câmera via API | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[reconhecimento-facial/captura-de-camera](paginas/reconhecimento-facial/captura-de-camera.md) |
| `secbox_is_active.fcgi` | — | — | [sistema/secbox](paginas/sistema/secbox.md) |
| `secbox_serial_number.fcgi` | — | — | [sistema/secbox](paginas/sistema/secbox.md) |
| `send_slideshow_images.fcgi` | — | — | [fotos-e-logotipo/gerenciar-slideshow](paginas/fotos-e-logotipo/gerenciar-slideshow.md)<br>[particularidade-dos-produtos/slideshow](paginas/particularidade-dos-produtos/slideshow.md) |
| `send_video.fcgi` | — | — | [fotos-e-logotipo/gerenciar-modo-propaganda](paginas/fotos-e-logotipo/gerenciar-modo-propaganda.md)<br>[particularidade-dos-produtos/streaming-idface](paginas/particularidade-dos-produtos/streaming-idface.md) |
| `session_is_valid.fcgi` | POST | Gerenciamento de sessão / Checar validade da sessão | [gerenciamento-secao/verificar-validade-da-sessao](paginas/gerenciamento-secao/verificar-validade-da-sessao.md)<br>[modos-de-operacao/configurar-modo-online](paginas/modos-de-operacao/configurar-modo-online.md) |
| `set_audio_access_message.fcgi` | POST | Configurações / Carregar mensagem sonora customizada | [particularidade-dos-produtos/mensagens-sonoras-acesso-idface](paginas/particularidade-dos-produtos/mensagens-sonoras-acesso-idface.md) |
| `set_configuration.fcgi` | POST | Ações / Personalizar mensagem de eventos<br>Configurações / iDBlock Next / Habilitar relé 1 da catraca (iDBlock Next)<br>Configurações / iDBlock Next / Desabilitar relé 1 da catraca (iDBlock Next)<br>Configurações / iDBlock Next / Ativar relé 1 da catraca em sentido anti-horário (iDBlock Next)<br>Configurações / iDBlock Next / Ativar relé 1 da catraca em sentido horário (iDBlock Next)<br>Configurações / iDBlock Next / Habilitar relé 2 da catraca (iDBlock Next)<br>… +250 | [acoes/personalizar-mensagem-eventos](paginas/acoes/personalizar-mensagem-eventos.md)<br>[alarme/introducao-ao-alarme](paginas/alarme/introducao-ao-alarme.md)<br>[configuracoes/ativar-desativar-usb](paginas/configuracoes/ativar-desativar-usb.md)<br>[configuracoes/modificar-configuracoes](paginas/configuracoes/modificar-configuracoes.md)<br>[fotos-e-logotipo/gerenciar-logotipo](paginas/fotos-e-logotipo/gerenciar-logotipo.md)<br>[fotos-e-logotipo/gerenciar-modo-propaganda](paginas/fotos-e-logotipo/gerenciar-modo-propaganda.md)<br>[fotos-e-logotipo/gerenciar-slideshow](paginas/fotos-e-logotipo/gerenciar-slideshow.md)<br>[modo-ponto/funcionamento](paginas/modo-ponto/funcionamento.md)<br>[modos-de-operacao/configurar-modo-online](paginas/modos-de-operacao/configurar-modo-online.md)<br>[monitor/introducao-ao-monitor](paginas/monitor/introducao-ao-monitor.md)<br>[particularidade-dos-produtos/idblock-next](paginas/particularidade-dos-produtos/idblock-next.md)<br>[particularidade-dos-produtos/interfonia-sip-idface](paginas/particularidade-dos-produtos/interfonia-sip-idface.md)<br>[particularidade-dos-produtos/mensagens-sonoras-acesso-idface](paginas/particularidade-dos-produtos/mensagens-sonoras-acesso-idface.md)<br>[particularidade-dos-produtos/modo-quiosque](paginas/particularidade-dos-produtos/modo-quiosque.md)<br>[particularidade-dos-produtos/particulariade-terminais-control-id](paginas/particularidade-dos-produtos/particulariade-terminais-control-id.md)<br>[particularidade-dos-produtos/qr-code](paginas/particularidade-dos-produtos/qr-code.md)<br>[particularidade-dos-produtos/sinais-configuraveis-idface-max](paginas/particularidade-dos-produtos/sinais-configuraveis-idface-max.md)<br>[particularidade-dos-produtos/streaming-idface](paginas/particularidade-dos-produtos/streaming-idface.md)<br>[reconhecimento-facial/cadastro-facial](paginas/reconhecimento-facial/cadastro-facial.md)<br>[reconhecimento-facial/configuracoes-reconhecimento-facial](paginas/reconhecimento-facial/configuracoes-reconhecimento-facial.md)<br>[sistema/alterar-data-e-hora](paginas/sistema/alterar-data-e-hora.md)<br>[sistema/desativar-interface-web](paginas/sistema/desativar-interface-web.md)<br>[sistema/reforco-de-seguranca](paginas/sistema/reforco-de-seguranca.md)<br>[sistema/suporte-a-SNMP](paginas/sistema/suporte-a-SNMP.md)<br>[visitantes/introducao-visitas](paginas/visitantes/introducao-visitas.md) |
| `set_custom_video.fcgi` | — | — | [fotos-e-logotipo/gerenciar-modo-propaganda](paginas/fotos-e-logotipo/gerenciar-modo-propaganda.md)<br>[particularidade-dos-produtos/streaming-idface](paginas/particularidade-dos-produtos/streaming-idface.md) |
| `set_dual_sided_secret.fcgi` | — | — | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[sistema/dual-sided-secbox](paginas/sistema/dual-sided-secbox.md) |
| `set_kiosk_icon.fcgi` | — | — | [particularidade-dos-produtos/modo-quiosque](paginas/particularidade-dos-produtos/modo-quiosque.md) |
| `set_network_interlock.fcgi` | POST | Configurações / Configurar o Intertravamento via Rede | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[configuracoes/intertravamento-via-rede](paginas/configuracoes/intertravamento-via-rede.md) |
| `set_ntp_server.fcgi` | — | — | [configuracoes/servidor-ntp](paginas/configuracoes/servidor-ntp.md) |
| `set_oem_approved_fw.fcgi` | — | — | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[sistema/firmware-aprovado-oem](paginas/sistema/firmware-aprovado-oem.md) |
| `set_oem_code.fcgi` | — | — | [sistema/obter-informacoes-do-sistema](paginas/sistema/obter-informacoes-do-sistema.md) |
| `set_osdp_installation_mode.fcgi` | POST | Configurações / Habilitar o Modo de Instalação OSDP<br>Configurações / Desabilitar o Modo de Instalação OSDP | [sistema/configuracoes-de-rede](paginas/sistema/configuracoes-de-rede.md) |
| `set_phone_icon.fcgi` | — | — | [particularidade-dos-produtos/interfonia-sip-idface](paginas/particularidade-dos-produtos/interfonia-sip-idface.md) |
| `set_pjsip_audio_message.fcgi` | POST | Interfonia SIP / Carregar som de chamada personalizado | [particularidade-dos-produtos/interfonia-sip-idface](paginas/particularidade-dos-produtos/interfonia-sip-idface.md) |
| `set_pro_operation_mode_and_reboot.fcgi` | — | — | [particularidade-dos-produtos/update-idface](paginas/particularidade-dos-produtos/update-idface.md)<br>[particularidade-dos-produtos/update-idface-max](paginas/particularidade-dos-produtos/update-idface-max.md) |
| `set_streaming_logo.fcgi` | — | — | [particularidade-dos-produtos/streaming-idface](paginas/particularidade-dos-produtos/streaming-idface.md) |
| `set_system_network.fcgi` | POST | Sistema / Alterar configuração de rede | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[sistema/configuracoes-de-rede](paginas/sistema/configuracoes-de-rede.md) |
| `set_system_time.fcgi` | POST | Sistema / Alterar data/hora | [sistema/alterar-data-e-hora](paginas/sistema/alterar-data-e-hora.md) |
| `set_vpn_file.fcgi` | POST | Sistema / Alterar arquivo de configuração de VPN com arquivo .config<br>Sistema / Alterar arquivo de configuração de VPN com arquivo .zip | [sistema/configuracoes-de-rede](paginas/sistema/configuracoes-de-rede.md) |
| `set_vpn_information.fcgi` | POST | Sistema / Alterar configuração de OpenVPN | [sistema/configuracoes-de-rede](paginas/sistema/configuracoes-de-rede.md) |
| `set_wifi_settings.fcgi` | — | — | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[sistema/configuracoes-de-rede](paginas/sistema/configuracoes-de-rede.md) |
| `set_wifi_source.fcgi` | — | — | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[sistema/configuracoes-de-rede](paginas/sistema/configuracoes-de-rede.md) |
| `ssl_certificate_change.fcgi` | — | — | [sistema/configuracoes-de-rede](paginas/sistema/configuracoes-de-rede.md) |
| `system_information.fcgi` | POST | Sistema / Obter informações do sistema | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[modo-push/idcloud](paginas/modo-push/idcloud.md)<br>[sistema/obter-informacoes-do-sistema](paginas/sistema/obter-informacoes-do-sistema.md) |
| `tag_create.fcgi` | — | — | [modos-de-operacao/eventos-de-identificacao-online](paginas/modos-de-operacao/eventos-de-identificacao-online.md) |
| `template_create.fcgi` | — | — | [acoes/cadastro-remoto-biometria-facial-cartao](paginas/acoes/cadastro-remoto-biometria-facial-cartao.md)<br>[modos-de-operacao/eventos-de-identificacao-online](paginas/modos-de-operacao/eventos-de-identificacao-online.md) |
| `template_extract.fcgi` | POST | Template / Extrair template | [acoes/operacoes-template-digital](paginas/acoes/operacoes-template-digital.md) |
| `template_match.fcgi` | POST | Template / Verificar template<br>Template / Verificar template único | [acoes/operacoes-template-digital](paginas/acoes/operacoes-template-digital.md) |
| `template_merge.fcgi` | — | — | [acoes/operacoes-template-digital](paginas/acoes/operacoes-template-digital.md) |
| `template_sync_end.fcgi` | POST | Template / Desabilitar sincronismo de biomatrias | [acoes/operacoes-template-digital](paginas/acoes/operacoes-template-digital.md)<br>[objetos/carregar-objetos](paginas/objetos/carregar-objetos.md) |
| `template_sync_init.fcgi` | POST | Template / Habilitar sincronismo de biometrias | [acoes/operacoes-template-digital](paginas/acoes/operacoes-template-digital.md)<br>[objetos/carregar-objetos](paginas/objetos/carregar-objetos.md) |
| `tests.fcgi` | GET | Hardware / Coletar testes de fábrica | — |
| `update_secbox_firmware.fcgi` | — | — | [sistema/secbox](paginas/sistema/secbox.md) |
| `update_secbox_firmware_status.fcgi` | — | — | [sistema/secbox](paginas/sistema/secbox.md) |
| `update_secbox_firmware_version.fcgi` | — | — | [sistema/secbox](paginas/sistema/secbox.md) |
| `upgrade_fifteen_thousand_face_templates.fcgi` | — | — | [particularidade-dos-produtos/update-idface](paginas/particularidade-dos-produtos/update-idface.md) |
| `upgrade_fifty_thousand_face_templates.fcgi` | POST | Configurações / Ativar licença Pro | [particularidade-dos-produtos/update-idface-max](paginas/particularidade-dos-produtos/update-idface-max.md) |
| `upgrade_hundred_thousand_face_templates.fcgi` | — | — | [particularidade-dos-produtos/update-idface-max](paginas/particularidade-dos-produtos/update-idface-max.md) |
| `upgrade_ten_thousand_face_templates.fcgi` | — | — | [particularidade-dos-produtos/update-idface](paginas/particularidade-dos-produtos/update-idface.md) |
| `upgrade_ten_thousand_templates.fcgi` | — | — | [particularidade-dos-produtos/update-idflex-idaccess-nano](paginas/particularidade-dos-produtos/update-idflex-idaccess-nano.md) |
| `upgrade_thirty_thousand_face_templates.fcgi` | — | — | [particularidade-dos-produtos/update-idface](paginas/particularidade-dos-produtos/update-idface.md) |
| `upgrade_twenty_thousand_face_templates.fcgi` | — | — | [particularidade-dos-produtos/update-idface-max](paginas/particularidade-dos-produtos/update-idface-max.md) |
| `user_destroy_image.fcgi` | POST | Cadastro facial / Excluir foto de usuário | [fotos-e-logotipo/gerenciar-fotos-de-usuarios](paginas/fotos-e-logotipo/gerenciar-fotos-de-usuarios.md)<br>[reconhecimento-facial/cadastro-facial](paginas/reconhecimento-facial/cadastro-facial.md) |
| `user_get_image.fcgi` | POST | Usuário / Obter imagem do usuário<br>Cadastro facial / Obter imagem do usuário | [fotos-e-logotipo/gerenciar-fotos-de-usuarios](paginas/fotos-e-logotipo/gerenciar-fotos-de-usuarios.md)<br>[modos-de-operacao/eventos-de-identificacao-online](paginas/modos-de-operacao/eventos-de-identificacao-online.md)<br>[reconhecimento-facial/cadastro-facial](paginas/reconhecimento-facial/cadastro-facial.md) |
| `user_get_image_list.fcgi` | POST | Cadastro facial / Obter lista de imagens de usuários | [reconhecimento-facial/cadastro-facial](paginas/reconhecimento-facial/cadastro-facial.md) |
| `user_hash_password.fcgi` | POST | Usuário / Gerar HASH de senha | [sistema/criar-hash-de-senha-de-usuario](paginas/sistema/criar-hash-de-senha-de-usuario.md) |
| `user_list_images.fcgi` | GET | Cadastro facial / Obter lista de usuários com foto cadastrada | [fotos-e-logotipo/gerenciar-fotos-de-usuarios](paginas/fotos-e-logotipo/gerenciar-fotos-de-usuarios.md)<br>[reconhecimento-facial/cadastro-facial](paginas/reconhecimento-facial/cadastro-facial.md) |
| `user_set_image.fcgi` | POST | Usuário / Atualizar imagem do usuário<br>Cadastro facial / Cadastrar imagem do usuário | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[fotos-e-logotipo/gerenciar-fotos-de-usuarios](paginas/fotos-e-logotipo/gerenciar-fotos-de-usuarios.md)<br>[reconhecimento-facial/cadastro-facial](paginas/reconhecimento-facial/cadastro-facial.md) |
| `user_set_image_list.fcgi` | POST | Cadastro facial / Cadastrar lista de imagens de usuários | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[fotos-e-logotipo/gerenciar-fotos-de-usuarios](paginas/fotos-e-logotipo/gerenciar-fotos-de-usuarios.md)<br>[reconhecimento-facial/cadastro-facial](paginas/reconhecimento-facial/cadastro-facial.md) |
| `user_test_image.fcgi` | POST | Cadastro facial / Testar imagem do usuário | [reconhecimento-facial/cadastro-facial](paginas/reconhecimento-facial/cadastro-facial.md) |
| `wifi_scan.fcgi` | — | — | [changelog/setembro-2026](paginas/changelog/setembro-2026.md)<br>[sistema/configuracoes-de-rede](paginas/sistema/configuracoes-de-rede.md) |
