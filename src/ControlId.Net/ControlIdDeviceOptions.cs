using System;

namespace ControlId;

/// <summary>
/// Configuração de conexão com um equipamento Control iD.
/// </summary>
public sealed class ControlIdDeviceOptions
{
    /// <summary>
    /// Endereço base do equipamento, por exemplo <c>http://192.168.0.129</c> (linha de acesso)
    /// ou <c>https://192.168.0.129</c> (REP iDClass).
    /// </summary>
    public Uri BaseAddress { get; set; } = null!;

    /// <summary>Usuário da API. Padrão de fábrica: <c>admin</c>.</summary>
    public string Login { get; set; } = "admin";

    /// <summary>Senha da API. Padrão de fábrica: <c>admin</c>.</summary>
    public string Password { get; set; } = "admin";

    /// <summary>Timeout de cada requisição. Padrão: 30 segundos.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Aceita qualquer certificado TLS, inclusive inválido ou falso. Padrão: <c>false</c>. Evite: login e senha ficam expostos
    /// a quem se passar pelo equipamento na rede, e o cliente registra o log 1013 (Warning) ao ser criado assim. Para o REP
    /// iDClass, que sai de fábrica com certificado autoassinado, prefira <see cref="ServerCertificateSha256"/>, que aceita só
    /// aquele certificado (e faz esta opção ser ignorada).
    /// Só tem efeito quando o cliente cria o próprio <see cref="System.Net.Http.HttpClient"/>.
    /// </summary>
    public bool AcceptAnyServerCertificate { get; set; }

    /// <summary>
    /// Impressão digital SHA-256 do certificado do equipamento (hexadecimal; aceita <c>:</c> e espaços). Com ela, só esse
    /// certificado é aceito, mesmo autoassinado, e <see cref="AcceptAnyServerCertificate"/> é ignorado: é o jeito seguro de
    /// falar com o REP iDClass. Obtenha o valor com <see cref="ControlIdCertificates.GetServerCertificateSha256Async"/>.
    /// Só tem efeito quando o cliente cria o próprio <see cref="System.Net.Http.HttpClient"/> (ou pelo pacote de DI).
    /// </summary>
    public string? ServerCertificateSha256 { get; set; }

    /// <summary>Valor padrão de <see cref="MaxConcurrentRequests"/>.</summary>
    public const int DefaultMaxConcurrentRequests = 4;

    /// <summary>
    /// Máximo de requisições simultâneas a este equipamento (login incluído). Padrão: 4. As chamadas além do limite
    /// esperam a vez, então dá para disparar muitas em paralelo sem sobrecarregar o equipamento. O limite é o mesmo em
    /// qualquer runtime (sozinho, o .NET Framework limitaria a 2 conexões e o .NET Core não limitaria).
    /// <para>
    /// Aumentar o valor não acelera um único equipamento: um iDFace (firmware 6.9.12) atende uma requisição por vez, e
    /// 32 chamadas levaram o mesmo tempo com 1, 4 ou 8 simultâneas. O ganho do paralelismo está entre equipamentos
    /// diferentes (<see cref="ControlIdDevices.RunAsync{TDevice, TResult}(System.Collections.Generic.IEnumerable{System.Collections.Generic.KeyValuePair{string, TDevice}}, System.Func{TDevice, System.Threading.CancellationToken, System.Threading.Tasks.Task{TResult}}, int, System.Threading.CancellationToken)"/>).
    /// </para>
    /// </summary>
    public int MaxConcurrentRequests { get; set; } = DefaultMaxConcurrentRequests;

    /// <summary>Maior valor aceito em <see cref="ReadRetries"/>.</summary>
    public const int MaxReadRetries = 5;

    /// <summary>
    /// Novas tentativas de um comando de <b>leitura</b> que falhou por um motivo passageiro: falha de rede, timeout ou
    /// HTTP 5xx, 408 ou 429. Padrão: 0 (desligado). Máximo: <see cref="MaxReadRetries"/>.
    /// <para>
    /// Só valem para comandos que não alteram o equipamento (<c>load_objects</c>, <c>system_information</c>,
    /// <c>get_configuration</c>, <c>get_afd</c>...). Na API da Control iD tudo é POST, então o método HTTP não diz o que é
    /// seguro repetir: um <c>execute_actions</c> repetido abriria a porta duas vezes, e um <c>create_objects</c> criaria o
    /// registro duas vezes. Esses comandos nunca são repetidos (ADR 0013).
    /// </para>
    /// </summary>
    public int ReadRetries { get; set; }

