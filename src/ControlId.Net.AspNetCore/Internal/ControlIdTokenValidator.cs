using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

[assembly: InternalsVisibleTo("ControlId.Net.Tests")]

namespace ControlId.AspNetCore.Internal;

/// <summary>Como a rota de callback é protegida.</summary>
internal enum CallbackSecurity
{
    /// <summary>Sem token nem liberação explícita: 401 (padrão).</summary>
    Closed,

    /// <summary><c>AllowAnonymousControlIdCallbacks()</c> sem token.</summary>
    Anonymous,

    /// <summary>Token único (com ou sem o anterior): não depende do <c>deviceId</c>, autoriza antes de ler o corpo.</summary>
    SharedToken,

    /// <summary>Token por equipamento: depende do <c>deviceId</c>, que pode vir do corpo.</summary>
    DeviceToken,
}

/// <summary>Metadado de <c>AllowAnonymousControlIdCallbacks()</c>.</summary>
internal sealed class AllowAnonymousControlIdCallbacksMetadata
{
    public static readonly AllowAnonymousControlIdCallbacksMetadata Instance = new();

    private AllowAnonymousControlIdCallbacksMetadata()
    {
    }
}

/// <summary>Estado por rota de callback (uma instância por endpoint, adicionada pelo <c>MapControlId*</c>).</summary>
internal sealed class CallbackRouteState
{
    private int _closedWarned;

    /// <summary><see langword="true"/> só na primeira chamada: o aviso 2006 sai uma vez por rota.</summary>
    public bool TryMarkClosedWarned() => Interlocked.Exchange(ref _closedWarned, 1) == 0;
}

/// <summary>Valida o token Bearer dos callbacks do equipamento.</summary>
internal static class ControlIdTokenValidator
{
    private const string Scheme = "Bearer";

    /// <summary>Opções de token do endpoint, ou <see langword="null"/> quando a rota não exige token.</summary>
    public static ControlIdTokenOptions? GetOptions(HttpContext context)
        => context.GetEndpoint()?.Metadata.GetMetadata<ControlIdTokenOptions>();

    /// <summary>Proteção da rota: o token vence a liberação anônima; sem nenhum dos dois, fechada.</summary>
    public static CallbackSecurity GetSecurity(HttpContext context, out ControlIdTokenOptions? options)
    {
        options = GetOptions(context);
        if (options is not null)
        {
            return options.DeviceToken is null ? CallbackSecurity.SharedToken : CallbackSecurity.DeviceToken;
        }

        return context.GetEndpoint()?.Metadata.GetMetadata<AllowAnonymousControlIdCallbacksMetadata>() is null
            ? CallbackSecurity.Closed
            : CallbackSecurity.Anonymous;
    }

    /// <summary>
    /// Verifica o cabeçalho. Sem <c>Bearer</c>, recusa sem chamar <see cref="ControlIdTokenOptions.DeviceToken"/>. Com token por
    /// equipamento, só ele vale; o token único (e o anterior) só entra sem <see cref="ControlIdTokenOptions.DeviceToken"/> ou com
    /// <see cref="ControlIdTokenOptions.AllowTokenFallback"/>.
    /// </summary>
    public static bool IsValid(HttpContext context, ControlIdTokenOptions options, long? deviceId)
    {
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

        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        if (options.DeviceToken is { } byDevice)
        {
            if (deviceId is { } id && byDevice(id) is { Length: > 0 } deviceToken)
            {
                return Matches(suppliedHash, deviceToken);
            }

            if (!options.AllowTokenFallback)
            {
                return false;
            }
        }

        // Sem curto-circuito: as duas comparações sempre rodam.
        return Matches(suppliedHash, options.Token) | Matches(suppliedHash, options.PreviousToken);
    }

    /// <summary>
    /// Autoriza a requisição conforme <see cref="GetSecurity"/>. Falhando (rota fechada ou token inválido), deixa 401 com
    /// <c>WWW-Authenticate: Bearer</c> na resposta e devolve <see langword="false"/>.
    /// </summary>
    public static bool Authorize(HttpContext context, long? deviceId)
    {
        var security = GetSecurity(context, out var options);
        if (security == CallbackSecurity.Anonymous || (options is not null && IsValid(context, options, deviceId)))
        {
            return true;
        }

        Challenge(context);
        return false;
    }

    /// <summary>401 com <c>WWW-Authenticate: Bearer</c>.</summary>
    public static void Challenge(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers[HeaderNames.WWWAuthenticate] = Scheme;
    }

    private static bool Matches(byte[] suppliedHash, string? expected) =>
        !string.IsNullOrEmpty(expected)
        && CryptographicOperations.FixedTimeEquals(suppliedHash, SHA256.HashData(Encoding.UTF8.GetBytes(expected)));
}
