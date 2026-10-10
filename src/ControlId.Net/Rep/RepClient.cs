using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
    {
        _transport = new ControlIdTransport(options, httpClient);
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

    /// <inheritdoc/>
    public void Dispose() => _transport.Dispose();

    // O AFD é ISO-8859-1 pelas portarias; ler como UTF-8 sem charset estragaria os nomes acentuados.
    private async Task<string> GetAfdTextAsync(object? body, CancellationToken cancellationToken)
    {
        var (content, charSet) = await _transport.PostForBytesAsync("get_afd.fcgi", body, cancellationToken, ModeQuery).ConfigureAwait(false);
        return AfdParser.Decode(content, charSet);
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
