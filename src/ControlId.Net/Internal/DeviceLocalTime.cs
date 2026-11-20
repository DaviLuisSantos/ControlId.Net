using System;

namespace ControlId.Internal;

/// <summary>Converte o Unix timestamp gravado pelo equipamento, que é a hora de parede local dele.</summary>
internal static class DeviceLocalTime
{
    /// <summary>
    /// Lê <paramref name="seconds"/> como hora local do equipamento, sem fuso:
    /// o <see cref="DateTime.Kind"/> do resultado é <see cref="DateTimeKind.Unspecified"/>.
    /// </summary>
    internal static DateTime FromEpoch(long seconds) =>
        DateTime.SpecifyKind(DateTimeOffset.FromUnixTimeSeconds(seconds).DateTime, DateTimeKind.Unspecified);
}
