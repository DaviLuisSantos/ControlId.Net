using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ControlId.Access.Monitoring;
using ControlId.Access.Online;
using ControlId.Access.Push;
using ControlId.Internal;

namespace ControlId.Access;

/// <inheritdoc cref="IAccessClient"/>
public sealed class AccessClient : IAccessClient
{
    /// <summary>Tamanho padrão de página das leituras paginadas (<c>Enumerate*Async</c>).</summary>
    public const int DefaultPageSize = 500;

    private const int MaxImageBytes = 1024 * 1024;
    private static readonly TimeSpan MaxBuzz = TimeSpan.FromSeconds(3);

    // "Node or attribute not found. Node path: config->general->param[name=x]" ou "...->modules->module[name=monitor]->param[name=x]"
    private static readonly Regex MissingConfigFieldError = new(
        @"Node or attribute not found\. Node path: config->(?:modules->module\[name=(?<section>[^\]]+)\]|(?<section>[^-]+))->param\[name=(?<field>[^\]]+)\]",
        RegexOptions.CultureInvariant);

    private readonly ControlIdTransport _transport;
    private readonly Uri _baseAddress;

    // Parâmetros ("seção.campo") que o firmware deste equipamento não tem, para não pedi-los de novo.
    private readonly ConcurrentDictionary<string, byte> _missingConfigFields = new(StringComparer.Ordinal);

    /// <summary>Cria o cliente. Se <paramref name="httpClient"/> for informado (ex.: via IHttpClientFactory), ele não é descartado por este cliente.</summary>
    public AccessClient(ControlIdDeviceOptions options, HttpClient? httpClient = null)
    {
        _transport = new ControlIdTransport(options, httpClient);
        _baseAddress = options.BaseAddress;
    }

    /// <summary>Atalho: <c>new AccessClient("http://192.168.0.129", "admin", "admin")</c>.</summary>
    public AccessClient(string baseAddress, string login = "admin", string password = "admin")
        : this(new ControlIdDeviceOptions { BaseAddress = new Uri(baseAddress), Login = login, Password = password })
    {
    }

    /// <summary>Sessão atual, ou <c>null</c> se ainda não logou.</summary>
    public string? Session => _transport.Session;

    /// <inheritdoc/>
    public Task LoginAsync(CancellationToken cancellationToken = default) => _transport.LoginAsync(cancellationToken);

