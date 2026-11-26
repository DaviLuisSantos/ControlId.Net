using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ControlId.Internal;

namespace ControlId.Access;

public sealed partial class AccessClient
{
    /// <inheritdoc/>
    public async Task<bool> GetOsdpInstallationModeAsync(CancellationToken cancellationToken = default) =>
        (await GetJsonAsync<OsdpInstallationModeResponse>("get_osdp_installation_mode.fcgi", cancellationToken).ConfigureAwait(false)).InstallationMode;

    /// <inheritdoc/>
    public Task<OsdpScbkInfo> GetOsdpScbkAsync(CancellationToken cancellationToken = default) =>
        _transport.PostAsync<OsdpScbkInfo>("osdp_scbk.fcgi", new Dictionary<string, object>(), cancellationToken);

    /// <inheritdoc/>
    public Task SetOsdpScbkAsync(string scbk, CancellationToken cancellationToken = default)
    {
        OsdpRequireHex(scbk, nameof(scbk));
        return _transport.PostRawAsync("osdp_scbk.fcgi", new Dictionary<string, object> { ["scbk"] = scbk }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<int, int>> GetOsdpConnectionStatusAsync(int panelId, CancellationToken cancellationToken = default)
    {
        var text = await _transport.PostRawAsync(
            "osdp_get_connection_status.fcgi", new Dictionary<string, object> { ["panel_id"] = panelId }, cancellationToken).ConfigureAwait(false);
        var raw = ControlIdJson.Deserialize<Dictionary<string, int>>(text);
        var result = new Dictionary<int, int>(raw.Count);
        foreach (var pair in raw)
        {
            if (!int.TryParse(pair.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var address))
            {
                throw new FormatException($"Endereço OSDP inesperado na resposta: '{pair.Key}'.");
            }

            result[address] = pair.Value;
        }

        return result;
    }

    /// <inheritdoc/>
    public Task<OsdpOutputCapabilities> GetOsdpOutputCapabilitiesAsync(int address, CancellationToken cancellationToken = default) =>
        _transport.PostAsync<OsdpOutputCapabilities>(
            "osdp_get_output_capabilities.fcgi", new Dictionary<string, object> { ["address"] = address }, cancellationToken);

    /// <inheritdoc/>
    public Task SetOsdpSecureKeyAsync(int panelId, int address, string scbkHex, CancellationToken cancellationToken = default)
    {
        OsdpRequireHex(scbkHex, nameof(scbkHex));
        return _transport.PostRawAsync(
            "osdp_set_secure_key.fcgi",
            new Dictionary<string, object> { ["panel_id"] = panelId, ["address"] = address, ["scbk_hex"] = scbkHex },
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task SetOsdpSecureTypeAsync(int panelId, int address, OsdpSecureType secureType, CancellationToken cancellationToken = default)
    {
        if (secureType is not (OsdpSecureType.Insecure or OsdpSecureType.Installation or OsdpSecureType.SecureOnly))
        {
            throw new ArgumentOutOfRangeException(nameof(secureType));
        }

        return _transport.PostRawAsync(
            "osdp_set_secure_type.fcgi",
            new Dictionary<string, object> { ["panel_id"] = panelId, ["address"] = address, ["secure_type"] = (int)secureType },
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task RefreshOsdpAsync(CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync("osdp_refresh.fcgi", new Dictionary<string, object>(), cancellationToken);

    // A mensagem nunca inclui o valor: a SCBK é um segredo.
    private static void OsdpRequireHex(string value, string paramName)
    {
        Required(value, paramName);
        if (value.Length % 2 != 0)
        {
            throw new ArgumentException("A chave precisa ter um número par de dígitos hexadecimais.", paramName);
        }

        foreach (var c in value)
        {
            var hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
            if (!hex)
            {
                throw new ArgumentException("A chave precisa conter apenas dígitos hexadecimais.", paramName);
            }
        }
    }

    private sealed class OsdpInstallationModeResponse
    {
        [JsonPropertyName("installation_mode")]
        public bool InstallationMode { get; set; }
    }
}
