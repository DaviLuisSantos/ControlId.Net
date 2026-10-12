using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ControlId.Internal;

/// <summary>
/// Camada HTTP comum a todos os equipamentos Control iD: monta as URLs <c>*.fcgi</c>,
/// faz login sob demanda, guarda a sessão e refaz o login uma vez se ela expirar (HTTP 401).
/// </summary>
internal sealed class ControlIdTransport : IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;
    private readonly ControlIdDeviceOptions _options;
    private readonly SemaphoreSlim _loginLock = new(1, 1);

    // Limita as requisições em voo a este equipamento. Só envolve o envio HTTP e nunca é segurado enquanto se espera
    // o _loginLock, então não há espera circular entre os dois.
    private readonly SemaphoreSlim _throttle;
    private string? _session;

    // Cópia própria: change_login atualiza estas credenciais sem alterar o objeto de opções do chamador.
    private volatile Credentials _credentials;

    public ControlIdTransport(ControlIdDeviceOptions options, HttpClient? httpClient = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
        _credentials = new Credentials(options.Login, options.Password);
        _throttle = new SemaphoreSlim(options.MaxConcurrentRequests, options.MaxConcurrentRequests);

        if (httpClient is null)
        {
            var handler = new HttpClientHandler();
            HttpHandlers.LimitConnections(handler, options.MaxConcurrentRequests);
            if (options.AcceptAnyServerCertificate)
            {
                handler.ServerCertificateCustomValidationCallback = static (_, _, _, _) => true;
            }

            _http = new HttpClient(handler, disposeHandler: true) { Timeout = options.Timeout };
            _ownsHttpClient = true;
        }
        else
        {
            _http = httpClient;
        }
    }

    /// <summary>Sessão atual, ou <c>null</c> se ainda não logou.</summary>
    public string? Session => Volatile.Read(ref _session);

    public async Task LoginAsync(CancellationToken cancellationToken)
    {
        await _loginLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Volatile.Write(ref _session, await RequestSessionAsync(cancellationToken).ConfigureAwait(false));
        }
        finally
        {
            _loginLock.Release();
        }
    }

    public async Task LogoutAsync(CancellationToken cancellationToken)
    {
        var session = Interlocked.Exchange(ref _session, null);
        if (session is null)
        {
            return;
        }

        using var request = BuildRequest(HttpMethod.Post, "logout.fcgi", JsonContent(null), session, null);
        await _throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _throttle.Release();
        }

        // Logout é best-effort: o equipamento não retorna corpo e uma sessão já expirada não é erro.
    }

    /// <summary>Passa a usar novas credenciais nos próximos logins (após <c>change_login.fcgi</c>).</summary>
    public void UpdateCredentials(string login, string password) => _credentials = new Credentials(login, password);

    /// <summary>Envia um comando e desserializa a resposta JSON.</summary>
    public async Task<T> PostAsync<T>(string endpoint, object? body, CancellationToken cancellationToken, string? extraQuery = null)
    {
        var text = await PostRawAsync(endpoint, body, cancellationToken, extraQuery).ConfigureAwait(false);
        try
        {
            return ControlIdJson.Deserialize<T>(text);
        }
        catch (JsonException ex)
        {
            throw new ControlIdException($"Resposta inválida de '{endpoint}'.", responseBody: text, innerException: ex);
        }
    }

    /// <summary>Envia um comando e devolve o corpo da resposta como texto (ex.: AFD).</summary>
    public async Task<string> PostRawAsync(string endpoint, object? body, CancellationToken cancellationToken, string? extraQuery = null, bool requiresSession = true)
    {
        using var response = await SendCommandAsync(HttpMethod.Post, endpoint, () => JsonContent(body), extraQuery, requiresSession, cancellationToken).ConfigureAwait(false);
        return await ReadAsStringAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>POST com corpo JSON; devolve a resposta em bytes e o charset declarado (se houver), para o chamador decodificar.</summary>
    public async Task<(byte[] Content, string? CharSet)> PostForBytesAsync(string endpoint, object? body, CancellationToken cancellationToken, string? extraQuery = null)
    {
        using var response = await SendCommandAsync(HttpMethod.Post, endpoint, () => JsonContent(body), extraQuery, requiresSession: true, cancellationToken).ConfigureAwait(false);
        var content = await ReadAsByteArrayAsync(response, cancellationToken).ConfigureAwait(false);
        return (content, response.Content.Headers.ContentType?.CharSet);
    }

    /// <summary>Envia bytes crus (<paramref name="contentType"/>) e devolve o corpo da resposta como texto.</summary>
    public async Task<string> PostBytesAsync(string endpoint, byte[] content, string contentType, CancellationToken cancellationToken, string? extraQuery = null)
    {
        using var response = await SendCommandAsync(
            HttpMethod.Post,
            endpoint,
            () =>
            {
                var httpContent = new ByteArrayContent(content);
                httpContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                return httpContent;
            },
            extraQuery,
            requiresSession: true,
            cancellationToken).ConfigureAwait(false);
        return await ReadAsStringAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>GET sem corpo; devolve a resposta em bytes (ex.: foto do usuário).</summary>
    public async Task<byte[]> GetBytesAsync(string endpoint, CancellationToken cancellationToken, string? extraQuery = null, bool requiresSession = true)
    {
        using var response = await SendCommandAsync(HttpMethod.Get, endpoint, static () => null, extraQuery, requiresSession, cancellationToken).ConfigureAwait(false);
        return await ReadAsByteArrayAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Envia o comando com a sessão atual, refaz o login uma vez em 401 e lança <see cref="ControlIdException"/>
    /// se o equipamento devolver erro. O chamador descarta a resposta.
    /// </summary>
    private async Task<HttpResponseMessage> SendCommandAsync(
        HttpMethod method, string endpoint, Func<HttpContent?> createContent, string? extraQuery, bool requiresSession, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            var session = requiresSession ? await EnsureSessionAsync(cancellationToken).ConfigureAwait(false) : null;

            using var request = BuildRequest(method, endpoint, createContent(), session, extraQuery);
            var response = await SendAsync(request, endpoint, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.Unauthorized && requiresSession && attempt == 0)
            {
                // Sessão expirou ou o equipamento reiniciou: descarta e tenta de novo com login novo.
                response.Dispose();
                Interlocked.CompareExchange(ref _session, null, session);
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                using (response)
                {
                    var text = await ReadAsStringAsync(response, cancellationToken).ConfigureAwait(false);
                    EnsureSuccess(response, endpoint, text);
                }
            }

            return response;
        }
    }

    private async Task<string> EnsureSessionAsync(CancellationToken cancellationToken)
    {
        var current = Volatile.Read(ref _session);
        if (current is not null)
        {
            return current;
        }

        await _loginLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            current = Volatile.Read(ref _session);
            if (current is null)
            {
                current = await RequestSessionAsync(cancellationToken).ConfigureAwait(false);
                Volatile.Write(ref _session, current);
            }

            return current;
        }
        finally
        {
            _loginLock.Release();
        }
    }

    private async Task<string> RequestSessionAsync(CancellationToken cancellationToken)
    {
        var credentials = _credentials;
        var body = new LoginRequest { Login = credentials.Login, Password = credentials.Password };
        using var request = BuildRequest(HttpMethod.Post, "login.fcgi", JsonContent(body), null, null);
        using var response = await SendAsync(request, "login.fcgi", cancellationToken).ConfigureAwait(false);
        var text = await ReadAsStringAsync(response, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new ControlIdAuthenticationException("Login recusado pelo equipamento. Verifique usuário e senha.", response.StatusCode, text);
        }

        EnsureSuccess(response, "login.fcgi", text);

        LoginResponse? parsed;
        try
        {
            parsed = ControlIdJson.Deserialize<LoginResponse>(text);
        }
        catch (JsonException ex)
        {
            throw new ControlIdException("Resposta inválida de 'login.fcgi'.", response.StatusCode, responseBody: text, innerException: ex);
        }

        if (string.IsNullOrEmpty(parsed?.Session))
        {
            throw new ControlIdAuthenticationException("O equipamento não retornou uma sessão.", response.StatusCode, text);
        }

        return parsed!.Session!;
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string endpoint, HttpContent? content, string? session, string? extraQuery)
    {
        var query = new StringBuilder();
        if (session is not null)
        {
            query.Append("session=").Append(Uri.EscapeDataString(session));
        }

        if (!string.IsNullOrEmpty(extraQuery))
        {
            if (query.Length > 0)
            {
                query.Append('&');
            }

            query.Append(extraQuery);
        }

        var path = query.Length > 0 ? $"{endpoint}?{query}" : endpoint;
        var uri = new Uri(EnsureTrailingSlash(_options.BaseAddress), path);

        return new HttpRequestMessage(method, uri) { Content = content };
    }

    private static HttpContent JsonContent(object? body) =>
        new StringContent(ControlIdJson.Serialize(body), Encoding.UTF8, "application/json");

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, string endpoint, CancellationToken cancellationToken)
    {
        await _throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Com ResponseContentRead (padrão) o corpo já chega lido, então a vaga cobre a requisição inteira.
            return await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new ControlIdException($"Falha de comunicação com {_options.BaseAddress} ao chamar '{endpoint}'.", innerException: ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ControlIdException($"Timeout ao chamar '{endpoint}' em {_options.BaseAddress}.", innerException: ex);
        }
        finally
        {
            _throttle.Release();
        }
    }

    private static Task<string> ReadAsStringAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
