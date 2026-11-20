using System;
using ControlId.AspNetCore.Internal;
using Microsoft.AspNetCore.Builder;

namespace ControlId.AspNetCore;

/// <summary>
/// Segurança das rotas mapeadas pelo pacote (<c>MapControlIdPush</c>, <c>MapControlIdMonitor</c>, <c>MapControlIdOnline</c>).
/// Os callbacks são fechados por padrão: sem <c>RequireControlIdToken</c> nem <see cref="AllowAnonymousControlIdCallbacks"/>,
/// a resposta é 401.
/// </summary>
public static class ControlIdEndpointConventionBuilderExtensions
{
    /// <summary>Exige <c>Authorization: Bearer</c> com o token informado nos callbacks do grupo ou da rota.</summary>
    /// <param name="builder">Grupo ou rota.</param>
    /// <param name="token">Token esperado; não pode ser vazio.</param>
    public static TBuilder RequireControlIdToken<TBuilder>(this TBuilder builder, string token)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(token);
        return builder.RequireControlIdToken(o => o.Token = token);
    }

    /// <summary>
    /// Exige <c>Authorization: Bearer</c> nos callbacks do grupo ou da rota, com token único, token anterior (rotação)
    /// e/ou token por equipamento.
    /// </summary>
    /// <param name="builder">Grupo ou rota.</param>
    /// <param name="configure">Configura <see cref="ControlIdTokenOptions"/>.</param>
    public static TBuilder RequireControlIdToken<TBuilder>(this TBuilder builder, Action<ControlIdTokenOptions> configure)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new ControlIdTokenOptions();
        configure(options);
        builder.Add(endpoint => endpoint.Metadata.Add(options));
        return builder;
    }

    /// <summary>
    /// Libera os callbacks do grupo ou da rota sem token. Use só em rede isolada ou atrás de outra autenticação (proxy,
    /// filtro de IP): sem token, qualquer host que alcance a aplicação drena a fila do Push, forja resultados e injeta
    /// notificações. Se o grupo também tiver <c>RequireControlIdToken</c>, vale o token.
    /// </summary>
    /// <param name="builder">Grupo ou rota.</param>
    public static TBuilder AllowAnonymousControlIdCallbacks<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Add(endpoint => endpoint.Metadata.Add(AllowAnonymousControlIdCallbacksMetadata.Instance));
        return builder;
    }
}
