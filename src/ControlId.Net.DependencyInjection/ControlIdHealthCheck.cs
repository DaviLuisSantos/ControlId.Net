using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ControlId.DependencyInjection;

/// <summary>Opções do health check dos equipamentos (<c>AddControlIdDevices</c> e <c>AddControlIdDeviceChecks</c>).</summary>
public sealed class ControlIdHealthCheckOptions
{
    /// <summary>Prazo de cada equipamento para responder. Padrão: 5 segundos. Precisa ser positivo.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Acima deste tempo de resposta, o equipamento conta como lento (<see cref="HealthStatus.Degraded"/>). Padrão: 2 segundos.
    /// Não pode ser negativo; zero faz toda resposta contar como lenta.
    /// </summary>
    public TimeSpan SlowThreshold { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Equipamentos verificados ao mesmo tempo no check agregado. Padrão: 8.</summary>
    public int MaxParallelism { get; set; } = ControlIdDevices.DefaultMaxParallelism;

    /// <summary>
    /// Idade máxima do último contato (Push, Monitor ou Online) de um equipamento de acesso; além dela, o check por equipamento
    /// (<c>AddControlIdDeviceChecks</c>) fica <see cref="HealthStatus.Degraded"/>. Exige um <c>DeviceContactTracker</c> registrado
    /// e vale só para equipamentos que já tiveram contato. Padrão: <c>null</c> (não verifica).
    /// </summary>
    public TimeSpan? MaxContactAge { get; set; }

    /// <summary>
    /// Exige pelo menos um equipamento registrado. Padrão: <c>true</c>: sem nenhum <c>AddControlIdAccess*</c>/<c>AddControlIdRep*</c>
    /// (ex.: seção de configuração com o nome errado), o check agregado devolve o <c>failureStatus</c> do registro, e
    /// <c>AddControlIdDeviceChecks</c> registra um check <c>{namePrefix}:none</c> com esse status. Com <c>false</c>, zero
    /// equipamentos é <see cref="HealthStatus.Healthy"/>.
    /// </summary>
    public bool RequireDevices { get; set; } = true;

    /// <summary>
    /// Por quanto tempo o resultado da sonda de cada equipamento é reaproveitado. Padrão: <see cref="TimeSpan.Zero"/> (sem cache:
    /// toda verificação consulta o equipamento). O cache é compartilhado entre o check agregado e os por equipamento: com os
    /// dois registrados, uma verificação faz uma requisição por equipamento. Também evita sondar a cada chamada de um
    /// <c>/health</c> consultado com frequência. Não pode ser negativo.
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Prazo total do check agregado (<c>AddControlIdDevices</c>): ao vencer, os equipamentos que ainda não responderam contam
    /// como falha e o resultado sai com o que já se sabe. Padrão: 30 segundos. Precisa ser positivo.
    /// </summary>
    public TimeSpan TotalTimeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Verifica todos os equipamentos registrados (acesso e REP) em paralelo, com uma leitura que não altera nada
/// (<c>system_information</c> no acesso, <c>get_about</c> no REP).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Todos responderam no prazo e rápido: <see cref="HealthStatus.Healthy"/>.</item>
/// <item>Algum falhou ou passou de <see cref="ControlIdHealthCheckOptions.SlowThreshold"/>: <see cref="HealthStatus.Degraded"/>.</item>
/// <item>Todos falharam, ou nenhum registrado com <see cref="ControlIdHealthCheckOptions.RequireDevices"/>: o <c>failureStatus</c>
/// do registro (padrão <see cref="HealthStatus.Unhealthy"/>).</item>
/// </list>
/// O resultado de cada equipamento vai em <see cref="HealthCheckResult.Data"/>, com a chave <c>access:{nome}</c> ou
/// <c>rep:{nome}</c> (o nome vazio do equipamento único aparece como <c>default</c>).
/// </remarks>
internal sealed class ControlIdHealthCheck : IHealthCheck
{
    internal const string NoDevicesMessage =
        "Nenhum equipamento Control iD registrado. Registre com AddControlIdAccess*/AddControlIdRep* (confira o nome da seção de " +
        "configuração) ou desligue ControlIdHealthCheckOptions.RequireDevices.";

    // Null quando nenhum AddControlIdAccess*/AddControlIdRep* foi chamado (a fábrica nem é registrada).
    private readonly IControlIdClientFactory? _devices;
    private readonly ControlIdHealthCheckOptions _options;
    private readonly ControlIdProbeCache _cache;

    public ControlIdHealthCheck(IControlIdClientFactory? devices, ControlIdHealthCheckOptions options, ControlIdProbeCache cache)
    {
        _devices = devices;
        _options = options;
        _cache = cache;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var devices = _devices;
        var targets = devices is null
            ? new List<(string Kind, string Device)>()
            : devices.AccessDeviceNames.Select(n => (Kind: ControlIdProbe.AccessKind, Device: n))
                .Concat(devices.RepDeviceNames.Select(n => (Kind: ControlIdProbe.RepKind, Device: n)))
                .ToList();

        if (devices is null || targets.Count == 0)
        {
            return _options.RequireDevices
                ? new HealthCheckResult(context.Registration.FailureStatus, NoDevicesMessage)
                : HealthCheckResult.Healthy("Nenhum equipamento registrado.");
        }

        (ControlIdProbeResult Result, bool Shared)[] results;
        using (var total = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        using (var slots = new SemaphoreSlim(_options.MaxParallelism, _options.MaxParallelism))
        {
            total.CancelAfter(_options.TotalTimeout);
            results = await Task.WhenAll(targets.Select(t => CheckOneAsync(devices, t.Kind, t.Device, slots, total.Token, cancellationToken)))
                .ConfigureAwait(false);
        }

        var data = new Dictionary<string, object>(StringComparer.Ordinal);
        var failed = 0;
        var slow = 0;
        for (var i = 0; i < targets.Count; i++)
        {
            var (kind, device) = targets[i];
            var (result, shared) = results[i];
            var key = kind + ":" + (device.Length == 0 ? "default" : device);
            var ms = ((long)result.Elapsed.TotalMilliseconds).ToString(CultureInfo.InvariantCulture);
            var suffix = shared ? " (cache)" : string.Empty;
            if (!result.Succeeded)
            {
                failed++;
                data[key] = $"falhou em {ms} ms: {result.Error!.Message}{suffix}";
            }
            else if (result.Elapsed > _options.SlowThreshold)
            {
                slow++;
                data[key] = $"lento: {ms} ms{suffix}";
            }
            else
            {
                data[key] = $"ok: {ms} ms{suffix}";
            }
        }

        var summary = $"{targets.Count - failed} de {targets.Count} equipamentos respondendo" + (slow > 0 ? $", {slow} lentos" : string.Empty) + ".";
        if (failed == targets.Count)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, summary, data: data);
        }

        return failed > 0 || slow > 0
            ? HealthCheckResult.Degraded(summary, data: data)
            : HealthCheckResult.Healthy(summary, data);
    }

    // Um equipamento: espera a vez (MaxParallelism), obtém o cliente e sonda (ou reaproveita a sonda do cache).
    // Falha ao obter o cliente e o prazo total vencido ficam só no resultado dele.
    private async Task<(ControlIdProbeResult Result, bool Shared)> CheckOneAsync(
        IControlIdClientFactory devices,
        string kind,
        string device,
        SemaphoreSlim slots,
        CancellationToken total,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var acquired = false;
        try
        {
            await slots.WaitAsync(total).ConfigureAwait(false);
            acquired = true;
            stopwatch.Restart();
            object client = kind == ControlIdProbe.AccessKind ? devices.GetAccessClient(device) : devices.GetRepClient(device);
            return await _cache.GetAsync(
                ControlIdProbe.Key(kind, device),
                _options.CacheDuration,
                ct => ControlIdProbe.RunAsync(client, _options.Timeout, ct),
                total).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (total.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            var seconds = _options.TotalTimeout.TotalSeconds.ToString(CultureInfo.InvariantCulture);
            return (new ControlIdProbeResult(stopwatch.Elapsed, new TimeoutException($"Prazo total de {seconds} s do health check esgotado.")), false);
        }
        catch (Exception ex) when (!(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            return (new ControlIdProbeResult(stopwatch.Elapsed, ex), false);
        }
        finally
        {
            if (acquired)
            {
                slots.Release();
            }
        }
    }
}

/// <summary>Check registrado por <c>AddControlIdDeviceChecks</c> quando não há equipamentos e <see cref="ControlIdHealthCheckOptions.RequireDevices"/> está ligado.</summary>
internal sealed class ControlIdNoDevicesHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new HealthCheckResult(context.Registration.FailureStatus, ControlIdHealthCheck.NoDevicesMessage));
}