#if NET5_0_OR_GREATER
        return response.Content.ReadAsStringAsync(cancellationToken);
#else
        _ = cancellationToken;
        return response.Content.ReadAsStringAsync();
#endif
    }

    private static Task<byte[]> ReadAsByteArrayAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
#if NET5_0_OR_GREATER
        return response.Content.ReadAsByteArrayAsync(cancellationToken);
#else
        _ = cancellationToken;
        return response.Content.ReadAsByteArrayAsync();
#endif
    }

    private static void EnsureSuccess(HttpResponseMessage response, string endpoint, string body)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string? deviceMessage = null;
        int? deviceCode = null;
        try
        {
            var error = ControlIdJson.Deserialize<DeviceError>(body);
            deviceMessage = error?.Error;
            deviceCode = error?.Code;
        }
        catch (JsonException)
        {
            // Corpo não é JSON; fica só no ResponseBody.
        }

        var message = deviceMessage is null
            ? $"'{endpoint}' falhou com HTTP {(int)response.StatusCode}."
            : $"'{endpoint}' falhou com HTTP {(int)response.StatusCode}: {deviceMessage}";

        throw new ControlIdException(message, response.StatusCode, deviceCode, body);
    }

    private static Uri EnsureTrailingSlash(Uri uri) =>
        uri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal) ? uri : new Uri(uri.AbsoluteUri + "/");

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _http.Dispose();
        }

        _loginLock.Dispose();
        _throttle.Dispose();
    }

    private sealed class Credentials
    {
        public Credentials(string login, string password)
        {
            Login = login;
            Password = password;
        }

        public string Login { get; }

        public string Password { get; }
    }

    private sealed class LoginRequest
    {
        [JsonPropertyName("login")]
        public string Login { get; set; } = string.Empty;

        [JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;
    }

    private sealed class LoginResponse
    {
        [JsonPropertyName("session")]
        public string? Session { get; set; }
    }

    private sealed class DeviceError
    {
        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("code")]
        public int? Code { get; set; }
    }
}
