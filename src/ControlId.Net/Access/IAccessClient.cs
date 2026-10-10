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
