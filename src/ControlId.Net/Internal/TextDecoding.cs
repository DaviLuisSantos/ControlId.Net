using System;
using System.Text;

namespace ControlId.Internal;

/// <summary>
/// Decodifica texto devolvido pelo equipamento (AFD, relatórios, logs): usa o charset informado; sem ele, UTF-8 se os
/// bytes forem UTF-8 válido e, senão, ISO-8859-1. Ler como UTF-8 às cegas corromperia nomes acentuados em Latin-1.
/// </summary>
internal static class TextDecoding
{
    private static readonly Encoding Latin1 = Encoding.GetEncoding("iso-8859-1");
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static string Decode(byte[] content, string? charSet)
    {
        if (!string.IsNullOrWhiteSpace(charSet))
        {
            try
            {
                return Encoding.GetEncoding(charSet!.Trim('"', ' ')).GetString(content);
            }
            catch (ArgumentException)
            {
                // Charset desconhecido: cai na detecção abaixo.
            }
        }

        try
        {
            var text = StrictUtf8.GetString(content);
            return text.Length > 0 && text[0] == '\uFEFF' ? text.Substring(1) : text;
        }
        catch (DecoderFallbackException)
        {
            return Latin1.GetString(content);
        }
    }
}
