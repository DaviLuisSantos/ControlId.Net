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
