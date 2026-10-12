using System;
using System.Text.Json.Serialization;
using ControlId.Internal;

namespace ControlId.Access;

/// <summary>Objeto <c>qrcodes</c>: QR Code de identificação (só no modo alfanumérico; no outro modo vira cartão).</summary>
[ControlIdObject("qrcodes")]
public sealed class AccessQrCode
{
    /// <summary>ID no equipamento.</summary>
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    /// <summary>Conteúdo do QR Code (único no equipamento).</summary>
    [JsonPropertyName("value")]
    public string? Value { get; set; }

    /// <summary>Dono do QR Code.</summary>
    [JsonPropertyName("user_id")]
    public long UserId { get; set; }
}

/// <summary>Objeto <c>uhf_tags</c>: tag UHF de identificação (só no modo extended; no standard vira cartão).</summary>
[ControlIdObject("uhf_tags")]
public sealed class AccessUhfTag
{
    /// <summary>ID no equipamento.</summary>
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    /// <summary>Valor da tag em hexadecimal, sem <c>0x</c> (ex.: <c>CAFEDAD0</c>), único no equipamento.</summary>
    [JsonPropertyName("value")]
    public string? Value { get; set; }

    /// <summary>Dono da tag.</summary>
    [JsonPropertyName("user_id")]
    public long UserId { get; set; }
}

/// <summary>Objeto <c>pins</c>: PIN de identificação.</summary>
[ControlIdObject("pins")]
public sealed class AccessPin
{
    /// <summary>ID no equipamento.</summary>
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    /// <summary>Valor do PIN (único no equipamento).</summary>
    [JsonPropertyName("value")]
    public string? Value { get; set; }

    /// <summary>Dono do PIN.</summary>
    [JsonPropertyName("user_id")]
    public long UserId { get; set; }
}

/// <summary>Objeto <c>contingency_cards</c>: cartões aceitos no modo de contingência.</summary>
[ControlIdObject("contingency_cards")]
public sealed class ContingencyCard
{
    /// <summary>ID (auto-incremental).</summary>
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    /// <summary>Número do cartão.</summary>
    [JsonPropertyName("value")]
    public long Value { get; set; }
}

/// <summary>Objeto <c>alarm_zones</c>: entradas de alarme.</summary>
[ControlIdObject("alarm_zones", Key = "zone")]
public sealed class AlarmZone
{
    /// <summary>Número da zona (chave).</summary>
    [JsonPropertyName("zone")]
    public long Zone { get; set; }

    /// <summary>Entrada habilitada.</summary>
    [JsonPropertyName("enabled"), JsonConverter(typeof(IntBooleanConverter))]
    public bool Enabled { get; set; }

    /// <summary><c>true</c> = ativo alto; <c>false</c> = ativo baixo.</summary>
    [JsonPropertyName("active_level"), JsonConverter(typeof(IntBooleanConverter))]
    public bool ActiveHigh { get; set; }

    /// <summary>Atraso do disparo depois que o sinal é detectado (a documentação não informa a unidade).</summary>
    [JsonPropertyName("alarm_delay")]
    public int AlarmDelay { get; set; }
}

/// <summary>Objeto <c>timed_alarms</c>: horários fixos de sirene em dias da semana.</summary>
[ControlIdObject("timed_alarms")]
public sealed class TimedAlarm
{
    /// <summary>ID no equipamento.</summary>
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    /// <summary>Nome do alarme.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Horário, em segundos desde a meia-noite.</summary>
    [JsonPropertyName("time")]
    public int Time { get; set; }

    /// <summary>Domingo.</summary>
    [JsonPropertyName("sun"), JsonConverter(typeof(IntBooleanConverter))]
    public bool Sunday { get; set; }

    /// <summary>Segunda-feira.</summary>
    [JsonPropertyName("mon"), JsonConverter(typeof(IntBooleanConverter))]
    public bool Monday { get; set; }

    /// <summary>Terça-feira.</summary>
    [JsonPropertyName("tue"), JsonConverter(typeof(IntBooleanConverter))]
    public bool Tuesday { get; set; }

