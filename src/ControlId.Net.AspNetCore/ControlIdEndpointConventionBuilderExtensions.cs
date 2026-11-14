using System;
using Microsoft.AspNetCore.Builder;

namespace ControlId.AspNetCore;

/// <summary>Exige o token Bearer do equipamento nas rotas mapeadas pelo pacote.</summary>
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

    /// <summary>Exige <c>Authorization: Bearer</c> nos callbacks do grupo ou da rota, com token único e/ou por equipamento.</summary>
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
}
