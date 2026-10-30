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
    /// Aceita certificados TLS inválidos/autoassinados. O REP iDClass usa HTTPS com certificado
    /// autoassinado, então normalmente isso precisa ser <c>true</c> para ele.
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

    internal void Validate()
    {
        if (MaxConcurrentRequests < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxConcurrentRequests), MaxConcurrentRequests, $"{nameof(MaxConcurrentRequests)} precisa ser pelo menos 1.");
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
