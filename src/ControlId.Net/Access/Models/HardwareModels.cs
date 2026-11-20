using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ControlId.Access;

/// <summary>Resposta de <c>face_template_match.fcgi</c>: resultado da verificação de um template facial.</summary>
public sealed class FaceTemplateMatchResult
{
    /// <summary><c>true</c> se um template correspondente foi encontrado.</summary>
    [JsonPropertyName("match")]
    public bool Match { get; set; }

    /// <summary>Detalhes da correspondência; presente só quando <see cref="Match"/> é <c>true</c>.</summary>
    [JsonPropertyName("info")]
    public FaceTemplateMatchInfo? Info { get; set; }
}

/// <summary>Detalhes de uma correspondência de <c>face_template_match.fcgi</c>.</summary>
public sealed class FaceTemplateMatchInfo
{
    /// <summary>ID do usuário correspondente.</summary>
    [JsonPropertyName("match_user_id")]
    public long MatchUserId { get; set; }

    /// <summary>Confiança principal do verificador, de 0 a 1000 (comparada a <c>match_score_threshold</c>, padrão 700).</summary>
    [JsonPropertyName("match_score")]
    public int MatchScore { get; set; }

    /// <summary>Similaridade secundária já multiplicada por 1000 (comparada a <c>match_similarity_threshold</c>, padrão 3,5 = 3500). É independente de <see cref="MatchScore"/>.</summary>
    [JsonPropertyName("match_similarity")]
    public int MatchSimilarity { get; set; }
}

/// <summary>Resposta de <c>io_state.fcgi</c>: GPIOs, relés e SecBoxes numa única leitura.</summary>
public sealed class IoState
{
    /// <summary>Um item por GPIO do equipamento.</summary>
    [JsonPropertyName("gpios")]
    public List<IoGpioState> Gpios { get; set; } = new();

    /// <summary>Um item por relé do equipamento.</summary>
    [JsonPropertyName("relays")]
    public List<IoRelayState> Relays { get; set; } = new();

    /// <summary>Um item por SecBox conectada.</summary>
    [JsonPropertyName("sec_boxes")]
    public List<IoSecBoxState> SecBoxes { get; set; } = new();
}

/// <summary>Estado de um GPIO em <see cref="IoState"/>. Os campos anuláveis só vêm quando o GPIO tem o papel/estado que os define.</summary>
public sealed class IoGpioState
{
    /// <summary>Número do GPIO.</summary>
    [JsonPropertyName("index")]
    public int Index { get; set; }

    /// <summary>Função: <c>tamper</c>, <c>gpio_ext</c>, <c>relay</c>, <c>speaker</c> ou <c>generic</c>.</summary>
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    /// <summary>Número da entrada configurável (o N de <c>gpio_extN</c>); só quando <see cref="Role"/> é <c>gpio_ext</c>.</summary>
    [JsonPropertyName("ext_id")]
    public int? ExtId { get; set; }

    /// <summary>Modo da entrada configurável; só para <c>gpio_ext</c>.</summary>
    [JsonPropertyName("mode")]
    public int? Mode { get; set; }

    /// <summary>Modo de ativação (1 = pulso, 2 = borda); só para <c>gpio_ext</c>.</summary>
    [JsonPropertyName("activation_mode")]
    public int? ActivationMode { get; set; }

    /// <summary>Debounce em milissegundos; só para <c>gpio_ext</c>.</summary>
    [JsonPropertyName("debounce_ms")]
    public int? DebounceMs { get; set; }

    /// <summary>GPIO habilitado.</summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    /// <summary>O estado pôde ser lido (falso para GPIO desabilitado); os campos abaixo só vêm quando é <c>true</c>.</summary>
    [JsonPropertyName("available")]
    public bool Available { get; set; }

    /// <summary>Configurado como entrada (<c>true</c>) ou saída (<c>false</c>).</summary>
    [JsonPropertyName("in")]
    public bool? In { get; set; }

    /// <summary>Valor atual.</summary>
    [JsonPropertyName("value")]
    public int? Value { get; set; }

    /// <summary>Valor quando ocioso.</summary>
    [JsonPropertyName("idle")]
    public int? Idle { get; set; }

    /// <summary>Ativo, isto é, <see cref="Value"/> diferente de <see cref="Idle"/>.</summary>
    [JsonPropertyName("active")]
    public bool? Active { get; set; }

    /// <summary>O software de acesso é avisado de mudanças.</summary>
    [JsonPropertyName("notify")]
    public bool? Notify { get; set; }

    /// <summary>Quantas vezes o GPIO notificou uma mudança.</summary>
    [JsonPropertyName("notified")]
    public int? Notified { get; set; }
}

/// <summary>Estado de um relé em <see cref="IoState"/>.</summary>
public sealed class IoRelayState
{
    /// <summary>Número do relé, a partir de 1.</summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>GPIO que aciona o relé.</summary>
    [JsonPropertyName("gpio")]
    public int Gpio { get; set; }

    /// <summary>Relé habilitado.</summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    /// <summary>Tempo de acionamento em milissegundos.</summary>
    [JsonPropertyName("timeout_ms")]
    public int TimeoutMs { get; set; }

