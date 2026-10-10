using System;
using System.Text.Json.Serialization;

namespace ControlId.Access;

/// <summary>
/// Corpo de <c>set_system_network.fcgi</c>. IP, máscara e gateway são obrigatórios; campos opcionais em
/// <c>null</c> não são enviados (a documentação não diz como cada firmware trata a ausência deles).
/// </summary>
public sealed class NetworkSettings
{
    /// <summary>Cria a configuração com os campos obrigatórios.</summary>
    public NetworkSettings(string ip, string netmask, string gateway)
    {
        Ip = Required(ip, nameof(ip));
        Netmask = Required(netmask, nameof(netmask));
        Gateway = Required(gateway, nameof(gateway));
    }

    /// <summary>IP que o equipamento assume, ex.: <c>192.168.0.33</c>.</summary>
    [JsonPropertyName("ip")]
    public string Ip { get; }

    /// <summary>Máscara, ex.: <c>255.255.255.0</c>.</summary>
    [JsonPropertyName("netmask")]
    public string Netmask { get; }

    /// <summary>Gateway, ex.: <c>192.168.0.1</c>.</summary>
    [JsonPropertyName("gateway")]
    public string Gateway { get; }

    /// <summary>Usa <see cref="DeviceHostname"/> em vez do padrão <c>CID-&lt;serial&gt;</c>.</summary>
    [JsonPropertyName("custom_hostname_enabled")]
    public bool? CustomHostnameEnabled { get; set; }

    /// <summary>Hostname customizado (com <see cref="CustomHostnameEnabled"/>).</summary>
    [JsonPropertyName("device_hostname")]
    public string? DeviceHostname { get; set; }

    /// <summary>Porta do servidor web do equipamento, ex.: 80.</summary>
    [JsonPropertyName("web_server_port")]
    public int? WebServerPort { get; set; }

    /// <summary>Habilita HTTPS no servidor web.</summary>
    [JsonPropertyName("ssl_enabled")]
    public bool? SslEnabled { get; set; }

    /// <summary>Com SSL: <c>true</c> usa certificado autoassinado; <c>false</c>, certificado de terceiro.</summary>
    [JsonPropertyName("self_signed_certificate")]
    public bool? SelfSignedCertificate { get; set; }

    /// <summary>DNS primário (padrão do equipamento: 8.8.8.8).</summary>
    [JsonPropertyName("dns_primary")]
    public string? DnsPrimary { get; set; }

    /// <summary>DNS secundário (padrão do equipamento: 8.8.4.4).</summary>
    [JsonPropertyName("dns_secondary")]
    public string? DnsSecondary { get; set; }

    private static string Required(string value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Campo obrigatório.", name) : value;
}

/// <summary>Resposta de <c>gpio_state.fcgi</c>.</summary>
public sealed class GpioState
{
    /// <summary>Nome do pino.</summary>
    [JsonPropertyName("pin")]
    public string? Pin { get; set; }

    /// <summary>1 se o GPIO está habilitado (ler um desabilitado tem efeito indefinido).</summary>
    [JsonPropertyName("enabled")]
    public int Enabled { get; set; }

    /// <summary>1 = entrada, 0 = saída.</summary>
    [JsonPropertyName("in")]
    public int In { get; set; }

    /// <summary>Valor atual.</summary>
    [JsonPropertyName("value")]
    public int Value { get; set; }

    /// <summary>Valor quando ocioso.</summary>
    [JsonPropertyName("idle")]
    public int Idle { get; set; }

    /// <summary>1 se o pull-up está habilitado.</summary>
    [JsonPropertyName("pullup")]
    public int Pullup { get; set; }

    /// <summary>1 se o software de acesso é avisado das mudanças.</summary>
    [JsonPropertyName("notify")]
    public int Notify { get; set; }

    /// <summary>Quantas vezes o GPIO notificou o software de acesso.</summary>
    [JsonPropertyName("notified")]
    public long Notified { get; set; }

    /// <summary><see cref="Enabled"/> como booleano.</summary>
    [JsonIgnore]
    public bool IsEnabled => Enabled == 1;

    /// <summary><see cref="In"/> como booleano.</summary>
    [JsonIgnore]
    public bool IsInput => In == 1;

    /// <summary><see cref="Value"/> diferente do valor ocioso (ex.: sensor de porta acionado).</summary>
    [JsonIgnore]
    public bool IsActive => Value != Idle;
}

/// <summary>Resposta de <c>user_hash_password.fcgi</c>: use em <see cref="AccessUser.Password"/> e <see cref="AccessUser.Salt"/>.</summary>
public sealed class PasswordHash
{
    /// <summary>Hash da senha.</summary>
    [JsonPropertyName("password")]
    public string Hash { get; set; } = string.Empty;

    /// <summary>Salt aleatório usado no hash.</summary>
    [JsonPropertyName("salt")]
    public string Salt { get; set; } = string.Empty;
}
