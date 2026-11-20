using System.Text.Json.Serialization;

namespace ControlId.Access;

/// <summary>Configuração do OpenVPN (<c>set_vpn_information.fcgi</c>).</summary>
public sealed class VpnSettings
{
    /// <summary>VPN habilitada.</summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    /// <summary>A VPN usa login e senha informados manualmente.</summary>
    [JsonPropertyName("login_enabled")]
    public bool LoginEnabled { get; set; }

    /// <summary>Usuário (com <see cref="LoginEnabled"/>).</summary>
    [JsonPropertyName("login")]
    public string? Login { get; set; }

    /// <summary>Senha (com <see cref="LoginEnabled"/>).</summary>
    [JsonPropertyName("password")]
    public string? Password { get; set; }
}

/// <summary>Resposta de <c>get_vpn_information.fcgi</c> (a senha não volta, só se ela existe).</summary>
public sealed class VpnInformation
{
    /// <summary>VPN habilitada.</summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    /// <summary>A VPN usa login e senha informados manualmente.</summary>
    [JsonPropertyName("login_enabled")]
    public bool LoginEnabled { get; set; }

    /// <summary>Usuário configurado.</summary>
    [JsonPropertyName("login")]
    public string? Login { get; set; }

    /// <summary>Há senha configurada.</summary>
    [JsonPropertyName("password")]
    public bool HasPassword { get; set; }
}

/// <summary>Estado do OpenVPN (<c>get_vpn_status.fcgi</c>).</summary>
public enum VpnStatus
{
    /// <summary>Código que esta versão da biblioteca não conhece (ex.: de um firmware mais novo). Vale -1 porque o 0 já é <see cref="Connected"/>.</summary>
    Unknown = -1,

    /// <summary>0: conectado.</summary>
    Connected = 0,

    /// <summary>1: servidor recusou a autenticação.</summary>
    AuthFailed = 1,

    /// <summary>2: falta a autoridade certificadora.</summary>
    CaMissing = 2,

    /// <summary>3: o servidor não validou a autoridade certificadora.</summary>
    CaFailed = 3,

    /// <summary>4: falta o certificado público, a chave privada ou ambos.</summary>
    CertificateOrKeyMissing = 4,

    /// <summary>5: certificado público recusado.</summary>
    CertificateFailed = 5,

    /// <summary>6: chave privada recusada.</summary>
    KeyFailed = 6,

    /// <summary>7: falha no TLS.</summary>
    TlsFailed = 7,

    /// <summary>8: VPN desabilitada.</summary>
    Disconnected = 8,

    /// <summary>9: tentando conectar.</summary>
    TryingToConnect = 9,

    /// <summary>10: rede desconectada.</summary>
    NoConnection = 10,
}

/// <summary>Tipo do arquivo de <c>set_vpn_file.fcgi</c>.</summary>
public enum VpnFileType
{
    /// <summary>Arquivo de configuração <c>.conf</c> (<c>config</c>).</summary>
    Config,

    /// <summary><c>.zip</c> com configuração, certificados e chaves (<c>zip</c>).</summary>
    Zip,
}

/// <summary>Autenticação interna do 802.1X (PEAP).</summary>
public enum Ieee8021XInnerAuthentication
{
    /// <summary>0: MS-CHAPv2.</summary>
    MsChapV2 = 0,

    /// <summary>1: MD5.</summary>
    Md5 = 1,

    /// <summary>2: GTC.</summary>
    Gtc = 2,
}

/// <summary>Método EAP externo do 802.1X (<c>eap_auth</c>).</summary>
public enum Ieee8021XEapMethod
{
    /// <summary>0: PEAP.</summary>
    Peap = 0,

    /// <summary>1: TTLS.</summary>
    Ttls = 1,

    /// <summary>2: TLS.</summary>
    Tls = 2,
}

/// <summary>Corpo de <c>configure_802_1X.fcgi</c>. Campos opcionais em <c>null</c> não são enviados.</summary>
public sealed class Ieee8021XSettings
{
    /// <summary>Liga ou desliga a autenticação 802.1X.</summary>
    public bool Enabled { get; set; }

    /// <summary>Login do suplicante.</summary>
    public string? Login { get; set; }

    /// <summary>Senha do suplicante (nunca vai para mensagem de erro nem log).</summary>
    public string? Password { get; set; }

    /// <summary>Autenticação interna (<c>inner_auth</c>).</summary>
    public Ieee8021XInnerAuthentication InnerAuthentication { get; set; } = Ieee8021XInnerAuthentication.MsChapV2;

    /// <summary>Método EAP externo (<c>eap_auth</c>); <c>null</c> mantém o atual do equipamento.</summary>
    public Ieee8021XEapMethod? EapMethod { get; set; }

    /// <summary>Usa certificado de cliente (<c>uses_certificate</c>).</summary>
    public bool? UsesCertificate { get; set; }

    /// <summary>Interface: <c>"1"</c> é a cabeada; qualquer outro valor, a sem fio. Omitido, vale a cabeada.</summary>
    public string? Interface { get; set; }
}

/// <summary>Licenças de capacidade do iDFace e do iDFace Max (exigem a senha fornecida pela Control iD na compra).</summary>
public enum FaceLicense
{
    /// <summary>iDFace Pro 10 mil faces (<c>upgrade_ten_thousand_face_templates</c>); pré-requisito das demais.</summary>
    Pro10k,

    /// <summary>iDFace Pro 15 mil faces (<c>upgrade_fifteen_thousand_face_templates</c>).</summary>
    Pro15k,

    /// <summary>iDFace Pro 30 mil faces (<c>upgrade_thirty_thousand_face_templates</c>).</summary>
    Pro30k,

    /// <summary>iDFace Max: Pro 50 mil faces (<c>upgrade_fifty_thousand_face_templates</c>).</summary>
    Pro50k,

    /// <summary>iDFace Max: Pro 100 mil faces (<c>upgrade_hundred_thousand_face_templates</c>).</summary>
    Pro100k,
}
