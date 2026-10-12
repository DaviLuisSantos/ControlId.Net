using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ControlId.Access.Monitoring;
using ControlId.Access.Online;
using ControlId.Access.Push;

namespace ControlId.Access;

/// <summary>
/// Cliente da API de acesso Control iD (iDAccess, iDFlex, iDFace, iDBox, iDUHF...).
/// O login é feito automaticamente na primeira chamada e refeito se a sessão expirar.
/// </summary>
public interface IAccessClient : IControlIdClient
{
    /// <summary>Força o login agora (opcional — as chamadas já fazem login sob demanda).</summary>
    Task LoginAsync(CancellationToken cancellationToken = default);

    /// <summary>Encerra a sessão no equipamento.</summary>
    Task LogoutAsync(CancellationToken cancellationToken = default);

    /// <summary><c>session_is_valid.fcgi</c>.</summary>
    Task<bool> IsSessionValidAsync(CancellationToken cancellationToken = default);

    /// <summary><c>load_objects.fcgi</c> — lê registros de qualquer objeto.</summary>
    Task<IReadOnlyList<T>> LoadObjectsAsync<T>(LoadObjectsRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>create_objects.fcgi</c> — devolve os IDs criados.</summary>
    Task<IReadOnlyList<long>> CreateObjectsAsync<T>(string @object, IEnumerable<T> values, CancellationToken cancellationToken = default);

    /// <summary><c>modify_objects.fcgi</c> — devolve a quantidade de registros alterados.</summary>
    Task<int> ModifyObjectsAsync(string @object, object values, object where, CancellationToken cancellationToken = default);

    /// <summary><c>destroy_objects.fcgi</c> — devolve a quantidade de registros removidos.</summary>
    Task<int> DestroyObjectsAsync(string @object, object where, CancellationToken cancellationToken = default);

    /// <summary>Lista todos os usuários.</summary>
    Task<IReadOnlyList<AccessUser>> GetUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>Cria um usuário e devolve o ID gerado.</summary>
    Task<long> CreateUserAsync(AccessUser user, CancellationToken cancellationToken = default);

    /// <summary>Remove um usuário pelo ID.</summary>
    Task<bool> DeleteUserAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary>Cartões; com <paramref name="userId"/>, só os do usuário.</summary>
    Task<IReadOnlyList<AccessCard>> GetCardsAsync(long? userId = null, CancellationToken cancellationToken = default);

    /// <summary>Cadastra um cartão e devolve o ID gerado.</summary>
    Task<long> CreateCardAsync(AccessCard card, CancellationToken cancellationToken = default);

    /// <summary>Remove um cartão pelo ID.</summary>
    Task<bool> DeleteCardAsync(long cardId, CancellationToken cancellationToken = default);

    /// <summary>Digitais (<c>templates</c>); com <paramref name="userId"/>, só as do usuário.</summary>
    Task<IReadOnlyList<AccessTemplate>> GetTemplatesAsync(long? userId = null, CancellationToken cancellationToken = default);

    /// <summary>Cadastra uma digital já extraída e devolve o ID gerado.</summary>
    Task<long> CreateTemplateAsync(AccessTemplate template, CancellationToken cancellationToken = default);

    /// <summary>Remove uma digital pelo ID.</summary>
    Task<bool> DeleteTemplateAsync(long templateId, CancellationToken cancellationToken = default);

    /// <summary>Envia a foto (JPEG, menor que 1 MB) do usuário (<c>user_set_image.fcgi</c>).</summary>
    Task SetUserImageAsync(long userId, byte[] jpeg, CancellationToken cancellationToken = default);

    /// <summary>Envia fotos de vários usuários (<c>user_set_image_list.fcgi</c>). O lote inteiro deve ter menos de 1 MB.</summary>
    Task SetUserImagesAsync(IEnumerable<UserImage> images, CancellationToken cancellationToken = default);

    /// <summary>Baixa a foto (JPEG) do usuário (<c>user_get_image.fcgi</c>).</summary>
    Task<byte[]> GetUserImageAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary>IDs dos usuários que têm foto (<c>user_list_images.fcgi</c>, firmware 3.10.0+).</summary>
    Task<IReadOnlyList<long>> GetUserIdsWithImageAsync(CancellationToken cancellationToken = default);

    /// <summary>Remove a foto do usuário (<c>user_destroy_image.fcgi</c>).</summary>
    Task DeleteUserImageAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary>Remove as fotos de todos os usuários.</summary>
    Task DeleteAllUserImagesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Cadastro remoto síncrono (<c>remote_enroll.fcgi</c> com <c>sync</c>): o equipamento pede o cartão, a face, a digital,
    /// o PIN ou a senha, e a chamada só termina quando a pessoa conclui ou o cadastro falha. Falhas do cadastro (ex.:
    /// <c>FACE_EXISTS</c>) voltam em <see cref="RemoteEnrollResult.Error"/>, sem exceção.
    /// <para>
    /// A espera é limitada por <see cref="ControlIdDeviceOptions.Timeout"/> (padrão 30 s). Se ela estourar ou
    /// <paramref name="cancellationToken"/> for cancelado, o cadastro é cancelado no equipamento.
    /// Para esperas longas, prefira <see cref="StartRemoteEnrollAsync"/> com o Monitor.
    /// </para>
    /// </summary>
    Task<RemoteEnrollResult> RemoteEnrollAsync(RemoteEnrollRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inicia o cadastro remoto assíncrono e retorna em seguida. O resultado chega pelo Monitor
    /// (<c>card</c>, <c>user_image</c>, <c>face_template</c>, <c>template</c>, <c>pin</c>, <c>password</c>) como
    /// <see cref="Monitoring.RemoteEnrollNotification"/>; o Monitor precisa estar configurado.
    /// </summary>
    Task StartRemoteEnrollAsync(RemoteEnrollRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>cancel_remote_enroll.fcgi</c>: interrompe o cadastro remoto em andamento.</summary>
    Task CancelRemoteEnrollAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lê registros de qualquer modelo marcado com <see cref="ControlIdObjectAttribute"/>, ex.:
    /// <c>GetObjectsAsync&lt;TimedAlarm&gt;()</c>. <paramref name="where"/> segue o formato de <see cref="Where"/>, com o nome
    /// de <see cref="ControlIdObjects.NameOf{T}"/>.
    /// </summary>
    Task<IReadOnlyList<T>> GetObjectsAsync<T>(object? where = null, int? limit = null, CancellationToken cancellationToken = default);

    /// <summary>Cria um registro de qualquer modelo com <see cref="ControlIdObjectAttribute"/> e devolve o ID gerado (0 se a tabela não tiver ID).</summary>
    Task<long> CreateObjectAsync<T>(T value, CancellationToken cancellationToken = default);

    /// <summary>Grava o registro inteiro, localizado pela chave do modelo (normalmente <c>id</c>). Devolve a quantidade alterada.</summary>
    Task<int> UpdateObjectAsync<T>(T value, CancellationToken cancellationToken = default);

    /// <summary>Remove o registro pela chave do modelo (normalmente <c>id</c>).</summary>
    Task<bool> DeleteObjectAsync<T>(long key, CancellationToken cancellationToken = default);

    /// <summary>QR Codes; com <paramref name="userId"/>, só os do usuário.</summary>
    Task<IReadOnlyList<AccessQrCode>> GetQrCodesAsync(long? userId = null, CancellationToken cancellationToken = default);

    /// <summary>Cadastra um QR Code e devolve o ID gerado.</summary>
    Task<long> CreateQrCodeAsync(AccessQrCode qrCode, CancellationToken cancellationToken = default);

    /// <summary>Tags UHF; com <paramref name="userId"/>, só as do usuário.</summary>
    Task<IReadOnlyList<AccessUhfTag>> GetUhfTagsAsync(long? userId = null, CancellationToken cancellationToken = default);

    /// <summary>Cadastra uma tag UHF e devolve o ID gerado.</summary>
    Task<long> CreateUhfTagAsync(AccessUhfTag tag, CancellationToken cancellationToken = default);

    /// <summary>PINs; com <paramref name="userId"/>, só os do usuário.</summary>
    Task<IReadOnlyList<AccessPin>> GetPinsAsync(long? userId = null, CancellationToken cancellationToken = default);

    /// <summary>Cadastra um PIN e devolve o ID gerado.</summary>
    Task<long> CreatePinAsync(AccessPin pin, CancellationToken cancellationToken = default);

    /// <summary>Torna o usuário administrador do equipamento, ou tira o privilégio (<c>user_roles</c>).</summary>
    Task SetAdministratorAsync(long userId, bool isAdministrator, CancellationToken cancellationToken = default);

    /// <summary><c>change_logs</c> (iDFace) com ID maior que <paramref name="afterId"/>, em ordem crescente.</summary>
    Task<IReadOnlyList<ChangeLog>> GetChangeLogsAsync(long afterId = 0, int? limit = null, CancellationToken cancellationToken = default);

    /// <summary>Regras de acesso avaliadas em um log de acesso (<c>access_log_access_rules</c>).</summary>
    Task<IReadOnlyList<long>> GetAccessLogRuleIdsAsync(long accessLogId, CancellationToken cancellationToken = default);

    /// <summary>Lista os grupos (departamentos).</summary>
    Task<IReadOnlyList<AccessGroup>> GetGroupsAsync(CancellationToken cancellationToken = default);

    /// <summary>Cria um grupo e devolve o ID gerado.</summary>
    Task<long> CreateGroupAsync(AccessGroup group, CancellationToken cancellationToken = default);

    /// <summary>Remove um grupo pelo ID.</summary>
    Task<bool> DeleteGroupAsync(long groupId, CancellationToken cancellationToken = default);

    /// <summary>Lista as regras de acesso.</summary>
    Task<IReadOnlyList<AccessRule>> GetAccessRulesAsync(CancellationToken cancellationToken = default);

    /// <summary>Cria uma regra de acesso e devolve o ID gerado.</summary>
    Task<long> CreateAccessRuleAsync(AccessRule rule, CancellationToken cancellationToken = default);

    /// <summary>Remove uma regra de acesso pelo ID.</summary>
    Task<bool> DeleteAccessRuleAsync(long accessRuleId, CancellationToken cancellationToken = default);

    /// <summary>Lista os horários (<c>time_zones</c>).</summary>
    Task<IReadOnlyList<AccessTimeZone>> GetTimeZonesAsync(CancellationToken cancellationToken = default);

    /// <summary>Cria um horário e devolve o ID gerado.</summary>
    Task<long> CreateTimeZoneAsync(AccessTimeZone timeZone, CancellationToken cancellationToken = default);

    /// <summary>Remove um horário pelo ID.</summary>
    Task<bool> DeleteTimeZoneAsync(long timeZoneId, CancellationToken cancellationToken = default);

    /// <summary>Intervalos de horário; com <paramref name="timeZoneId"/>, só os daquele horário.</summary>
    Task<IReadOnlyList<AccessTimeSpan>> GetTimeSpansAsync(long? timeZoneId = null, CancellationToken cancellationToken = default);

    /// <summary>Cria um intervalo de horário e devolve o ID gerado.</summary>
    Task<long> CreateTimeSpanAsync(AccessTimeSpan timeSpan, CancellationToken cancellationToken = default);

    /// <summary>Remove um intervalo de horário pelo ID.</summary>
    Task<bool> DeleteTimeSpanAsync(long timeSpanId, CancellationToken cancellationToken = default);

    /// <summary>Lista os portais (passagens entre áreas).</summary>
    Task<IReadOnlyList<AccessPortal>> GetPortalsAsync(CancellationToken cancellationToken = default);

    /// <summary>Lista as áreas.</summary>
    Task<IReadOnlyList<AccessArea>> GetAreasAsync(CancellationToken cancellationToken = default);

    /// <summary>Cria uma área e devolve o ID gerado.</summary>
    Task<long> CreateAreaAsync(AccessArea area, CancellationToken cancellationToken = default);

    /// <summary>Remove uma área pelo ID.</summary>
    Task<bool> DeleteAreaAsync(long areaId, CancellationToken cancellationToken = default);

    /// <summary>Lista os feriados.</summary>
    Task<IReadOnlyList<AccessHoliday>> GetHolidaysAsync(CancellationToken cancellationToken = default);

    /// <summary>Cria um feriado e devolve o ID gerado. Veja <see cref="AccessHoliday.FullDay"/>.</summary>
    Task<long> CreateHolidayAsync(AccessHoliday holiday, CancellationToken cancellationToken = default);

    /// <summary>Remove um feriado pelo ID.</summary>
    Task<bool> DeleteHolidayAsync(long holidayId, CancellationToken cancellationToken = default);

    /// <summary>Eventos de porta, SecBox e catraca (<c>access_events</c>) com ID maior que <paramref name="afterId"/>, em ordem crescente.</summary>
    Task<IReadOnlyList<AccessDeviceEvent>> GetAccessEventsAsync(long afterId = 0, int? limit = null, CancellationToken cancellationToken = default);

    /// <summary>Logs de alarme com ID maior que <paramref name="afterId"/>, em ordem crescente.</summary>
    Task<IReadOnlyList<AlarmLog>> GetAlarmLogsAsync(long afterId = 0, int? limit = null, CancellationToken cancellationToken = default);

    /// <summary>Configuração da SecBox (iDFlex, iDAccess Pro, iDAccess Nano); <c>null</c> se o equipamento não tiver.</summary>
    Task<SecBox?> GetSecBoxAsync(CancellationToken cancellationToken = default);

    /// <summary>Grava a configuração da SecBox (leia com <see cref="GetSecBoxAsync"/>, altere e envie).</summary>
    Task UpdateSecBoxAsync(SecBox secBox, CancellationToken cancellationToken = default);

    /// <summary><c>report_generate.fcgi</c>: gera um relatório em texto (estilo CSV) a partir de qualquer objeto.</summary>
    Task<string> GenerateReportAsync(ReportRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>export_afd.fcgi</c>: AFD gerado pelo equipamento de acesso (ex.: iDFlex Attendance), a partir de um NSR ou de
    /// uma data (ou completo). Com <paramref name="portaria671"/> <c>false</c>, vem no leiaute anterior (Portaria 595/1510).
    /// </summary>
    Task<string> ExportAfdAsync(long? initialNsr = null, DateTime? initialDate = null, bool portaria671 = true, CancellationToken cancellationToken = default);

    /// <summary><see cref="ExportAfdAsync"/> já interpretado pelo <see cref="Rep.Afd.AfdParser"/>.</summary>
    Task<Rep.Afd.AfdFile> ExportAfdFileAsync(long? initialNsr = null, DateTime? initialDate = null, bool portaria671 = true, CancellationToken cancellationToken = default);

    /// <summary><c>export_audit_logs.fcgi</c>: até os últimos 25 mil logs de auditoria das categorias escolhidas (em inglês).</summary>
    Task<string> ExportAuditLogsAsync(AuditLogCategories categories = AuditLogCategories.All, CancellationToken cancellationToken = default);

    /// <summary>Cria a associação (usuário↔grupo, grupo↔regra, regra↔horário...).</summary>
    Task LinkAsync(AccessLink link, CancellationToken cancellationToken = default);

    /// <summary>Desfaz a associação. Devolve <c>false</c> se ela não existia.</summary>
    Task<bool> UnlinkAsync(AccessLink link, CancellationToken cancellationToken = default);

    /// <summary>Logs de acesso com ID maior que <paramref name="afterId"/>, em ordem crescente.</summary>
    Task<IReadOnlyList<AccessLog>> GetAccessLogsAsync(long afterId = 0, int? limit = null, CancellationToken cancellationToken = default);

    /// <summary><c>execute_actions.fcgi</c> com ações arbitrárias. Devolve o JSON bruto (vazio sem intertravamento).</summary>
    Task<JsonElement?> ExecuteActionsAsync(IEnumerable<DeviceAction> actions, CancellationToken cancellationToken = default);

    /// <summary>Aciona o relé da porta (<c>door=N</c>).</summary>
    Task OpenDoorAsync(int door = 1, CancellationToken cancellationToken = default);

    /// <summary>Aciona a SecBox (<c>sec_box</c>).</summary>
    Task OpenSecBoxAsync(long secBoxId = 65793, int reason = 3, CancellationToken cancellationToken = default);

    /// <summary>Libera a catraca no sentido informado (<c>catra</c>).</summary>
    Task ReleaseTurnstileAsync(TurnstileDirection direction, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>create_or_modify_objects.fcgi</c>: altera os registros cujo <c>id</c> já existe e cria os demais.
    /// Devolve a quantidade de registros criados ou alterados.
    /// </summary>
    Task<int> CreateOrModifyObjectsAsync<T>(string @object, IEnumerable<T> values, CancellationToken cancellationToken = default);

    /// <summary><c>message_to_screen.fcgi</c>: mostra a mensagem no display; sem <paramref name="duration"/>, fica até ser trocada.</summary>
    Task ShowMessageAsync(string message, TimeSpan? duration = null, CancellationToken cancellationToken = default);

    /// <summary>Remove a mensagem exibida por <see cref="ShowMessageAsync"/>.</summary>
    Task ClearMessageAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>buzzer_buzz.fcgi</c>: aciona o buzzer. <paramref name="duration"/> é limitada a 3 s por chamada;
    /// <paramref name="dutyCycle"/> é a porcentagem do tempo com o sinal ativo.
    /// </summary>
    Task BuzzAsync(TimeSpan duration, int frequency = 4000, int dutyCycle = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>change_login.fcgi</c>: troca o usuário e a senha da API. Este cliente passa a usar as novas credenciais
    /// nos próximos logins; atualize também a sua configuração, ou o próximo processo usará as antigas.
    /// </summary>
    Task ChangeLoginAsync(string login, string password, CancellationToken cancellationToken = default);

    /// <summary><c>user_hash_password.fcgi</c>: gera hash e salt de uma senha de usuário (o salt muda a cada chamada).</summary>
    Task<PasswordHash> HashPasswordAsync(string password, CancellationToken cancellationToken = default);

    /// <summary><c>reboot.fcgi</c>: reinicia o equipamento. Depois, o cliente refaz o login sozinho.</summary>
    Task RebootAsync(CancellationToken cancellationToken = default);

    /// <summary><c>reboot_recovery.fcgi</c>: reinicia em modo de atualização de firmware.</summary>
    Task RebootToUpdateModeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>reset_to_factory_default.fcgi</c>: APAGA TODOS OS DADOS e volta às configurações de fábrica.
    /// Com <paramref name="keepNetworkSettings"/>, mantém IP e rede.
    /// </summary>
    Task ResetToFactoryDefaultAsync(bool keepNetworkSettings, CancellationToken cancellationToken = default);

    /// <summary><c>delete_admins.fcgi</c>: remove todos os administradores do equipamento.</summary>
    Task DeleteAllAdministratorsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>set_system_network.fcgi</c>: altera IP, máscara, gateway, DNS, hostname e servidor web. Se o IP ou a porta
    /// mudarem, este cliente perde o equipamento: crie outro com o novo endereço.
    /// </summary>
    Task SetNetworkAsync(NetworkSettings settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>logo.fcgi</c>: baixa o logotipo (PNG) do slot; sem slot, o primeiro.
    /// A documentação diz slots de 1 a 8, mas o exemplo trata <c>id=5</c> como a 6ª posição: confira no seu firmware.
    /// </summary>
    Task<byte[]> GetLogoAsync(int? slot = null, CancellationToken cancellationToken = default);

    /// <summary><c>logo_change.fcgi</c>: grava um logotipo PNG (menor que 1 MB, até 1000x1000) no slot.</summary>
    Task SetLogoAsync(byte[] png, int? slot = null, CancellationToken cancellationToken = default);

    /// <summary><c>logo_destroy.fcgi</c>: remove o logotipo do slot; sem slot, remove o primeiro e deixa de exibir logotipo.</summary>
    Task DeleteLogoAsync(int? slot = null, CancellationToken cancellationToken = default);

    /// <summary>Exibe o logotipo do slot (1 a 8) na tela principal; 0 não exibe nenhum (<c>general.show_logo</c>).</summary>
    Task ShowLogoAsync(int slot, CancellationToken cancellationToken = default);

    /// <summary><c>gpio_state.fcgi</c>: lê um pino (ex.: iDAccess pino 5 = sensor de porta 1; iDFlex/iDFace pino 0 = violação).</summary>
    Task<GpioState> GetGpioStateAsync(int gpio, CancellationToken cancellationToken = default);

    /// <summary><c>led_rgb_refresh.fcgi</c>: faz o equipamento reler a configuração do LED RGB.</summary>
    Task RefreshLedAsync(CancellationToken cancellationToken = default);

    /// <summary>Habilita ou desabilita a porta USB (<c>general.usb_port_enabled</c>).</summary>
    Task SetUsbPortEnabledAsync(bool enabled, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>execute_actions.fcgi</c> com o resultado de cada ação. Só vem preenchido com o intertravamento via rede ativo
    /// (<c>allowed</c>/<c>denied</c>); sem ele, a lista é vazia.
    /// </summary>
    Task<IReadOnlyList<DeviceActionStatus>> ExecuteActionsWithStatusAsync(IEnumerable<DeviceAction> actions, CancellationToken cancellationToken = default);

    /// <summary><c>door_state.fcgi</c>: estado (aberta/fechada) das portas por relé ou das SecBoxes.</summary>
    Task<DoorsState> GetDoorStatesAsync(CancellationToken cancellationToken = default);

    /// <summary><c>alarm_status.fcgi</c>: alarme ativo e a causa. Não desliga o alarme.</summary>
    Task<AlarmStatus> GetAlarmStatusAsync(CancellationToken cancellationToken = default);

    /// <summary><c>alarm_status.fcgi</c> com <c>stop</c>: desliga o alarme sonoro.</summary>
    Task StopAlarmAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>GET /reboot</c>: reinicia sem sessão, para quando a aplicação do equipamento não responde. Exige
    /// <c>general.url_reboot_enabled = 1</c>.
    /// </summary>
    Task RebootWithoutSessionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>remote_led_control.fcgi</c> (iDBlock Next): muda o estado dos LEDs. <paramref name="argbColor"/> é usada com
    /// <see cref="TurnstileLedEvent.SetIdleColor"/>, ex.: <c>0xFF00FF00</c> (verde).
    /// </summary>
    Task SetTurnstileLedAsync(TurnstileLedEvent ledEvent, uint? argbColor = null, CancellationToken cancellationToken = default);

    /// <summary><c>system_information.fcgi</c>.</summary>
    Task<AccessSystemInformation> GetSystemInformationAsync(CancellationToken cancellationToken = default);

    /// <summary><c>set_configuration.fcgi</c> com seções arbitrárias, ex.: <c>new { general = new { beep_enabled = "0" } }</c>. Os valores devem ser strings.</summary>
    Task SetConfigurationAsync(object configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>get_configuration.fcgi</c>: lê vários parâmetros de várias seções, ex.:
    /// <c>{ ["general"] = new[] { "beep_enabled", "relay1_timeout" } }</c>. Devolve seção → campo → valor.
    /// </summary>
    Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> GetConfigurationAsync(
        IReadOnlyDictionary<string, IEnumerable<string>> fields, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>get_configuration.fcgi</c> de uma seção, ex.: <c>GetConfigurationAsync("monitor", new[] { "hostname", "port" })</c>.
    /// Campos que o equipamento não devolver ficam fora do dicionário.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> GetConfigurationAsync(string section, IEnumerable<string> fields, CancellationToken cancellationToken = default);

    /// <summary>Aponta o Monitor (notificações assíncronas) para o seu servidor.</summary>
    Task ConfigureMonitorAsync(MonitorConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>Aponta o modo Push (equipamento busca comandos) para o seu servidor.</summary>
    Task ConfigurePushAsync(PushConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Liga o modo online (Pro ou Enterprise) seguindo o passo a passo da documentação: registra o servidor na tabela
    /// <c>devices</c> (reaproveita o registro com o mesmo endereço, se existir), aponta <c>online_client.server_id</c>
    /// para ele e ativa <c>general.online</c>. Devolve o ID do registro do servidor.
    /// </summary>
    Task<long> ConfigureOnlineModeAsync(OnlineModeConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>Volta ao modo standalone (<c>general.online = 0</c>).</summary>
    Task DisableOnlineModeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>remote_user_authorization.fcgi</c>: autoriza (ou nega) um acesso em tempo real, mostrando nome e foto no display.
    /// </summary>
    Task AuthorizeRemotelyAsync(OnlineAuthorization authorization, CancellationToken cancellationToken = default);

    /// <summary><c>set_system_time.fcgi</c> — ajusta o relógio do equipamento (horário local dele).</summary>
    Task SetSystemTimeAsync(DateTime localTime, CancellationToken cancellationToken = default);
}
