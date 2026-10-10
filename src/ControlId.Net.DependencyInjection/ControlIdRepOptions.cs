using System;
using ControlId.Rep;

namespace ControlId.DependencyInjection;

/// <summary>
/// Configuração de um REP iDClass registrado com <c>AddControlIdRep</c>. Mesmos campos de
/// <see cref="ControlIdDeviceOptions"/>, mais o <see cref="Mode"/>; pensada para vir do <c>appsettings.json</c>.
/// </summary>
public sealed class ControlIdRepOptions
{
    /// <summary>Endereço do REP, ex.: <c>https://192.168.0.130</c>.</summary>
    public Uri BaseAddress { get; set; } = null!;

    /// <summary>Usuário da API. Padrão de fábrica: <c>admin</c>.</summary>
    public string Login { get; set; } = "admin";

    /// <summary>Senha da API. Padrão de fábrica: <c>admin</c>.</summary>
    public string Password { get; set; } = "admin";

    /// <summary>Timeout de cada requisição. Padrão: 30 segundos.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Aceita o certificado autoassinado do REP. Padrão: <c>true</c> (o iDClass sai de fábrica assim).</summary>
    public bool AcceptAnyServerCertificate { get; set; } = true;

    /// <summary>Portaria usada nas chamadas que suportam modo. Padrão: <see cref="RepMode.Portaria671"/>.</summary>
    public RepMode Mode { get; set; } = RepMode.Portaria671;

    internal ControlIdDeviceOptions ToDeviceOptions() => new()
    {
        BaseAddress = BaseAddress,
        Login = Login,
        Password = Password,
        Timeout = Timeout,
        AcceptAnyServerCertificate = AcceptAnyServerCertificate,
    };
}
