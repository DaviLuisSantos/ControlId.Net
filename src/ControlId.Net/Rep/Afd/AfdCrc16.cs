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

        return Compute(text, 0, text.Length);
    }

    /// <summary>CRC de um trecho de <paramref name="text"/>, sem copiá-lo (o parser calcula sobre a linha inteira).</summary>
    internal static ushort Compute(string text, int start, int length)
    {
        ushort crc = 0;
        var end = start + length;
        for (var i = start; i < end; i++)
        {
            // ISO-8859-1: cada caractere é um byte com o mesmo código. Fora dessa faixa, usa '?' como o encoder faria.
            var c = text[i];
            var b = c <= 0xFF ? c : '?';
            crc = (ushort)((crc >> 8) ^ Table[(crc ^ b) & 0xFF]);
        }

        return crc;
    }

    // CRC-16/KERMIT (polinômio 0x1021 refletido = 0x8408): a tabela troca os 8 passos por bit de cada byte por uma consulta.
    private static readonly ushort[] Table = BuildTable();

    private static ushort[] BuildTable()
    {
        var table = new ushort[256];
        for (var i = 0; i < 256; i++)
        {
            var crc = (ushort)i;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (ushort)((crc >> 1) ^ 0x8408) : (ushort)(crc >> 1);
            }

            table[i] = crc;
        }

        return table;
    }

    /// <summary>CRC em 4 dígitos hexadecimais maiúsculos, como gravado no AFD.</summary>
    public static string ComputeHex(string text) => Compute(text).ToString("X4", CultureInfo.InvariantCulture);
}