    /// <summary>Quarta-feira.</summary>
    [JsonPropertyName("wed"), JsonConverter(typeof(IntBooleanConverter))]
    public bool Wednesday { get; set; }

    /// <summary>Quinta-feira.</summary>
    [JsonPropertyName("thu"), JsonConverter(typeof(IntBooleanConverter))]
    public bool Thursday { get; set; }

    /// <summary>Sexta-feira.</summary>
    [JsonPropertyName("fri"), JsonConverter(typeof(IntBooleanConverter))]
    public bool Friday { get; set; }

    /// <summary>Sábado.</summary>
    [JsonPropertyName("sat"), JsonConverter(typeof(IntBooleanConverter))]
    public bool Saturday { get; set; }

    /// <summary>Sirene de segunda a sexta no horário informado.</summary>
    public static TimedAlarm Weekdays(string name, TimeSpan time) => new()
    {
        Name = name,
        Time = time >= TimeSpan.Zero && time < TimeSpan.FromDays(1)
            ? (int)time.TotalSeconds
            : throw new ArgumentOutOfRangeException(nameof(time), time, "Use um horário entre 00:00 e 23:59:59."),
        Monday = true,
        Tuesday = true,
        Wednesday = true,
        Thursday = true,
        Friday = true,
    };
}

/// <summary>Objeto <c>user_roles</c>: usuários com privilégio diferente do padrão (ex.: administradores).</summary>
[ControlIdObject("user_roles", Key = "user_id")]
public sealed class UserRole
{
    /// <summary>Usuário (chave).</summary>
    [JsonPropertyName("user_id")]
    public long UserId { get; set; }

    /// <summary>Nível de privilégio; 1 = administrador.</summary>
    [JsonPropertyName("role")]
    public int Role { get; set; }

    /// <summary><see cref="Role"/> igual a 1.</summary>
    [JsonIgnore]
    public bool IsAdministrator => Role == 1;
}

/// <summary>Objeto <c>scheduled_unlocks</c>: liberações agendadas (vinculadas a regras com <see cref="AccessLink.ScheduledUnlockAccessRule"/>).</summary>
[ControlIdObject("scheduled_unlocks")]
public sealed class ScheduledUnlock
{
    /// <summary>ID no equipamento.</summary>
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    /// <summary>Nome da liberação.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Mensagem exibida durante a liberação.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

/// <summary>Objeto <c>actions</c>: scripts de ação (vinculados a portais com <see cref="AccessLink.PortalAction"/>).</summary>
[ControlIdObject("actions", Key = "group_id")]
public sealed class ActionScript
{
    /// <summary>ID do script (a API chama o campo de <c>group_id</c>).</summary>
    [JsonPropertyName("group_id")]
    public long? Id { get; set; }

    /// <summary>Nome descritivo.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Arquivo do script de ação.</summary>
    [JsonPropertyName("action")]
    public string? Action { get; set; }

    /// <summary>Parâmetros do script.</summary>
    [JsonPropertyName("parameters")]
    public string? Parameters { get; set; }
}

/// <summary>Objeto <c>devices</c>: equipamentos conhecidos (inclusive o próprio e o servidor do modo online).</summary>
[ControlIdObject("devices")]
public sealed class AccessDevice
{
    /// <summary>ID no equipamento.</summary>
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    /// <summary>Nome descritivo.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Endereço, ex.: <c>192.168.0.129</c> ou <c>exemplo.controlid.com.br</c>.</summary>
    [JsonPropertyName("ip")]
    public string? Ip { get; set; }

    /// <summary>Chave pública (usada pelo servidor do modo online).</summary>
    [JsonPropertyName("public_key")]
    public string? PublicKey { get; set; }
}

/// <summary>Objeto <c>catra_infos</c>: contadores de giro da catraca iDBlock (somente leitura).</summary>
[ControlIdObject("catra_infos")]
public sealed class TurnstileInfo
{
    /// <summary>ID da catraca.</summary>
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    /// <summary>Giros à esquerda.</summary>
    [JsonPropertyName("left_turns")]
    public long LeftTurns { get; set; }

    /// <summary>Giros à direita.</summary>
    [JsonPropertyName("right_turns")]
    public long RightTurns { get; set; }

