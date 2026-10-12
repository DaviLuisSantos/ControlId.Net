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

    /// <summary>
    /// <c>user_set_image.fcgi</c>: grava a foto (JPEG ou PNG, menor que 1 MB). Nos equipamentos faciais a foto vira o
    /// cadastro facial e o resultado traz notas de qualidade e erros (rosto não detectado, já cadastrado...); nos
    /// demais o equipamento não responde nada e o resultado vem como sucesso.
    /// </summary>
    /// <param name="userId">Usuário.</param>
    /// <param name="image">Imagem.</param>
    /// <param name="rejectExistingFace">Recusa se o rosto já pertence a outro usuário (<c>match=1</c>).</param>
    /// <param name="timestamp">Data do cadastro (obrigatória na API); sem ela, o momento atual.</param>
    /// <param name="cancellationToken">Cancelamento.</param>
    Task<FaceImageResult> SetUserImageAsync(
        long userId, byte[] image, bool rejectExistingFace = false, DateTimeOffset? timestamp = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>user_set_image_list.fcgi</c>: grava fotos em lote (a documentação limita a requisição a 1 MB na página de
    /// fotos e a 2 MB na de reconhecimento facial). Um resultado por foto, quando o equipamento é facial.
    /// </summary>
    Task<IReadOnlyList<FaceImageResult>> SetUserImagesAsync(
        IEnumerable<UserImage> images, bool rejectExistingFaces = false, CancellationToken cancellationToken = default);

    /// <summary><c>user_test_image.fcgi</c>: testa se a foto seria aceita no cadastro facial, sem gravar.</summary>
    Task<FaceImageResult> TestUserImageAsync(byte[] image, CancellationToken cancellationToken = default);

    /// <summary>Baixa a foto (JPEG) do usuário (<c>user_get_image.fcgi</c>).</summary>
    Task<byte[]> GetUserImageAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary><c>user_get_image.fcgi</c> com <c>get_timestamp</c>: a foto e a data de cadastro.</summary>
    Task<UserImageData> GetUserImageWithTimestampAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary><c>user_get_image_list.fcgi</c>: fotos de até 100 usuários por chamada, com o erro de cada um que falhar.</summary>
    Task<IReadOnlyList<UserImageEntry>> GetUserImagesAsync(IEnumerable<long> userIds, CancellationToken cancellationToken = default);

    /// <summary>IDs dos usuários que têm foto (<c>user_list_images.fcgi</c>, firmware 3.10.0+).</summary>
    Task<IReadOnlyList<long>> GetUserIdsWithImageAsync(CancellationToken cancellationToken = default);

    /// <summary><c>user_list_images.fcgi</c> com <c>get_timestamp</c>: usuários com foto e a data de cada uma (para sincronização).</summary>
    Task<IReadOnlyList<UserImageInfo>> GetUserImageInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>Remove a foto do usuário (<c>user_destroy_image.fcgi</c>); no facial, remove também o cadastro facial.</summary>
    Task DeleteUserImageAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary>Remove as fotos dos usuários informados.</summary>
    Task DeleteUserImagesAsync(IEnumerable<long> userIds, CancellationToken cancellationToken = default);

    /// <summary>Remove as fotos que não pertencem a nenhum usuário (<c>dangling</c>).</summary>
    Task DeleteDanglingUserImagesAsync(CancellationToken cancellationToken = default);

    /// <summary>Remove as fotos de todos os usuários.</summary>
    Task DeleteAllUserImagesAsync(CancellationToken cancellationToken = default);

    /// <summary><c>face_template_extract.fcgi</c>: extrai o template facial de uma imagem (em <see cref="FaceImageResult.FaceTemplate"/>).</summary>
    Task<FaceImageResult> ExtractFaceTemplateAsync(byte[] image, CancellationToken cancellationToken = default);

    /// <summary><c>face_template_extract_list.fcgi</c>: extração em lote; cada resultado traz o <see cref="FaceImageResult.Id"/> do item.</summary>
    Task<IReadOnlyList<FaceImageResult>> ExtractFaceTemplatesAsync(IEnumerable<FaceTemplateImage> images, CancellationToken cancellationToken = default);

    /// <summary><c>face_template_enroll.fcgi</c>: cadastra templates faciais nos usuários; um resultado por usuário.</summary>
    Task<IReadOnlyList<FaceImageResult>> EnrollFaceTemplatesAsync(
        IEnumerable<FaceTemplateEnrollment> templates, bool rejectExistingFaces = false, CancellationToken cancellationToken = default);

    /// <summary><c>face_template_destroy.fcgi</c>: remove os templates faciais dos usuários informados.</summary>
    Task DeleteFaceTemplatesAsync(IEnumerable<long> userIds, CancellationToken cancellationToken = default);

    /// <summary><c>face_template_destroy.fcgi</c> com <c>all</c>: remove todos os templates faciais.</summary>
    Task DeleteAllFaceTemplatesAsync(CancellationToken cancellationToken = default);

    /// <summary><c>save_screenshot.fcgi</c>: captura da câmera (ângulo maior que o do display), em PNG.</summary>
    Task<byte[]> CaptureCameraAsync(CameraType camera = CameraType.Rgb, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>template_sync_init.fcgi</c>: suspende a sincronização de templates antes de carregar uma base grande de
    /// usuários com biometria. Termine com <see cref="EndTemplateSyncAsync"/>.
    /// </summary>
    Task StartTemplateSyncAsync(CancellationToken cancellationToken = default);

    /// <summary><c>template_sync_end.fcgi</c>: conclui a carga iniciada por <see cref="StartTemplateSyncAsync"/>.</summary>
    Task EndTemplateSyncAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>export_objects.fcgi</c>: exporta os objetos informados para sincronização entre equipamentos. A documentação não
    /// descreve o formato da resposta (o texto fala em zip com CSV), então ela volta em bytes.
    /// </summary>
    Task<byte[]> ExportObjectsAsync(IEnumerable<ExportObjectRequest> objects, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// <c>send_video.fcgi</c>: envia o vídeo do modo propaganda (MP4 vertical, de preferência 800x1280, até 20 MB),
    /// dividido em partes de até 2 MB como a API exige. Depois, ligue com <see cref="SetAdvertisingModeAsync"/>.
    /// </summary>
    Task UploadAdvertisingVideoAsync(byte[] mp4, CancellationToken cancellationToken = default);

    /// <summary><c>set_custom_video.fcgi</c>: liga ou desliga o modo propaganda (exige vídeo carregado).</summary>
    Task SetAdvertisingModeAsync(bool enabled, CancellationToken cancellationToken = default);

    /// <summary><c>remove_custom_video.fcgi</c>: remove o vídeo do modo propaganda.</summary>
    Task RemoveAdvertisingVideoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>set_audio_access_message.fcgi</c> (iDFace): envia o áudio personalizado de um evento (até 5 MB), em partes de até 2 MB.
    /// Para usá-lo, configure <c>buzzer.audio_message_*</c> como <c>custom</c>.
    /// </summary>
    Task UploadAccessAudioAsync(AccessAudioEvent audioEvent, byte[] audio, CancellationToken cancellationToken = default);

    /// <summary><c>get_audio_access_message.fcgi</c>: baixa o áudio personalizado do evento.</summary>
    Task<byte[]> GetAccessAudioAsync(AccessAudioEvent audioEvent, CancellationToken cancellationToken = default);

    /// <summary><c>has_audio_access_messages.fcgi</c>: quais eventos já têm áudio personalizado.</summary>
    Task<AccessAudioMessages> GetAccessAudioStatusAsync(CancellationToken cancellationToken = default);

    /// <summary><c>make_sip_call.fcgi</c> (iDFace): liga para o ramal; só funciona na tela inicial ou em streaming de identificação.</summary>
    Task MakeSipCallAsync(string target, CancellationToken cancellationToken = default);

    /// <summary><c>finalize_sip_call.fcgi</c>: encerra a chamada em andamento.</summary>
    Task FinalizeSipCallAsync(CancellationToken cancellationToken = default);

    /// <summary><c>get_sip_status.fcgi</c>: registro no servidor SIP e se há chamada em andamento.</summary>
    Task<SipStatus> GetSipStatusAsync(CancellationToken cancellationToken = default);

    /// <summary><c>set_pjsip_audio_message.fcgi</c>: envia o som de chamada personalizado (WAV, até 5 MB), em partes de até 2 MB.</summary>
    Task UploadSipCallAudioAsync(byte[] wav, CancellationToken cancellationToken = default);

    /// <summary><c>get_pjsip_audio_message.fcgi</c>: baixa o som de chamada personalizado (WAV).</summary>
    Task<byte[]> GetSipCallAudioAsync(CancellationToken cancellationToken = default);

    /// <summary><c>has_pjsip_audio_message.fcgi</c>: há som de chamada personalizado.</summary>
    Task<bool> HasSipCallAudioAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// URL do vídeo RTSP da câmera do iDFace (<c>rtsp://[usuário:senha@]ip:porta/main_stream</c>), para abrir num cliente
    /// RTSP. Exige <c>onvif.rtsp_enabled = 1</c> (vale após reiniciar).
    /// </summary>
    Uri GetRtspStreamUri(int port = 554, string? username = null, string? password = null);

    /// <summary><c>connection_test.fcgi</c>: o equipamento tem acesso à internet.</summary>
    Task<bool> TestInternetConnectionAsync(CancellationToken cancellationToken = default);

    /// <summary><c>ping_test.fcgi</c>: executa um ping no equipamento e devolve a saída do comando.</summary>
    Task<string> PingAsync(string destination, CancellationToken cancellationToken = default);

    /// <summary><c>nslookup_test.fcgi</c>: consulta DNS a partir do equipamento e devolve a saída do comando.</summary>
    Task<string> NsLookupAsync(string destination, string dnsServer, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>set_network_interlock.fcgi</c>: liga o intertravamento via rede e as exceções para abertura pela API e pela
    /// botoeira (a API exige as três opções juntas). Os equipamentos remotos são <see cref="NetworkInterlockingRule"/>.
    /// </summary>
    Task SetNetworkInterlockAsync(bool enabled, bool bypassForApi, bool bypassForExitButton, CancellationToken cancellationToken = default);

    /// <summary><c>is_valid_biometry.fcgi</c>: o template de digital é válido.</summary>
    Task<bool> IsValidBiometryAsync(string template, CancellationToken cancellationToken = default);

    /// <summary><c>set_vpn_information.fcgi</c>: liga o OpenVPN e define login manual.</summary>
    Task SetVpnAsync(VpnSettings settings, CancellationToken cancellationToken = default);

    /// <summary><c>get_vpn_information.fcgi</c>: configuração atual do OpenVPN.</summary>
    Task<VpnInformation> GetVpnInformationAsync(CancellationToken cancellationToken = default);

    /// <summary><c>get_vpn_status.fcgi</c>: estado da conexão OpenVPN.</summary>
    Task<VpnStatus> GetVpnStatusAsync(CancellationToken cancellationToken = default);

    /// <summary><c>set_vpn_file.fcgi</c>: envia o arquivo de configuração (<c>.conf</c>) ou o <c>.zip</c> com certificados e chaves.</summary>
    Task UploadVpnFileAsync(byte[] file, VpnFileType fileType, CancellationToken cancellationToken = default);

    /// <summary><c>has_vpn_file.fcgi</c>: já existe arquivo de configuração OpenVPN.</summary>
    Task<bool> HasVpnFileAsync(CancellationToken cancellationToken = default);

    /// <summary><c>get_vpn_file.fcgi</c>: baixa os arquivos de exemplo de configuração (zip).</summary>
    Task<byte[]> GetVpnExampleFilesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>ssl_certificate_change.fcgi</c>: instala o certificado do servidor web (PEM com chave e certificado). Habilite o SSL
    /// antes, em <see cref="SetNetworkAsync"/>.
    /// </summary>
    Task UploadSslCertificateAsync(byte[] pem, CancellationToken cancellationToken = default);

    /// <summary><c>configure_802_1X.fcgi</c>: autenticação 802.1X (PEAP) da porta de rede.</summary>
    Task Configure8021XAsync(
        bool enabled, string? login = null, string? password = null,
        Ieee8021XInnerAuthentication innerAuthentication = Ieee8021XInnerAuthentication.MsChapV2, CancellationToken cancellationToken = default);

    /// <summary><c>set_osdp_installation_mode.fcgi</c>: modo de instalação do OSDP (permite trocar as chaves de criptografia).</summary>
    Task SetOsdpInstallationModeAsync(bool enabled, CancellationToken cancellationToken = default);

    /// <summary><c>change_idcloud_code.fcgi</c>: gera um novo código de verificação do iDCloud.</summary>
    Task ChangeIdCloudCodeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Aplica uma licença de capacidade do iDFace / iDFace Max (<c>upgrade_*_face_templates.fcgi</c>). A senha é fornecida
    /// pela Control iD na compra; <see cref="FaceLicense.Pro10k"/> é pré-requisito das demais.
    /// </summary>
    Task ApplyFaceLicenseAsync(FaceLicense license, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>idflex_upgrade_enterprise.fcgi</c>: habilita a versão Enterprise no iDFlex/iDAccess Nano (necessária para o modo
    /// online Enterprise), com a senha fornecida pela Control iD.
    /// </summary>
    Task UpgradeToEnterpriseAsync(string password, CancellationToken cancellationToken = default);

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
