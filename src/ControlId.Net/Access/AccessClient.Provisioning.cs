using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ControlId.Access;

public sealed partial class AccessClient
{
    private static readonly string[] ProvisioningProModes = { "10k", "15k", "30k", "20k", "50k", "100k" };

    private static readonly string[] ProvisioningReservedQueryNames = { "command", "device_id", "address", "login", "password", "session" };

    /// <inheritdoc/>
    public Task UpgradeFingerprintCapacityAsync(string password, CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync(
            "upgrade_ten_thousand_templates.fcgi", new Dictionary<string, object> { ["password"] = Required(password, nameof(password)) }, cancellationToken);

    /// <inheritdoc/>
    public Task SetProOperationModeAndRebootAsync(string mode, CancellationToken cancellationToken = default)
    {
        if (Array.IndexOf(ProvisioningProModes, Required(mode, nameof(mode))) < 0)
        {
            throw new ArgumentException("Modo Pro inválido: use 10k, 15k, 30k (iDFace) ou 20k, 50k, 100k (iDFace Max).", nameof(mode));
        }

        return _transport.PostRawAsync(
            "set_pro_operation_mode_and_reboot.fcgi", new Dictionary<string, object> { ["pro_operation_mode"] = mode }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<string> RedirectRequestAsync(
        RedirectTarget target,
        string command,
        object? body = null,
        IReadOnlyDictionary<string, string>? query = null,
        CancellationToken cancellationToken = default)
    {
        if (target is null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        // A query carrega a credencial do destino: só o endpoint (sem query) aparece em exceção e log.
        var extra = new StringBuilder("command=").Append(Uri.EscapeDataString(Required(command, nameof(command))))
            .Append('&').Append(target.BuildQuery());

        if (query is not null)
        {
            foreach (var pair in query)
            {
                if (Array.IndexOf(ProvisioningReservedQueryNames, pair.Key) >= 0)
                {
                    throw new ArgumentException("Nome de query reservado ao roteamento.", nameof(query));
                }

                extra.Append('&').Append(Uri.EscapeDataString(pair.Key)).Append('=').Append(Uri.EscapeDataString(pair.Value ?? string.Empty));
            }
        }

        return _transport.PostRawAsync(
            "request_redirect.fcgi", body ?? new Dictionary<string, object>(), cancellationToken, extra.ToString());
    }
}
