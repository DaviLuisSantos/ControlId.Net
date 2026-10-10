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

    internal void Validate()
    {
        if (BaseAddress is null)
        {
            throw new ArgumentException($"{nameof(BaseAddress)} é obrigatório.", nameof(BaseAddress));
        }

        if (!BaseAddress.IsAbsoluteUri)
        {
            throw new ArgumentException($"{nameof(BaseAddress)} precisa ser uma URI absoluta.", nameof(BaseAddress));
        }

        if (string.IsNullOrEmpty(Login))
        {
            throw new ArgumentException($"{nameof(Login)} é obrigatório.", nameof(Login));
        }
    }
}
