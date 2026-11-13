using System;
using System.Collections.Generic;
using ControlId.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Health check dos equipamentos Control iD.</summary>
public static class ControlIdHealthChecksBuilderExtensions
{
    /// <summary>
    /// Verifica todos os equipamentos registrados com <c>AddControlIdAccess*</c> e <c>AddControlIdRep*</c>:
    /// <code>
    /// builder.Services.AddHealthChecks().AddControlIdDevices();
    /// app.MapHealthChecks("/health");
    /// </code>
    /// Saudável se todos respondem; degradado se algum falha ou está lento; <paramref name="failureStatus"/> se nenhum
    /// responde. O resultado de cada equipamento vai nos dados do health check.
    /// </summary>
    /// <param name="builder">O builder de <c>AddHealthChecks()</c>.</param>
    /// <param name="configure">Prazo, limite de lentidão e paralelismo.</param>
    /// <param name="name">Nome do health check. Padrão: <c>controlid</c>.</param>
    /// <param name="failureStatus">Status quando nenhum equipamento responde. Padrão: <see cref="HealthStatus.Unhealthy"/>.</param>
    /// <param name="tags">Tags para filtrar o health check (ex.: <c>ready</c>).</param>
    public static IHealthChecksBuilder AddControlIdDevices(
        this IHealthChecksBuilder builder,
        Action<ControlIdHealthCheckOptions>? configure = null,
        string name = "controlid",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        var options = new ControlIdHealthCheckOptions();
        configure?.Invoke(options);
        if (options.Timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(configure), options.Timeout, "O prazo precisa ser positivo.");
        }

        if (options.MaxParallelism < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(configure), options.MaxParallelism, "O paralelismo precisa ser pelo menos 1.");
        }

        return builder.Add(new HealthCheckRegistration(
            name,
            sp => new ControlIdHealthCheck(sp.GetRequiredService<IControlIdClientFactory>(), options),
            failureStatus,
            tags));
    }
}
