using System;

namespace ControlId.Internal;

/// <summary>Conversão de códigos numéricos vindos do equipamento para enums sem gerar valores indefinidos.</summary>
internal static class EnumValues
{
    /// <summary>
    /// Devolve <paramref name="value"/> como <typeparamref name="T"/> se for um membro definido; senão, o membro
    /// <c>Unknown</c> do enum (código de um firmware mais novo, por exemplo).
    /// </summary>
    public static T ToDefined<T>(long value)
        where T : struct, Enum
    {
        if (value >= int.MinValue && value <= int.MaxValue)
        {
            var boxed = Enum.ToObject(typeof(T), (int)value);
            if (Enum.IsDefined(typeof(T), boxed))
            {
                return (T)boxed;
            }
        }

        return Cache<T>.Unknown;
    }

    private static class Cache<T>
        where T : struct, Enum
    {
#if NET5_0_OR_GREATER
        public static readonly T Unknown = Enum.Parse<T>("Unknown");
#else
        public static readonly T Unknown = (T)Enum.Parse(typeof(T), "Unknown");
#endif
    }
}
