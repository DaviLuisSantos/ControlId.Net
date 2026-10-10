using System;
using System.Globalization;

namespace ControlId.Rep.Afd;

/// <summary>
/// CRC-16/KERMIT (CCITT-TRUE), o algoritmo que a Portaria 671 exige no AFD de REP-A e REP-P.
/// Exemplo do leiaute: <c>"123456789"</c> → <c>"2189"</c>.
/// </summary>
public static class AfdCrc16
{
    /// <summary>Calcula o CRC dos caracteres de <paramref name="text"/> em ISO-8859-1.</summary>
    public static ushort Compute(string text)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        ushort crc = 0;
        foreach (var c in text)
        {
            // ISO-8859-1: cada caractere é um byte com o mesmo código. Fora dessa faixa, usa '?' como o encoder faria.
            crc ^= c <= 0xFF ? c : '?';
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (ushort)((crc >> 1) ^ 0x8408) : (ushort)(crc >> 1);
            }
        }

        return crc;
    }

    /// <summary>CRC em 4 dígitos hexadecimais maiúsculos, como gravado no AFD.</summary>
    public static string ComputeHex(string text) => Compute(text).ToString("X4", CultureInfo.InvariantCulture);
}
