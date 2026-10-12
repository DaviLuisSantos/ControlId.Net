using System;
using System.Net.Http;

namespace ControlId.Internal;

/// <summary>Ajustes comuns aos <see cref="HttpClientHandler"/> criados para os equipamentos.</summary>
internal static class HttpHandlers
{
    /// <summary>
    /// Alinha o limite de conexões do handler ao de requisições simultâneas. Sem isso, no .NET Framework o padrão de
    /// 2 conexões por servidor seguraria as requisições abaixo de <c>MaxConcurrentRequests</c>.
    /// </summary>
    public static void LimitConnections(HttpClientHandler handler, int maxConnections)
    {
        try
        {
            handler.MaxConnectionsPerServer = maxConnections;
        }
        catch (PlatformNotSupportedException)
        {
            // Plataforma sem suporte: fica o padrão do runtime, e o limite do transporte continua valendo.
        }
    }
}
