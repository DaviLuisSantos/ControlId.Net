using System;
using System.Text.Json.Serialization;

namespace ControlId.Internal;

/// <summary>Formato de data/hora usado pelos dois tipos de equipamento (campos separados).</summary>
internal sealed class DeviceDateTime
{
    [JsonPropertyName("day")] public int Day { get; set; }

    [JsonPropertyName("month")] public int Month { get; set; }

    [JsonPropertyName("year")] public int Year { get; set; }

    [JsonPropertyName("hour")] public int Hour { get; set; }

    [JsonPropertyName("minute")] public int Minute { get; set; }

    [JsonPropertyName("second")] public int Second { get; set; }

    public static DeviceDateTime From(DateTime value) => new()
    {
        Day = value.Day,
        Month = value.Month,
        Year = value.Year,
        Hour = value.Hour,
        Minute = value.Minute,
        Second = value.Second,
    };

    public DateTime ToDateTime() => new(Year, Month, Day, Hour, Minute, Second, DateTimeKind.Unspecified);
}
