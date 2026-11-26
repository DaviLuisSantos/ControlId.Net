using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ControlId;
using ControlId.DependencyInjection;
using ControlId.Internal;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Guardião de configuração: mantém os equipamentos como a aplicação espera.</summary>
public static class ControlIdConfigurationGuardExtensions
{
    /// <summary>
    /// Registra um serviço em segundo plano que, a cada <see cref="ControlIdConfigurationGuardOptions.Interval"/>, lê a
    /// configuração de cada equipamento de <see cref="ControlIdConfigurationGuardOptions.Devices"/> e grava só o que diverge
    /// (nenhum <c>set_configuration</c> se estiver tudo igual). Também acerta o relógio quando passa da tolerância.
    /// Um equipamento fora do ar não atrasa os demais: o erro dele fica no log e no health check, e o ciclo seguinte tenta de novo.
    /// </summary>
    /// <remarks>
    /// Cada correção gera um log de aviso e soma em <c>controlid.guard.corrections</c> (<see cref="ControlIdTelemetry.MeterName"/>).
    /// Chame uma vez por aplicação. O resultado do último ciclo aparece em <c>AddHealthChecks().AddControlIdConfigurationGuard()</c>.
    /// </remarks>
    /// <param name="services">A coleção de serviços (os equipamentos precisam estar registrados com <c>AddControlIdAccess*</c>).</param>
    /// <param name="configure">Intervalo, tolerância do relógio, fuso e configuração desejada por equipamento.</param>
    /// <exception cref="ArgumentOutOfRangeException">Intervalo não positivo ou tolerância negativa.</exception>
    /// <exception cref="InvalidOperationException">O guardião já foi registrado.</exception>
    public static IServiceCollection AddControlIdConfigurationGuard(
        this IServiceCollection services, Action<ControlIdConfigurationGuardOptions> configure)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        if (services.Any(d => d.ServiceType == typeof(ControlIdConfigurationGuardOptions)))
        {
            throw new InvalidOperationException("O guardião de configuração já foi registrado: chame AddControlIdConfigurationGuard uma vez só.");
        }

        var options = new ControlIdConfigurationGuardOptions();
        configure(options);
        if (options.Interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(configure), options.Interval, "O intervalo (Interval) precisa ser positivo.");
        }

        if (options.ClockTolerance is { } tolerance && tolerance < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(configure), tolerance, "A tolerância do relógio (ClockTolerance) não pode ser negativa.");
        }

        if (options.TimeZone is null)
        {
            throw new ArgumentException("Informe o fuso (TimeZone).", nameof(configure));
        }

        services.AddSingleton(options);
        services.TryAddSingleton<ControlIdConfigurationGuardState>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, ControlIdConfigurationGuardService>());
        return services;
    }

    /// <summary>
    /// Health check do guardião: saudável se o último ciclo conferiu todos os equipamentos, degradado se algum falhou ou se ainda
    /// não houve ciclo, e <paramref name="failureStatus"/> se todos falharam. Os dados trazem os ciclos, as correções e os erros.
    /// Exige <c>AddControlIdConfigurationGuard(IServiceCollection, ...)</c>.
    /// </summary>
    /// <param name="builder">O builder de <c>AddHealthChecks()</c>.</param>
    /// <param name="name">Nome do health check. Padrão: <c>controlid-guard</c>.</param>
    /// <param name="failureStatus">Status quando todos os equipamentos falharam no último ciclo. Padrão: <see cref="HealthStatus.Unhealthy"/>.</param>
    /// <param name="tags">Tags para filtrar o health check.</param>
    public static IHealthChecksBuilder AddControlIdConfigurationGuard(
        this IHealthChecksBuilder builder,
        string name = "controlid-guard",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        var allFailed = failureStatus ?? HealthStatus.Unhealthy;
        builder.Services.TryAddSingleton<ControlIdConfigurationGuardState>();
        return builder.Add(new HealthCheckRegistration(
            name,
            sp => new ControlIdConfigurationGuardHealthCheck(sp.GetRequiredService<ControlIdConfigurationGuardState>(), allFailed),
            failureStatus,
            tags));
    }
}

/// <summary>Resultado de um equipamento num ciclo.</summary>
internal sealed class GuardDeviceOutcome
{
    public GuardDeviceOutcome(string device, IReadOnlyList<string> corrections, Exception? error)
    {
        Device = device;
        Corrections = corrections;
        Error = error;
    }

    public string Device { get; }

    public IReadOnlyList<string> Corrections { get; }

    public Exception? Error { get; }
}

/// <summary>Estado do último ciclo, lido pelo health check.</summary>
internal sealed class ControlIdConfigurationGuardState
{
    private readonly object _gate = new();
    private IReadOnlyList<GuardDeviceOutcome> _last = Array.Empty<GuardDeviceOutcome>();
    private long _cycles;
    private long _corrections;
    private DateTimeOffset? _lastCycleAt;

