using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ControlId.Internal;
using ControlId.Rep.Afd;

namespace ControlId.Rep;

/// <inheritdoc cref="IRepClient"/>
public sealed class RepClient : IRepClient
{
    private const int MaxPageSize = 100;
    private readonly ControlIdTransport _transport;

    /// <summary>Cria o cliente. Se <paramref name="httpClient"/> for informado, ele não é descartado por este cliente.</summary>
    public RepClient(ControlIdDeviceOptions options, RepMode mode = RepMode.Portaria671, HttpClient? httpClient = null)
        : this(options, mode, null, httpClient)
    {
    }

    // Usado pelo pacote de DI: o ILoggerFactory do contêiner vale só se as opções não trouxerem o seu.
    internal RepClient(ControlIdDeviceOptions options, RepMode mode, Microsoft.Extensions.Logging.ILoggerFactory? loggerFactory, HttpClient? httpClient)
    {
        _transport = new ControlIdTransport(options, httpClient, loggerFactory);
        Mode = mode;
    }

    /// <summary>Atalho: <c>new RepClient("https://192.168.0.129")</c>. Aceita o certificado autoassinado do REP.</summary>
    public RepClient(string baseAddress, string login = "admin", string password = "admin", RepMode mode = RepMode.Portaria671)
        : this(new ControlIdDeviceOptions
        {
            BaseAddress = new Uri(baseAddress),
            Login = login,
            Password = password,
            AcceptAnyServerCertificate = true,
        }, mode)
    {
    }

    /// <inheritdoc/>
    public RepMode Mode { get; }

    private string? ModeQuery => Mode == RepMode.Portaria671 ? "mode=671" : null;

    /// <inheritdoc/>
    public Task LoginAsync(CancellationToken cancellationToken = default) => _transport.LoginAsync(cancellationToken);

    /// <inheritdoc/>
    public Task LogoutAsync(CancellationToken cancellationToken = default) => _transport.LogoutAsync(cancellationToken);

    /// <inheritdoc/>
    public Task<RepAbout> GetAboutAsync(CancellationToken cancellationToken = default) =>
        _transport.PostAsync<RepAbout>("get_about.fcgi", null, cancellationToken);

    /// <inheritdoc/>
    public Task<RepSystemInformation> GetSystemInformationAsync(CancellationToken cancellationToken = default) =>
        _transport.PostAsync<RepSystemInformation>("get_system_information.fcgi", null, cancellationToken);

    /// <inheritdoc/>
    public async Task<DateTime> GetDateTimeAsync(CancellationToken cancellationToken = default)
    {
        var value = await _transport.PostAsync<DeviceDateTime>("get_system_date_time.fcgi", null, cancellationToken).ConfigureAwait(false);
        return value.ToDateTime();
    }

