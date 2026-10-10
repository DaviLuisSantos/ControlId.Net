using System;
using System.Text.Json.Serialization;
using ControlId.Internal;

namespace ControlId.Access;

/// <summary>
/// Objeto <c>time_spans</c>: um intervalo dentro de um <see cref="AccessTimeZone"/>,
/// válido nos dias marcados. <see cref="Start"/> e <see cref="End"/> são segundos desde a meia-noite.
/// </summary>
public sealed class AccessTimeSpan
{
    /// <summary>ID no equipamento.</summary>
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    /// <summary>Horário ao qual o intervalo pertence.</summary>
    [JsonPropertyName("time_zone_id")]
    public long TimeZoneId { get; set; }

    /// <summary>Início, em segundos desde a meia-noite.</summary>
    [JsonPropertyName("start")]
    public int Start { get; set; }

    /// <summary>Fim, em segundos desde a meia-noite (86400 = fim do dia).</summary>
    [JsonPropertyName("end")]
    public int End { get; set; }

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

    /// <summary>Feriados do tipo 1.</summary>
    [JsonPropertyName("hol1"), JsonConverter(typeof(IntBooleanConverter))]
    public bool Holiday1 { get; set; }

    /// <summary>Feriados do tipo 2.</summary>
    [JsonPropertyName("hol2"), JsonConverter(typeof(IntBooleanConverter))]
    public bool Holiday2 { get; set; }

    /// <summary>Feriados do tipo 3.</summary>
    [JsonPropertyName("hol3"), JsonConverter(typeof(IntBooleanConverter))]
    public bool Holiday3 { get; set; }

    /// <summary>Intervalo de segunda a sexta entre <paramref name="start"/> e <paramref name="end"/>.</summary>
    public static AccessTimeSpan Weekdays(long timeZoneId, TimeSpan start, TimeSpan end) => new()
    {
        TimeZoneId = timeZoneId,
        Start = ToSeconds(start, nameof(start)),
        End = ToSeconds(end, nameof(end)),
        Monday = true,
        Tuesday = true,
        Wednesday = true,
        Thursday = true,
        Friday = true,
    };

    /// <summary>Intervalo em todos os dias da semana (sem feriados) entre <paramref name="start"/> e <paramref name="end"/>.</summary>
    public static AccessTimeSpan EveryDay(long timeZoneId, TimeSpan start, TimeSpan end)
    {
        var span = Weekdays(timeZoneId, start, end);
        span.Saturday = true;
        span.Sunday = true;
        return span;
    }

    private static int ToSeconds(TimeSpan time, string paramName) =>
        time >= TimeSpan.Zero && time <= TimeSpan.FromDays(1)
            ? (int)time.TotalSeconds
            : throw new ArgumentOutOfRangeException(paramName, time, "Use um horário entre 00:00 e 24:00.");
}