    public (long Cycles, long Corrections, DateTimeOffset? LastCycleAt, IReadOnlyList<GuardDeviceOutcome> Last) Snapshot()
    {
        lock (_gate)
        {
            return (_cycles, _corrections, _lastCycleAt, _last);
        }
    }

    public void Record(IReadOnlyList<GuardDeviceOutcome> outcomes, DateTimeOffset at)
    {
        lock (_gate)
        {
            _last = outcomes;
            _cycles++;
            _corrections += outcomes.Sum(o => o.Corrections.Count);
            _lastCycleAt = at;
        }
    }
}

internal sealed class ControlIdConfigurationGuardHealthCheck : IHealthCheck
{
    private readonly ControlIdConfigurationGuardState _state;
    private readonly HealthStatus _allFailedStatus;

    public ControlIdConfigurationGuardHealthCheck(ControlIdConfigurationGuardState state, HealthStatus allFailedStatus)
    {
        _state = state;
        _allFailedStatus = allFailedStatus;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var (cycles, corrections, lastCycleAt, last) = _state.Snapshot();
        var data = new Dictionary<string, object>
        {
            ["cycles"] = cycles,
            ["corrections"] = corrections,
        };

        if (lastCycleAt is { } at)
        {
            data["lastCycleUtc"] = at.UtcDateTime;
        }

        if (cycles == 0)
        {
            return Task.FromResult(HealthCheckResult.Degraded("O guardião de configuração ainda não concluiu o primeiro ciclo.", data: data));
        }

        foreach (var outcome in last)
        {
            data["device:" + outcome.Device] = outcome.Error is null
                ? (outcome.Corrections.Count == 0 ? "ok" : "corrigido: " + string.Join(", ", outcome.Corrections))
                : "falhou: " + outcome.Error.Message;
        }

        var failed = last.Where(o => o.Error is not null).ToList();
        if (failed.Count == 0)
        {
            return Task.FromResult(HealthCheckResult.Healthy("Todos os equipamentos conferidos no último ciclo.", data));
        }

        var description = $"{failed.Count} de {last.Count} equipamentos falharam no último ciclo: {string.Join(", ", failed.Select(o => o.Device))}.";
        return Task.FromResult(failed.Count == last.Count
            ? new HealthCheckResult(_allFailedStatus, description, failed[0].Error, data)
            : HealthCheckResult.Degraded(description, failed[0].Error, data));
    }
}

/// <summary>O ciclo do guardião: conferir a configuração e o relógio de cada equipamento e corrigir o que divergir.</summary>
internal sealed class ControlIdConfigurationGuardService : BackgroundService
{
    private static readonly Meter GuardMeter = new(ControlIdTelemetry.MeterName);

    private static readonly Counter<long> CorrectionsCounter = GuardMeter.CreateCounter<long>(
        "controlid.guard.corrections", "{correction}",
        "Correções feitas pelo guardião de configuração (tag controlid.guard.field: o parâmetro ou o relógio).");

    private static readonly Action<ILogger, string, string, Exception?> LogCorrected = LoggerMessage.Define<string, string>(
        LogLevel.Warning, new EventId(3001, "GuardCorrected"), "Equipamento {Device}: {Field} divergia do desejado e foi corrigido.");

    private static readonly Action<ILogger, string, Exception?> LogFailed = LoggerMessage.Define<string>(
        LogLevel.Warning, new EventId(3002, "GuardDeviceFailed"), "Equipamento {Device}: o guardião não conseguiu conferir a configuração; tenta de novo no próximo ciclo.");

    private static readonly Action<ILogger, Exception?> LogCycleFailed = LoggerMessage.Define(
        LogLevel.Error, new EventId(3003, "GuardCycleFailed"), "O ciclo do guardião de configuração falhou; tenta de novo no próximo.");

    private readonly IControlIdClientFactory _factory;
    private readonly ControlIdConfigurationGuardOptions _options;
    private readonly ControlIdConfigurationGuardState _state;
    private readonly ILogger _logger;

    public ControlIdConfigurationGuardService(
        IControlIdClientFactory factory,
        ControlIdConfigurationGuardOptions options,
        ControlIdConfigurationGuardState state,
        ILoggerFactory? loggerFactory = null)
    {
        _factory = factory;
        _options = options;
        _state = state;
        _logger = loggerFactory?.CreateLogger(ControlIdTelemetry.LoggerCategory) ?? NullLogger.Instance;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Não segura a partida do host com o primeiro ciclo.
        await Task.Yield();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCycleAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogCycleFailed(_logger, ex);
            }