    /// <inheritdoc/>
    public Task SetDateTimeAsync(DateTime localTime, CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync("set_system_date_time.fcgi", DeviceDateTime.From(localTime), cancellationToken);

    /// <inheritdoc/>
    public async Task<int> CountUsersAsync(CancellationToken cancellationToken = default)
    {
        var response = await _transport.PostAsync<CountResponse>("count_users.fcgi", null, cancellationToken).ConfigureAwait(false);
        return response.Count;
    }

    /// <inheritdoc/>
    public Task<RepUserPage> LoadUsersAsync(int limit = MaxPageSize, int offset = 0, bool includeTemplates = false, CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxPageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), $"O REP aceita de 1 a {MaxPageSize} usuários por página.");
        }

        var body = new LoadUsersRequest { Limit = limit, Offset = offset, Templates = includeTemplates ? true : null };
        return _transport.PostAsync<RepUserPage>("load_users.fcgi", body, cancellationToken, ModeQuery);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RepUser>> GetAllUsersAsync(bool includeTemplates = false, CancellationToken cancellationToken = default)
    {
        var all = new List<RepUser>();
        while (true)
        {
            var page = await LoadUsersAsync(MaxPageSize, all.Count, includeTemplates, cancellationToken).ConfigureAwait(false);
            all.AddRange(page.Users);
            if (page.Users.Count < MaxPageSize || all.Count >= page.Count)
            {
                return all;
            }
        }
    }

    /// <inheritdoc/>
    public Task AddUsersAsync(IEnumerable<RepUser> users, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object> { ["users"] = users.ToList() };
        return _transport.PostRawAsync("add_users.fcgi", body, cancellationToken, ModeQuery);
    }

    /// <inheritdoc/>
    public Task RemoveUsersAsync(IEnumerable<long> identifiers, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object> { ["users"] = identifiers.ToList() };
        return _transport.PostRawAsync("remove_users.fcgi", body, cancellationToken, ModeQuery);
    }

    /// <inheritdoc/>
    public Task<string> GetAfdFromNsrAsync(long initialNsr, CancellationToken cancellationToken = default) =>
        GetAfdTextAsync(new Dictionary<string, object> { ["initial_nsr"] = initialNsr }, cancellationToken);

    /// <inheritdoc/>
    public Task<string> GetAfdFromDateAsync(DateTime initialDate, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object>
        {
            ["initial_date"] = new Dictionary<string, int>
            {
                ["year"] = initialDate.Year,
                ["month"] = initialDate.Month,
                ["day"] = initialDate.Day,
            },
        };

        return GetAfdTextAsync(body, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<string> GetAfdAsync(CancellationToken cancellationToken = default) =>
        GetAfdTextAsync(null, cancellationToken);

    /// <inheritdoc/>
    public async Task<AfdFile> GetAfdFileFromNsrAsync(long initialNsr, CancellationToken cancellationToken = default) =>
        AfdParser.Parse(await GetAfdFromNsrAsync(initialNsr, cancellationToken).ConfigureAwait(false));

    /// <inheritdoc/>
    public async Task<AfdFile> GetAfdFileFromDateAsync(DateTime initialDate, CancellationToken cancellationToken = default) =>
        AfdParser.Parse(await GetAfdFromDateAsync(initialDate, cancellationToken).ConfigureAwait(false));

    /// <inheritdoc/>
    public async Task<AfdFile> GetAfdFileAsync(CancellationToken cancellationToken = default) =>
        AfdParser.Parse(await GetAfdAsync(cancellationToken).ConfigureAwait(false));

    /// <inheritdoc/>
    public Task<T> SendAsync<T>(string endpoint, object? body = null, string? queryString = null, CancellationToken cancellationToken = default) =>
        GenericEndpoints.SendAsync<T>(_transport, endpoint, body, queryString, cancellationToken);

    /// <inheritdoc/>
    public Task<string> SendRawAsync(string endpoint, object? body = null, string? queryString = null, CancellationToken cancellationToken = default) =>
        GenericEndpoints.SendRawAsync(_transport, endpoint, body, queryString, cancellationToken);

    /// <inheritdoc/>
    public Task<string> SendBytesAsync(
        string endpoint, byte[] content, string contentType = "application/octet-stream", string? queryString = null, CancellationToken cancellationToken = default) =>
        GenericEndpoints.SendBytesAsync(_transport, endpoint, content, contentType, queryString, cancellationToken);

    /// <inheritdoc/>
    public Task<byte[]> DownloadAsync(
        string endpoint, object? body = null, string? queryString = null, HttpMethod? method = null, CancellationToken cancellationToken = default) =>
        GenericEndpoints.DownloadAsync(_transport, endpoint, body, queryString, method, cancellationToken);


    // ---------------------------------------------------------------- Sessão

    /// <inheritdoc/>
    public async Task<bool> IsSessionValidAsync(CancellationToken cancellationToken = default)
    {
        var session = _transport.Session;
        if (session is null)
        {
            return false;
        }

        var text = await _transport.PostRawAsync(
            "session_is_valid.fcgi", null, cancellationToken, "session=" + Uri.EscapeDataString(session), requiresSession: false).ConfigureAwait(false);
        return ControlIdJson.Deserialize<SessionIsValidResponse>(text).SessionIsValid;
    }

    /// <inheritdoc/>
    public async Task ChangeLoginAsync(string login, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(login))
        {
            throw new ArgumentException("Informe o novo usuário.", nameof(login));
        }

        if (password is null)
        {
            throw new ArgumentNullException(nameof(password));
        }

        var body = new Dictionary<string, object> { ["login"] = login, ["password"] = password, ["confirmation"] = password };
        await _transport.PostRawAsync("change_login.fcgi", body, cancellationToken).ConfigureAwait(false);
        _transport.UpdateCredentials(login, password);
    }

    // ---------------------------------------------------------------- Usuários

    /// <inheritdoc/>
    public Task AddUsersAsync(IEnumerable<RepUser> users, bool doMatch, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object>
        {
            ["do_match"] = doMatch,
            ["users"] = (users ?? throw new ArgumentNullException(nameof(users))).ToList(),
        };
        return _transport.PostRawAsync("add_users.fcgi", body, cancellationToken, ModeQuery);
    }

    /// <inheritdoc/>
    public Task UpdateUsersAsync(IEnumerable<RepUserUpdate> users, bool? doMatch = null, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object> { ["users"] = (users ?? throw new ArgumentNullException(nameof(users))).ToList() };
        if (doMatch is { } match)
        {
            body["do_match"] = match;
        }

        return _transport.PostRawAsync("update_users.fcgi", body, cancellationToken, ModeQuery);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RepUser>> GetUsersAsync(IEnumerable<long> identifiers, bool includeTemplates = false, CancellationToken cancellationToken = default)
    {
        var ids = (identifiers ?? throw new ArgumentNullException(nameof(identifiers))).ToList();
        var key = Mode == RepMode.Portaria671 ? "users_cpf" : "users_pis";
        var all = new List<RepUser>();
        for (var start = 0; start < ids.Count; start += MaxPageSize)
        {
            var body = new Dictionary<string, object> { [key] = ids.Skip(start).Take(MaxPageSize).ToList() };
            if (includeTemplates)
            {
                body["templates"] = true;
            }

            var page = await _transport.PostAsync<RepUserPage>("load_users.fcgi", body, cancellationToken, ModeQuery).ConfigureAwait(false);
            all.AddRange(page.Users);
        }

        return all;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<RepUser> EnumerateUsersAsync(bool includeTemplates = false, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var offset = 0;
        while (true)
        {
            var page = await LoadUsersAsync(MaxPageSize, offset, includeTemplates, cancellationToken).ConfigureAwait(false);
            foreach (var user in page.Users)
            {
                yield return user;
            }

            offset += page.Users.Count;
            if (page.Users.Count < MaxPageSize || offset >= page.Count)
            {
                yield break;
            }
        }
    }

    /// <inheritdoc/>
    public async Task<RepCsvImportResult> ImportUsersCsvAsync(byte[] csv, CancellationToken cancellationToken = default)
    {
        if (csv is null || csv.Length == 0)
        {
            throw new ArgumentException("Informe o conteúdo do CSV.", nameof(csv));
        }

        var text = await _transport.PostBytesAsync("import_users_csv.fcgi", csv, "application/octet-stream", cancellationToken, ModeQuery).ConfigureAwait(false);
        return ControlIdJson.Deserialize<RepCsvImportResult>(text);
    }

    /// <inheritdoc/>
    public async Task<string> ExportUsersCsvAsync(CancellationToken cancellationToken = default)
    {
        var (content, charSet) = await _transport.PostForBytesAsync("export_users_csv.fcgi", null, cancellationToken, ModeQuery).ConfigureAwait(false);
        return TextDecoding.Decode(content, charSet);
    }

    /// <inheritdoc/>
    public Task RemoveAdminsAsync(DataLossConfirmation confirmation, CancellationToken cancellationToken = default)
    {
        DataLoss.Require(confirmation, nameof(RemoveAdminsAsync));
        return _transport.PostRawAsync("remove_admins.fcgi", null, cancellationToken);
    }

    // ---------------------------------------------------------------- Empregador

    /// <inheritdoc/>
    public async Task<RepCompany> GetCompanyAsync(CancellationToken cancellationToken = default)
    {
        var response = await _transport.PostAsync<CompanyResponse>("load_company.fcgi", null, cancellationToken).ConfigureAwait(false);
        return response.Company ?? new RepCompany();
    }

    /// <inheritdoc/>
    public Task SetCompanyAsync(RepCompany company, CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync(
            "edit_company.fcgi",
            new Dictionary<string, object> { ["company"] = company ?? throw new ArgumentNullException(nameof(company)) },
            cancellationToken);

    // ---------------------------------------------------------------- Sistema

    /// <inheritdoc/>
    public Task<RepInfo> GetInfoAsync(CancellationToken cancellationToken = default) =>
        _transport.PostAsync<RepInfo>("get_info.fcgi", null, cancellationToken);

    /// <inheritdoc/>
    public Task<RepSystemConfiguration> GetSystemConfigurationAsync(CancellationToken cancellationToken = default) =>
        _transport.PostAsync<RepSystemConfiguration>("get_system_configuration.fcgi", null, cancellationToken);

    /// <inheritdoc/>
    public Task SetBeepEnabledAsync(bool enabled, CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync("set_buzzer_beep.fcgi", new Dictionary<string, object> { ["enabled"] = enabled }, cancellationToken);

    /// <inheritdoc/>
    public Task SetAnimationEnabledAsync(bool enabled, CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync("set_animation_screen.fcgi", new Dictionary<string, object> { ["enabled"] = enabled }, cancellationToken);

    /// <inheritdoc/>
    public Task SetEconomicReceiptEnabledAsync(bool enabled, CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync("set_ticket_size.fcgi", new Dictionary<string, object> { ["economic_receipt_enabled"] = enabled }, cancellationToken);

    /// <inheritdoc/>
    public Task SetOneToOneIdentificationAsync(bool enabled, CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync("set_identification_type.fcgi", new Dictionary<string, object> { ["one_to_one_enabled"] = enabled }, cancellationToken);

    /// <inheritdoc/>
    public async Task<bool> GetIdCloudEnabledAsync(CancellationToken cancellationToken = default) =>
        (await _transport.PostAsync<EnableResponse>("get_idcloud.fcgi", null, cancellationToken).ConfigureAwait(false)).Enable;

    /// <inheritdoc/>
    public Task SetIdCloudEnabledAsync(bool enabled, CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync("set_idcloud.fcgi", new Dictionary<string, object> { ["enable"] = enabled }, cancellationToken);

    /// <inheritdoc/>
    public Task<RepPublicKey> GetPublicKeyAsync(CancellationToken cancellationToken = default) =>
        _transport.PostAsync<RepPublicKey>("get_public_key.fcgi", null, cancellationToken);

    /// <inheritdoc/>
    public async Task<int> GetCoilPaperAsync(CancellationToken cancellationToken = default) =>
        (await _transport.PostAsync<CoilPaperResponse>("get_coil_paper.fcgi", null, cancellationToken).ConfigureAwait(false)).CoilPaper;

    /// <inheritdoc/>
    public Task SetCoilPaperAsync(int coilPaper, CancellationToken cancellationToken = default)
    {
        if (coilPaper < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(coilPaper), coilPaper, "O tamanho da bobina não pode ser negativo.");
        }

        return _transport.PostRawAsync("set_coil_paper.fcgi", new Dictionary<string, object> { ["coil_paper"] = coilPaper }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<RepNetworkSettings> GetNetworkAsync(CancellationToken cancellationToken = default) =>
        _transport.PostAsync<RepNetworkSettings>("get_system_network.fcgi", null, cancellationToken);

    /// <inheritdoc/>
    public Task SetNetworkAsync(RepNetworkSettings settings, CancellationToken cancellationToken = default)
    {
        if (settings is null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        if (!settings.UseDhcp && (string.IsNullOrWhiteSpace(settings.Ip) || string.IsNullOrWhiteSpace(settings.Netmask)))
        {
            throw new ArgumentException("Sem DHCP, informe ao menos o IP e a máscara.", nameof(settings));
        }

        return _transport.PostRawAsync("set_system_network.fcgi", settings, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<RepDaylightSavingTime?> GetDaylightSavingTimeAsync(CancellationToken cancellationToken = default)
    {
        var response = await _transport.PostAsync<DaylightSavingTimeBody>("get_system_daylight_saving_time.fcgi", null, cancellationToken).ConfigureAwait(false);
        return response.Start?.ToDate() is { } start && response.End?.ToDate() is { } end && end >= start
            ? new RepDaylightSavingTime(start, end)
            : null;
    }

    /// <inheritdoc/>
    public Task SetDaylightSavingTimeAsync(RepDaylightSavingTime period, CancellationToken cancellationToken = default)
    {
        if (period is null)
        {
            throw new ArgumentNullException(nameof(period));
        }

        var body = new DaylightSavingTimeBody { Start = DateOnlyBody.From(period.Start), End = DateOnlyBody.From(period.End) };
        return _transport.PostRawAsync("set_system_daylight_saving_time.fcgi", body, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<DateTimeOffset> GetDateTimeOffsetAsync(CancellationToken cancellationToken = default)
    {
        var value = await _transport.PostAsync<DateTimeWithZone>("get_system_date_time.fcgi", null, cancellationToken, RequirePortaria671()).ConfigureAwait(false);
        var offset = value.TimeZone is { Length: > 0 } zone
            ? ParseTimeZone(zone)
            : throw new ControlIdException("O REP não devolveu o fuso horário (o modelo segue a Portaria 671?).");
        return new DateTimeOffset(value.ToDateTime(), offset);
    }

    /// <inheritdoc/>
    public Task SetDateTimeAsync(DateTimeOffset value, CancellationToken cancellationToken = default)
    {
        var query = RequirePortaria671();
        var body = DateTimeWithZone.From(value.DateTime);
        body.TimeZone = FormatTimeZone(value.Offset);
        return _transport.PostRawAsync("set_system_date_time.fcgi", body, cancellationToken, query);
    }

    // ---------------------------------------------------------------- AFD

    /// <inheritdoc/>
    public Task<string> GetAfdAsync(RepAfdQuery query, CancellationToken cancellationToken = default) =>
        GetAfdTextAsync(AfdBody(query ?? throw new ArgumentNullException(nameof(query))), cancellationToken);

    /// <inheritdoc/>
    public async Task<Stream> OpenAfdStreamAsync(RepAfdQuery? query = null, CancellationToken cancellationToken = default)
    {
        var (stream, _) = await _transport.PostForStreamAsync("get_afd.fcgi", AfdBody(query), cancellationToken, ModeQuery).ConfigureAwait(false);
        return stream;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<AfdRecord> EnumerateAfdRecordsAsync(RepAfdQuery? query = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var (stream, charSet) = await _transport.PostForStreamAsync("get_afd.fcgi", AfdBody(query), cancellationToken, ModeQuery).ConfigureAwait(false);
        using var reader = new StreamReader(stream, StreamEncoding(charSet));
        var lineNumber = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
#if NET7_0_OR_GREATER
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
#else
            var line = await reader.ReadLineAsync().ConfigureAwait(false);
#endif
            if (line is null)
            {
                yield break;
            }

            lineNumber++;
            if (line.Trim().Length > 0)
            {
                yield return AfdParser.ParseLine(line, lineNumber);
            }
        }
    }

    // ---------------------------------------------------------------- Biometria

    /// <inheritdoc/>
    public async Task<RepTemplateExtraction> ExtractTemplateAsync(byte[] bitmap, int width, int height, CancellationToken cancellationToken = default)
    {
        if (bitmap is null || bitmap.Length == 0)
        {
            throw new ArgumentException("Informe a imagem da digital.", nameof(bitmap));
        }

        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(width <= 0 ? nameof(width) : nameof(height), "Largura e altura precisam ser positivas.");
        }

        var query = string.Format(System.Globalization.CultureInfo.InvariantCulture, "width={0}&height={1}", width, height);
        var text = await _transport.PostBytesAsync("template_extract.fcgi", bitmap, "application/octet-stream", cancellationToken, query).ConfigureAwait(false);
        return ControlIdJson.Deserialize<RepTemplateExtraction>(text);
    }

    /// <inheritdoc/>
    public async Task<RepTemplateMergeResult> MergeTemplatesAsync(
        long userIdentifier, IEnumerable<string> templates, IEnumerable<string>? newTemplates = null, bool ignoreExistingTemplates = false,
        CancellationToken cancellationToken = default)
    {
        var list = (templates ?? throw new ArgumentNullException(nameof(templates))).ToList();
        if (list.Count == 0)
        {
            throw new ArgumentException("Informe as leituras da digital (a documentação pede três).", nameof(templates));
        }

        var body = new Dictionary<string, object>
        {
            ["templates"] = list,
            [Mode == RepMode.Portaria671 ? "user_cpf" : "user_pis"] = userIdentifier,
            ["new_templates"] = newTemplates?.ToList() ?? new List<string>(),
            ["remove"] = ignoreExistingTemplates,
        };
        return await _transport.PostAsync<RepTemplateMergeResult>("template_merge.fcgi", body, cancellationToken, ModeQuery).ConfigureAwait(false);
    }

    // ---------------------------------------------------------------- Facial

    /// <inheritdoc/>
    public Task<RepFacialModuleConfig> GetFacialModuleConfigAsync(CancellationToken cancellationToken = default) =>
        _transport.PostAsync<RepFacialModuleConfig>("get_facial_module_config.fcgi", null, cancellationToken);

    /// <inheritdoc/>
    public Task SetFacialModuleConfigAsync(RepFacialModuleConfig config, CancellationToken cancellationToken = default)
    {
        if (config is null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        if (config.IdentificationDistance is < 30 or > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(config), config.IdentificationDistance, "A distância de identificação vai de 30 a 200 cm.");
        }

        if (config.LedsBrightness is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(config), config.LedsBrightness, "A luminosidade dos LEDs vai de 0 a 100%.");
        }

        var body = new Dictionary<string, object>
        {
            ["identification_distance"] = config.IdentificationDistance,
            ["leds_brightness"] = config.LedsBrightness,
            ["led_activation_ambient_brightness"] = (int)config.LedActivation,
        };
        return _transport.PostRawAsync("set_facial_module_config.fcgi", body, cancellationToken);
    }

    /// <inheritdoc/>
    public Task SetFacialLimitAsync(string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("Informe a senha da licença fornecida pela Control iD.", nameof(password));
        }

        var body = new Dictionary<string, object> { ["facial_limit"] = "10000", ["password"] = password };
        return _transport.PostRawAsync("set_facial_limit.fcgi", body, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<RepFacialAbout> GetAboutFacialAsync(CancellationToken cancellationToken = default) =>
        _transport.PostAsync<RepFacialAbout>("get_about_facial.fcgi", null, cancellationToken);

    /// <inheritdoc/>
    public Task<RepFacialEnrollerState> GetFacialEnrollerStateAsync(CancellationToken cancellationToken = default) =>
        _transport.PostAsync<RepFacialEnrollerState>("facial_enroller_state.fcgi", null, cancellationToken);

    /// <inheritdoc/>
    public Task StartRemoteEnrollAsync(long cpf, CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync("remote_enroll.fcgi", new Dictionary<string, object> { ["cpf"] = cpf }, cancellationToken, FacialQuery);

    /// <inheritdoc/>
    public Task CancelRemoteEnrollAsync(CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync("cancel_remote_enroll.fcgi", null, cancellationToken);

    /// <inheritdoc/>
    public Task SetUserImageAsync(long cpf, byte[] image, DateTimeOffset? timestamp = null, bool doMatch = false, CancellationToken cancellationToken = default)
    {
        ValidateImage(image);
        var query = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "do_match={0}&cpf={1}&image_timestamp={2}&raw=true&{3}",
            doMatch ? "true" : "false",
            cpf,
            (timestamp ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds(),
            FacialQuery);
        return _transport.PostBytesAsync("user_set_image.fcgi", image, "application/octet-stream", cancellationToken, query);
    }

    /// <inheritdoc/>
    public async Task<byte[]> GetUserImageAsync(long cpf, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object> { ["cpf"] = cpf, ["raw"] = true };
        var (content, _) = await _transport.PostForBytesAsync("user_get_image.fcgi", body, cancellationToken, FacialQuery).ConfigureAwait(false);
        return content;
    }

    /// <inheritdoc/>
    public Task DeleteUserImageAsync(long cpf, CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync("user_destroy_image.fcgi", new Dictionary<string, object> { ["cpf"] = cpf }, cancellationToken, FacialQuery);

    /// <inheritdoc/>
    public Task ImportUserImageAsync(long cpf, byte[] image, CancellationToken cancellationToken = default)
    {
        ValidateImage(image);
        var query = string.Format(System.Globalization.CultureInfo.InvariantCulture, "cpf={0}&{1}", cpf, FacialQuery);
        return _transport.PostRawAsync("user_import_image.fcgi", new Dictionary<string, object> { ["image"] = Convert.ToBase64String(image) }, cancellationToken, query);
    }

    /// <inheritdoc/>
    public void Dispose() => _transport.Dispose();

    // O AFD é ISO-8859-1 pelas portarias; ler como UTF-8 sem charset estragaria os nomes acentuados.
    private async Task<string> GetAfdTextAsync(object? body, CancellationToken cancellationToken)
    {
        var (content, charSet) = await _transport.PostForBytesAsync("get_afd.fcgi", body, cancellationToken, ModeQuery).ConfigureAwait(false);
        return AfdParser.Decode(content, charSet);
    }

    // As APIs faciais exigem mode=671 em qualquer modo do cliente (documentação: "obrigatoriamente").
    private const string FacialQuery = "mode=671";

    private static readonly System.Text.Encoding Latin1 = System.Text.Encoding.GetEncoding("iso-8859-1");

    private string RequirePortaria671() => Mode == RepMode.Portaria671
        ? "mode=671"
        : throw new InvalidOperationException("O fuso horário só existe no modo Portaria 671 (RepMode.Portaria671).");

    private static Dictionary<string, object>? AfdBody(RepAfdQuery? query)
    {
        if (query is null)
        {
            return null;
        }

        if (query.InitialNsr is not null && query.InitialDate is not null)
        {
            throw new ArgumentException("Informe o NSR inicial ou a data inicial, não os dois.", nameof(query));
        }

        var body = new Dictionary<string, object>();
        if (query.InitialNsr is { } nsr)
        {
            body["initial_nsr"] = nsr;
        }

        if (query.InitialDate is { } date)
        {
            body["initial_date"] = new Dictionary<string, int> { ["year"] = date.Year, ["month"] = date.Month, ["day"] = date.Day };
        }

        if (query.HeaderTop)
        {
            body["headerTop"] = true;
        }

        return body;
    }

    // Lido aos poucos, não dá para testar se é UTF-8 válido como no texto inteiro: vale o charset da resposta ou ISO-8859-1.
    private static System.Text.Encoding StreamEncoding(string? charSet)
    {
        if (!string.IsNullOrWhiteSpace(charSet))
        {
            try
            {
                return System.Text.Encoding.GetEncoding(charSet!.Trim('"', ' '));
            }
            catch (ArgumentException)
            {
                // Charset desconhecido: fica a codificação das portarias.
            }
        }

        return Latin1;
    }

    private static void ValidateImage(byte[] image)
    {
        if (image is null || image.Length == 0)
        {
            throw new ArgumentException("Informe a foto.", nameof(image));
        }
    }

    // sHHMM, ex.: -0300; o REP só aceita múltiplos de 15 minutos.
    internal static string FormatTimeZone(TimeSpan offset)
    {
        if (offset.Ticks % TimeSpan.FromMinutes(15).Ticks != 0)
        {
            throw new ArgumentException("O REP só aceita fusos em múltiplos de 15 minutos.", nameof(offset));
        }

        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var absolute = offset.Duration();
        return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}{1:00}{2:00}", sign, (int)absolute.TotalHours, absolute.Minutes);
    }

    internal static TimeSpan ParseTimeZone(string zone)
    {
        var text = zone.Trim();
        if (text.Length == 5 && (text[0] == '+' || text[0] == '-')
            && int.TryParse(text.Substring(1, 2), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var hours)
            && int.TryParse(text.Substring(3, 2), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var minutes))
        {
            var offset = new TimeSpan(hours, minutes, 0);
            return text[0] == '-' ? -offset : offset;
        }

        throw new ControlIdException($"Fuso horário em formato inesperado: '{zone}' (esperado sHHMM, ex.: -0300).");
    }

    private sealed class SessionIsValidResponse
    {
        [JsonPropertyName("session_is_valid")]
        public bool SessionIsValid { get; set; }
    }

    private sealed class CompanyResponse
    {
        [JsonPropertyName("company")]
        public RepCompany? Company { get; set; }
    }

    private sealed class EnableResponse
    {
        [JsonPropertyName("enable")]
        public bool Enable { get; set; }
    }

    private sealed class CoilPaperResponse
    {
        [JsonPropertyName("coil_paper")]
        public int CoilPaper { get; set; }
    }

    private sealed class DateOnlyBody
    {
        [JsonPropertyName("year")] public int Year { get; set; }

        [JsonPropertyName("month")] public int Month { get; set; }

        [JsonPropertyName("day")] public int Day { get; set; }

        public static DateOnlyBody From(DateTime date) => new() { Year = date.Year, Month = date.Month, Day = date.Day };

        // Sem horário de verão o REP devolve zeros (ou nada).
        public DateTime? ToDate() =>
            Year is >= 1 and <= 9999 && Month is >= 1 and <= 12 && Day >= 1 && Day <= DateTime.DaysInMonth(Year, Month)
                ? new DateTime(Year, Month, Day)
                : null;
    }

    private sealed class DaylightSavingTimeBody
    {
        [JsonPropertyName("start_date_time")] public DateOnlyBody? Start { get; set; }

        [JsonPropertyName("end_date_time")] public DateOnlyBody? End { get; set; }
    }

    private sealed class DateTimeWithZone
    {
        [JsonPropertyName("day")] public int Day { get; set; }

        [JsonPropertyName("month")] public int Month { get; set; }

        [JsonPropertyName("year")] public int Year { get; set; }

        [JsonPropertyName("hour")] public int Hour { get; set; }

        [JsonPropertyName("minute")] public int Minute { get; set; }

        [JsonPropertyName("second")] public int Second { get; set; }

        [JsonPropertyName("timezone")] public string? TimeZone { get; set; }

        public static DateTimeWithZone From(DateTime value) => new()
        {
            Day = value.Day, Month = value.Month, Year = value.Year, Hour = value.Hour, Minute = value.Minute, Second = value.Second,
        };

        public DateTime ToDateTime() => new(Year, Month, Day, Hour, Minute, Second, DateTimeKind.Unspecified);
    }

    private sealed class CountResponse
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }
    }

    private sealed class LoadUsersRequest
    {
        [JsonPropertyName("limit")]
        public int Limit { get; set; }

        [JsonPropertyName("offset")]
        public int Offset { get; set; }

        [JsonPropertyName("templates")]
        public bool? Templates { get; set; }
    }
}
