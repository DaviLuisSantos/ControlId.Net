using System;
using System.Collections.Generic;
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
        var access = _registry?.AccessDevices.ToArray() ?? Array.Empty<string>();
        var rep = _registry?.RepDevices.ToArray() ?? Array.Empty<string>();
        if (access.Length == 0 && rep.Length == 0)
        {
            if (_options.RequireDevices)
            {
                options.Registrations.Add(new HealthCheckRegistration(
                    _prefix + ":none", _ => new ControlIdNoDevicesHealthCheck(), _failureStatus, _tags));
            }

            return;
        }

        foreach (var name in access)
        {
            Add(options, ControlIdProbe.AccessKind, name);
        }

        foreach (var name in rep)
        {
            Add(options, ControlIdProbe.RepKind, name);
        }
    }

    private void Add(HealthCheckServiceOptions options, string kind, string device)
    {
        var checkName = $"{_prefix}:{kind}:{(device.Length == 0 ? "default" : device)}";
        var isAccess = kind == ControlIdProbe.AccessKind;
        options.Registrations.Add(new HealthCheckRegistration(
            checkName,
            sp => new ControlIdDeviceHealthCheck(
                sp.GetRequiredService<IControlIdClientFactory>(),
                isAccess,
                device,
                _options,
                sp.GetService<DeviceContactTracker>(),
                sp.GetRequiredService<ControlIdProbeCache>()),
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
    private readonly ControlIdProbeCache _cache;

    public ControlIdDeviceHealthCheck(
        IControlIdClientFactory devices,
        bool isAccess,
        string device,
        ControlIdHealthCheckOptions options,
        DeviceContactTracker? tracker,
        ControlIdProbeCache cache)
    {
        _devices = devices;
        _isAccess = isAccess;
        _device = device;
        _options = options;
        _tracker = tracker;
        _cache = cache;
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

        // Com CacheDuration, a sonda é compartilhada com o check agregado e os outros checks deste equipamento.
        var (probe, shared) = await _cache.GetAsync(
            ControlIdProbe.Key(_isAccess ? ControlIdProbe.AccessKind : ControlIdProbe.RepKind, _device),
            _options.CacheDuration,
            ct => ControlIdProbe.RunAsync(client, _options.Timeout, ct),
            cancellationToken).ConfigureAwait(false);

        var elapsed = (long)probe.Elapsed.TotalMilliseconds;
        data["latencyMs"] = elapsed;
        data["circuit"] = CurrentCircuit(client).ToString();
        if (shared)
        {
            data["cached"] = true;
        }

        if (!probe.Succeeded)
        {
            return new HealthCheckResult(failure, $"Falhou em {elapsed} ms: {probe.Error!.Message}", probe.Error, data);
        }

        string? deviceId = null;
        if (probe.Access is { } info)
        {
            data["serial"] = info.Serial ?? string.Empty;
            data["firmware"] = info.Version ?? string.Empty;
            data["deviceId"] = info.DeviceId ?? string.Empty;
            deviceId = info.DeviceId;
        }
        else if (probe.Rep is { } about)
        {
            data["serial"] = about.SerialNumber ?? string.Empty;
            data["firmware"] = about.FirmwareVersion.ToString(CultureInfo.InvariantCulture);
        }

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

        if (probe.Elapsed > _options.SlowThreshold)
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

}