            try
            {
                await Task.Delay(_options.Interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    internal async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        // Cada equipamento é isolado: um erro (inclusive ao obter o cliente) fica só no resultado dele.
        var results = await ControlIdDevices.RunAsync(
            _options.Devices.Keys.ToList(),
            name => name,
            (name, ct) => CorrectAsync(name, ct),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var outcomes = results
            .Select(r => r.Succeeded ? r.Value : new GuardDeviceOutcome(r.Device, Array.Empty<string>(), r.Error))
            .ToList();
        foreach (var failed in outcomes.Where(o => o.Error is not null))
        {
            LogFailed(_logger, failed.Device, failed.Error);
        }

        _state.Record(outcomes, DateTimeOffset.UtcNow);
    }

    private async Task<GuardDeviceOutcome> CorrectAsync(string name, CancellationToken cancellationToken)
    {
        var client = _factory.GetAccessClient(name);
        var corrections = new List<string>();

        var wanted = BuildDesired(_options.Devices[name]);
        if (wanted.Count > 0)
        {
            var fields = new Dictionary<string, IEnumerable<string>>();
            foreach (var section in wanted)
            {
                fields[section.Key] = section.Value.Keys.ToList();
            }

            var current = await client.GetConfigurationAsync(fields, cancellationToken).ConfigureAwait(false);
            var diff = new Dictionary<string, Dictionary<string, string>>();
            var changed = new List<string>();
            foreach (var section in wanted)
            {
                if (!current.TryGetValue(section.Key, out var currentSection))
                {
                    continue;
                }

                foreach (var field in section.Value)
                {
                    // Parâmetro que o firmware não devolve não é conferido: gravá-lo faria o equipamento recusar tudo.
                    if (currentSection.TryGetValue(field.Key, out var value) && !string.Equals(value, field.Value, StringComparison.Ordinal))
                    {
                        if (!diff.TryGetValue(section.Key, out var values))
                        {
                            diff[section.Key] = values = new Dictionary<string, string>(StringComparer.Ordinal);
                        }

                        values[field.Key] = field.Value;
                        changed.Add(section.Key + "." + field.Key);
                    }
                }
            }

            if (diff.Count > 0)
            {
                await client.SetConfigurationAsync(diff, cancellationToken).ConfigureAwait(false);
                foreach (var field in changed)
                {
                    corrections.Add(field);
                    Corrected(name, field);
                }
            }
        }

        if (_options.ClockTolerance is { } tolerance)
        {
            var info = await client.GetSystemInformationAsync(cancellationToken).ConfigureAwait(false);
            if (info.Time > 0)
            {
                var deviceTime = DeviceLocalTime.FromEpoch(info.Time);
                var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, _options.TimeZone).DateTime;
                if ((now - deviceTime).Duration() > tolerance)
                {
                    await client.SetSystemTimeAsync(now, cancellationToken).ConfigureAwait(false);
                    corrections.Add("clock");
                    Corrected(name, "clock");
                }
            }
        }

        return new GuardDeviceOutcome(name, corrections, null);
    }

    private void Corrected(string device, string field)
    {
        LogCorrected(_logger, device, field, null);
        CorrectionsCounter.Add(
            1,
            new KeyValuePair<string, object?>("controlid.device", device),
            new KeyValuePair<string, object?>("controlid.guard.field", field));
    }

    // Seção -> parâmetro -> valor desejado (texto, como a API espera). Os campos tipados vencem os de Settings.
    internal static Dictionary<string, Dictionary<string, string>> BuildDesired(DesiredDeviceConfiguration desired)
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        void Set(string section, string field, string value)
        {
            if (!result.TryGetValue(section, out var values))
            {
                result[section] = values = new Dictionary<string, string>(StringComparer.Ordinal);
            }

            values[field] = value;
        }

        foreach (var section in desired.Settings)
        {
            if (section.Value is null)
            {
                continue;
            }

            foreach (var field in section.Value)
            {
                Set(section.Key, field.Key, field.Value);
            }
        }

        if (desired.Online is { } online)
        {
            Set("general", "online", online ? "1" : "0");
        }

        if (desired.OnlineServerId is { } serverId)
        {
            Set("online_client", "server_id", serverId.ToString(CultureInfo.InvariantCulture));
        }

        if (desired.Monitor is { } monitor)
        {
            if (monitor.Hostname is not null)
            {
                Set("monitor", "hostname", monitor.Hostname);
            }

            if (monitor.Port is { } port)
            {
                Set("monitor", "port", port.ToString(CultureInfo.InvariantCulture));
            }

            if (monitor.Path is not null)
            {
                Set("monitor", "path", monitor.Path.Trim('/'));
            }

            if (monitor.RequestTimeout is { } timeout)
            {
                Set("monitor", "request_timeout", ((long)timeout.TotalMilliseconds).ToString(CultureInfo.InvariantCulture));
            }
        }

        return result;
    }
}
