using System.Collections.Generic;
using System.Text.Json.Serialization;
using ControlId.Access.Monitoring;

namespace ControlId.Access;

/// <summary>Resposta de <c>door_state.fcgi</c>: portas por relé ou SecBoxes, conforme o equipamento.</summary>
public sealed class DoorsState
{
    /// <summary>Portas acionadas por relé (iDAccess, iDFit, iDBox).</summary>
    [JsonPropertyName("doors")]
    public List<DoorState> Doors { get; set; } = new();

    /// <summary>SecBoxes (iDFlex, iDAccess Pro, iDAccess Nano).</summary>
    [JsonPropertyName("sec_boxes")]
    public List<DoorState> SecBoxes { get; set; } = new();
}

/// <summary>Resposta de <c>alarm_status.fcgi</c>.</summary>
public sealed class AlarmStatus
{
    /// <summary>Alarme ativo.</summary>
    [JsonPropertyName("active")]
    public bool Active { get; set; }

    /// <summary>Código da causa (0 = sem causa).</summary>
    [JsonPropertyName("cause")]
    public int Cause { get; set; }

    /// <summary><see cref="Cause"/> como enum.</summary>
    [JsonIgnore]
    public AlarmCause CauseType => (AlarmCause)Cause;
}

/// <summary>Causa de alarme, numerada como em <c>alarm_status</c> (os mesmos códigos de <c>alarm_logs.cause</c>).</summary>
public enum AlarmCause
{
    /// <summary>0: sem causa.</summary>
    None = 0,

    /// <summary>1: zona de alarme 1.</summary>
    Zone1 = 1,

    /// <summary>2: zona de alarme 2.</summary>
    Zone2 = 2,

    /// <summary>3: zona de alarme 3.</summary>
    Zone3 = 3,

    /// <summary>4: zona de alarme 4.</summary>
    Zone4 = 4,

    /// <summary>5: zona de alarme 5.</summary>
    Zone5 = 5,

    /// <summary>6: porta aberta.</summary>
    DoorOpen = 6,

    /// <summary>7: arrombamento de porta.</summary>
    DoorForced = 7,

    /// <summary>8: dedo de pânico.</summary>
    PanicFinger = 8,

    /// <summary>9: violação do equipamento.</summary>
    DeviceTamper = 9,

    /// <summary>10: cartão de pânico.</summary>
    PanicCard = 10,

    /// <summary>11: PIN de pânico.</summary>
    PanicPin = 11,

    /// <summary>12: senha de pânico.</summary>
    PanicPassword = 12,

    /// <summary>13: zona monitorada sem presença (iDFit e iDAccess).</summary>
    MonitoredZoneWithoutPresence = 13,

    /// <summary>14: zona monitorada aberta (iDFit e iDAccess).</summary>
    MonitoredZoneOpen = 14,
}

/// <summary>Resultado de uma ação de <c>execute_actions</c> quando o intertravamento via rede está ativo.</summary>
public sealed class DeviceActionStatus
{
    /// <summary>Ação executada (ex.: <c>sec_box</c>).</summary>
    [JsonPropertyName("action")]
    public string? Action { get; set; }

    /// <summary><c>allowed</c> ou <c>denied</c> (porta de um equipamento intertravado está aberta).</summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary><see cref="Status"/> igual a <c>allowed</c>.</summary>
    [JsonIgnore]
    public bool Allowed => Status == "allowed";
}

/// <summary>Evento de <c>remote_led_control</c> (LEDs da iDBlock Next).</summary>
public enum TurnstileLedEvent
{
    /// <summary>0: autorizado (verde).</summary>
    Authorized = 0,

    /// <summary>1: não autorizado (vermelho).</summary>
    Unauthorized = 1,

    /// <summary>2: volta à cor de repouso (idle).</summary>
    Idle = 2,

    /// <summary>3: apaga os LEDs.</summary>
    Off = 3,

    /// <summary>4: troca a cor de repouso pela cor informada.</summary>
    SetIdleColor = 4,
}