    /// <summary>Fechamento automático habilitado.</summary>
    [JsonPropertyName("auto_close")]
    public bool AutoClose { get; set; }

    /// <summary>Modo de saída (valores de <c>relay_out_mode</c>).</summary>
    [JsonPropertyName("output_mode")]
    public int OutputMode { get; set; }

    /// <summary>O estado pôde ser lido.</summary>
    [JsonPropertyName("available")]
    public bool Available { get; set; }

    /// <summary>Relé acionado; só quando <see cref="Available"/> é <c>true</c>.</summary>
    [JsonPropertyName("active")]
    public bool? Active { get; set; }
}

/// <summary>Estado de uma SecBox em <see cref="IoState"/>.</summary>
public sealed class IoSecBoxState
{
    /// <summary>Identificador (UID) da SecBox.</summary>
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>Número de série.</summary>
    [JsonPropertyName("serial")]
    public string? Serial { get; set; }

    /// <summary>Versão de firmware.</summary>
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    /// <summary>SecBox habilitada.</summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    /// <summary>Estado do relé.</summary>
    [JsonPropertyName("relay")]
    public bool Relay { get; set; }

    /// <summary>Nível lido na entrada da botoeira.</summary>
    [JsonPropertyName("rex")]
    public bool Rex { get; set; }

    /// <summary>Botoeira acionada.</summary>
    [JsonPropertyName("rex_active")]
    public bool RexActive { get; set; }

    /// <summary>Nível lido na entrada do sensor de porta.</summary>
    [JsonPropertyName("door_sensor")]
    public bool DoorSensor { get; set; }

    /// <summary>Sensor de porta habilitado.</summary>
    [JsonPropertyName("door_sensor_enabled")]
    public bool DoorSensorEnabled { get; set; }

    /// <summary>Nível do sensor de porta quando ocioso.</summary>
    [JsonPropertyName("door_sensor_idle")]
    public int DoorSensorIdle { get; set; }

    /// <summary>Sensor de porta ativo.</summary>
    [JsonPropertyName("door_sensor_active")]
    public bool DoorSensorActive { get; set; }

    /// <summary>Porta da SecBox aberta.</summary>
    [JsonPropertyName("open")]
    public bool Open { get; set; }
}

/// <summary>Resposta de <c>get_catra_info.fcgi</c>: contadores de giro da catraca (zerados num reset de fábrica).</summary>
public sealed class TurnstileCounters
{
    /// <summary>Giros para a esquerda.</summary>
    [JsonPropertyName("left_turns")]
    public long LeftTurns { get; set; }

    /// <summary>Giros para a direita.</summary>
    [JsonPropertyName("right_turns")]
    public long RightTurns { get; set; }

    /// <summary>Giros no sentido de entrada.</summary>
    [JsonPropertyName("entrance_turns")]
    public long EntranceTurns { get; set; }

    /// <summary>Giros no sentido de saída.</summary>
    [JsonPropertyName("exit_turns")]
    public long ExitTurns { get; set; }

    /// <summary>Total de giros.</summary>
    [JsonPropertyName("total_turns")]
    public long TotalTurns { get; set; }
}

/// <summary>Resposta de <c>get_hid_last_card.fcgi</c>: último cartão apresentado ao leitor HID.</summary>
public sealed class HidLastCard
{
    /// <summary>Número de série do cartão (CSN).</summary>
    [JsonPropertyName("csn")]
    public string? Csn { get; set; }

    /// <summary>Valor de credencial (PAC).</summary>
    [JsonPropertyName("pac")]
    public string? Pac { get; set; }

    /// <summary>Tipo do cartão.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>Número de bits dos dados do cartão.</summary>
    [JsonPropertyName("bits")]
    public int Bits { get; set; }

    /// <summary>Bloco <c>custom_data[custom_use_index]</c> em hexadecimal, ou vazio.</summary>
    [JsonPropertyName("custom")]
    public string? Custom { get; set; }
}

/// <summary>Resposta de <c>get_wiegand_card_read.fcgi</c>: diagnóstico da última leitura Wiegand.</summary>
public sealed class WiegandCardRead
{
    /// <summary>Valor Wiegand bruto.</summary>
    [JsonPropertyName("card_read_value")]
    public string? CardReadValue { get; set; }

    /// <summary>Número de bits lidos.</summary>
    [JsonPropertyName("card_read_num_bits")]
    public int CardReadNumBits { get; set; }

    /// <summary>Facility code extraído.</summary>
    [JsonPropertyName("facility_code")]
    public string? FacilityCode { get; set; }

    /// <summary>Número do cartão extraído.</summary>
    [JsonPropertyName("card_number")]
    public string? CardNumber { get; set; }

    /// <summary>Resultado da verificação de paridade.</summary>
    [JsonPropertyName("parities")]
    public int Parities { get; set; }

    /// <summary>Data e hora da última leitura, como o equipamento devolve.</summary>
    [JsonPropertyName("last_read_time")]
    public string? LastReadTime { get; set; }
}
