using System;
using System.Globalization;
#if NET8_0_OR_GREATER
using System.Buffers;
#endif

namespace ControlId.Access;

/// <summary>
/// Converte entre o valor de 64 bits do cartão (<c>cards.value</c>) e o formato impresso
/// <c>facility,número</c> (por exemplo <c>123,45678</c>).
/// Para cartões Wiegand/ASK com facility code, o valor é <c>facility × 2^32 + número</c>.
/// </summary>
/// <remarks>
/// Quando a configuração <c>ignore_facility</c> do equipamento está ligada, o equipamento compara só
/// a parte do número e o cartão pode ser cadastrado com facility 0. O campo <c>identifier_id</c> dos
/// eventos de acesso traz o mesmo valor de 64 bits, e <see cref="ToFacility(long)"/> o mostra como está impresso no cartão.
/// </remarks>
public readonly struct CardNumber
{
    private const long FacilityMultiplier = 1L << 32;

#if NET8_0_OR_GREATER
    private static readonly SearchValues<char> Separators = SearchValues.Create(",.-");
#else
    private static readonly char[] Separators = { ',', '.', '-' };
#endif

    /// <summary>Monta o valor do cartão a partir de facility code e número.</summary>
    /// <param name="facility">Facility code, de 0 a <see cref="int.MaxValue"/>.</param>
    /// <param name="number">Número do cartão, de 0 a <see cref="int.MaxValue"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">Algum dos valores é negativo.</exception>
    public static long FromFacility(int facility, int number)
    {
        if (facility < 0) throw new ArgumentOutOfRangeException(nameof(facility), facility, "O facility code não pode ser negativo.");
        if (number < 0) throw new ArgumentOutOfRangeException(nameof(number), number, "O número do cartão não pode ser negativo.");
        return facility * FacilityMultiplier + number;
    }

    /// <summary>Separa o valor do cartão em facility code e número.</summary>
    /// <exception cref="ArgumentOutOfRangeException">O valor é negativo.</exception>
    public static (int Facility, int Number) ToFacility(long value)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), value, "O valor do cartão não pode ser negativo.");
        return ((int)(value >> 32), (int)(value & 0xFFFFFFFFL));
    }

    /// <summary>Formata o valor como <c>facility,número</c>.</summary>
    public static string Format(long value)
    {
        var (facility, number) = ToFacility(value);
        return facility.ToString(CultureInfo.InvariantCulture) + "," + number.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Interpreta <c>123,45678</c>, <c>123.45678</c>, <c>123-45678</c> ou só o número (<c>12345678</c>, facility 0).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> é nulo.</exception>
    /// <exception cref="FormatException">O texto não está em nenhum dos formatos aceitos.</exception>
    public static long Parse(string text)
    {
        if (text is null) throw new ArgumentNullException(nameof(text));
        if (!TryParse(text, out var value))
            throw new FormatException($"Número de cartão inválido: '{text}'. Use 'facility,número' ou só o número.");
        return value;
    }

    /// <summary>Versão de <see cref="Parse(string)"/> que não lança exceção.</summary>
    public static bool TryParse(string? text, out long value)
    {
        value = 0;
        if (text is null) return false;
        var s = text.Trim();
        if (s.Length == 0) return false;

#if NET8_0_OR_GREATER
        var sep = s.AsSpan().IndexOfAny(Separators);
#else
        var sep = s.IndexOfAny(Separators);
#endif
        if (sep < 0)
        {
            return TryDigits(s, out value);
        }

        var left = s.Substring(0, sep);
        var right = s.Substring(sep + 1);
        if (!TryDigits(left, out var facility) || !TryDigits(right, out var number)) return false;
        if (facility > int.MaxValue || number > int.MaxValue) return false;
        value = FromFacility((int)facility, (int)number);
        return true;
    }

    private static bool TryDigits(string s, out long value)
    {
        value = 0;
        if (s.Length == 0) return false;
        foreach (var c in s)
        {
            if (c < '0' || c > '9') return false;
        }
        return long.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }
}
