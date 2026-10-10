using System;

namespace ControlId.Internal;

/// <summary>Normaliza endpoints e query strings recebidos nos métodos genéricos (<c>SendAsync</c>...).</summary>
internal static class EndpointPath
{
    /// <summary><c>"load_objects"</c>, <c>"/load_objects.fcgi"</c> → <c>"load_objects.fcgi"</c>.</summary>
    public static string Normalize(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new ArgumentException("Informe o endpoint, ex.: \"message_to_screen\".", nameof(endpoint));
        }

        var path = endpoint.Trim().TrimStart('/');
        if (path.IndexOf('?') >= 0)
        {
            throw new ArgumentException("Passe os parâmetros de query em queryString, não no endpoint.", nameof(endpoint));
        }

        if (path.IndexOf('/') >= 0 || path.IndexOf(':') >= 0)
        {
            throw new ArgumentException("Informe só o nome do endpoint, sem URL nem pastas.", nameof(endpoint));
        }

        return path.EndsWith(".fcgi", StringComparison.OrdinalIgnoreCase) ? path : path + ".fcgi";
    }

    /// <summary>Remove <c>?</c>/<c>&amp;</c> iniciais; vazio vira <c>null</c>. Os valores devem vir já escapados.</summary>
    public static string? NormalizeQuery(string? queryString)
    {
        var query = queryString?.Trim().TrimStart('?', '&');
        return string.IsNullOrEmpty(query) ? null : query;
    }
}
