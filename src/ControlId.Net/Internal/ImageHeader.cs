using System;
using System.Globalization;

namespace ControlId.Internal;

/// <summary>
/// Lê largura e altura do cabeçalho de uma imagem JPEG (SOF0, SOF1 e SOF2) ou PNG (IHDR), sem biblioteca de imagens,
/// e confere a foto contra os limites de <see cref="ControlIdDeviceOptions"/> antes de enviá-la.
/// </summary>
internal static class ImageHeader
{
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    /// <summary>Lê as dimensões do cabeçalho; <c>false</c> se não for JPEG nem PNG, ou se o cabeçalho estiver truncado ou corrompido.</summary>
    internal static bool TryReadSize(byte[] image, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (image is null)
        {
            return false;
        }

        if (image.Length >= 4 && image[0] == 0xFF && image[1] == 0xD8)
        {
            return TryReadJpeg(image, out width, out height);
        }

        if (image.Length >= 24 && StartsWithPngSignature(image))
        {
            return TryReadPng(image, out width, out height);
        }

        return false;
    }

    /// <summary>
    /// Confere a foto contra os limites das opções. Devolve a exceção a lançar (ou a registrar no resultado daquele usuário),
    /// ou <c>null</c> se a foto passa. A imagem vazia não é tratada aqui.
    /// </summary>
    internal static ControlIdImageRejectedException? Check(ControlIdDeviceOptions options, long? userId, byte[] image)
    {
        var owner = userId is null ? "A foto" : $"A foto do usuário {userId.Value.ToString(CultureInfo.InvariantCulture)}";
        if (!TryReadSize(image, out var width, out var height))
        {
            return new ControlIdImageRejectedException(
                ImageRejectionReason.Format,
                userId,
                null,
                null,
                image.Length,
                $"{owner} foi recusada: o cabeçalho não é de uma imagem JPEG ou PNG válida.");
        }

        var longSide = Math.Max(width, height);
        var shortSide = Math.Min(width, height);
        if (longSide > options.MaxImageLongSide || shortSide > options.MaxImageShortSide)
        {
            return new ControlIdImageRejectedException(
                ImageRejectionReason.Dimensions,
                userId,
                width,
                height,
                image.Length,
                $"{owner} foi recusada: tem {width}x{height} pixels e o equipamento aceita no máximo {options.MaxImageLongSide}x{options.MaxImageShortSide} (lado maior x lado menor).");
        }

        if (image.Length > options.MaxImageBytes)
        {
            return new ControlIdImageRejectedException(
                ImageRejectionReason.Size,
                userId,
                width,
                height,
                image.Length,
                $"{owner} foi recusada: tem {image.Length} bytes e o limite é {options.MaxImageBytes} bytes.");
        }

        return null;
    }

    private static bool StartsWithPngSignature(byte[] image)
    {
        for (var i = 0; i < PngSignature.Length; i++)
        {
            if (image[i] != PngSignature[i])
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryReadPng(byte[] image, out int width, out int height)
    {
        width = 0;
        height = 0;

        // Depois da assinatura: tamanho do bloco (4 bytes), tipo (4 bytes) e o IHDR com largura e altura.
        if (image[12] != (byte)'I' || image[13] != (byte)'H' || image[14] != (byte)'D' || image[15] != (byte)'R')
        {
            return false;
        }

        var w = ReadBigEndian32(image, 16);
        var h = ReadBigEndian32(image, 20);
        if (w < 1 || h < 1 || w > int.MaxValue || h > int.MaxValue)
        {
            return false;
        }

        width = (int)w;
        height = (int)h;
        return true;
    }

    private static long ReadBigEndian32(byte[] data, int offset) =>
        ((long)data[offset] << 24) | ((long)data[offset + 1] << 16) | ((long)data[offset + 2] << 8) | data[offset + 3];

    private static bool TryReadJpeg(byte[] image, out int width, out int height)
    {
        width = 0;
        height = 0;
        var i = 2;
        while (i < image.Length)
        {
            if (image[i] != 0xFF)
            {
                return false;
            }

            while (i < image.Length && image[i] == 0xFF)
            {
                i++;
            }

            if (i >= image.Length)
            {
                return false;
            }

            var marker = image[i++];

            // Marcadores sem tamanho (TEM, RSTn, SOI): seguem para o próximo.
            if (marker == 0x00 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD8))
            {
                continue;
            }

            // Fim da imagem ou início dos dados sem ter passado por um SOF.
            if (marker == 0xD9 || marker == 0xDA)
            {
                return false;
            }

            if (i + 1 >= image.Length)
            {
                return false;
            }

            var segmentLength = (image[i] << 8) | image[i + 1];
            if (segmentLength < 2)
            {
                return false;
            }

            if (marker >= 0xC0 && marker <= 0xC2)
            {
                // Tamanho (2), precisão (1), altura (2), largura (2).
                if (segmentLength < 7 || i + 6 >= image.Length)
                {
                    return false;
                }

                var h = (image[i + 3] << 8) | image[i + 4];
                var w = (image[i + 5] << 8) | image[i + 6];
                if (w < 1 || h < 1)
                {
                    return false;
                }

                width = w;
                height = h;
                return true;
            }

            i += segmentLength;
        }

        return false;
    }
}
