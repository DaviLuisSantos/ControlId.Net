using System;
using System.Text.Json.Serialization;

namespace ControlId.Rep;

/// <summary>Resposta de <c>get_info.fcgi</c>: contadores de usuários e de impressão.</summary>
public sealed class RepInfo
{
    /// <summary>Número de série.</summary>
    [JsonPropertyName("num_serie")] public string? SerialNumber { get; set; }

    /// <summary>Usuários.</summary>
    [JsonPropertyName("user_count")] public int UserCount { get; set; }

    /// <summary>Administradores.</summary>
    [JsonPropertyName("administrator_count")] public int AdministratorCount { get; set; }

    /// <summary>Digitais.</summary>
    [JsonPropertyName("template_count")] public int TemplateCount { get; set; }

    /// <summary>Senhas.</summary>
    [JsonPropertyName("password_count")] public int PasswordCount { get; set; }

    /// <summary>Códigos de barras.</summary>
    [JsonPropertyName("bars_count")] public int BarsCount { get; set; }

    /// <summary>Cartões de proximidade.</summary>
    [JsonPropertyName("rfid_count")] public int RfidCount { get; set; }

    /// <summary>Tempo ligado.</summary>
    [JsonPropertyName("uptime")] public long Uptime { get; set; }

    /// <summary>Pouco papel; <c>null</c> se o REP não tiver sensor.</summary>
    [JsonPropertyName("low_on_paper")] public bool? LowOnPaper { get; set; }

    /// <summary>Tickets impressos (campo obsoleto na documentação; prefira <see cref="TotalPrinted"/>).</summary>
    [JsonPropertyName("cuts")] public long? Cuts { get; set; }

    /// <summary>Total impresso, em decímetros.</summary>
    [JsonPropertyName("total_printed")] public long? TotalPrinted { get; set; }
}

/// <summary>Resposta de <c>get_system_configuration.fcgi</c>.</summary>
public sealed class RepSystemConfiguration
{
    /// <summary>Beep ligado (<c>set_buzzer_beep</c>).</summary>
    [JsonPropertyName("beep_enabled")] public bool BeepEnabled { get; set; }

    /// <summary>Animação da tela ligada (<c>set_animation_screen</c>).</summary>
    [JsonPropertyName("animation_enabled")] public bool AnimationEnabled { get; set; }

    /// <summary>Comprovante curto (<c>set_ticket_size</c>).</summary>
    [JsonPropertyName("economic_receipt_enabled")] public bool EconomicReceiptEnabled { get; set; }

    /// <summary>Identificação 1:1 (<c>set_identification_type</c>).</summary>
    [JsonPropertyName("one_to_one_enabled")] public bool OneToOneEnabled { get; set; }
}

/// <summary>Rede do REP (<c>get_system_network</c> e <c>set_system_network</c>).</summary>
public sealed class RepNetworkSettings
{
    /// <summary>IP, ex.: <c>192.168.0.130</c>.</summary>
    [JsonPropertyName("ip")] public string? Ip { get; set; }

    /// <summary>Máscara de rede.</summary>
    [JsonPropertyName("netmask")] public string? Netmask { get; set; }

    /// <summary>Gateway.</summary>
    [JsonPropertyName("gateway")] public string? Gateway { get; set; }

    /// <summary>DNS. A leitura devolve <c>DNS</c>; a gravação usa <c>dns</c>, como no exemplo da documentação.</summary>
    [JsonPropertyName("dns")] public string? Dns { get; set; }

    /// <summary>Porta da API (padrão 443).</summary>
    [JsonPropertyName("port")] public int? Port { get; set; }

    /// <summary>
    /// DHCP ligado. Na gravação vai sempre: o REP assume <c>false</c> se o campo faltar. A tabela da documentação escreve
    /// <c>use_dhpc</c>; as duas grafias são lidas.
    /// </summary>
    [JsonPropertyName("use_dhcp")] public bool UseDhcp { get; set; }

    // Grafia da tabela da documentação.
    [JsonPropertyName("use_dhpc"), JsonInclude]
    internal bool? UseDhpc
    {
        get => null;
        set
        {
            if (value is { } dhcp)
            {
                UseDhcp = dhcp;
            }
        }
    }
}

/// <summary>Período do horário de verão (<c>get/set_system_daylight_saving_time</c>), em dias.</summary>
public sealed class RepDaylightSavingTime
{
    /// <summary>Cria o período.</summary>
    public RepDaylightSavingTime(DateTime start, DateTime end)
    {
        if (end.Date < start.Date)
        {
            throw new ArgumentException("O fim do horário de verão não pode ser antes do início.", nameof(end));
        }

        Start = start.Date;
        End = end.Date;
    }

    /// <summary>Primeiro dia.</summary>
    public DateTime Start { get; }

    /// <summary>Último dia.</summary>
    public DateTime End { get; }
}

/// <summary>Chave pública do REP (<c>get_public_key.fcgi</c>).</summary>
public sealed class RepPublicKey
{
    /// <summary>Em base 32.</summary>
    [JsonPropertyName("base32")] public string? Base32 { get; set; }

    /// <summary>Em base 64.</summary>
    [JsonPropertyName("base64")] public string? Base64 { get; set; }
}

/// <summary>Leitura do AFD (<c>get_afd.fcgi</c>): a partir de um NSR, de uma data, ou completo.</summary>
public sealed class RepAfdQuery
{
    /// <summary>Marcações a partir deste NSR. Não combine com <see cref="InitialDate"/>.</summary>
    public long? InitialNsr { get; set; }

    /// <summary>Marcações a partir desta data. Não combine com <see cref="InitialNsr"/>.</summary>
    public DateTime? InitialDate { get; set; }

    /// <summary>
    /// <c>headerTop</c> (firmware revisão 104 ou mais nova): o cabeçalho vem no começo e sem a linha com o nome do arquivo,
    /// como no arquivo final. Sem isso, o REP devolve corpo, assinatura, cabeçalho e nome do arquivo, nessa ordem.
    /// </summary>
    public bool HeaderTop { get; set; }
}
