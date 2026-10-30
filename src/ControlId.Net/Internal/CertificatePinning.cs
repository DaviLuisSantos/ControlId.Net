using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace ControlId.Internal;

/// <summary>Validação do certificado TLS dos equipamentos: impressão digital fixada, qualquer certificado ou a padrão.</summary>
internal static class CertificatePinning
{
    private const string HexDigits = "0123456789ABCDEF";

    public static void Configure(HttpClientHandler handler, string? sha256, bool acceptAny)
    {
        if (Normalize(sha256) is { } expected)
        {
            // Com a impressão digital fixada, só aquele certificado passa, mesmo autoassinado (é o caso do iDClass).
            handler.ServerCertificateCustomValidationCallback = (_, certificate, _, _) =>
                certificate is not null && string.Equals(Sha256(certificate), expected, StringComparison.Ordinal);
        }
        else if (acceptAny)
        {
            handler.ServerCertificateCustomValidationCallback = static (_, _, _, _) => true;
        }
    }

    /// <summary>SHA-256 do certificado (DER), em hexadecimal maiúsculo sem separadores.</summary>
    public static string Sha256(X509Certificate2 certificate)
    {
        using var sha = SHA256.Create();
        return ToHex(sha.ComputeHash(certificate.RawData));
    }

    /// <summary>Aceita maiúsculas ou minúsculas e separadores (<c>:</c>, espaço, <c>-</c>), como os navegadores mostram.</summary>
    public static string? Normalize(string? sha256)
    {
        if (string.IsNullOrWhiteSpace(sha256))
        {
            return null;
        }

        var hex = sha256!.Replace(":", string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty).ToUpperInvariant();
        if (hex.Length != 64 || !IsHex(hex))
        {
            throw new ArgumentException("A impressão digital precisa ser um SHA-256 em hexadecimal (64 caracteres).", nameof(sha256));
        }

        return hex;
    }

    private static bool IsHex(string value)
    {
        foreach (var c in value)
        {
            if (HexDigits.IndexOf(c) < 0)
            {
                return false;
            }
        }

        return true;
    }

    private static string ToHex(byte[] bytes)
    {
        var chars = new char[bytes.Length * 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            chars[i * 2] = HexDigits[bytes[i] >> 4];
            chars[(i * 2) + 1] = HexDigits[bytes[i] & 0xF];
        }

        return new string(chars);
    }
}
