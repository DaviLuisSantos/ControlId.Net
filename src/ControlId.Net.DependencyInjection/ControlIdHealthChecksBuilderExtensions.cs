using System;
using System.Collections.Generic;
using System.Linq;
using ControlId.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

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

        var options = BuildOptions(configure);

        return builder.Add(new HealthCheckRegistration(
            name,
            sp => new ControlIdHealthCheck(sp.GetRequiredService<IControlIdClientFactory>(), options),
            failureStatus,
            tags));
    }

    /// <summary>
    /// Um health check por equipamento, nomeado <c>{namePrefix}:access:{nome}</c> ou <c>{namePrefix}:rep:{nome}</c>
    /// (o nome vazio aparece como <c>default</c>), para cada equipamento registrado com <c>AddControlIdAccess*</c>/<c>AddControlIdRep*</c>,
    /// em qualquer ordem de registro. Circuito aberto: <paramref name="failureStatus"/> sem requisição. Senão sonda o equipamento:
    /// falha dá <paramref name="failureStatus"/>, lentidão ou <see cref="ControlIdHealthCheckOptions.MaxContactAge"/> vencido dá
    /// <see cref="HealthStatus.Degraded"/>. Os dados trazem série, firmware, deviceId, latência, circuito e último contato
    /// (este exige um <c>DeviceContactTracker</c> registrado; sem ele o check segue sem esses dados).
    /// </summary>
    /// <param name="builder">O builder de <c>AddHealthChecks()</c>.</param>
    /// <param name="configure">Prazo, limite de lentidão e idade máxima do último contato.</param>
    /// <param name="namePrefix">Prefixo dos nomes. Padrão: <c>controlid</c>.</param>
    /// <param name="failureStatus">Status de falha. Padrão: <see cref="HealthStatus.Unhealthy"/>.</param>
    /// <param name="tags">Tags para filtrar os health checks (ex.: <c>ready</c>).</param>
    public static IHealthChecksBuilder AddControlIdDeviceChecks(
        this IHealthChecksBuilder builder,
        Action<ControlIdHealthCheckOptions>? configure = null,
        string namePrefix = "controlid",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        if (namePrefix is null)
        {
            throw new ArgumentNullException(nameof(namePrefix));
        }

        var options = BuildOptions(configure);
        var tagList = tags?.ToArray();
        builder.Services.AddSingleton<IConfigureOptions<HealthCheckServiceOptions>>(sp =>
            new ControlIdDeviceChecksRegistration(sp.GetService<ControlIdRegistry>(), namePrefix, options, failureStatus, tagList));
        return builder;
    }

    private static ControlIdHealthCheckOptions BuildOptions(Action<ControlIdHealthCheckOptions>? configure)
    {
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

        if (options.MaxContactAge is { } age && age < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(configure), age, "A idade máxima do contato não pode ser negativa.");
        }

        return options;
    }
}
