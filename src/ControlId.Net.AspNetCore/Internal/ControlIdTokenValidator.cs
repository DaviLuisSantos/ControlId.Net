using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

[assembly: InternalsVisibleTo("ControlId.Net.Tests")]

namespace ControlId.AspNetCore.Internal;

/// <summary>Valida o token Bearer dos callbacks do equipamento.</summary>
internal static class ControlIdTokenValidator
{
    private const string Scheme = "Bearer";

    /// <summary>Opções de token do endpoint, ou <see langword="null"/> quando a rota não exige token.</summary>
    public static ControlIdTokenOptions? GetOptions(HttpContext context)
        => context.GetEndpoint()?.Metadata.GetMetadata<ControlIdTokenOptions>();

    /// <summary>Verifica o cabeçalho; sem token configurado para o equipamento a resposta é negativa.</summary>
    public static bool IsValid(HttpContext context, ControlIdTokenOptions options, long? deviceId)
    {
        var expected = deviceId is { } id && options.DeviceToken is { } byDevice ? byDevice(id) : null;
        if (string.IsNullOrEmpty(expected))
        {
            expected = options.Token;
        }

        if (string.IsNullOrEmpty(expected))
        {
            return false;
        }

        var header = context.Request.Headers[HeaderNames.Authorization].ToString();
        var prefix = Scheme + " ";
        if (!header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var supplied = header.Substring(prefix.Length).Trim();
        if (supplied.Length == 0)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(supplied)),
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)));
    }

    /// <summary>
    /// Autoriza a requisição. Sem metadado no endpoint, libera. Falhando, deixa 401 com
    /// <c>WWW-Authenticate: Bearer</c> na resposta e devolve <see langword="false"/>.
    /// </summary>
    public static bool Authorize(HttpContext context, long? deviceId)
    {
        var options = GetOptions(context);
        if (options is null || IsValid(context, options, deviceId))
        {
            return true;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers[HeaderNames.WWWAuthenticate] = Scheme;
        return false;
    }
}
