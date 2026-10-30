using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ControlId.Rep.Afd;

namespace ControlId.Rep;

/// <summary>
/// Cliente da API dos relógios de ponto REP iDClass. A API roda em HTTPS com certificado
/// autoassinado — fixe a impressão digital dele com <see cref="ControlIdDeviceOptions.ServerCertificateSha256"/>
/// (ou aceite qualquer certificado com <see cref="ControlIdDeviceOptions.AcceptAnyServerCertificate"/>).
/// </summary>
public interface IRepClient : IControlIdClient
{
    /// <summary>Modo usado nas chamadas que o suportam.</summary>
    RepMode Mode { get; }

    /// <summary>Força o login agora (opcional).</summary>
    Task LoginAsync(CancellationToken cancellationToken = default);

    /// <summary>Encerra a sessão.</summary>
    Task LogoutAsync(CancellationToken cancellationToken = default);

    /// <summary><c>get_about.fcgi</c>.</summary>
    Task<RepAbout> GetAboutAsync(CancellationToken cancellationToken = default);

    /// <summary><c>get_system_information.fcgi</c>.</summary>
    Task<RepSystemInformation> GetSystemInformationAsync(CancellationToken cancellationToken = default);

    /// <summary><c>get_system_date_time.fcgi</c>.</summary>
    Task<DateTime> GetDateTimeAsync(CancellationToken cancellationToken = default);

    /// <summary><c>set_system_date_time.fcgi</c>.</summary>
    Task SetDateTimeAsync(DateTime localTime, CancellationToken cancellationToken = default);

    /// <summary><c>count_users.fcgi</c>.</summary>
    Task<int> CountUsersAsync(CancellationToken cancellationToken = default);

    /// <summary><c>load_users.fcgi</c> paginado (máx. 100 por página).</summary>
    Task<RepUserPage> LoadUsersAsync(int limit = 100, int offset = 0, bool includeTemplates = false, CancellationToken cancellationToken = default);

    /// <summary>Percorre todas as páginas de <c>load_users.fcgi</c>.</summary>
    Task<IReadOnlyList<RepUser>> GetAllUsersAsync(bool includeTemplates = false, CancellationToken cancellationToken = default);

    /// <summary><c>add_users.fcgi</c>.</summary>
    Task AddUsersAsync(IEnumerable<RepUser> users, CancellationToken cancellationToken = default);

    /// <summary><c>remove_users.fcgi</c> — PIS (legado) ou CPF (Portaria 671).</summary>
    Task RemoveUsersAsync(IEnumerable<long> identifiers, CancellationToken cancellationToken = default);

    /// <summary><c>get_afd.fcgi</c> a partir de um NSR. Devolve o AFD em texto (decodificado de ISO-8859-1 quando preciso).</summary>
    Task<string> GetAfdFromNsrAsync(long initialNsr, CancellationToken cancellationToken = default);

    /// <summary><c>get_afd.fcgi</c> a partir de uma data. Devolve o AFD em texto.</summary>
    Task<string> GetAfdFromDateAsync(DateTime initialDate, CancellationToken cancellationToken = default);

    /// <summary><c>get_afd.fcgi</c> completo.</summary>
    Task<string> GetAfdAsync(CancellationToken cancellationToken = default);

    /// <summary>AFD a partir de um NSR, já interpretado. Para leitura incremental, use <see cref="AfdFile.LastNsr"/> + 1 na próxima chamada.</summary>
    Task<AfdFile> GetAfdFileFromNsrAsync(long initialNsr, CancellationToken cancellationToken = default);

    /// <summary>AFD a partir de uma data, já interpretado.</summary>
    Task<AfdFile> GetAfdFileFromDateAsync(DateTime initialDate, CancellationToken cancellationToken = default);

    /// <summary>AFD completo, já interpretado.</summary>
    Task<AfdFile> GetAfdFileAsync(CancellationToken cancellationToken = default);

    // ---------------------------------------------------------------- Sessão