    /// <summary>Giros de entrada.</summary>
    [JsonPropertyName("entrance_turns")]
    public long EntranceTurns { get; set; }

    /// <summary>Giros de saída.</summary>
    [JsonPropertyName("exit_turns")]
    public long ExitTurns { get; set; }
}

/// <summary>Objeto <c>log_types</c>: tipos de batida (modo ponto).</summary>
[ControlIdObject("log_types")]
public sealed class LogType
{
    /// <summary>ID do tipo.</summary>
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    /// <summary>Nome do tipo.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

/// <summary>Objeto <c>contacts</c>: contatos para ligações de interfonia SIP (iDFace).</summary>
[ControlIdObject("contacts")]
public sealed class SipContact
{
    /// <summary>ID no equipamento.</summary>
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    /// <summary>Nome do contato.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Número (ramal).</summary>
    [JsonPropertyName("number")]
    public string? Number { get; set; }
}

/// <summary>Objeto <c>custom_thresholds</c>: rigidez da identificação facial por usuário (iDFace).</summary>
[ControlIdObject("custom_thresholds")]
public sealed class CustomThreshold
{
    /// <summary>ID no equipamento.</summary>
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    /// <summary>Usuário.</summary>
    [JsonPropertyName("user_id")]
    public long UserId { get; set; }

    /// <summary>Threshold do usuário.</summary>
    [JsonPropertyName("threshold")]
    public int Threshold { get; set; }
}

/// <summary>
/// Objeto <c>network_interlocking_rules</c>: intertravamento remoto. Este equipamento consulta a porta do equipamento
/// remoto e não abre a sua enquanto a outra estiver aberta.
/// </summary>
[ControlIdObject("network_interlocking_rules")]
public sealed class NetworkInterlockingRule
{
    /// <summary>ID da regra.</summary>
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    /// <summary>IP do equipamento remoto.</summary>
    [JsonPropertyName("ip")]
    public string? Ip { get; set; }

    /// <summary>Login da interface web do equipamento remoto.</summary>
    [JsonPropertyName("login")]
    public string? Login { get; set; }

    /// <summary>Senha da interface web do equipamento remoto.</summary>
    [JsonPropertyName("password")]
    public string? Password { get; set; }

    /// <summary>Nome da regra.</summary>
    [JsonPropertyName("portal_name")]
    public string? PortalName { get; set; }

    /// <summary>Regra habilitada.</summary>
    [JsonPropertyName("enabled"), JsonConverter(typeof(IntBooleanConverter))]
    public bool Enabled { get; set; }
}

/// <summary>
/// Objeto <c>change_logs</c>: inserções, alterações e remoções em <c>users</c>, <c>templates</c>, <c>face_templates</c>
/// e <c>cards</c> (iDFace; até 10 mil, com rotação).
/// </summary>
[ControlIdObject("change_logs")]
public sealed class ChangeLog
{
    /// <summary>ID sequencial.</summary>
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>Operação realizada.</summary>
    [JsonPropertyName("operation_type")]
    public string? OperationType { get; set; }

    /// <summary>Objeto alterado.</summary>
    [JsonPropertyName("table_name")]
    public string? TableName { get; set; }

    /// <summary>ID do registro alterado.</summary>
    [JsonPropertyName("table_id")]
    public long TableId { get; set; }

    /// <summary>Momento (Unix timestamp).</summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    /// <summary><see cref="Timestamp"/> como data, em UTC.</summary>
    [JsonIgnore]
    public DateTimeOffset TimestampUtc => DateTimeOffset.FromUnixTimeSeconds(Timestamp);
}

/// <summary>Objeto <c>access_log_access_rules</c>: regras de acesso avaliadas num log (somente leitura).</summary>
[ControlIdObject("access_log_access_rules", Key = null)]
public sealed class AccessLogAccessRule
{
    /// <summary>Log de acesso.</summary>
    [JsonPropertyName("access_log_id")]
    public long AccessLogId { get; set; }

    /// <summary>Regra de acesso.</summary>
    [JsonPropertyName("access_rule_id")]
    public long AccessRuleId { get; set; }
}
