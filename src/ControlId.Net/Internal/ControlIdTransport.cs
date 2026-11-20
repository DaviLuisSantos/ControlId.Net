using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
    private readonly RequestSlots _throttle;

    // Recusa as requisições enquanto o equipamento falha; a porta fica antes do login e antes de _throttle.
    private readonly CircuitBreaker _circuit;

    // server.address e server.port do equipamento, nos spans e nas métricas.
    private readonly System.Collections.Generic.KeyValuePair<string, object?>[] _deviceTags;
    private readonly ILogger _logger;
    private readonly string _device;
    private string? _session;

    // Muda a cada sessão nova (login explícito, primeiro uso, relogin após 401): quem guarda algo por sessão compara.
    private long _sessionGeneration;

    // Cópia própria: change_login atualiza estas credenciais sem alterar o objeto de opções do chamador.
    private volatile Credentials _credentials;

    public ControlIdTransport(ControlIdDeviceOptions options, HttpClient? httpClient = null, ILoggerFactory? fallbackLoggerFactory = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();

        // A fábrica das opções vence a do contêiner (DI); sem nenhuma, não há log.
        var loggerFactory = options.LoggerFactory ?? fallbackLoggerFactory;
        _logger = loggerFactory?.CreateLogger(ControlIdTelemetry.LoggerCategory) ?? NullLogger.Instance;
        _device = options.BaseAddress.Authority;
        _credentials = new Credentials(options.Login, options.Password);
        _deviceTags = Telemetry.DeviceTags(options.BaseAddress);
        _throttle = new RequestSlots(options.MaxConcurrentRequests, _deviceTags);
        _circuit = new CircuitBreaker(
            options.CircuitBreakerFailureThreshold, options.CircuitBreakerBreakDuration, _deviceTags, _logger, _device, options.CircuitClock, options.Timeout);

        // Vale também para o HttpClient do pacote de DI, configurado a partir destas opções.
        if (options.AcceptAnyServerCertificate && CertificatePinning.Normalize(options.ServerCertificateSha256) is null)
        {
            Log.AcceptingAnyCertificate(_logger, _device);
        }

        if (httpClient is null)
        {
            var handler = new HttpClientHandler();
            HttpHandlers.LimitConnections(handler, options.MaxConcurrentRequests);
            CertificatePinning.Configure(handler, options.ServerCertificateSha256, options.AcceptAnyServerCertificate);

            OwnedHandler = handler;
            _http = new HttpClient(handler, disposeHandler: true) { Timeout = options.Timeout };
            _ownsHttpClient = true;
        }
        else
        {
            _http = httpClient;
        }
    }

    /// <summary>Opções com que o transporte foi criado (testes).</summary>
    internal ControlIdDeviceOptions Options => _options;

    /// <summary>Handler criado pelo próprio transporte, ou <c>null</c> se o <see cref="HttpClient"/> veio de fora (testes).</summary>
    internal HttpClientHandler? OwnedHandler { get; }

    /// <summary>Logger do equipamento (categoria <see cref="ControlIdTelemetry.LoggerCategory"/>), para os clientes.</summary>
    public ILogger Logger => _logger;

    /// <summary><c>host:porta</c> do equipamento, como aparece nos logs.</summary>
    public string Device => _device;

    /// <summary>Sessão atual, ou <c>null</c> se ainda não logou.</summary>
    public string? Session => Volatile.Read(ref _session);

    /// <summary>
    /// Número da sessão atual: muda a cada login (explícito, sob demanda ou refeito após HTTP 401, como depois de o
    /// equipamento reiniciar). 0 antes do primeiro login.
    /// </summary>
    public long SessionGeneration => Interlocked.Read(ref _sessionGeneration);

    /// <summary>Estado do circuit breaker deste equipamento (<c>Closed</c> se estiver desligado).</summary>
    public ControlIdCircuitState CircuitState => _circuit.State;

    /// <summary>Fecha o circuit breaker na hora (log 1011).</summary>
    public void ResetCircuit() => _circuit.Reset();

    public async Task LoginAsync(CancellationToken cancellationToken)
    {
        await _loginLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            SetSession(await RequestSessionAsync(cancellationToken).ConfigureAwait(false));
        }
        finally
        {
            _loginLock.Release();
        }
    }

    /// <summary>
    /// Encerra a sessão. Best-effort: a sessão local é descartada antes; falha de rede ou timeout vira log 1012 (Debug) e
    /// retorno normal. Só o cancelamento de quem chamou lança.
    /// </summary>
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
        catch (HttpRequestException)
        {
            Log.LogoutFailed(_logger, _device, "falha de rede");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout do HttpClient (TaskCanceledException sem o token de quem chamou).
            Log.LogoutFailed(_logger, _device, "timeout");
        }
        finally
        {
            _throttle.Release();
        }

        // O equipamento não retorna corpo e uma sessão já expirada não é erro.
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

    /// <summary>
    /// POST com corpo JSON; devolve o corpo da resposta como stream, lido sob demanda (ex.: AFD grande). A vaga do limite de
    /// requisições fica ocupada até o stream ser fechado. Refaz o login uma vez em 401, como os demais comandos.
    /// </summary>
    public async Task<(Stream Stream, string? CharSet)> PostForStreamAsync(string endpoint, object? body, CancellationToken cancellationToken, string? extraQuery = null)
    {
        // O span cobre até os cabeçalhos da resposta; a leitura do corpo fica com quem consome o stream.
        using var scope = new CommandScope(endpoint, _deviceTags);
        try
        {
            return await PostForStreamCoreAsync(endpoint, body, extraQuery, scope, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            scope.Fail(ex);
            throw;
        }
    }

    private async Task<(Stream Stream, string? CharSet)> PostForStreamCoreAsync(
        string endpoint, object? body, string? extraQuery, CommandScope scope, CancellationToken cancellationToken)
    {
        // Só repete antes de os cabeçalhos chegarem: depois disso o corpo já está com quem consome o stream.
        var maxRetries = ReadRetry.IsReadOnly(scope.Command) ? _options.ReadRetries : 0;
        var retries = 0;
        var relogged = false;
        while (true)
        {
            var session = await EnsureSessionAsync(cancellationToken).ConfigureAwait(false);
            using var request = BuildRequest(HttpMethod.Post, endpoint, JsonContent(body), session, extraQuery);

            var probe = _circuit.Acquire();
            try
            {
                await _throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                _circuit.Cancel(probe);
                throw;
            }

            HttpResponseMessage response;
            ControlIdException? failure = null;
            try
            {
                response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                _circuit.Record(response.StatusCode, probe);
            }
            catch (HttpRequestException ex)
            {
                _throttle.Release();
                _circuit.Failure(probe);
                Log.CommunicationFailed(_logger, _device, scope.Command, ex);
                failure = CommunicationFailure(endpoint, ex);
                response = null!;
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                _throttle.Release();
                _circuit.Failure(probe);
                Log.CommunicationFailed(_logger, _device, scope.Command, ex);
                failure = new ControlIdException($"Timeout ao chamar '{endpoint}' em {_options.BaseAddress}.", innerException: ex);
                response = null!;
            }
            catch
            {
                _throttle.Release();
                _circuit.Cancel(probe);
                throw;
            }

            if (failure is not null)
            {
                if (retries < maxRetries)
                {
                    retries++;
                    scope.Retry(retries, failure.InnerException!.GetType().Name);
                    Log.Retrying(_logger, _device, scope.Command, retries, failure.InnerException!.GetType().Name);
                    await ReadRetry.WaitAsync(_options.RetryDelay, retries, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                throw failure;
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized && !relogged)
            {
                response.Dispose();
                _throttle.Release();
                Interlocked.CompareExchange(ref _session, null, session);
                relogged = true;
                scope.Relogin();
                Log.SessionExpired(_logger, _device, scope.Command);
                continue;
            }

            if (!response.IsSuccessStatusCode && retries < maxRetries && ReadRetry.IsTransient(response.StatusCode))
            {
                var status = ((int)response.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture);
                response.Dispose();
                _throttle.Release();
                retries++;
                scope.Retry(retries, status);
                Log.Retrying(_logger, _device, scope.Command, retries, status);
                await ReadRetry.WaitAsync(_options.RetryDelay, retries, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                try
                {
                    var text = await ReadAsStringAsync(response, cancellationToken).ConfigureAwait(false);
                    EnsureSuccess(response, endpoint, text);
                }
                finally
                {
                    response.Dispose();
                    _throttle.Release();
                }
            }

            try
            {
#if NET5_0_OR_GREATER
                var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#else
                var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
#endif
                return (new ResponseStream(stream, response, _throttle), response.Content.Headers.ContentType?.CharSet);
            }
            catch
            {
                response.Dispose();
                _throttle.Release();
                throw;
            }
        }
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
        using var scope = new CommandScope(endpoint, _deviceTags);
        var maxRetries = ReadRetry.IsReadOnly(scope.Command) ? _options.ReadRetries : 0;
        var retries = 0;
        var relogged = false;
        try
        {
            while (true)
            {
                var session = requiresSession ? await EnsureSessionAsync(cancellationToken).ConfigureAwait(false) : null;

                using var request = BuildRequest(method, endpoint, createContent(), session, extraQuery);
                HttpResponseMessage response;
                try
                {
                    response = await SendAsync(request, endpoint, cancellationToken).ConfigureAwait(false);
                }
                catch (ControlIdException ex) when (retries < maxRetries && ReadRetry.IsTransient(ex))
                {
                    // Leitura que falhou na rede ou estourou o prazo: espera e repete (ADR 0013).
                    retries++;
                    var reason = ex.InnerException?.GetType().Name ?? "network";
                    scope.Retry(retries, reason);
                    Log.Retrying(_logger, _device, scope.Command, retries, reason);
                    await ReadRetry.WaitAsync(_options.RetryDelay, retries, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (response.StatusCode == HttpStatusCode.Unauthorized && requiresSession && !relogged)
                {
                    // Sessão expirou ou o equipamento reiniciou: descarta e tenta de novo com login novo.
                    response.Dispose();
                    Interlocked.CompareExchange(ref _session, null, session);
                    relogged = true;
                    scope.Relogin();
                    Log.SessionExpired(_logger, _device, scope.Command);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    if (retries < maxRetries && ReadRetry.IsTransient(response.StatusCode))
                    {
                        var status = ((int)response.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture);
                        response.Dispose();
                        retries++;
                        scope.Retry(retries, status);
                        Log.Retrying(_logger, _device, scope.Command, retries, status);
                        await ReadRetry.WaitAsync(_options.RetryDelay, retries, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    using (response)
                    {
                        var text = await ReadAsStringAsync(response, cancellationToken).ConfigureAwait(false);
                        EnsureSuccess(response, endpoint, text);
                    }
                }

                return response;
            }
        }
        catch (Exception ex)
        {
            scope.Fail(ex);
            throw;
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
                SetSession(current);
            }

            return current;
        }
        finally
        {
            _loginLock.Release();
        }
    }

    // Chamar com o _loginLock.
    private void SetSession(string session)
    {
        Interlocked.Increment(ref _sessionGeneration);
        Volatile.Write(ref _session, session);
    }

    // Falha de rede: com o certificado TLS recusado, a mensagem diz como fixar a impressão digital.
    private ControlIdException CommunicationFailure(string endpoint, HttpRequestException exception)
    {
        var message = $"Falha de comunicação com {_options.BaseAddress} ao chamar '{endpoint}'.";
        if (IsCertificateFailure(exception))
        {
            message += " O certificado TLS do equipamento foi recusado: fixe a impressão digital em ControlIdDeviceOptions.ServerCertificateSha256"
                + " (obtenha o valor com ControlIdCertificates.GetServerCertificateSha256Async).";
        }

        return new ControlIdException(message, innerException: exception);
    }

    private static bool IsCertificateFailure(Exception exception)
    {
        for (var current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is System.Security.Authentication.AuthenticationException
                || current is WebException { Status: WebExceptionStatus.TrustFailure or WebExceptionStatus.SecureChannelFailure })
            {
                return true;
            }
        }

        return false;
    }

    private async Task<string> RequestSessionAsync(CancellationToken cancellationToken)
    {
        using var scope = new CommandScope("login.fcgi", _deviceTags);
        try
        {
            return await RequestSessionCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            scope.Fail(ex);
            throw;
        }
    }

    private async Task<string> RequestSessionCoreAsync(CancellationToken cancellationToken)
    {
        var credentials = _credentials;
        var body = new LoginRequest { Login = credentials.Login, Password = credentials.Password };
        using var request = BuildRequest(HttpMethod.Post, "login.fcgi", JsonContent(body), null, null);
        using var response = await SendAsync(request, "login.fcgi", cancellationToken).ConfigureAwait(false);
        var text = await ReadAsStringAsync(response, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            Log.LoginRefused(_logger, _device, (int)response.StatusCode);
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

        Log.LoginSucceeded(_logger, _device);
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

    private static StringContent JsonContent(object? body) =>
        new StringContent(ControlIdJson.Serialize(body), Encoding.UTF8, "application/json");

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, string endpoint, CancellationToken cancellationToken)
    {
        // Porta do circuit breaker: comandos e login passam por aqui. Aberto, falha antes de pegar uma vaga.
        var probe = _circuit.Acquire();
        try
        {
            await _throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            _circuit.Cancel(probe);
            throw;
        }

        try
        {
            // Com ResponseContentRead (padrão) o corpo já chega lido, então a vaga cobre a requisição inteira.
            var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            _circuit.Record(response.StatusCode, probe);
            return response;
        }
        catch (HttpRequestException ex)
        {
            _circuit.Failure(probe);
            Log.CommunicationFailed(_logger, _device, Telemetry.CommandName(endpoint), ex);
            throw CommunicationFailure(endpoint, ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _circuit.Failure(probe);
            Log.CommunicationFailed(_logger, _device, Telemetry.CommandName(endpoint), ex);
            throw new ControlIdException($"Timeout ao chamar '{endpoint}' em {_options.BaseAddress}.", innerException: ex);
        }
        catch
        {
            _circuit.Cancel(probe);
            throw;
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

    private void EnsureSuccess(HttpResponseMessage response, string endpoint, string body)
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

        Log.DeviceError(_logger, _device, Telemetry.CommandName(endpoint), (int)response.StatusCode, deviceCode, deviceMessage);
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
