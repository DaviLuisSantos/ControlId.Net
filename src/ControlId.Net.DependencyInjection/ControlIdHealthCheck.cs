using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ControlId.DependencyInjection;

/// <summary>Opções do health check dos equipamentos (<c>AddControlIdDevices</c>).</summary>
public sealed class ControlIdHealthCheckOptions
{
    /// <summary>Prazo de cada equipamento para responder. Padrão: 5 segundos.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Acima deste tempo de resposta, o equipamento conta como lento (<see cref="HealthStatus.Degraded"/>). Padrão: 2 segundos.</summary>
    public TimeSpan SlowThreshold { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Equipamentos verificados ao mesmo tempo. Padrão: 8.</summary>
    public int MaxParallelism { get; set; } = ControlIdDevices.DefaultMaxParallelism;
}

/// <summary>
/// Verifica todos os equipamentos registrados (acesso e REP) em paralelo, com uma leitura que não altera nada
/// (<c>system_information</c> no acesso, <c>get_about</c> no REP).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Todos responderam no prazo e rápido: <see cref="HealthStatus.Healthy"/>.</item>
/// <item>Algum falhou ou passou de <see cref="ControlIdHealthCheckOptions.SlowThreshold"/>: <see cref="HealthStatus.Degraded"/>.</item>
/// <item>Todos falharam: o <c>failureStatus</c> do registro (padrão <see cref="HealthStatus.Unhealthy"/>).</item>
/// </list>
/// O resultado de cada equipamento vai em <see cref="HealthCheckResult.Data"/>, com a chave <c>access:{nome}</c> ou
/// <c>rep:{nome}</c> (o nome vazio do equipamento único aparece como <c>default</c>).
/// </remarks>
internal sealed class ControlIdHealthCheck : IHealthCheck
{
    private readonly IControlIdClientFactory _devices;
    private readonly ControlIdHealthCheckOptions _options;

    public ControlIdHealthCheck(IControlIdClientFactory devices, ControlIdHealthCheckOptions options)
    {
        _devices = devices;
        _options = options;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var access = _devices.RunOnAccessDevicesAsync(
            (device, ct) => WithTimeoutAsync(token => device.GetSystemInformationAsync(token), ct), _options.MaxParallelism, cancellationToken);
        var rep = _devices.RunOnRepDevicesAsync(
            (device, ct) => WithTimeoutAsync(token => device.GetAboutAsync(token), ct), _options.MaxParallelism, cancellationToken);

        var results = (await access.ConfigureAwait(false)).Select(r => ("access", (DeviceResult)r))
            .Concat((await rep.ConfigureAwait(false)).Select(r => ("rep", (DeviceResult)r)))
            .ToList();

        if (results.Count == 0)
        {
            return HealthCheckResult.Healthy("Nenhum equipamento registrado.");
        }

        var data = new Dictionary<string, object>(StringComparer.Ordinal);
        var failed = 0;
        var slow = 0;
        foreach (var (kind, result) in results)
        {
            var key = kind + ":" + (result.Device.Length == 0 ? "default" : result.Device);
            var ms = ((long)result.Elapsed.TotalMilliseconds).ToString(CultureInfo.InvariantCulture);
            if (!result.Succeeded)
            {
                failed++;
                data[key] = $"falhou em {ms} ms: {result.Error!.Message}";
            }
            else if (result.Elapsed > _options.SlowThreshold)
            {
                slow++;
                data[key] = $"lento: {ms} ms";
            }
            else
            {
                data[key] = $"ok: {ms} ms";
            }
        }

        var summary = $"{results.Count - failed} de {results.Count} equipamentos respondendo" + (slow > 0 ? $", {slow} lentos" : string.Empty) + ".";
        if (failed == results.Count)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, summary, data: data);
        }

        return failed > 0 || slow > 0
            ? HealthCheckResult.Degraded(summary, data: data)
            : HealthCheckResult.Healthy(summary, data);
    }

    // O prazo vale por equipamento: um que não responde vira falha só dele, sem cancelar a verificação dos outros.
    private async Task<T> WithTimeoutAsync<T>(Func<CancellationToken, Task<T>> probe, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.Timeout);
        try
        {
            return await probe(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Sem resposta em {_options.Timeout.TotalSeconds.ToString(CultureInfo.InvariantCulture)} s.");
        }
    }
}