    /// <summary><c>session_is_valid.fcgi</c>: a sessão atual ainda vale. Sem sessão, <c>false</c> sem chamar o REP.</summary>
    Task<bool> IsSessionValidAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>change_login.fcgi</c>: troca o usuário e a senha da API (a confirmação vai igual à senha). O REP encerra a sessão
    /// de todos; este cliente passa a usar as novas credenciais e refaz o login na próxima chamada.
    /// </summary>
    Task ChangeLoginAsync(string login, string password, CancellationToken cancellationToken = default);

    // ---------------------------------------------------------------- Usuários

    /// <summary>
    /// <c>add_users.fcgi</c> com <c>do_match</c> (REP iDClass Facial): com <c>true</c>, o REP recusa fotos de pessoas já
    /// cadastradas; com <c>false</c> (o recomendado), o cadastro é bem mais rápido e evitar repetição fica com você.
    /// </summary>
    Task AddUsersAsync(IEnumerable<RepUser> users, bool doMatch, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>update_users.fcgi</c>: altera os usuários (por PIS no modo legado, por CPF na Portaria 671). Só os campos
    /// preenchidos mudam. <paramref name="doMatch"/> vale para fotos, como em <see cref="AddUsersAsync(IEnumerable{RepUser}, bool, CancellationToken)"/>.
    /// </summary>
    Task UpdateUsersAsync(IEnumerable<RepUserUpdate> users, bool? doMatch = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>load_users.fcgi</c> pelos identificadores (<c>users_pis</c> no modo legado, <c>users_cpf</c> na Portaria 671).
    /// Mais de 100 viram várias chamadas.
    /// </summary>
    Task<IReadOnlyList<RepUser>> GetUsersAsync(IEnumerable<long> identifiers, bool includeTemplates = false, CancellationToken cancellationToken = default);

    /// <summary>Todos os usuários, lidos em páginas de 100 sob demanda (para bases grandes, no lugar de <see cref="GetAllUsersAsync"/>).</summary>
    IAsyncEnumerable<RepUser> EnumerateUsersAsync(bool includeTemplates = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>import_users_csv.fcgi</c>: cadastra os usuários do CSV que ainda não existem (os existentes ficam como estão). O
    /// arquivo segue o formato de <see cref="ExportUsersCsvAsync"/>.
    /// </summary>
    Task<RepCsvImportResult> ImportUsersCsvAsync(byte[] csv, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>export_users_csv.fcgi</c>: todos os usuários em CSV, uma linha por usuário: PIS (ou CPF na Portaria 671); nome;
    /// administrador; matrícula; RFID; código; senha; código de barras; digitais.
    /// </summary>
    Task<string> ExportUsersCsvAsync(CancellationToken cancellationToken = default);

    /// <summary><c>remove_admins.fcgi</c>: tira o privilégio de administrador de todos os usuários, sem volta.</summary>
    Task RemoveAdminsAsync(DataLossConfirmation confirmation, CancellationToken cancellationToken = default);

    // ---------------------------------------------------------------- Empregador

    /// <summary><c>load_company.fcgi</c>: o empregador cadastrado.</summary>
    Task<RepCompany> GetCompanyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>edit_company.fcgi</c>: cadastra o empregador. Com <see cref="RepCompanyDocumentType.Cnpj"/>, o REP confere os dígitos
    /// verificadores e responde erro se não baterem.
    /// </summary>
    Task SetCompanyAsync(RepCompany company, CancellationToken cancellationToken = default);

    // ---------------------------------------------------------------- Sistema

    /// <summary><c>get_info.fcgi</c>: contadores de usuários, administradores, digitais, senhas, cartões e impressão.</summary>
    Task<RepInfo> GetInfoAsync(CancellationToken cancellationToken = default);

    /// <summary><c>get_system_configuration.fcgi</c>: beep, animação, comprovante curto e identificação 1:1.</summary>
    Task<RepSystemConfiguration> GetSystemConfigurationAsync(CancellationToken cancellationToken = default);

    /// <summary><c>set_buzzer_beep.fcgi</c>: liga ou desliga o beep.</summary>
    Task SetBeepEnabledAsync(bool enabled, CancellationToken cancellationToken = default);

    /// <summary><c>set_animation_screen.fcgi</c>: liga ou desliga a animação da tela.</summary>
    Task SetAnimationEnabledAsync(bool enabled, CancellationToken cancellationToken = default);

    /// <summary><c>set_ticket_size.fcgi</c>: comprovante curto (<c>economic_receipt_enabled</c>).</summary>
    Task SetEconomicReceiptEnabledAsync(bool enabled, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>set_identification_type.fcgi</c>: com <c>true</c>, identificação 1:1 (senha ou cartão antes da digital); com
    /// <c>false</c>, 1:N.
    /// </summary>
    Task SetOneToOneIdentificationAsync(bool enabled, CancellationToken cancellationToken = default);

    /// <summary><c>get_idcloud.fcgi</c>: o iDCloud está ligado.</summary>
    Task<bool> GetIdCloudEnabledAsync(CancellationToken cancellationToken = default);

    /// <summary><c>set_idcloud.fcgi</c>: liga ou desliga o iDCloud.</summary>
    Task SetIdCloudEnabledAsync(bool enabled, CancellationToken cancellationToken = default);

    /// <summary><c>get_public_key.fcgi</c>: chave pública do REP.</summary>
    Task<RepPublicKey> GetPublicKeyAsync(CancellationToken cancellationToken = default);

    /// <summary><c>get_coil_paper.fcgi</c>: bobina restante.</summary>
    Task<int> GetCoilPaperAsync(CancellationToken cancellationToken = default);

    /// <summary><c>set_coil_paper.fcgi</c>: informa o tamanho da bobina colocada.</summary>
    Task SetCoilPaperAsync(int coilPaper, CancellationToken cancellationToken = default);

    /// <summary><c>get_system_network.fcgi</c>: IP, máscara, gateway, DNS, porta e DHCP.</summary>
    Task<RepNetworkSettings> GetNetworkAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>set_system_network.fcgi</c>: altera a rede. Se o IP ou a porta mudarem, este cliente perde o REP: crie outro com o
    /// novo endereço. <see cref="RepNetworkSettings.UseDhcp"/> vai sempre, porque o REP assume <c>false</c> se faltar.
    /// </summary>
    Task SetNetworkAsync(RepNetworkSettings settings, CancellationToken cancellationToken = default);

    /// <summary><c>get_system_daylight_saving_time.fcgi</c>: o período do horário de verão; <c>null</c> se não houver.</summary>
    Task<RepDaylightSavingTime?> GetDaylightSavingTimeAsync(CancellationToken cancellationToken = default);

    /// <summary><c>set_system_daylight_saving_time.fcgi</c>: define o período do horário de verão.</summary>
    Task SetDaylightSavingTimeAsync(RepDaylightSavingTime period, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>get_system_date_time.fcgi</c> com o fuso horário (Portaria 671). Exige <see cref="RepMode.Portaria671"/>.
    /// </summary>
    Task<DateTimeOffset> GetDateTimeOffsetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>set_system_date_time.fcgi</c> com o fuso horário (<c>sHHMM</c>, em múltiplos de 15 minutos). Exige
    /// <see cref="RepMode.Portaria671"/>.
    /// </summary>
    Task SetDateTimeAsync(DateTimeOffset value, CancellationToken cancellationToken = default);

    // ---------------------------------------------------------------- AFD

    /// <summary><c>get_afd.fcgi</c> com todas as opções (NSR ou data inicial e <c>headerTop</c>). Devolve o AFD em texto.</summary>
    Task<string> GetAfdAsync(RepAfdQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>get_afd.fcgi</c> como stream, lido sob demanda, para AFDs grandes. Feche o stream ao terminar: enquanto aberto, ele
    /// ocupa uma das requisições simultâneas permitidas ao REP. A codificação é a do cabeçalho da resposta ou, sem ela,
    /// ISO-8859-1 (a das portarias).
    /// </summary>
    Task<System.IO.Stream> OpenAfdStreamAsync(RepAfdQuery? query = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registros do AFD lidos e interpretados um a um, sem carregar o arquivo inteiro. Não valida o trailer; para isso,
    /// use <see cref="GetAfdFileAsync"/>.
    /// </summary>
    IAsyncEnumerable<AfdRecord> EnumerateAfdRecordsAsync(RepAfdQuery? query = null, CancellationToken cancellationToken = default);

    // ---------------------------------------------------------------- Biometria

    /// <summary><c>template_extract.fcgi</c>: extrai o template biométrico de uma imagem bitmap da digital.</summary>
    Task<RepTemplateExtraction> ExtractTemplateAsync(byte[] bitmap, int width, int height, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>template_merge.fcgi</c>: combina as três leituras de uma digital num template final para o usuário (PIS no modo
    /// legado, CPF na Portaria 671). <paramref name="newTemplates"/> são templates já combinados e ainda não gravados; com
    /// <paramref name="ignoreExistingTemplates"/>, os que o usuário já tem não entram na checagem.
    /// </summary>
    Task<RepTemplateMergeResult> MergeTemplatesAsync(
        long userIdentifier, IEnumerable<string> templates, IEnumerable<string>? newTemplates = null, bool ignoreExistingTemplates = false,
        CancellationToken cancellationToken = default);

    // ---------------------------------------------------------------- Facial (REP iDClass Facial)

    /// <summary><c>get_facial_module_config.fcgi</c>: distância de identificação, LEDs e calibração.</summary>
    Task<RepFacialModuleConfig> GetFacialModuleConfigAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>set_facial_module_config.fcgi</c>: o REP exige os três parâmetros; leia com <see cref="GetFacialModuleConfigAsync"/>,
    /// altere e envie.
    /// </summary>
    Task SetFacialModuleConfigAsync(RepFacialModuleConfig config, CancellationToken cancellationToken = default);

    /// <summary><c>set_facial_limit.fcgi</c>: aplica a licença de 10 mil faces, com a senha fornecida pela Control iD.</summary>
    Task SetFacialLimitAsync(string password, CancellationToken cancellationToken = default);

    /// <summary><c>get_about_facial.fcgi</c>: série, firmware e capacidade do módulo facial.</summary>
    Task<RepFacialAbout> GetAboutFacialAsync(CancellationToken cancellationToken = default);

    /// <summary><c>facial_enroller_state.fcgi</c>: há cadastro facial em andamento (pela tela ou remoto).</summary>
    Task<RepFacialEnrollerState> GetFacialEnrollerStateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>remote_enroll.fcgi</c>: a tela do REP passa a pedir a face do funcionário. Acompanhe com
    /// <see cref="GetFacialEnrollerStateAsync"/>. Vai sempre com <c>mode=671</c>, que o endpoint exige.
    /// </summary>
    Task StartRemoteEnrollAsync(long cpf, CancellationToken cancellationToken = default);

    /// <summary><c>cancel_remote_enroll.fcgi</c>: interrompe o cadastro facial remoto.</summary>
    Task CancelRemoteEnrollAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>user_set_image.fcgi</c>: grava a foto do funcionário (já cadastrado), de 160x160 a 1920x1080 px, com o rosto inteiro.
    /// Com <paramref name="doMatch"/>, o REP recusa se a pessoa já tiver foto (mais lento).
    /// </summary>
    Task SetUserImageAsync(long cpf, byte[] image, DateTimeOffset? timestamp = null, bool doMatch = false, CancellationToken cancellationToken = default);

    /// <summary><c>user_get_image.fcgi</c>: a foto do funcionário. O REP responde erro se não houver foto.</summary>
    Task<byte[]> GetUserImageAsync(long cpf, CancellationToken cancellationToken = default);

    /// <summary><c>user_destroy_image.fcgi</c>: apaga a foto do funcionário. O REP responde erro se não houver foto.</summary>
    Task DeleteUserImageAsync(long cpf, CancellationToken cancellationToken = default);

    /// <summary><c>user_import_image.fcgi</c>: importa a foto num backup, depois de importar o CSV com <see cref="ImportUsersCsvAsync"/>.</summary>
    Task ImportUserImageAsync(long cpf, byte[] image, CancellationToken cancellationToken = default);
}
