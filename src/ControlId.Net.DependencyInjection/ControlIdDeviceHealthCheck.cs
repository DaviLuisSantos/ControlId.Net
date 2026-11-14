using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ControlId.Access;
using ControlId.Rep;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace ControlId.DependencyInjection;

/// <summary>Registra um check por equipamento quando o <c>HealthCheckServiceOptions</c> é resolvido (depois de todos os registros).</summary>
internal sealed class ControlIdDeviceChecksRegistration : IConfigureOptions<HealthCheckServiceOptions>
{
    private readonly ControlIdRegistry? _registry;
    private readonly string _prefix;
    private readonly ControlIdHealthCheckOptions _options;
    private readonly HealthStatus? _failureStatus;
    private readonly string[]? _tags;

    public ControlIdDeviceChecksRegistration(
        ControlIdRegistry? registry,
        string prefix,
        ControlIdHealthCheckOptions options,
        HealthStatus? failureStatus,
        string[]? tags)
    {
        _registry = registry;
        _prefix = prefix;
        _options = options;
        _failureStatus = failureStatus;
        _tags = tags;
    }

    public void Configure(HealthCheckServiceOptions options)
    {
        if (_registry is null)
        {
            return;
        }

        foreach (var name in _registry.AccessDevices.ToArray())
        {
            Add(options, "access", name);
        }

        foreach (var name in _registry.RepDevices.ToArray())
        {
            Add(options, "rep", name);
        }
    }

    private void Add(HealthCheckServiceOptions options, string kind, string device)
    {
        var checkName = $"{_prefix}:{kind}:{(device.Length == 0 ? "default" : device)}";
        var isAccess = kind == "access";
        options.Registrations.Add(new HealthCheckRegistration(
            checkName,
            sp => new ControlIdDeviceHealthCheck(
                sp.GetRequiredService<IControlIdClientFactory>(), isAccess, device, _options, sp.GetService<DeviceContactTracker>()),
            _failureStatus,
            _tags));
    }
}

/// <summary>
/// Health check de um equipamento (acesso ou REP). Circuito aberto: falha sem requisição; senão sonda com uma leitura que
/// não altera nada (<c>system_information</c> / <c>get_about</c>).
/// </summary>
internal sealed class ControlIdDeviceHealthCheck : IHealthCheck
{
    private readonly IControlIdClientFactory _devices;
    private readonly bool _isAccess;
    private readonly string _device;
    private readonly ControlIdHealthCheckOptions _options;
    private readonly DeviceContactTracker? _tracker;

    public ControlIdDeviceHealthCheck(
        IControlIdClientFactory devices,
        bool isAccess,
        string device,
        ControlIdHealthCheckOptions options,
        DeviceContactTracker? tracker)
    {
        _devices = devices;
        _isAccess = isAccess;
        _device = device;
        _options = options;
        _tracker = tracker;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var failure = context.Registration.FailureStatus;
        var data = new Dictionary<string, object>(StringComparer.Ordinal);

        object client;
        try
        {
            client = _isAccess ? _devices.GetAccessClient(_device) : _devices.GetRepClient(_device);
        }
        catch (InvalidOperationException ex)
        {
            return new HealthCheckResult(failure, ex.Message, ex, data);
        }

        var circuit = CurrentCircuit(client);
        data["circuit"] = circuit.ToString();
        if (circuit == ControlIdCircuitState.Open)
        {
            return new HealthCheckResult(failure, "Circuito aberto: equipamento sem resposta, sem nova requisição.", data: data);
        }

        var started = Stopwatch.GetTimestamp();
        string? deviceId = null;
        try
        {
            if (_isAccess)
            {
                var info = await WithTimeoutAsync(ct => ((IAccessClient)client).GetSystemInformationAsync(ct), cancellationToken).ConfigureAwait(false);
                data["serial"] = info.Serial ?? string.Empty;
                data["firmware"] = info.Version ?? string.Empty;
                data["deviceId"] = info.DeviceId ?? string.Empty;
                deviceId = info.DeviceId;
            }
            else
            {
                var about = await WithTimeoutAsync(ct => ((IRepClient)client).GetAboutAsync(ct), cancellationToken).ConfigureAwait(false);
                data["serial"] = about.SerialNumber ?? string.Empty;
                data["firmware"] = about.FirmwareVersion.ToString(CultureInfo.InvariantCulture);
            }
        }
        catch (Exception ex) when (!(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            var failedMs = ElapsedMs(started);
            data["latencyMs"] = failedMs;
            data["circuit"] = CurrentCircuit(client).ToString();
            return new HealthCheckResult(failure, $"Falhou em {failedMs} ms: {ex.Message}", ex, data);
        }

        var elapsed = ElapsedMs(started);
        data["latencyMs"] = elapsed;
        data["circuit"] = CurrentCircuit(client).ToString();

        var contactStale = false;
        if (_isAccess && _tracker is not null && long.TryParse(deviceId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
        {
            var contact = _tracker.GetLastContact(id);
            if (contact is not null)
            {
                data["lastContact"] = contact.At.ToString("O", CultureInfo.InvariantCulture);
                data["lastContactSource"] = contact.Source.ToString();
                contactStale = _options.MaxContactAge is { } max && DateTimeOffset.UtcNow - contact.At > max;
            }
        }

        if (elapsed > (long)_options.SlowThreshold.TotalMilliseconds)
        {
            return HealthCheckResult.Degraded($"Lento: {elapsed} ms.", data: data);
        }

        return contactStale
            ? HealthCheckResult.Degraded("Último contato além do limite.", data: data)
            : HealthCheckResult.Healthy($"Ok: {elapsed} ms.", data);
    }

    private static ControlIdCircuitState CurrentCircuit(object client) => client switch
    {
        AccessClient a => a.CircuitState,
        RepClient r => r.CircuitState,
        _ => ControlIdCircuitState.Closed,
    };

    private static long ElapsedMs(long started) =>
        (long)((Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency);

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
