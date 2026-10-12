using System;
using System.Globalization;
using System.Text.Json.Serialization;

namespace ControlId.Access;

/// <summary>Uma ação de <c>execute_actions.fcgi</c>.</summary>
public sealed class DeviceAction
{
    /// <summary>Cria a ação.</summary>
    public DeviceAction(string action, string parameters = "")
    {
        Action = action;
        Parameters = parameters;
    }

    /// <summary>Nome da ação: <c>door</c>, <c>sec_box</c>, <c>catra</c>, <c>open_collector</c>...</summary>
    [JsonPropertyName("action")]
    public string Action { get; }

    /// <summary>Parâmetros no formato do equipamento, ex.: <c>door=1</c>.</summary>
    [JsonPropertyName("parameters")]
    public string Parameters { get; }

    /// <summary>Aciona o relé da porta (iDAccess, iDFit, iDBox).</summary>
    public static DeviceAction OpenDoor(int door = 1) => new("door", "door=" + door.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// Aciona a SecBox (iDFlex, iDAccess Pro, iDAccess Nano). <paramref name="reason"/>: 0 desconhecido,
    /// 1 autorizado, 2 botoeira, 3 comando web.
    /// </summary>
    public static DeviceAction OpenSecBox(long secBoxId = 65793, int reason = 1) =>
        new("sec_box", $"id={secBoxId.ToString(CultureInfo.InvariantCulture)}, reason={reason.ToString(CultureInfo.InvariantCulture)}");

    /// <summary>Abre o coletor (urna) da catraca iDBlock.</summary>
    public static DeviceAction OpenTurnstileCollector() => new("open_collector", string.Empty);

    /// <summary>Aciona o relé 1 ou 2 da catraca.</summary>
    public static DeviceAction ActivateTurnstileRelay(int relay) => relay is 1 or 2
        ? new("catra", "relay=" + relay.ToString(CultureInfo.InvariantCulture))
        : throw new ArgumentOutOfRangeException(nameof(relay), relay, "A catraca tem os relés 1 e 2.");

    /// <summary>Libera a catraca no sentido informado (iDBlock).</summary>
    public static DeviceAction ReleaseTurnstile(TurnstileDirection direction) => new("catra", direction switch
    {
        TurnstileDirection.Clockwise => "allow=clockwise",
        TurnstileDirection.Anticlockwise => "allow=anticlockwise",
        TurnstileDirection.Both => "allow=both",
        _ => throw new ArgumentOutOfRangeException(nameof(direction)),
    });
}
