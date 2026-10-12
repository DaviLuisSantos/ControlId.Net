using System;
using System.Text.Json.Serialization;
using ControlId.Internal;

namespace ControlId.Access;

/// <summary>Objeto <c>areas</c>: área cujo acesso se controla; portais ligam duas áreas.</summary>
[ControlIdObject("areas")]
public sealed class AccessArea
{
    /// <summary>ID no equipamento.</summary>
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    /// <summary>Nome da área.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

/// <summary>
/// Objeto <c>holidays</c>: feriado e os grupos de feriado a que pertence. Os intervalos de horário
/// (<see cref="AccessTimeSpan.Holiday1"/>...) valem nos feriados do grupo correspondente.
/// </summary>
[ControlIdObject("holidays")]
public sealed class AccessHoliday
{
    /// <summary>ID no equipamento.</summary>
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    /// <summary>Nome do feriado.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Início (Unix timestamp).</summary>
    [JsonPropertyName("start")]
    public long Start { get; set; }

    /// <summary>Fim (Unix timestamp).</summary>
    [JsonPropertyName("end")]
    public long End { get; set; }

    /// <summary>Pertence ao grupo de feriados 1.</summary>
    [JsonPropertyName("hol1"), JsonConverter(typeof(IntBooleanConverter))]
    public bool Group1 { get; set; }

    /// <summary>Pertence ao grupo de feriados 2.</summary>
    [JsonPropertyName("hol2"), JsonConverter(typeof(IntBooleanConverter))]
    public bool Group2 { get; set; }

    /// <summary>Pertence ao grupo de feriados 3.</summary>
    [JsonPropertyName("hol3"), JsonConverter(typeof(IntBooleanConverter))]
    public bool Group3 { get; set; }

    /// <summary>Repete todo ano.</summary>
    [JsonPropertyName("repeats"), JsonConverter(typeof(IntBooleanConverter))]
    public bool Repeats { get; set; }

    /// <summary>
    /// Feriado de dia inteiro, de 00:00 de <paramref name="date"/> até 00:00 do dia seguinte, no fuso
    /// <paramref name="utcOffset"/> (o do equipamento, ex.: <c>TimeSpan.FromHours(-3)</c>), no grupo 1.
    /// </summary>
    public static AccessHoliday FullDay(string name, DateTime date, TimeSpan utcOffset, bool repeats = false)
    {
        var start = new DateTimeOffset(date.Date, utcOffset);
        return new AccessHoliday
        {
            Name = name,
            Start = start.ToUnixTimeSeconds(),
            End = start.AddDays(1).ToUnixTimeSeconds(),
            Group1 = true,
            Repeats = repeats,
        };
    }
}

/// <summary>
/// Objeto <c>access_events</c>: aberturas de porta/SecBox e giros de catraca (até 10 mil, com rotação).
/// </summary>
[ControlIdObject("access_events")]
public sealed class AccessDeviceEvent
{
    /// <summary>ID sequencial.</summary>
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>Categoria: <c>door</c>, <c>secbox</c> ou <c>catra</c>.</summary>
    [JsonPropertyName("event")]
    public string? Event { get; set; }

    /// <summary>Porta/SecBox: <c>OPEN</c> ou <c>CLOSE</c>. Catraca: <c>TURN_LEFT</c>, <c>TURN_RIGHT</c> ou <c>GIVE_UP</c>.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>ID da porta/SecBox em texto, ou o uuid do evento de catraca.</summary>
    [JsonPropertyName("identification")]
    public string? Identification { get; set; }

    /// <summary>Equipamento que registrou.</summary>
    [JsonPropertyName("device_id")]
    public long? DeviceId { get; set; }

    /// <summary>Momento (Unix timestamp).</summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    /// <summary><see cref="Timestamp"/> como data, em UTC.</summary>
    [JsonIgnore]
    public DateTimeOffset TimestampUtc => DateTimeOffset.FromUnixTimeSeconds(Timestamp);
}

/// <summary>Objeto <c>alarm_logs</c>: alarmes disparados e desligados.</summary>
[ControlIdObject("alarm_logs")]
public sealed class AlarmLog
{
    /// <summary>ID sequencial.</summary>
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>
    /// Evento. A documentação lista "alarme ativado" e "alarme desativado", sem os códigos numéricos.
    /// </summary>
    [JsonPropertyName("event")]
    public int Event { get; set; }

    /// <summary>
    /// Causa. A documentação lista, sem os códigos: zonas de alarme 1 a 5, porta aberta, arrombamento de porta,
    /// dedo de pânico, violação do equipamento e cartão de pânico.
    /// </summary>
    [JsonPropertyName("cause")]
    public int Cause { get; set; }

    /// <summary>Usuário envolvido.</summary>
    [JsonPropertyName("user_id")]
    public long? UserId { get; set; }

    /// <summary>Momento (Unix timestamp).</summary>
    [JsonPropertyName("time")]
    public long Time { get; set; }

    /// <summary>Log de acesso relacionado.</summary>
    [JsonPropertyName("access_log_id")]
    public long? AccessLogId { get; set; }

    /// <summary>Porta envolvida.</summary>
    [JsonPropertyName("door_id")]
    public long? DoorId { get; set; }

    /// <summary><see cref="Time"/> como data, em UTC.</summary>
    [JsonIgnore]
    public DateTimeOffset TimeUtc => DateTimeOffset.FromUnixTimeSeconds(Time);
}

/// <summary>Objeto <c>sec_boxs</c>: o módulo de acionamento externo (MAE/SecBox) do iDFlex, iDAccess Pro e iDAccess Nano.</summary>
[ControlIdObject("sec_boxs")]
public sealed class SecBox
{
    /// <summary>ID (sempre 65793, segundo a documentação).</summary>
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>Versão da SecBox.</summary>
    [JsonPropertyName("version")]
    public int? Version { get; set; }

    /// <summary>Nome.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Habilitada.</summary>
    [JsonPropertyName("enabled"), JsonConverter(typeof(LenientBooleanConverter))]
    public bool Enabled { get; set; }

    /// <summary>Tempo de abertura do relé.</summary>
    [JsonPropertyName("relay_timeout")]
    public int RelayTimeoutMilliseconds { get; set; }

    /// <summary>Sensor de porta habilitado.</summary>
    [JsonPropertyName("door_sensor_enabled"), JsonConverter(typeof(LenientBooleanConverter))]
    public bool DoorSensorEnabled { get; set; }

    /// <summary>Sensor de porta NA (<c>true</c>) ou NF (<c>false</c>).</summary>
    [JsonPropertyName("door_sensor_idle"), JsonConverter(typeof(LenientBooleanConverter))]
    public bool DoorSensorNormallyOpen { get; set; }

    /// <summary>Fecha o relé quando o sensor de porta abre.</summary>
    [JsonPropertyName("auto_close_enabled"), JsonConverter(typeof(IntBooleanConverter))]
    public bool AutoCloseEnabled { get; set; }
}