    /// <inheritdoc/>
    public Task LogoutAsync(CancellationToken cancellationToken = default) => _transport.LogoutAsync(cancellationToken);

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
    public async Task<IReadOnlyList<T>> LoadObjectsAsync<T>(LoadObjectsRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var response = await _transport.PostAsync<Dictionary<string, List<T>>>("load_objects.fcgi", request, cancellationToken).ConfigureAwait(false);
        return response.TryGetValue(request.Object, out var items) ? items : new List<T>();
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<T> EnumerateObjectsAsync<T>(LoadObjectsRequest request, int pageSize = DefaultPageSize, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        ValidatePageSize(pageSize);
        return EnumerateByOffsetAsync<T>(request, pageSize, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<long>> CreateObjectsAsync<T>(string @object, IEnumerable<T> values, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?> { ["object"] = @object, ["values"] = values.ToList() };
        var response = await _transport.PostAsync<IdsResponse>("create_objects.fcgi", body, cancellationToken).ConfigureAwait(false);
        return response.Ids ?? new List<long>();
    }

    /// <inheritdoc/>
    public async Task<int> ModifyObjectsAsync(string @object, object values, object where, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?> { ["object"] = @object, ["values"] = values, ["where"] = where };
        var response = await _transport.PostAsync<ChangesResponse>("modify_objects.fcgi", body, cancellationToken).ConfigureAwait(false);
        return response.Changes;
    }

    /// <inheritdoc/>
    public async Task<int> DestroyObjectsAsync(string @object, object where, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?> { ["object"] = @object, ["where"] = where };
        var response = await _transport.PostAsync<ChangesResponse>("destroy_objects.fcgi", body, cancellationToken).ConfigureAwait(false);
        return response.Changes;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<AccessUser>> GetUsersAsync(CancellationToken cancellationToken = default) =>
        LoadObjectsAsync<AccessUser>(new LoadObjectsRequest("users"), cancellationToken);

    /// <inheritdoc/>
    public IAsyncEnumerable<AccessUser> EnumerateUsersAsync(int pageSize = DefaultPageSize, CancellationToken cancellationToken = default)
    {
        ValidatePageSize(pageSize);
        return EnumerateAfterAsync<AccessUser>("users", 0, pageSize, static u => u.Id ?? 0, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<long> CreateUserAsync(AccessUser user, CancellationToken cancellationToken = default) =>
        CreateOneAsync("users", user, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> DeleteUserAsync(long userId, CancellationToken cancellationToken = default) =>
        DeleteByIdAsync("users", userId, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<AccessCard>> GetCardsAsync(long? userId = null, CancellationToken cancellationToken = default) =>
        LoadAsync<AccessCard>("cards", "user_id", userId, cancellationToken);

    /// <inheritdoc/>
    public Task<long> CreateCardAsync(AccessCard card, CancellationToken cancellationToken = default) =>
        CreateOneAsync("cards", card, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> DeleteCardAsync(long cardId, CancellationToken cancellationToken = default) =>
        DeleteByIdAsync("cards", cardId, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<AccessTemplate>> GetTemplatesAsync(long? userId = null, CancellationToken cancellationToken = default) =>
        LoadAsync<AccessTemplate>("templates", "user_id", userId, cancellationToken);

    /// <inheritdoc/>
    public Task<long> CreateTemplateAsync(AccessTemplate template, CancellationToken cancellationToken = default) =>
        CreateOneAsync("templates", template, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> DeleteTemplateAsync(long templateId, CancellationToken cancellationToken = default) =>
        DeleteByIdAsync("templates", templateId, cancellationToken);

    /// <inheritdoc/>
    public async Task<FaceImageResult> SetUserImageAsync(
        long userId, byte[] image, bool rejectExistingFace = false, DateTimeOffset? timestamp = null, CancellationToken cancellationToken = default)
    {
        ValidateImage(image, nameof(image));
        var query = string.Join(
            "&",
            "user_id=" + userId.ToString(CultureInfo.InvariantCulture),
            "timestamp=" + (timestamp ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            "match=" + (rejectExistingFace ? "1" : "0"));
        var text = await _transport.PostBytesAsync("user_set_image.fcgi", image, "application/octet-stream", cancellationToken, query).ConfigureAwait(false);
        return ParseFaceResult(text);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<FaceImageResult>> SetUserImagesAsync(
        IEnumerable<UserImage> images, bool rejectExistingFaces = false, CancellationToken cancellationToken = default)
    {
        if (images is null)
        {
            throw new ArgumentNullException(nameof(images));
        }

        var body = new Dictionary<string, object> { ["match"] = rejectExistingFaces, ["user_images"] = images.ToList() };
        var text = await _transport.PostRawAsync("user_set_image_list.fcgi", body, cancellationToken).ConfigureAwait(false);
        return ParseResults(text);
    }

    /// <inheritdoc/>
    public async Task<FaceImageResult> TestUserImageAsync(byte[] image, CancellationToken cancellationToken = default)
    {
        ValidateImage(image, nameof(image));
        var text = await _transport.PostBytesAsync("user_test_image.fcgi", image, "application/octet-stream", cancellationToken).ConfigureAwait(false);
        return ParseFaceResult(text);
    }

    /// <inheritdoc/>
    public Task<byte[]> GetUserImageAsync(long userId, CancellationToken cancellationToken = default) =>
        _transport.GetBytesAsync("user_get_image.fcgi", cancellationToken, "user_id=" + userId.ToString(CultureInfo.InvariantCulture));

    /// <inheritdoc/>
    public async Task<UserImageData> GetUserImageWithTimestampAsync(long userId, CancellationToken cancellationToken = default)
    {
        var bytes = await _transport.GetBytesAsync(
            "user_get_image.fcgi", cancellationToken, "user_id=" + userId.ToString(CultureInfo.InvariantCulture) + "&get_timestamp=1").ConfigureAwait(false);
        return ControlIdJson.Deserialize<UserImageData>(System.Text.Encoding.UTF8.GetString(bytes));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<UserImageEntry>> GetUserImagesAsync(IEnumerable<long> userIds, CancellationToken cancellationToken = default)
    {
        var ids = (userIds ?? throw new ArgumentNullException(nameof(userIds))).ToList();
        if (ids.Count is 0 or > 100)
        {
            throw new ArgumentException("Informe de 1 a 100 usuários por chamada.", nameof(userIds));
        }

        var response = await _transport.PostAsync<UserImagesResponse>(
            "user_get_image_list.fcgi", new Dictionary<string, object> { ["user_ids"] = ids }, cancellationToken).ConfigureAwait(false);
        return response.UserImages ?? new List<UserImageEntry>();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<long>> GetUserIdsWithImageAsync(CancellationToken cancellationToken = default)
    {
        var response = await _transport.PostAsync<UserIdsResponse>("user_list_images.fcgi", null, cancellationToken).ConfigureAwait(false);
        return response.UserIds ?? new List<long>();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<UserImageInfo>> GetUserImageInfoAsync(CancellationToken cancellationToken = default)
    {
        // O exemplo da documentação usa GET com get_timestamp na query.
        var bytes = await _transport.GetBytesAsync("user_list_images.fcgi", cancellationToken, "get_timestamp=1").ConfigureAwait(false);
        return ControlIdJson.Deserialize<ImageInfoResponse>(System.Text.Encoding.UTF8.GetString(bytes)).ImageInfo ?? new List<UserImageInfo>();
    }

    /// <inheritdoc/>
    public Task DeleteUserImageAsync(long userId, CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync("user_destroy_image.fcgi", new Dictionary<string, object> { ["user_id"] = userId }, cancellationToken);

    /// <inheritdoc/>
    public Task DeleteUserImagesAsync(IEnumerable<long> userIds, CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync(
            "user_destroy_image.fcgi",
            new Dictionary<string, object> { ["user_ids"] = (userIds ?? throw new ArgumentNullException(nameof(userIds))).ToList() },
            cancellationToken);

    /// <inheritdoc/>
    public Task DeleteDanglingUserImagesAsync(CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync("user_destroy_image.fcgi", new Dictionary<string, object> { ["dangling"] = true }, cancellationToken);

    /// <inheritdoc/>
    public Task DeleteAllUserImagesAsync(CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync("user_destroy_image.fcgi", new Dictionary<string, object> { ["all"] = true }, cancellationToken);

    /// <inheritdoc/>
    public async Task<FaceImageResult> ExtractFaceTemplateAsync(byte[] image, CancellationToken cancellationToken = default)
    {
        ValidateImage(image, nameof(image));
        var text = await _transport.PostBytesAsync("face_template_extract.fcgi", image, "application/octet-stream", cancellationToken).ConfigureAwait(false);
        return ParseFaceResult(text);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<FaceImageResult>> ExtractFaceTemplatesAsync(
        IEnumerable<FaceTemplateImage> images, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object> { ["face_images"] = (images ?? throw new ArgumentNullException(nameof(images))).ToList() };
        return ParseResults(await _transport.PostRawAsync("face_template_extract_list.fcgi", body, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<FaceImageResult>> EnrollFaceTemplatesAsync(
        IEnumerable<FaceTemplateEnrollment> templates, bool rejectExistingFaces = false, CancellationToken cancellationToken = default)
    {
        // A tabela da documentação escreve "face_tempaltes"; o exemplo usa "face_templates".
        var body = new Dictionary<string, object>
        {
            ["match"] = rejectExistingFaces,
            ["face_templates"] = (templates ?? throw new ArgumentNullException(nameof(templates))).ToList(),
        };
        return ParseResults(await _transport.PostRawAsync("face_template_enroll.fcgi", body, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc/>
    public Task DeleteFaceTemplatesAsync(IEnumerable<long> userIds, CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync(
            "face_template_destroy.fcgi",
            new Dictionary<string, object> { ["user_ids"] = (userIds ?? throw new ArgumentNullException(nameof(userIds))).ToList() },
            cancellationToken);

    /// <inheritdoc/>
    public Task DeleteAllFaceTemplatesAsync(CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync("face_template_destroy.fcgi", new Dictionary<string, object> { ["all"] = true }, cancellationToken);

    /// <inheritdoc/>
    public async Task<byte[]> CaptureCameraAsync(CameraType camera = CameraType.Rgb, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object> { ["frame_type"] = "camera", ["camera"] = camera == CameraType.Infrared ? "ir" : "rgb" };
        var (content, _) = await _transport.PostForBytesAsync("save_screenshot.fcgi", body, cancellationToken).ConfigureAwait(false);
        return content;
    }

    /// <inheritdoc/>
    public Task StartTemplateSyncAsync(CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync("template_sync_init.fcgi", null, cancellationToken);

    /// <inheritdoc/>
    public Task EndTemplateSyncAsync(CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync("template_sync_end.fcgi", null, cancellationToken);

    /// <inheritdoc/>
    public async Task<byte[]> ExportObjectsAsync(IEnumerable<ExportObjectRequest> objects, CancellationToken cancellationToken = default)
    {
        var list = (objects ?? throw new ArgumentNullException(nameof(objects))).ToList();
        if (list.Count == 0)
        {
            throw new ArgumentException("Informe ao menos um objeto.", nameof(objects));
        }

        var (content, _) = await _transport.PostForBytesAsync(
            "export_objects.fcgi", new Dictionary<string, object> { ["objects"] = list }, cancellationToken).ConfigureAwait(false);
        return content;
    }

    /// <inheritdoc/>
    public async Task<RemoteEnrollResult> RemoteEnrollAsync(RemoteEnrollRequest request, CancellationToken cancellationToken = default)
    {
        var body = (request ?? throw new ArgumentNullException(nameof(request))).ToBody(sync: true);
        try
        {
            var text = await _transport.PostRawAsync("remote_enroll.fcgi", body, cancellationToken).ConfigureAwait(false);
            return RemoteEnrollResult.Parse(text);
        }
        catch (Exception ex) when (ex is OperationCanceledException || (ex is ControlIdException { InnerException: OperationCanceledException }))
        {
            // Sem isso o equipamento continuaria na tela de cadastro esperando a pessoa.
            await TryCancelRemoteEnrollAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc/>
    public Task StartRemoteEnrollAsync(RemoteEnrollRequest request, CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync("remote_enroll.fcgi", (request ?? throw new ArgumentNullException(nameof(request))).ToBody(sync: false), cancellationToken);

    /// <inheritdoc/>
    public Task CancelRemoteEnrollAsync(CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync("cancel_remote_enroll.fcgi", null, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<T>> GetObjectsAsync<T>(object? where = null, int? limit = null, CancellationToken cancellationToken = default) =>
        LoadObjectsAsync<T>(new LoadObjectsRequest(ControlIdObjects.NameOf<T>()) { Where = where, Limit = limit }, cancellationToken);

    /// <inheritdoc/>
    public IAsyncEnumerable<T> EnumerateObjectsAsync<T>(object? where = null, int pageSize = DefaultPageSize, CancellationToken cancellationToken = default)
    {
        var info = ControlIdObjects.Get(typeof(T));
        ValidatePageSize(pageSize);
        // Tabelas de associação não têm chave: ficam na ordem do equipamento.
        var request = new LoadObjectsRequest(info.Name) { Where = where, Order = info.Key is null ? null : new List<string> { "ascending", info.Key } };
        return EnumerateByOffsetAsync<T>(request, pageSize, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<long> CreateObjectAsync<T>(T value, CancellationToken cancellationToken = default)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        var ids = await CreateObjectsAsync(ControlIdObjects.NameOf<T>(), new[] { value }, cancellationToken).ConfigureAwait(false);
        return ids.Count > 0 ? ids[0] : 0;
    }

    /// <inheritdoc/>
    public Task<int> UpdateObjectAsync<T>(T value, CancellationToken cancellationToken = default)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        var info = ControlIdObjects.Get(typeof(T));
        return ModifyObjectsAsync(info.Name, value, Where.Equal(info.Name, info.Key!, info.GetKey(value)), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteObjectAsync<T>(long key, CancellationToken cancellationToken = default)
    {
        var info = ControlIdObjects.Get(typeof(T));
        if (info.Key is null)
        {
            throw new InvalidOperationException($"'{info.Name}' é uma tabela de associação, sem chave: use UnlinkAsync.");
        }

        return await DestroyObjectsAsync(info.Name, Where.Equal(info.Name, info.Key, key), cancellationToken).ConfigureAwait(false) > 0;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<AccessQrCode>> GetQrCodesAsync(long? userId = null, CancellationToken cancellationToken = default) =>
        LoadAsync<AccessQrCode>("qrcodes", "user_id", userId, cancellationToken);

    /// <inheritdoc/>
    public Task<long> CreateQrCodeAsync(AccessQrCode qrCode, CancellationToken cancellationToken = default) =>
        CreateOneAsync("qrcodes", qrCode, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<AccessUhfTag>> GetUhfTagsAsync(long? userId = null, CancellationToken cancellationToken = default) =>
        LoadAsync<AccessUhfTag>("uhf_tags", "user_id", userId, cancellationToken);

    /// <inheritdoc/>
    public Task<long> CreateUhfTagAsync(AccessUhfTag tag, CancellationToken cancellationToken = default) =>
        CreateOneAsync("uhf_tags", tag, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<AccessPin>> GetPinsAsync(long? userId = null, CancellationToken cancellationToken = default) =>
        LoadAsync<AccessPin>("pins", "user_id", userId, cancellationToken);

    /// <inheritdoc/>
    public Task<long> CreatePinAsync(AccessPin pin, CancellationToken cancellationToken = default) =>
        CreateOneAsync("pins", pin, cancellationToken);

    /// <inheritdoc/>
    public async Task SetAdministratorAsync(long userId, bool isAdministrator, CancellationToken cancellationToken = default)
    {
        // user_roles só guarda quem tem privilégio diferente do padrão: remover = voltar a usuário comum.
        await DestroyObjectsAsync("user_roles", Where.Equal("user_roles", "user_id", userId), cancellationToken).ConfigureAwait(false);
        if (isAdministrator)
        {
            await CreateObjectsAsync("user_roles", new[] { new UserRole { UserId = userId, Role = 1 } }, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<ChangeLog>> GetChangeLogsAsync(long afterId = 0, int? limit = null, CancellationToken cancellationToken = default) =>
        LoadAfterAsync<ChangeLog>("change_logs", afterId, limit, cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<long>> GetAccessLogRuleIdsAsync(long accessLogId, CancellationToken cancellationToken = default)
    {
        var rows = await LoadAsync<AccessLogAccessRule>("access_log_access_rules", "access_log_id", accessLogId, cancellationToken).ConfigureAwait(false);
        return rows.Select(r => r.AccessRuleId).ToList();
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<AccessGroup>> GetGroupsAsync(CancellationToken cancellationToken = default) =>
        LoadObjectsAsync<AccessGroup>(new LoadObjectsRequest("groups"), cancellationToken);

    /// <inheritdoc/>
    public Task<long> CreateGroupAsync(AccessGroup group, CancellationToken cancellationToken = default) =>
        CreateOneAsync("groups", group, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> DeleteGroupAsync(long groupId, CancellationToken cancellationToken = default) =>
        DeleteByIdAsync("groups", groupId, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<AccessRule>> GetAccessRulesAsync(CancellationToken cancellationToken = default) =>
        LoadObjectsAsync<AccessRule>(new LoadObjectsRequest("access_rules"), cancellationToken);

    /// <inheritdoc/>
    public Task<long> CreateAccessRuleAsync(AccessRule rule, CancellationToken cancellationToken = default) =>
        CreateOneAsync("access_rules", rule, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> DeleteAccessRuleAsync(long accessRuleId, CancellationToken cancellationToken = default) =>
        DeleteByIdAsync("access_rules", accessRuleId, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<AccessTimeZone>> GetTimeZonesAsync(CancellationToken cancellationToken = default) =>
        LoadObjectsAsync<AccessTimeZone>(new LoadObjectsRequest("time_zones"), cancellationToken);

    /// <inheritdoc/>
    public Task<long> CreateTimeZoneAsync(AccessTimeZone timeZone, CancellationToken cancellationToken = default) =>
        CreateOneAsync("time_zones", timeZone, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> DeleteTimeZoneAsync(long timeZoneId, CancellationToken cancellationToken = default) =>
        DeleteByIdAsync("time_zones", timeZoneId, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<AccessTimeSpan>> GetTimeSpansAsync(long? timeZoneId = null, CancellationToken cancellationToken = default) =>
        LoadAsync<AccessTimeSpan>("time_spans", "time_zone_id", timeZoneId, cancellationToken);

    /// <inheritdoc/>
    public Task<long> CreateTimeSpanAsync(AccessTimeSpan timeSpan, CancellationToken cancellationToken = default) =>
        CreateOneAsync("time_spans", timeSpan, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> DeleteTimeSpanAsync(long timeSpanId, CancellationToken cancellationToken = default) =>
        DeleteByIdAsync("time_spans", timeSpanId, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<AccessPortal>> GetPortalsAsync(CancellationToken cancellationToken = default) =>
        LoadObjectsAsync<AccessPortal>(new LoadObjectsRequest("portals"), cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<AccessArea>> GetAreasAsync(CancellationToken cancellationToken = default) =>
        LoadObjectsAsync<AccessArea>(new LoadObjectsRequest("areas"), cancellationToken);

    /// <inheritdoc/>
    public Task<long> CreateAreaAsync(AccessArea area, CancellationToken cancellationToken = default) =>
        CreateOneAsync("areas", area, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> DeleteAreaAsync(long areaId, CancellationToken cancellationToken = default) =>
        DeleteByIdAsync("areas", areaId, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<AccessHoliday>> GetHolidaysAsync(CancellationToken cancellationToken = default) =>
        LoadObjectsAsync<AccessHoliday>(new LoadObjectsRequest("holidays"), cancellationToken);

    /// <inheritdoc/>
    public Task<long> CreateHolidayAsync(AccessHoliday holiday, CancellationToken cancellationToken = default)
    {
        if (holiday is null)
        {
            throw new ArgumentNullException(nameof(holiday));
        }

        // start e end são inteiros de 32 bits no equipamento: datas depois de 19/01/2038 são recusadas.
        if (holiday.Start is < int.MinValue or > int.MaxValue || holiday.End is < int.MinValue or > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(holiday), "O equipamento guarda o feriado em segundos de 32 bits: use datas até 19/01/2038.");
        }

        return CreateOneAsync("holidays", holiday, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> DeleteHolidayAsync(long holidayId, CancellationToken cancellationToken = default) =>
        DeleteByIdAsync("holidays", holidayId, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<AccessDeviceEvent>> GetAccessEventsAsync(long afterId = 0, int? limit = null, CancellationToken cancellationToken = default) =>
        LoadAfterAsync<AccessDeviceEvent>("access_events", afterId, limit, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<AlarmLog>> GetAlarmLogsAsync(long afterId = 0, int? limit = null, CancellationToken cancellationToken = default) =>
        LoadAfterAsync<AlarmLog>("alarm_logs", afterId, limit, cancellationToken);

    /// <inheritdoc/>
    public async Task<SecBox?> GetSecBoxAsync(CancellationToken cancellationToken = default)
    {
        var boxes = await LoadObjectsAsync<SecBox>(new LoadObjectsRequest("sec_boxs"), cancellationToken).ConfigureAwait(false);
        return boxes.Count > 0 ? boxes[0] : null;
    }

    /// <inheritdoc/>
    public async Task UpdateSecBoxAsync(SecBox secBox, CancellationToken cancellationToken = default)
    {
        if (secBox is null)
        {
            throw new ArgumentNullException(nameof(secBox));
        }

        await ModifyObjectsAsync("sec_boxs", secBox, Where.Equal("sec_boxs", "id", secBox.Id), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<string> GenerateReportAsync(ReportRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var (content, charSet) = await _transport.PostForBytesAsync("report_generate.fcgi", request, cancellationToken).ConfigureAwait(false);
        return TextDecoding.Decode(content, charSet);
    }

    /// <inheritdoc/>
    public async Task<string> ExportAfdAsync(
        long? initialNsr = null, DateTime? initialDate = null, bool portaria671 = true, CancellationToken cancellationToken = default)
    {
        if (initialNsr is not null && initialDate is not null)
        {
            throw new ArgumentException("Filtre por NSR ou por data, não pelos dois.", nameof(initialDate));
        }

        var body = new Dictionary<string, object>();
        if (initialNsr is { } nsr)
        {
            body["initial_nsr"] = nsr;
        }

        if (initialDate is { } date)
        {
            body["initial_date"] = new Dictionary<string, int> { ["day"] = date.Day, ["month"] = date.Month, ["year"] = date.Year };
        }

        var (content, charSet) = await _transport.PostForBytesAsync(
            "export_afd.fcgi", body, cancellationToken, portaria671 ? "mode=671" : null).ConfigureAwait(false);
        return TextDecoding.Decode(content, charSet);
    }

    /// <inheritdoc/>
    public async Task<Rep.Afd.AfdFile> ExportAfdFileAsync(
        long? initialNsr = null, DateTime? initialDate = null, bool portaria671 = true, CancellationToken cancellationToken = default) =>
        Rep.Afd.AfdParser.Parse(await ExportAfdAsync(initialNsr, initialDate, portaria671, cancellationToken).ConfigureAwait(false));

    /// <inheritdoc/>
    public async Task<string> ExportAuditLogsAsync(AuditLogCategories categories = AuditLogCategories.All, CancellationToken cancellationToken = default)
    {
        if (categories == AuditLogCategories.None)
        {
            throw new ArgumentException("Escolha ao menos uma categoria.", nameof(categories));
        }

        int Flag(AuditLogCategories category) => (categories & category) != 0 ? 1 : 0;
        var body = new Dictionary<string, object>
        {
            ["config"] = Flag(AuditLogCategories.Config),
            ["api"] = Flag(AuditLogCategories.Api),
            ["usb"] = Flag(AuditLogCategories.Usb),
            ["network"] = Flag(AuditLogCategories.Network),
            ["time"] = Flag(AuditLogCategories.Time),
            ["online"] = Flag(AuditLogCategories.Online),
            ["menu"] = Flag(AuditLogCategories.Menu),
        };

        var (content, charSet) = await _transport.PostForBytesAsync("export_audit_logs.fcgi", body, cancellationToken).ConfigureAwait(false);
        return TextDecoding.Decode(content, charSet);
    }

    /// <inheritdoc/>
    public Task LinkAsync(AccessLink link, CancellationToken cancellationToken = default)
    {
        if (link is null)
        {
            throw new ArgumentNullException(nameof(link));
        }

        // Tabelas de associação não têm coluna id, então não há ID a devolver.
        return CreateObjectsAsync(link.Object, new[] { link.Values }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> UnlinkAsync(AccessLink link, CancellationToken cancellationToken = default)
    {
        if (link is null)
        {
            throw new ArgumentNullException(nameof(link));
        }

        return await DestroyObjectsAsync(link.Object, link.ToWhere(), cancellationToken).ConfigureAwait(false) > 0;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<AccessLog>> GetAccessLogsAsync(long afterId = 0, int? limit = null, CancellationToken cancellationToken = default) =>
        LoadAfterAsync<AccessLog>("access_logs", afterId, limit, cancellationToken);

    /// <inheritdoc/>
    public IAsyncEnumerable<AccessLog> EnumerateAccessLogsAsync(long afterId = 0, int pageSize = DefaultPageSize, CancellationToken cancellationToken = default)
    {
        ValidatePageSize(pageSize);
        return EnumerateAfterAsync<AccessLog>("access_logs", afterId, pageSize, static l => l.Id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<JsonElement?> ExecuteActionsAsync(IEnumerable<DeviceAction> actions, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object> { ["actions"] = actions.ToList() };
        var text = await _transport.PostRawAsync("execute_actions.fcgi", body, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.Clone();
    }

    /// <inheritdoc/>
    public Task OpenDoorAsync(int door = 1, CancellationToken cancellationToken = default) =>
        ExecuteActionsAsync(new[] { DeviceAction.OpenDoor(door) }, cancellationToken);

    /// <inheritdoc/>
    public Task OpenSecBoxAsync(long secBoxId = 65793, int reason = 3, CancellationToken cancellationToken = default) =>
        ExecuteActionsAsync(new[] { DeviceAction.OpenSecBox(secBoxId, reason) }, cancellationToken);

    /// <inheritdoc/>
    public Task ReleaseTurnstileAsync(TurnstileDirection direction, CancellationToken cancellationToken = default) =>
        ExecuteActionsAsync(new[] { DeviceAction.ReleaseTurnstile(direction) }, cancellationToken);

    /// <inheritdoc/>
    public async Task<int> CreateOrModifyObjectsAsync<T>(string @object, IEnumerable<T> values, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?> { ["object"] = @object, ["values"] = (values ?? throw new ArgumentNullException(nameof(values))).ToList() };
        var response = await _transport.PostAsync<ChangesResponse>("create_or_modify_objects.fcgi", body, cancellationToken).ConfigureAwait(false);
        return response.Changes;
    }

    /// <inheritdoc/>
    public Task ShowMessageAsync(string message, TimeSpan? duration = null, CancellationToken cancellationToken = default)
    {
        if (duration is { } d && d < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "A duração não pode ser negativa.");
        }

        return _transport.PostRawAsync(
            "message_to_screen.fcgi",
            MessageBody(message ?? throw new ArgumentNullException(nameof(message)), duration),
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task ClearMessageAsync(CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync("message_to_screen.fcgi", MessageBody(string.Empty, null), cancellationToken);

    /// <inheritdoc/>
    public Task BuzzAsync(TimeSpan duration, int frequency = 4000, int dutyCycle = 50, CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync("buzzer_buzz.fcgi", BuzzBody(duration, frequency, dutyCycle), cancellationToken);

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

        await _transport.PostRawAsync(
            "change_login.fcgi", new Dictionary<string, object> { ["login"] = login, ["password"] = password }, cancellationToken).ConfigureAwait(false);
        _transport.UpdateCredentials(login, password);
    }

    /// <inheritdoc/>
    public Task<PasswordHash> HashPasswordAsync(string password, CancellationToken cancellationToken = default) =>
        _transport.PostAsync<PasswordHash>(
            "user_hash_password.fcgi",
            new Dictionary<string, object> { ["password"] = password ?? throw new ArgumentNullException(nameof(password)) },
            cancellationToken);

    /// <inheritdoc/>
    public Task RebootAsync(CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync("reboot.fcgi", null, cancellationToken);

    /// <inheritdoc/>
    public Task RebootToUpdateModeAsync(CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync("reboot_recovery.fcgi", null, cancellationToken);

    /// <inheritdoc/>
    public Task ResetToFactoryDefaultAsync(bool keepNetworkSettings, CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync(
            "reset_to_factory_default.fcgi", new Dictionary<string, object> { ["keep_network_info"] = keepNetworkSettings }, cancellationToken);

    /// <inheritdoc/>
    public Task DeleteAllAdministratorsAsync(CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync("delete_admins.fcgi", null, cancellationToken);

    /// <inheritdoc/>
    public Task SetNetworkAsync(NetworkSettings settings, CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync("set_system_network.fcgi", settings ?? throw new ArgumentNullException(nameof(settings)), cancellationToken);

    /// <inheritdoc/>
    public async Task<byte[]> GetLogoAsync(int? slot = null, CancellationToken cancellationToken = default)
    {
        var (content, _) = await _transport.PostForBytesAsync("logo.fcgi", null, cancellationToken, SlotQuery(slot)).ConfigureAwait(false);
        return content;
    }

    /// <inheritdoc/>
    public Task SetLogoAsync(byte[] png, int? slot = null, CancellationToken cancellationToken = default)
    {
        if (png is null || png.Length == 0)
        {
            throw new ArgumentException("Informe a imagem PNG.", nameof(png));
        }

        if (png.Length >= MaxImageBytes)
        {
            throw new ArgumentException("O equipamento só aceita logotipos menores que 1 MB.", nameof(png));
        }

        return _transport.PostBytesAsync("logo_change.fcgi", png, "application/octet-stream", cancellationToken, SlotQuery(slot));
    }

    /// <inheritdoc/>
    public Task DeleteLogoAsync(int? slot = null, CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync("logo_destroy.fcgi", null, cancellationToken, SlotQuery(slot));

    /// <inheritdoc/>
    public Task ShowLogoAsync(int slot, CancellationToken cancellationToken = default)
    {
        if (slot is < 0 or > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(slot), slot, "Use 1 a 8, ou 0 para não exibir logotipo.");
        }

        return SetConfigurationAsync(GeneralSetting("show_logo", slot.ToString(CultureInfo.InvariantCulture)), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<GpioState> GetGpioStateAsync(int gpio, CancellationToken cancellationToken = default) =>
        _transport.PostAsync<GpioState>("gpio_state.fcgi", new Dictionary<string, object> { ["gpio"] = gpio }, cancellationToken);

    /// <inheritdoc/>
    public Task RefreshLedAsync(CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync("led_rgb_refresh.fcgi", null, cancellationToken);

    /// <inheritdoc/>
    public Task SetUsbPortEnabledAsync(bool enabled, CancellationToken cancellationToken = default) =>
        SetConfigurationAsync(GeneralSetting("usb_port_enabled", enabled ? "1" : "0"), cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DeviceActionStatus>> ExecuteActionsWithStatusAsync(
        IEnumerable<DeviceAction> actions, CancellationToken cancellationToken = default)
    {
        var response = await ExecuteActionsAsync(actions, cancellationToken).ConfigureAwait(false);
        if (response is not { ValueKind: JsonValueKind.Object } root
            || !root.TryGetProperty("actions", out var list)
            || list.ValueKind != JsonValueKind.Array)
        {
            return new List<DeviceActionStatus>();
        }

        return ControlIdJson.Deserialize<List<DeviceActionStatus>>(list.GetRawText());
    }

    /// <inheritdoc/>
    public Task<DoorsState> GetDoorStatesAsync(CancellationToken cancellationToken = default) =>
        // O título da documentação diz door_state; o exemplo da mesma página usa doors_state.
        _transport.PostAsync<DoorsState>("door_state.fcgi", null, cancellationToken);

    /// <inheritdoc/>
    public Task<AlarmStatus> GetAlarmStatusAsync(CancellationToken cancellationToken = default) =>
        // "stop" tem padrão true na documentação: enviado false para a consulta não desligar o alarme.
        _transport.PostAsync<AlarmStatus>("alarm_status.fcgi", new Dictionary<string, object> { ["stop"] = false }, cancellationToken);

    /// <inheritdoc/>
    public Task StopAlarmAsync(CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync("alarm_status.fcgi", new Dictionary<string, object> { ["stop"] = true }, cancellationToken);

    /// <inheritdoc/>
    public Task RebootWithoutSessionAsync(CancellationToken cancellationToken = default) =>
        _transport.GetBytesAsync("reboot", cancellationToken, requiresSession: false);

    /// <inheritdoc/>
    public Task SetTurnstileLedAsync(TurnstileLedEvent ledEvent, uint? argbColor = null, CancellationToken cancellationToken = default)
    {
        if (ledEvent == TurnstileLedEvent.SetIdleColor && argbColor is null)
        {
            throw new ArgumentException("Informe a cor para trocar a cor de repouso.", nameof(argbColor));
        }

        var body = new Dictionary<string, object>
        {
            ["sec_box"] = new Dictionary<string, object>
            {
                ["color"] = (argbColor ?? 0).ToString(CultureInfo.InvariantCulture),
                ["event"] = (int)ledEvent,
            },
        };
        return _transport.PostRawAsync("remote_led_control.fcgi", body, cancellationToken);
    }

    /// <inheritdoc/>
    public Task UploadAdvertisingVideoAsync(byte[] mp4, CancellationToken cancellationToken = default) =>
        UploadInPartsAsync("send_video.fcgi", mp4, 20 * MaxImageBytes, null, nameof(mp4), cancellationToken);

    /// <inheritdoc/>
    public Task SetAdvertisingModeAsync(bool enabled, CancellationToken cancellationToken = default) =>
        // A tabela da documentação diz "custom_video_enable" (bool); o exemplo usa "custom_video_enabled" com "1"/"0".
        _transport.PostRawAsync(
            "set_custom_video.fcgi", new Dictionary<string, object> { ["custom_video_enabled"] = enabled ? "1" : "0" }, cancellationToken);

    /// <inheritdoc/>
    public Task RemoveAdvertisingVideoAsync(CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync("remove_custom_video.fcgi", null, cancellationToken);

    /// <inheritdoc/>
    public Task UploadAccessAudioAsync(AccessAudioEvent audioEvent, byte[] audio, CancellationToken cancellationToken = default) =>
        UploadInPartsAsync("set_audio_access_message.fcgi", audio, 5 * MaxImageBytes, "event=" + AudioEventName(audioEvent), nameof(audio), cancellationToken);

    /// <inheritdoc/>
    public async Task<byte[]> GetAccessAudioAsync(AccessAudioEvent audioEvent, CancellationToken cancellationToken = default)
    {
        var (content, _) = await _transport.PostForBytesAsync(
            "get_audio_access_message.fcgi", new Dictionary<string, object> { ["event"] = AudioEventName(audioEvent) }, cancellationToken).ConfigureAwait(false);
        return content;
    }

    /// <inheritdoc/>
    public Task<AccessAudioMessages> GetAccessAudioStatusAsync(CancellationToken cancellationToken = default) =>
        _transport.PostAsync<AccessAudioMessages>("has_audio_access_messages.fcgi", null, cancellationToken);

    /// <inheritdoc/>
    public Task MakeSipCallAsync(string target, CancellationToken cancellationToken = default) =>
        string.IsNullOrWhiteSpace(target)
            ? throw new ArgumentException("Informe o ramal.", nameof(target))
            : _transport.PostRawAsync("make_sip_call.fcgi", new Dictionary<string, object> { ["target"] = target }, cancellationToken);

    /// <inheritdoc/>
    public Task FinalizeSipCallAsync(CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync("finalize_sip_call.fcgi", null, cancellationToken);

    /// <inheritdoc/>
    public Task<SipStatus> GetSipStatusAsync(CancellationToken cancellationToken = default) =>
        _transport.PostAsync<SipStatus>("get_sip_status.fcgi", null, cancellationToken);

    /// <inheritdoc/>
    public Task UploadSipCallAudioAsync(byte[] wav, CancellationToken cancellationToken = default) =>
        UploadInPartsAsync("set_pjsip_audio_message.fcgi", wav, 5 * MaxImageBytes, null, nameof(wav), cancellationToken);

    /// <inheritdoc/>
    public async Task<byte[]> GetSipCallAudioAsync(CancellationToken cancellationToken = default)
    {
        var (content, _) = await _transport.PostForBytesAsync("get_pjsip_audio_message.fcgi", null, cancellationToken).ConfigureAwait(false);
        return content;
    }

    /// <inheritdoc/>
    public async Task<bool> HasSipCallAudioAsync(CancellationToken cancellationToken = default) =>
        (await _transport.PostAsync<FileExistsResponse>("has_pjsip_audio_message.fcgi", null, cancellationToken).ConfigureAwait(false)).FileExists;

    /// <inheritdoc/>
    public Uri GetRtspStreamUri(int port = 554, string? username = null, string? password = null)
    {
        var builder = new UriBuilder("rtsp", _baseAddress.Host, port, "main_stream");
        if (!string.IsNullOrEmpty(username))
        {
            builder.UserName = Uri.EscapeDataString(username!);
            builder.Password = Uri.EscapeDataString(password ?? string.Empty);
        }

        return builder.Uri;
    }

    /// <inheritdoc/>
    public async Task<bool> TestInternetConnectionAsync(CancellationToken cancellationToken = default) =>
        // A documentação usa GET neste teste e POST nos demais.
        (await GetJsonAsync<ConnectionTestResponse>("connection_test.fcgi", cancellationToken).ConfigureAwait(false)).ConnectionResult;

    /// <inheritdoc/>
    public async Task<string> PingAsync(string destination, CancellationToken cancellationToken = default) =>
        (await _transport.PostAsync<PingResponse>(
            "ping_test.fcgi", new Dictionary<string, object> { ["dst"] = Required(destination, nameof(destination)) }, cancellationToken)
            .ConfigureAwait(false)).Output ?? string.Empty;

    /// <inheritdoc/>
    public async Task<string> NsLookupAsync(string destination, string dnsServer, CancellationToken cancellationToken = default) =>
        (await _transport.PostAsync<NsLookupResponse>(
            "nslookup_test.fcgi",
            new Dictionary<string, object> { ["dst"] = Required(destination, nameof(destination)), ["dns"] = Required(dnsServer, nameof(dnsServer)) },
            cancellationToken).ConfigureAwait(false)).Output ?? string.Empty;

    /// <inheritdoc/>
    public Task SetNetworkInterlockAsync(bool enabled, bool bypassForApi, bool bypassForExitButton, CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync(
            "set_network_interlock.fcgi",
            new Dictionary<string, object>
            {
                ["interlock_enabled"] = enabled ? 1 : 0,
                ["api_bypass_enabled"] = bypassForApi ? 1 : 0,
                ["rex_bypass_enabled"] = bypassForExitButton ? 1 : 0,
            },
            cancellationToken);

    /// <inheritdoc/>
    public async Task<bool> IsValidBiometryAsync(string template, CancellationToken cancellationToken = default) =>
        (await _transport.PostAsync<BiometryResponse>(
            "is_valid_biometry.fcgi", new Dictionary<string, object> { ["biometry"] = Required(template, nameof(template)) }, cancellationToken)
            .ConfigureAwait(false)).IsValid;

    /// <inheritdoc/>
    public Task SetVpnAsync(VpnSettings settings, CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync("set_vpn_information.fcgi", settings ?? throw new ArgumentNullException(nameof(settings)), cancellationToken);

    /// <inheritdoc/>
    public Task<VpnInformation> GetVpnInformationAsync(CancellationToken cancellationToken = default) =>
        GetJsonAsync<VpnInformation>("get_vpn_information.fcgi", cancellationToken);

    /// <inheritdoc/>
    public async Task<VpnStatus> GetVpnStatusAsync(CancellationToken cancellationToken = default) =>
        (VpnStatus)(await GetJsonAsync<VpnStatusResponse>("get_vpn_status.fcgi", cancellationToken).ConfigureAwait(false)).Status;

    /// <inheritdoc/>
    public Task UploadVpnFileAsync(byte[] file, VpnFileType fileType, CancellationToken cancellationToken = default) =>
        file is null || file.Length == 0
            ? throw new ArgumentException("Informe o arquivo.", nameof(file))
            : _transport.PostBytesAsync(
                "set_vpn_file.fcgi", file, "application/octet-stream", cancellationToken, "file_type=" + (fileType == VpnFileType.Zip ? "zip" : "config"));

    /// <inheritdoc/>
    public async Task<bool> HasVpnFileAsync(CancellationToken cancellationToken = default) =>
        (await GetJsonAsync<HasFileResponse>("has_vpn_file.fcgi", cancellationToken).ConfigureAwait(false)).HasFile;

    /// <inheritdoc/>
    public Task<byte[]> GetVpnExampleFilesAsync(CancellationToken cancellationToken = default) =>
        _transport.GetBytesAsync("get_vpn_file.fcgi", cancellationToken);

    /// <inheritdoc/>
    public Task UploadSslCertificateAsync(byte[] pem, CancellationToken cancellationToken = default) =>
        pem is null || pem.Length == 0
            ? throw new ArgumentException("Informe o certificado.", nameof(pem))
            : _transport.PostBytesAsync("ssl_certificate_change.fcgi", pem, "application/octet-stream", cancellationToken);

    /// <inheritdoc/>
    public Task Configure8021XAsync(
        bool enabled, string? login = null, string? password = null,
        Ieee8021XInnerAuthentication innerAuthentication = Ieee8021XInnerAuthentication.MsChapV2, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object> { ["enabled"] = enabled, ["inner_auth"] = (int)innerAuthentication };
        if (login is not null)
        {
            body["login"] = login;
        }

        if (password is not null)
        {
            body["password"] = password;
        }

        return _transport.PostRawAsync("configure_802_1X.fcgi", body, cancellationToken);
    }

    /// <inheritdoc/>
    public Task SetOsdpInstallationModeAsync(bool enabled, CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync("set_osdp_installation_mode.fcgi", new Dictionary<string, object> { ["enabled"] = enabled }, cancellationToken);

    /// <inheritdoc/>
    public Task ChangeIdCloudCodeAsync(CancellationToken cancellationToken = default) =>
        _transport.GetBytesAsync("change_idcloud_code.fcgi", cancellationToken);

    /// <inheritdoc/>
    public Task ApplyFaceLicenseAsync(FaceLicense license, string password, CancellationToken cancellationToken = default)
    {
        var endpoint = license switch
        {
            FaceLicense.Pro10k => "upgrade_ten_thousand_face_templates.fcgi",
            FaceLicense.Pro15k => "upgrade_fifteen_thousand_face_templates.fcgi",
            FaceLicense.Pro30k => "upgrade_thirty_thousand_face_templates.fcgi",
            FaceLicense.Pro50k => "upgrade_fifty_thousand_face_templates.fcgi",
            FaceLicense.Pro100k => "upgrade_hundred_thousand_face_templates.fcgi",
            _ => throw new ArgumentOutOfRangeException(nameof(license)),
        };

        return _transport.PostRawAsync(endpoint, new Dictionary<string, object> { ["password"] = Required(password, nameof(password)) }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task UpgradeToEnterpriseAsync(string password, CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync(
            "idflex_upgrade_enterprise.fcgi", new Dictionary<string, object> { ["password"] = Required(password, nameof(password)) }, cancellationToken);

    /// <inheritdoc/>
    public Task<AccessSystemInformation> GetSystemInformationAsync(CancellationToken cancellationToken = default) =>
        _transport.PostAsync<AccessSystemInformation>("system_information.fcgi", null, cancellationToken);

    /// <inheritdoc/>
    public Task SetConfigurationAsync(object configuration, CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync("set_configuration.fcgi", configuration ?? throw new ArgumentNullException(nameof(configuration)), cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> GetConfigurationAsync(
        IReadOnlyDictionary<string, IEnumerable<string>> fields, CancellationToken cancellationToken = default)
    {
        if (fields is null)
        {
            throw new ArgumentNullException(nameof(fields));
        }

        // O firmware recusa a requisição inteira se um único parâmetro não existir nele (o iDFace não tem
        // general.catra_timeout, por exemplo). O erro diz qual é: ele sai da lista e a leitura é repetida.
        var body = new Dictionary<string, List<string>>();
        foreach (var section in fields)
        {
            var requested = (section.Value ?? throw new ArgumentException($"Campos nulos na seção '{section.Key}'.", nameof(fields))).Distinct().ToList();
            var known = requested.Where(field => !_missingConfigFields.ContainsKey(section.Key + "." + field)).ToList();
            if (known.Count > 0 || requested.Count == 0)
            {
                body[section.Key] = known;
            }
        }

        Dictionary<string, Dictionary<string, JsonElement>> response;
        while (true)
        {
            if (body.Count == 0)
            {
                return new Dictionary<string, IReadOnlyDictionary<string, string>>();
            }

            try
            {
                response = await _transport.PostAsync<Dictionary<string, Dictionary<string, JsonElement>>>("get_configuration.fcgi", body, cancellationToken).ConfigureAwait(false);
                break;
            }
            catch (ControlIdException ex) when (FindMissingConfigField(ex, body) is { } missing)
            {
                _missingConfigFields.TryAdd(missing.Section + "." + missing.Field, 0);
                body[missing.Section].Remove(missing.Field);
                if (body[missing.Section].Count == 0)
                {
                    body.Remove(missing.Section);
                }
            }
        }

        return response.ToDictionary(
            section => section.Key,
            section => (IReadOnlyDictionary<string, string>)section.Value.ToDictionary(
                field => field.Key,
                // Os parâmetros são strings na API; números ou booleanos de algum firmware viram o texto JSON.
                field => field.Value.ValueKind == JsonValueKind.String ? field.Value.GetString()! : field.Value.GetRawText()));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, string>> GetConfigurationAsync(
        string section, IEnumerable<string> fields, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(section))
        {
            throw new ArgumentException("Informe a seção, ex.: \"general\".", nameof(section));
        }

        var all = await GetConfigurationAsync(
            new Dictionary<string, IEnumerable<string>> { [section] = fields ?? throw new ArgumentNullException(nameof(fields)) },
            cancellationToken).ConfigureAwait(false);
        return all.TryGetValue(section, out var values) ? values : new Dictionary<string, string>();
    }

    /// <inheritdoc/>
    public Task<T> GetConfigurationSectionAsync<T>(CancellationToken cancellationToken = default)
        where T : Configuration.ConfigurationSection, new() =>
        GetConfigurationSectionAsync(new T(), cancellationToken);

    /// <inheritdoc/>
    public async Task<T> GetConfigurationSectionAsync<T>(T section, CancellationToken cancellationToken = default)
        where T : Configuration.ConfigurationSection
    {
        if (section is null)
        {
            throw new ArgumentNullException(nameof(section));
        }

        var values = await GetConfigurationAsync(
            section.SectionName, Configuration.ConfigurationSectionMapper.FieldNames(section), cancellationToken).ConfigureAwait(false);
        Configuration.ConfigurationSectionMapper.Populate(section, values);
        return section;
    }

    /// <inheritdoc/>
    public Task SetConfigurationSectionAsync(Configuration.ConfigurationSection section, CancellationToken cancellationToken = default) =>
        SetConfigurationSectionsAsync(new[] { section ?? throw new ArgumentNullException(nameof(section)) }, cancellationToken);

    /// <inheritdoc/>
    public Task SetConfigurationSectionsAsync(IEnumerable<Configuration.ConfigurationSection> sections, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object>();
        foreach (var section in sections ?? throw new ArgumentNullException(nameof(sections)))
        {
            var values = Configuration.ConfigurationSectionMapper.ToValues(section);
            if (values.Count > 0)
            {
                body[section.SectionName] = values;
            }
        }

        if (body.Count == 0)
        {
            throw new ArgumentException("Nenhum parâmetro preenchido: preencha ao menos uma propriedade da seção.", nameof(sections));
        }

        return SetConfigurationAsync(body, cancellationToken);
    }

    /// <inheritdoc/>
    public Task ConfigureMonitorAsync(MonitorConfiguration configuration, CancellationToken cancellationToken = default) =>
        SetConfigurationAsync((configuration ?? throw new ArgumentNullException(nameof(configuration))).ToConfiguration(), cancellationToken);

    /// <inheritdoc/>
    public Task ConfigurePushAsync(PushConfiguration configuration, CancellationToken cancellationToken = default) =>
        SetConfigurationAsync((configuration ?? throw new ArgumentNullException(nameof(configuration))).ToConfiguration(), cancellationToken);

    /// <inheritdoc/>
    public async Task<long> ConfigureOnlineModeAsync(OnlineModeConfiguration configuration, CancellationToken cancellationToken = default)
    {
        if (configuration is null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        configuration.Validate();
        var address = configuration.ServerAddress.Trim();

        // Passo 1: o servidor é um registro em "devices". Só precisa existir uma vez.
        var existing = await LoadObjectsAsync<ServerDevice>(
            new LoadObjectsRequest("devices") { Where = Where.Equal("devices", "ip", address) }, cancellationToken).ConfigureAwait(false);
        var serverId = existing.Count > 0 && existing[0].Id is { } id
            ? id
            : await CreateOneAsync("devices", new ServerDevice { Name = configuration.ServerName, Ip = address, PublicKey = string.Empty }, cancellationToken)
                .ConfigureAwait(false);

        // Passos 2 e 3: aponta para o servidor e liga o modo online. A API espera os valores como texto.
        var onlineClient = new Dictionary<string, string> { ["server_id"] = serverId.ToString(CultureInfo.InvariantCulture) };
        AddFlag(onlineClient, "extract_template", configuration.ExtractTemplate);
        AddFlag(onlineClient, "contingency_enabled", configuration.ContingencyEnabled);
        if (configuration.MaxRequestAttempts is { } attempts)
        {
            onlineClient["max_request_attempts"] = attempts.ToString(CultureInfo.InvariantCulture);
        }

        if (configuration.RequestTimeout is { } timeout)
        {
            onlineClient["request_timeout"] = ((long)timeout.TotalMilliseconds).ToString(CultureInfo.InvariantCulture);
        }

        if (configuration.AliveInterval is { } alive)
        {
            onlineClient["alive_interval"] = ((long)alive.TotalMilliseconds).ToString(CultureInfo.InvariantCulture);
        }

        var enterprise = configuration.Mode == OnlineMode.Enterprise;
        var general = new Dictionary<string, string>
        {
            ["online"] = "1",
            ["local_identification"] = enterprise ? "0" : "1",
        };

        // Só a IHM tem ihm_enterprise_mode: o iDFace, por exemplo, recusa o set_configuration inteiro se ele vier.
        if (configuration.IsIhm)
        {
            general["ihm_enterprise_mode"] = enterprise ? "1" : "0";
        }

        await SetConfigurationAsync(
            new Dictionary<string, object> { ["online_client"] = onlineClient, ["general"] = general }, cancellationToken).ConfigureAwait(false);

        return serverId;
    }

    /// <inheritdoc/>
    public Task DisableOnlineModeAsync(CancellationToken cancellationToken = default) =>
        SetConfigurationAsync(
            new Dictionary<string, object> { ["general"] = new Dictionary<string, string> { ["online"] = "0" } }, cancellationToken);

    /// <inheritdoc/>
    public Task AuthorizeRemotelyAsync(OnlineAuthorization authorization, CancellationToken cancellationToken = default)
    {
        if (authorization is null)
        {
            throw new ArgumentNullException(nameof(authorization));
        }

        // O firmware exige user_id, portal_id, user_name, user_image e actions, mesmo sem usuário (não identificado).
        // Os que faltarem vão vazios, numa cópia: o objeto recebido não muda.
        var body = authorization.UserId is null || authorization.PortalId is null || authorization.UserName is null
            || authorization.UserImage is null || authorization.Actions is null
            ? new OnlineAuthorization
            {
                Event = authorization.Event,
                UserId = authorization.UserId ?? 0,
                UserName = authorization.UserName ?? string.Empty,
                UserImage = authorization.UserImage ?? false,
                UserImageHash = authorization.UserImageHash,
                PortalId = authorization.PortalId ?? 1,
                Actions = authorization.Actions ?? new List<DeviceAction>(),
                Duress = authorization.Duress,
                Message = authorization.Message,
            }
            : authorization;
        return _transport.PostRawAsync("remote_user_authorization.fcgi", body, cancellationToken);
    }

    /// <inheritdoc/>
    public Task SetSystemTimeAsync(DateTime localTime, CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync("set_system_time.fcgi", DeviceDateTime.From(localTime), cancellationToken);

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

    /// <inheritdoc/>
    public void Dispose() => _transport.Dispose();

    /// <summary>Corpo de <c>message_to_screen</c>: timeout 0 = mensagem fixa.</summary>
    internal static Dictionary<string, object> MessageBody(string message, TimeSpan? duration) => new()
    {
        ["message"] = message,
        ["timeout"] = duration is { } value ? (long)value.TotalMilliseconds : 0,
    };

    /// <summary>Corpo de <c>buzzer_buzz</c>, validado contra os limites da documentação.</summary>
    internal static Dictionary<string, object> BuzzBody(TimeSpan duration, int frequency, int dutyCycle)
    {
        if (duration <= TimeSpan.Zero || duration > MaxBuzz)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "O buzzer aceita até 3 s por chamada.");
        }

        if (frequency <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Informe a frequência em Hz.");
        }

        if (dutyCycle is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(dutyCycle), dutyCycle, "Use de 0 a 100.");
        }

        return new Dictionary<string, object>
        {
            ["frequency"] = frequency,
            ["duty_cycle"] = dutyCycle,
            ["timeout"] = (long)duration.TotalMilliseconds,
        };
    }

    private static Dictionary<string, object> GeneralSetting(string key, string value) =>
        new() { ["general"] = new Dictionary<string, string> { [key] = value } };

    // A documentação é ambígua sobre a base do slot (1 a 8 vs. "id=5 é a 6ª posição"), então o valor vai como recebido.
    private static string? SlotQuery(int? slot)
    {
        if (slot is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(slot), slot, "O slot não pode ser negativo.");
        }

        return slot is { } value ? "id=" + value.ToString(CultureInfo.InvariantCulture) : null;
    }

    private async Task TryCancelRemoteEnrollAsync()
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await CancelRemoteEnrollAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ControlIdException or OperationCanceledException)
        {
            // Melhor esforço: a falha original é a que importa para quem chamou.
        }
    }

    // Leitura incremental de tabelas de log: id > afterId, em ordem crescente.
    private Task<IReadOnlyList<T>> LoadAfterAsync<T>(string @object, long afterId, int? limit, CancellationToken cancellationToken) =>
        LoadObjectsAsync<T>(
            new LoadObjectsRequest(@object)
            {
                Where = Where.Compare(@object, "id", ">", afterId),
                Order = new List<string> { "ascending", "id" },
                Limit = limit,
            },
            cancellationToken);

    // Páginas por ID (id > último lido): estável mesmo com inserções e remoções durante a leitura.
    private async IAsyncEnumerable<T> EnumerateAfterAsync<T>(
        string @object, long afterId, int pageSize, Func<T, long> getId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (true)
        {
            var page = await LoadAfterAsync<T>(@object, afterId, pageSize, cancellationToken).ConfigureAwait(false);
            foreach (var item in page)
            {
                yield return item;
            }

            if (page.Count < pageSize)
            {
                yield break;
            }

            var lastId = getId(page[page.Count - 1]);
            if (lastId <= afterId)
            {
                // O equipamento não respeitou o filtro/ordem; parar evita um laço infinito.
                throw new ControlIdException($"'{@object}' devolveu uma página fora de ordem (ID {lastId} depois de {afterId}).");
            }

            afterId = lastId;
        }
    }

    // Páginas por limit/offset, para objetos sem ID numérico ou com filtro livre.
    private async IAsyncEnumerable<T> EnumerateByOffsetAsync<T>(
        LoadObjectsRequest request, int pageSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var offset = request.Offset ?? 0;
        var remaining = request.Limit ?? int.MaxValue;
        while (remaining > 0)
        {
            var limit = Math.Min(pageSize, remaining);
            var page = await LoadObjectsAsync<T>(request.WithPage(limit, offset), cancellationToken).ConfigureAwait(false);
            foreach (var item in page)
            {
                yield return item;
            }

            if (page.Count < limit)
            {
                yield break;
            }

            offset += page.Count;
            remaining -= page.Count;
        }
    }

    private static void ValidatePageSize(int pageSize)
    {
        if (pageSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, "O tamanho da página precisa ser pelo menos 1.");
        }
    }

    private Task<IReadOnlyList<T>> LoadAsync<T>(string @object, string filterField, long? filterValue, CancellationToken cancellationToken) =>
        LoadObjectsAsync<T>(
            new LoadObjectsRequest(@object) { Where = filterValue is { } value ? Where.Equal(@object, filterField, value) : null },
            cancellationToken);

    private async Task<long> CreateOneAsync<T>(string @object, T value, CancellationToken cancellationToken)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        var ids = await CreateObjectsAsync(@object, new[] { value }, cancellationToken).ConfigureAwait(false);
        return ids.Count > 0 ? ids[0] : throw new ControlIdException($"O equipamento não retornou o ID criado em '{@object}'.");
    }

    private async Task<bool> DeleteByIdAsync(string @object, long id, CancellationToken cancellationToken) =>
        await DestroyObjectsAsync(@object, Where.Equal(@object, "id", id), cancellationToken).ConfigureAwait(false) > 0;

    private sealed class SessionIsValidResponse
    {
        [JsonPropertyName("session_is_valid")]
        public bool SessionIsValid { get; set; }
    }

    private static void AddFlag(Dictionary<string, string> values, string key, bool? flag)
    {
        if (flag is { } value)
        {
            values[key] = value ? "1" : "0";
        }
    }

    private sealed class ServerDevice
    {
        [JsonPropertyName("id")]
        public long? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("ip")]
        public string? Ip { get; set; }

        [JsonPropertyName("public_key")]
        public string? PublicKey { get; set; }
    }

    // Vídeo e áudio vão em partes de até 2 MB (current/total na query), como a documentação exige.
    private async Task UploadInPartsAsync(
        string endpoint, byte[] content, int maxBytes, string? extraQuery, string paramName, CancellationToken cancellationToken)
    {
        if (content is null || content.Length == 0)
        {
            throw new ArgumentException("Informe o arquivo.", paramName);
        }

        if (content.Length > maxBytes)
        {
            throw new ArgumentException($"O equipamento aceita arquivos de até {maxBytes / MaxImageBytes} MB.", paramName);
        }

        const int partSize = 2 * MaxImageBytes;
        var total = (content.Length + partSize - 1) / partSize;
        for (var current = 1; current <= total; current++)
        {
            var offset = (current - 1) * partSize;
            var part = new byte[Math.Min(partSize, content.Length - offset)];
            Buffer.BlockCopy(content, offset, part, 0, part.Length);

            var query = (extraQuery is null ? string.Empty : extraQuery + "&")
                + "current=" + current.ToString(CultureInfo.InvariantCulture) + "&total=" + total.ToString(CultureInfo.InvariantCulture);
            await _transport.PostBytesAsync(endpoint, part, "application/octet-stream", cancellationToken, query).ConfigureAwait(false);
        }
    }

    private static string AudioEventName(AccessAudioEvent audioEvent) => audioEvent switch
    {
        AccessAudioEvent.NotIdentified => "not_identified",
        AccessAudioEvent.Authorized => "authorized",
        AccessAudioEvent.NotAuthorized => "not_authorized",
        AccessAudioEvent.UseMask => "use_mask",
        _ => throw new ArgumentOutOfRangeException(nameof(audioEvent)),
    };

    private async Task<T> GetJsonAsync<T>(string endpoint, CancellationToken cancellationToken)
    {
        var bytes = await _transport.GetBytesAsync(endpoint, cancellationToken).ConfigureAwait(false);
        return ControlIdJson.Deserialize<T>(TextDecoding.Decode(bytes, null));
    }

    private static string Required(string value, string paramName) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Valor obrigatório.", paramName) : value;

    private sealed class ConnectionTestResponse
    {
        [JsonPropertyName("connection_result")]
        public bool ConnectionResult { get; set; }
    }

    private sealed class PingResponse
    {
        [JsonPropertyName("output")]
        public string? Output { get; set; }
    }

    private sealed class NsLookupResponse
    {
        [JsonPropertyName("output_ns")]
        public string? Output { get; set; }
    }

    private sealed class BiometryResponse
    {
        [JsonPropertyName("isValid")]
        public bool IsValid { get; set; }
    }

    private sealed class VpnStatusResponse
    {
        [JsonPropertyName("status")]
        public int Status { get; set; }
    }

    private sealed class HasFileResponse
    {
        [JsonPropertyName("has_file")]
        public bool HasFile { get; set; }
    }

    private sealed class FileExistsResponse
    {
        [JsonPropertyName("file_exists")]
        public bool FileExists { get; set; }
    }

    private static void ValidateImage(byte[] image, string paramName)
    {
        if (image is null || image.Length == 0)
        {
            throw new ArgumentException("Informe a imagem.", paramName);
        }

        if (image.Length >= MaxImageBytes)
        {
            throw new ArgumentException("O equipamento só aceita imagens menores que 1 MB.", paramName);
        }
    }

    private static (string Section, string Field)? FindMissingConfigField(ControlIdException exception, Dictionary<string, List<string>> body)
    {
        var match = MissingConfigFieldError.Match(exception.ResponseBody ?? exception.Message);
        if (!match.Success)
        {
            return null;
        }

        var section = match.Groups["section"].Value;
        var field = match.Groups["field"].Value;
        return body.TryGetValue(section, out var requested) && requested.Contains(field) ? (section, field) : null;
    }

    // Equipamentos sem reconhecimento facial não devolvem nada ao gravar foto: isso conta como sucesso.
    private static FaceImageResult ParseFaceResult(string text) =>
        string.IsNullOrWhiteSpace(text) ? new FaceImageResult { Success = true } : ControlIdJson.Deserialize<FaceImageResult>(text);

    private static IReadOnlyList<FaceImageResult> ParseResults(string text) =>
        string.IsNullOrWhiteSpace(text) ? new List<FaceImageResult>() : ControlIdJson.Deserialize<ResultsResponse>(text).Results ?? new List<FaceImageResult>();

    private sealed class ResultsResponse
    {
        [JsonPropertyName("results")]
        public List<FaceImageResult>? Results { get; set; }
    }

    private sealed class UserImagesResponse
    {
        [JsonPropertyName("user_images")]
        public List<UserImageEntry>? UserImages { get; set; }
    }

    private sealed class ImageInfoResponse
    {
        [JsonPropertyName("image_info")]
        public List<UserImageInfo>? ImageInfo { get; set; }
    }

    private sealed class UserIdsResponse
    {
        [JsonPropertyName("user_ids")]
        public List<long>? UserIds { get; set; }
    }
}