    /// <summary>
    /// Espera antes da primeira nova tentativa; dobra a cada uma, com variação aleatória de 20% para equipamentos que voltam
    /// juntos não receberem as tentativas ao mesmo tempo. Padrão: 200 ms.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>Maior valor aceito em <see cref="CircuitBreakerFailureThreshold"/>.</summary>
    public const int MaxCircuitBreakerFailureThreshold = 100;

    /// <summary>
    /// Circuit breaker do equipamento: quantas falhas seguidas o abrem. Padrão: 0 (desligado). Máximo:
    /// <see cref="MaxCircuitBreakerFailureThreshold"/>. Falha é erro de rede, timeout do cliente ou HTTP 5xx, contada por
    /// tentativa; HTTP 4xx, sessão expirada (401), erro do equipamento no JSON, login recusado e cancelamento de quem chamou
    /// não contam, e qualquer resposta abaixo de 500 zera a contagem.
    /// <para>
    /// Aberto, as chamadas falham na hora com <see cref="ControlIdCircuitOpenException"/>, sem login nem requisição ao
    /// equipamento, durante <see cref="CircuitBreakerBreakDuration"/>. Depois, uma única requisição de sonda testa o
    /// equipamento: se der certo o circuito fecha, se falhar reabre. O estado fica em <c>CircuitState</c> do cliente.
    /// </para>
    /// </summary>
    public int CircuitBreakerFailureThreshold { get; set; }

    /// <summary>Quanto tempo o circuito fica aberto antes de aceitar uma sonda. Padrão: 30 segundos. Precisa ser positivo.</summary>
    public TimeSpan CircuitBreakerBreakDuration { get; set; } = TimeSpan.FromSeconds(30);

    // Relógio do circuit breaker (ticks de Stopwatch); só os testes o trocam.
    internal Func<long>? CircuitClock { get; set; }

    /// <summary>
    /// Fábrica de logs do <c>Microsoft.Extensions.Logging</c>. Padrão: <c>null</c> (nenhum log). O cliente cria um logger com
    /// a categoria <see cref="ControlIdTelemetry.LoggerCategory"/> e registra login, sessão expirada, novas tentativas e
    /// erros do equipamento, sem expor sessão, login, senha nem o corpo das mensagens. No pacote de DI, se não for
    /// informada aqui, vale a <c>ILoggerFactory</c> do contêiner.
    /// </summary>
    public Microsoft.Extensions.Logging.ILoggerFactory? LoggerFactory { get; set; }

    internal void Validate()
    {
        if (MaxConcurrentRequests < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxConcurrentRequests), MaxConcurrentRequests, $"{nameof(MaxConcurrentRequests)} precisa ser pelo menos 1.");
        }

        if (ReadRetries < 0 || ReadRetries > MaxReadRetries)
        {
            throw new ArgumentOutOfRangeException(nameof(ReadRetries), ReadRetries, $"{nameof(ReadRetries)} precisa estar entre 0 e {MaxReadRetries}.");
        }

        if (RetryDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(RetryDelay), RetryDelay, $"{nameof(RetryDelay)} não pode ser negativo.");
        }

        if (CircuitBreakerFailureThreshold < 0 || CircuitBreakerFailureThreshold > MaxCircuitBreakerFailureThreshold)
        {
            throw new ArgumentOutOfRangeException(nameof(CircuitBreakerFailureThreshold), CircuitBreakerFailureThreshold, $"{nameof(CircuitBreakerFailureThreshold)} precisa estar entre 0 e {MaxCircuitBreakerFailureThreshold}.");
        }

        if (CircuitBreakerBreakDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(CircuitBreakerBreakDuration), CircuitBreakerBreakDuration, $"{nameof(CircuitBreakerBreakDuration)} precisa ser positivo.");
        }

        if (BaseAddress is null)
        {
            throw new ArgumentException($"{nameof(BaseAddress)} é obrigatório.", nameof(BaseAddress));
        }

        if (!BaseAddress.IsAbsoluteUri)
        {
            throw new ArgumentException($"{nameof(BaseAddress)} precisa ser uma URI absoluta.", nameof(BaseAddress));
        }

        Internal.CertificatePinning.Normalize(ServerCertificateSha256);

        if (string.IsNullOrEmpty(Login))
        {
            throw new ArgumentException($"{nameof(Login)} é obrigatório.", nameof(Login));
        }
    }
}
