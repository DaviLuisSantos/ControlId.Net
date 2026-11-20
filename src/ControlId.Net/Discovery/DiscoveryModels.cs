using System;

namespace ControlId.Discovery;

/// <summary>Resultado da sondagem de um endereço pela <see cref="ControlIdDiscovery"/>.</summary>
public enum DiscoveryStatus
{
    /// <summary>Equipamento Control iD encontrado e autenticado.</summary>
    Found = 0,

    /// <summary>Ninguém atendeu: conexão recusada, endereço inalcançável ou prazo de conexão vencido.</summary>
    PortClosed = 1,

    /// <summary>Alguém respondeu, mas não é um equipamento Control iD (sem <c>login.fcgi</c> ou resposta que não é a dele).</summary>
    NotControlId = 2,

    /// <summary>É um equipamento Control iD, mas nenhuma das credenciais tentadas foi aceita.</summary>
    CredentialRejected = 3,

    /// <summary>Falha que não permite concluir (timeout depois da conexão, erro do equipamento, TLS recusado).</summary>
    Error = 4,
}

/// <summary>Credencial a tentar na descoberta.</summary>
public sealed class DiscoveryCredential
{
    /// <summary>Cria a credencial.</summary>
    /// <param name="login">Usuário.</param>
    /// <param name="password">Senha.</param>
    public DiscoveryCredential(string login, string password)
    {
        Login = login ?? throw new ArgumentNullException(nameof(login));
        Password = password ?? throw new ArgumentNullException(nameof(password));
    }

    /// <summary>Usuário.</summary>
    public string Login { get; }

    /// <summary>Senha. Nunca aparece em log, exceção nem em <see cref="ToString"/>.</summary>
    public string Password { get; }

    /// <summary>Só o usuário: a senha não é exibida.</summary>
    public override string ToString() => $"{Login}:***";
}

/// <summary>Ajustes da varredura de <see cref="ControlIdDiscovery"/>.</summary>
public sealed class DiscoveryOptions
{
    /// <summary>Quantos endereços são sondados ao mesmo tempo. Padrão: 32. Mínimo: 1.</summary>
    public int MaxParallelism { get; set; } = 32;

    /// <summary>
    /// Prazo para conectar em cada endereço. Padrão: 1 segundo. Só vale onde o handler HTTP tem prazo de conexão
    /// (<c>net8.0</c>); no <c>netstandard2.0</c> (.NET Framework) fica o <see cref="Timeout"/>.
    /// </summary>
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Prazo de cada requisição (login e leitura das informações). Padrão: 5 segundos.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Tenta também a credencial de fábrica (<c>admin</c>/<c>admin</c>) depois das informadas. Padrão: <c>false</c>, porque
    /// testar senha padrão em rede alheia é decisão de quem varre.
    /// </summary>
    public bool TryFactoryDefaults { get; set; }

    internal void Validate()
    {
        if (MaxParallelism < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxParallelism), MaxParallelism, $"{nameof(MaxParallelism)} precisa ser pelo menos 1.");
        }

        if (ConnectTimeout != System.Threading.Timeout.InfiniteTimeSpan && ConnectTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ConnectTimeout), ConnectTimeout, $"{nameof(ConnectTimeout)} precisa ser positivo ou Timeout.InfiniteTimeSpan.");
        }

        if (Timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(Timeout), Timeout, $"{nameof(Timeout)} precisa ser positivo.");
        }
    }
}

/// <summary>Um endereço sondado pela <see cref="ControlIdDiscovery"/>.</summary>
public sealed class DiscoveredDevice
{
    /// <summary>Cria o resultado.</summary>
    public DiscoveredDevice(
        Uri address, DiscoveryStatus status, string? model = null, string? firmware = null, string? serial = null, string? mac = null, string? credentialLogin = null)
    {
        Address = address ?? throw new ArgumentNullException(nameof(address));
        Status = status;
        Model = model;
        Firmware = firmware;
        Serial = serial;
        Mac = mac;
        CredentialLogin = credentialLogin;
    }

    /// <summary>Endereço sondado.</summary>
    public Uri Address { get; }

    /// <summary>O que foi encontrado.</summary>
    public DiscoveryStatus Status { get; }

    /// <summary>Modelo, quando o equipamento informa.</summary>
    public string? Model { get; }

    /// <summary>Versão do firmware (só em <see cref="DiscoveryStatus.Found"/>).</summary>
    public string? Firmware { get; }

    /// <summary>Número de série (só em <see cref="DiscoveryStatus.Found"/>).</summary>
    public string? Serial { get; }

    /// <summary>MAC da rede cabeada (só em <see cref="DiscoveryStatus.Found"/>).</summary>
    public string? Mac { get; }

    /// <summary>Usuário da credencial aceita. Nunca a senha.</summary>
    public string? CredentialLogin { get; }
}
