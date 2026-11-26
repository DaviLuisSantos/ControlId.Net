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

    // Comandos que este equipamento recusou como inexistentes ("Invalid command" ou 404): as próximas chamadas não saem (ADR 0027).
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _unsupported = new(StringComparer.Ordinal);

    private volatile string? _firmware;

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
            var handler = HttpHandlers.CreatePrimaryHandler(
                options.MaxConcurrentRequests, options.ConnectTimeout, options.ServerCertificateSha256, options.AcceptAnyServerCertificate);

            OwnedHandler = handler as HttpClientHandler;
            OwnedPrimaryHandler = handler;
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

    /// <summary>Handler primário criado pelo transporte (<see cref="HttpClientHandler"/> ou, com prazo de conexão, <c>SocketsHttpHandler</c>), ou <c>null</c> (testes).</summary>
    internal HttpMessageHandler? OwnedPrimaryHandler { get; }

    /// <summary>Firmware do equipamento, quando algum cliente já o leu; só entra nas mensagens de comando não suportado.</summary>
    internal string? Firmware
    {
        get => _firmware;
        set => _firmware = value;
    }

    /// <summary>O equipamento já recusou <paramref name="command"/> como inexistente nesta instância?</summary>
    internal bool IsKnownUnsupported(string command) => _unsupported.ContainsKey(command);

    /// <summary>Registra que o equipamento não tem <paramref name="command"/>: as próximas chamadas falham sem requisição.</summary>
    internal void MarkUnsupported(string command) => _unsupported[command] = 0;

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
            throw new ControlIdException($"Resposta inválida de '{endpoint}'.", responseBody: Capture(text), innerException: ex);
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
    /// requisições fica ocupada até o stream ser fechado. Refaz o login e repete as leituras como os demais comandos, mas só
    /// antes de os cabeçalhos chegarem: depois disso o corpo já está com quem consome o stream.
    /// </summary>
    public Task<(Stream Stream, string? CharSet)> PostForStreamAsync(string endpoint, object? body, CancellationToken cancellationToken, string? extraQuery = null) =>
        // O span cobre até os cabeçalhos da resposta; a leitura do corpo fica com quem consome o stream.
        TracedAsync<(Stream Stream, string? CharSet)>(endpoint, async scope =>
        {
            var response = await SendCommandCoreAsync(
                HttpMethod.Post, endpoint, () => JsonContent(body), extraQuery, requiresSession: true, HttpCompletionOption.ResponseHeadersRead, scope, cancellationToken)
                .ConfigureAwait(false);
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
                Discard(response, HttpCompletionOption.ResponseHeadersRead);
                throw;
            }
        });

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
    /// Envia o comando com a sessão atual (resposta lida inteira), refaz o login uma vez em 401 e lança
    /// <see cref="ControlIdException"/> se o equipamento devolver erro. O chamador descarta a resposta.
    /// </summary>
    private Task<HttpResponseMessage> SendCommandAsync(
        HttpMethod method, string endpoint, Func<HttpContent?> createContent, string? extraQuery, bool requiresSession, CancellationToken cancellationToken) =>
        TracedAsync(endpoint, scope => SendCommandCoreAsync(
            method, endpoint, createContent, extraQuery, requiresSession, HttpCompletionOption.ResponseContentRead, scope, cancellationToken));

    /// <summary>
    /// Núcleo único de envio de comandos: sessão, relogin uma vez em 401, repetição de leituras (ADR 0013) e erro do
    /// equipamento. Com <see cref="HttpCompletionOption.ResponseHeadersRead"/> a resposta devolvida ainda segura a vaga do
    /// limite de requisições: quem a recebe libera com <see cref="Discard"/> ou entregando-a a um <see cref="ResponseStream"/>.
    /// Toda resposta descartada aqui (401, erro, repetição) já libera a sua vaga.
    /// </summary>
    private async Task<HttpResponseMessage> SendCommandCoreAsync(
        HttpMethod method,
        string endpoint,
        Func<HttpContent?> createContent,
        string? extraQuery,
        bool requiresSession,
        HttpCompletionOption completion,
        CommandScope scope,
        CancellationToken cancellationToken)
    {
        if (_unsupported.ContainsKey(scope.Command))
        {
            // Já recusado antes: falha sem login, circuito nem requisição.
            throw new ControlIdNotSupportedException(scope.Command, _firmware, null, null, null);
        }

        var maxRetries = ReadRetry.IsReadOnly(scope.Command) ? _options.ReadRetries : 0;
        var retries = 0;
        var relogged = false;
        while (true)
        {
            var session = requiresSession ? await EnsureSessionAsync(cancellationToken).ConfigureAwait(false) : null;

            using var request = BuildRequest(method, endpoint, createContent(), session, extraQuery);
            HttpResponseMessage response;
            try
            {
                response = await SendAsync(request, endpoint, completion, cancellationToken).ConfigureAwait(false);
            }
            catch (ControlIdException ex) when (retries < maxRetries && ReadRetry.IsTransient(ex))
            {
                // Leitura que falhou na rede ou estourou o prazo: espera e repete (ADR 0013).
                await RetryAsync(scope, ++retries, ex.InnerException?.GetType().Name ?? "network", cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized && requiresSession && !relogged)
            {
                // Sessão expirou ou o equipamento reiniciou: descarta e tenta de novo com login novo.
                Discard(response, completion);
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
                    Discard(response, completion);
                    await RetryAsync(scope, ++retries, status, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                try
                {
                    var text = await ReadAsStringAsync(response, cancellationToken).ConfigureAwait(false);
                    EnsureSuccess(response, endpoint, text);
                }
                finally
                {
                    Discard(response, completion);
                }
            }

            return response;
        }
    }

    private Task RetryAsync(CommandScope scope, int retry, string reason, CancellationToken cancellationToken)
    {
        scope.Retry(retry, reason);
        Log.Retrying(_logger, _device, scope.Command, retry, reason);
        return ReadRetry.WaitAsync(_options.RetryDelay, retry, cancellationToken);
    }

    /// <summary>Descarta uma resposta de <see cref="SendAsync"/>; em stream, libera também a vaga que ela segurava.</summary>
    private void Discard(HttpResponseMessage response, HttpCompletionOption completion)
    {
        response.Dispose();
        if (completion == HttpCompletionOption.ResponseHeadersRead)
        {
            _throttle.Release();
        }
    }

    /// <summary>Abre o span e a métrica de duração do comando e marca a falha, se houver.</summary>
    private async Task<T> TracedAsync<T>(string endpoint, Func<CommandScope, Task<T>> action)
    {
        using var scope = new CommandScope(endpoint, _deviceTags);
        try
        {
            return await action(scope).ConfigureAwait(false);
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

    // Falha de rede: com o certificado TLS recusado, a mensagem diz como fixar a impressão digital. Se o comando pode ter
    // saído (escrita sem resposta), o desfecho é desconhecido; senão, é uma falha de conexão (leitura ou comando que não saiu).
    private ControlIdException CommunicationFailure(string endpoint, HttpRequestException exception)
    {
        var message = $"Falha de comunicação com {_options.BaseAddress} ao chamar '{endpoint}'.";
        var certificate = IsCertificateFailure(exception);
        if (certificate)
        {
            message += " O certificado TLS do equipamento foi recusado: fixe a impressão digital em ControlIdDeviceOptions.ServerCertificateSha256"
                + " (obtenha o valor com ControlIdCertificates.GetServerCertificateSha256Async).";
        }

        var command = Telemetry.CommandName(endpoint);
        if (certificate || ReadRetry.IsSafeToRepeat(command) || NeverReachedDevice(exception))
        {
            return new ControlIdConnectionException(message, exception);
        }

        return new ControlIdOutcomeUnknownException(
            message + " A conexão caiu depois do envio: não se sabe se o equipamento executou o comando. Confira o estado antes de repetir.",
            command,
            timedOut: false,
            exception);
    }

    // Timeout do cliente: leitura vira ControlIdTimeoutException; escrita, desfecho desconhecido.
    private ControlIdException TimeoutFailure(string endpoint, Exception exception)
    {
        var message = $"Timeout ao chamar '{endpoint}' em {_options.BaseAddress}.";
        var command = Telemetry.CommandName(endpoint);
        if (ReadRetry.IsSafeToRepeat(command))
        {
            return new ControlIdTimeoutException(message, exception);
        }

        return new ControlIdOutcomeUnknownException(
            message + " O comando foi enviado e não se sabe se o equipamento o executou. Confira o estado antes de repetir.",
            command,
            timedOut: true,
            exception);
    }

    // A falha aconteceu antes de o comando sair (recusa, DNS, rede inacessível, prazo de conexão): seguro repetir.
    private static bool NeverReachedDevice(HttpRequestException exception)
    {
#if NET8_0_OR_GREATER
        if (exception.HttpRequestError is HttpRequestError.ConnectionError or HttpRequestError.NameResolutionError or HttpRequestError.SecureConnectionError)
        {
            return true;
        }
#endif
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            switch (current)
            {
                case System.Net.Sockets.SocketException socket when socket.SocketErrorCode is
                    System.Net.Sockets.SocketError.ConnectionRefused or System.Net.Sockets.SocketError.HostUnreachable
                    or System.Net.Sockets.SocketError.NetworkUnreachable or System.Net.Sockets.SocketError.HostNotFound
                    or System.Net.Sockets.SocketError.TryAgain or System.Net.Sockets.SocketError.NoData
                    or System.Net.Sockets.SocketError.AddressNotAvailable or System.Net.Sockets.SocketError.TimedOut:
                    return true;
                case TimeoutException:
                    return true;
                case WebException { Status: WebExceptionStatus.ConnectFailure or WebExceptionStatus.NameResolutionFailure or WebExceptionStatus.ProxyNameResolutionFailure }:
                    return true;
            }
        }

        return false;
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

    private Task<string> RequestSessionAsync(CancellationToken cancellationToken) =>
        TracedAsync("login.fcgi", _ => RequestSessionCoreAsync(cancellationToken));

    private async Task<string> RequestSessionCoreAsync(CancellationToken cancellationToken)
    {
        var credentials = _credentials;
        var body = new LoginRequest { Login = credentials.Login, Password = credentials.Password };
        using var request = BuildRequest(HttpMethod.Post, "login.fcgi", JsonContent(body), null, null);
        using var response = await SendAsync(request, "login.fcgi", HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        var text = await ReadAsStringAsync(response, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            Log.LoginRefused(_logger, _device, (int)response.StatusCode);
            throw new ControlIdAuthenticationException("Login recusado pelo equipamento. Verifique usuário e senha.", response.StatusCode, Capture(text));
        }

        EnsureSuccess(response, "login.fcgi", text);

        LoginResponse? parsed;
        try
        {
            parsed = ControlIdJson.Deserialize<LoginResponse>(text);
        }
        catch (JsonException ex)
        {
            throw new ControlIdException("Resposta inválida de 'login.fcgi'.", response.StatusCode, responseBody: Capture(text), innerException: ex);
        }

        if (string.IsNullOrEmpty(parsed?.Session))
        {
            throw new ControlIdAuthenticationException("O equipamento não retornou uma sessão.", response.StatusCode, Capture(text));
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

    /// <summary>
    /// Uma tentativa: porta do circuit breaker, vaga do limite, envio e classificação da falha (rede ou timeout do cliente
    /// contam no circuito e viram <see cref="ControlIdException"/>; cancelamento de quem chamou só devolve a vez da sonda).
    /// Com <see cref="HttpCompletionOption.ResponseContentRead"/> o corpo já chega lido e a vaga é liberada aqui; com
    /// <see cref="HttpCompletionOption.ResponseHeadersRead"/> a resposta devolvida segura a vaga (ver <see cref="Discard"/>).
    /// Em qualquer falha a vaga é liberada aqui.
    /// </summary>
    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, string endpoint, HttpCompletionOption completion, CancellationToken cancellationToken)
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

        HttpResponseMessage? response = null;
        var keepSlot = false;
        try
        {
            response = await _http.SendAsync(request, completion, cancellationToken).ConfigureAwait(false);
            _circuit.Record(response.StatusCode, probe);
            keepSlot = completion == HttpCompletionOption.ResponseHeadersRead;
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
            throw TimeoutFailure(endpoint, ex);
        }
        catch
        {
            response?.Dispose();
            _circuit.Cancel(probe);
            throw;
        }
        finally
        {
            if (!keepSlot)
            {
                _throttle.Release();
            }
        }
    }

    private static Task<string> ReadAsStringAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
#if NET5_0_OR_GREATER
        return response.Content.ReadAsStringAsync(cancellationToken);
#else
        _ = cancellationToken;
        // No .NET Framework uma resposta sem corpo (ex.: 404 vazio) chega com Content nulo.
        return response.Content is null ? Task.FromResult(string.Empty) : response.Content.ReadAsStringAsync();
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

        var error = ParseDeviceError(body);
        var deviceMessage = error?.Error;
        var deviceCode = error?.Code;
        var command = Telemetry.CommandName(endpoint);

        var message = deviceMessage is null
            ? $"'{endpoint}' falhou com HTTP {(int)response.StatusCode}."
            : $"'{endpoint}' falhou com HTTP {(int)response.StatusCode}: {deviceMessage}";

        Log.DeviceError(_logger, _device, command, (int)response.StatusCode, deviceCode, deviceMessage);

        // O login nunca vira "não suportado": marcá-lo travaria o cliente.
        if (!string.Equals(command, "login", StringComparison.Ordinal))
        {
            var invalidCommand = Mentions(deviceMessage, "Invalid command");
            var notFound = response.StatusCode == HttpStatusCode.NotFound;
            if (invalidCommand || notFound || Mentions(deviceMessage, "Invalid table"))
            {
                // "Invalid table" é da tabela, não do comando: lança sem marcar.
                if (invalidCommand || notFound)
                {
                    MarkUnsupported(command);
                }

                throw new ControlIdNotSupportedException(command, _firmware, response.StatusCode, deviceMessage, Capture(body));
            }
        }

        throw new ControlIdDeviceErrorException(message, response.StatusCode, deviceCode, deviceMessage, Capture(body));
    }

    private static bool Mentions(string? text, string fragment) =>
        text is not null && text.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;

    // O JSON de erro do equipamento ({"error":..,"code":..}); null se o corpo não for isso.
    private static DeviceError? ParseDeviceError(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            var error = ControlIdJson.Deserialize<DeviceError>(body!);
            return error is not null && (error.Error is not null || error.Code is not null) ? error : null;
        }
        catch (JsonException)
        {
            // Corpo não é JSON.
            return null;
        }
    }

    // Corpo da resposta que vai para ControlIdException.ResponseBody, conforme ExceptionResponseBody.
    private string? Capture(string? body) => _options.ExceptionResponseBody switch
    {
        ExceptionResponseBody.None => null,
        ExceptionResponseBody.Full => body,
        _ => ParseDeviceError(body) is null ? null : body,
    };

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
