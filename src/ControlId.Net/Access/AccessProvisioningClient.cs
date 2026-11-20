using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ControlId.Internal;

namespace ControlId.Access;

/// <summary>Cria o <see cref="IAccessProvisioningClient"/> (ADR 0024).</summary>
public static class AccessProvisioningClientExtensions
{
    /// <summary>
    /// Cria o cliente de operações sensíveis (chaves do leitor HID e código OEM) sobre a mesma conexão e sessão de
    /// <paramref name="client"/>. O cliente criado não precisa ser descartado: ele vive enquanto <paramref name="client"/>
    /// viver.
    /// </summary>
    /// <exception cref="NotSupportedException"><paramref name="client"/> não é um <see cref="AccessClient"/>.</exception>
    public static IAccessProvisioningClient CreateProvisioningClient(this IAccessClient client)
    {
        if (client is null)
        {
            throw new ArgumentNullException(nameof(client));
        }

        return client is AccessClient access
            ? new AccessProvisioningClient(access.Transport)
            : throw new NotSupportedException("O cliente de provisionamento só pode ser criado a partir de um AccessClient.");
    }
}

internal sealed class AccessProvisioningClient : IAccessProvisioningClient
{
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(1);
    private static readonly Regex OidFormat = new(@"^\d+(\.\d+){5}$", RegexOptions.CultureInvariant);
    private static readonly Regex FirmwareFormat = new(@"^\d+\.\d+\.\d+$", RegexOptions.CultureInvariant);

    private readonly ControlIdTransport _transport;

    internal AccessProvisioningClient(ControlIdTransport transport) => _transport = transport;

    public async Task<bool> InjectHidDefaultKekAsync(string key, string? currentKek = null, string? keyId = null, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object> { ["key"] = Hex(key, nameof(key)) };
        if (currentKek is not null)
        {
            body["kek"] = Hex(currentKek, nameof(currentKek));
        }

        if (keyId is not null)
        {
            if (!HidHex.IsHex(keyId) || keyId.Length != 16)
            {
                throw new ArgumentException("O identificador da chave deve ter 8 bytes (16 dígitos hexadecimais).", nameof(keyId));
            }

            body["key_id"] = keyId;
        }

        return (await _transport.PostAsync<SuccessResponse>("hid_inject_default_kek.fcgi", body, cancellationToken).ConfigureAwait(false)).Success;
    }

    public Task<bool> InjectHidCustomKekAsync(string targetRef, string kekRef, string key, string? kek = null, CancellationToken cancellationToken = default) =>
        InjectWrappedAsync("hid_inject_custom_kek.fcgi", targetRef, kekRef, key, kek, cancellationToken);

    public Task<bool> InjectHidMediaKeyAsync(string targetRef, string kekRef, string key, string? kek = null, CancellationToken cancellationToken = default) =>
        InjectWrappedAsync("hid_inject_media_key.fcgi", targetRef, kekRef, key, kek, cancellationToken);

    public Task<HidKeyInfo> GetHidKeyInfoAsync(string targetRef, CancellationToken cancellationToken = default) =>
        _transport.PostAsync<HidKeyInfo>(
            "hid_get_key_info.fcgi",
            new Dictionary<string, object> { ["target_ref"] = Hex(targetRef, nameof(targetRef)) },
            cancellationToken);

    public Task<bool> SetHidOidAsync(string oid, bool value, CancellationToken cancellationToken = default) =>
        SetOidAsync(oid, value, cancellationToken);

    public Task<bool> SetHidOidAsync(string oid, int value, CancellationToken cancellationToken = default) =>
        SetOidAsync(oid, value, cancellationToken);

    public Task<bool> SetHidOidAsync(string oid, byte[] value, CancellationToken cancellationToken = default)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (value.Length == 0)
        {
            throw new ArgumentException("A sequência de bytes não pode ser vazia.", nameof(value));
        }

        return SetOidAsync(oid, HidHex.FromBytes(value), cancellationToken);
    }

    public async Task<HidOidValue> GetHidOidAsync(string oid, CancellationToken cancellationToken = default)
    {
        var response = await _transport.PostAsync<OidResponse>(
            "hid_get_oid.fcgi",
            new Dictionary<string, object> { ["oid"] = Oid(oid) },
            cancellationToken).ConfigureAwait(false);
        return HidOidValue.FromJson(response.Value);
    }

    public Task<HidConfigResetStatus> ResetHidConfigAsync(DataLossConfirmation confirmation, CancellationToken cancellationToken = default)
    {
        DataLoss.Require(confirmation, nameof(ResetHidConfigAsync));
        return _transport.PostAsync<HidConfigResetStatus>("hid_reset_config.fcgi", new Dictionary<string, object> { ["start"] = true }, cancellationToken);
    }

    public async Task<HidConfigResetStatus> RestoreHidConfigAsync(DataLossConfirmation confirmation, byte[] orcoFile, CancellationToken cancellationToken = default)
    {
        DataLoss.Require(confirmation, nameof(RestoreHidConfigAsync));
        if (orcoFile is null)
        {
            throw new ArgumentNullException(nameof(orcoFile));
        }

        if (orcoFile.Length == 0)
        {
            throw new ArgumentException("O arquivo .orco não pode ser vazio.", nameof(orcoFile));
        }

        var json = await _transport.PostBytesAsync("hid_reset_config.fcgi", orcoFile, "application/octet-stream", cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(json) ? new HidConfigResetStatus() : ControlIdJson.Deserialize<HidConfigResetStatus>(json);
    }

    public Task<HidConfigResetStatus> GetHidConfigResetStatusAsync(CancellationToken cancellationToken = default) =>
        _transport.PostAsync<HidConfigResetStatus>("hid_reset_config.fcgi", null, cancellationToken);

    public async Task<HidConfigResetStatus> WaitHidConfigResetAsync(TimeSpan? pollInterval = null, CancellationToken cancellationToken = default)
    {
        var interval = pollInterval ?? DefaultPollInterval;
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(pollInterval), "O intervalo de consulta deve ser maior que zero.");
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var status = await GetHidConfigResetStatusAsync(cancellationToken).ConfigureAwait(false);
            if (status.IsDone)
            {
                return status;
            }

            await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<string> GetOemCodeAsync(CancellationToken cancellationToken = default)
    {
        var bytes = await _transport.GetBytesAsync("get_oem_code.fcgi", cancellationToken).ConfigureAwait(false);
        return ControlIdJson.Deserialize<OemCodeResponse>(TextDecoding.Decode(bytes, null)).OemCode ?? string.Empty;
    }

    public Task SetOemCodeAsync(string oemCode, string license, CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync(
            "set_oem_code.fcgi",
            new Dictionary<string, object> { ["oem_code"] = Required(oemCode, nameof(oemCode)), ["license"] = Required(license, nameof(license)) },
            cancellationToken);

    public async Task<IReadOnlyList<string>> GetOemDowngradeApprovedVersionsAsync(CancellationToken cancellationToken = default) =>
        (await _transport.PostAsync<VersionsResponse>("get_oem_downgrade_approved_versions.fcgi", null, cancellationToken).ConfigureAwait(false)).Versions
        ?? new List<string>();

    public Task SetOemApprovedFirmwareAsync(string oemCode, string license, string approvedFirmware, CancellationToken cancellationToken = default)
    {
        if (approvedFirmware is null || !FirmwareFormat.IsMatch(approvedFirmware))
        {
            throw new ArgumentException("A versão aprovada deve estar no formato major.minor.patch (ex.: 7.14.0).", nameof(approvedFirmware));
        }

        return SetApprovedFirmwareAsync(oemCode, license, approvedFirmware, cancellationToken);
    }

    public Task ClearOemApprovedFirmwareAsync(string oemCode, string license, CancellationToken cancellationToken = default) =>
        SetApprovedFirmwareAsync(oemCode, license, string.Empty, cancellationToken);

    private Task<string> SetApprovedFirmwareAsync(string oemCode, string license, string approvedFirmware, CancellationToken cancellationToken) =>
        _transport.PostRawAsync(
            "set_oem_approved_fw.fcgi",
            new Dictionary<string, object>
            {
                ["oem_code"] = Required(oemCode, nameof(oemCode)),
                ["license"] = Required(license, nameof(license)),
                ["approved_fw"] = approvedFirmware,
            },
            cancellationToken);

    private async Task<bool> InjectWrappedAsync(string endpoint, string targetRef, string kekRef, string key, string? kek, CancellationToken cancellationToken)
    {
        var body = new Dictionary<string, object>
        {
            ["target_ref"] = Hex(targetRef, nameof(targetRef)),
            ["kek_ref"] = Hex(kekRef, nameof(kekRef)),
            ["key"] = Hex(key, nameof(key)),
        };
        if (kek is not null)
        {
            body["kek"] = Hex(kek, nameof(kek));
        }

        return (await _transport.PostAsync<SuccessResponse>(endpoint, body, cancellationToken).ConfigureAwait(false)).Success;
    }

    private async Task<bool> SetOidAsync(string oid, object value, CancellationToken cancellationToken) =>
        (await _transport.PostAsync<SuccessResponse>(
            "hid_set_oid.fcgi",
            new Dictionary<string, object> { ["oid"] = Oid(oid), ["value"] = value },
            cancellationToken).ConfigureAwait(false)).Success;

    // A mensagem nunca inclui o valor recebido: pode ser uma chave.
    private static string Hex(string value, string paramName)
    {
        if (value is null)
        {
            throw new ArgumentNullException(paramName);
        }

        return HidHex.IsHex(value)
            ? value
            : throw new ArgumentException("O valor deve ser uma string hexadecimal não vazia com número par de dígitos.", paramName);
    }

    private static string Oid(string oid) =>
        oid is not null && OidFormat.IsMatch(oid)
            ? oid
            : throw new ArgumentException("O OID deve ter seis partes numéricas separadas por ponto (ex.: 1.1.1.2.3.5).", nameof(oid));

    private static string Required(string value, string paramName) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Valor obrigatório.", paramName) : value;

    private sealed class SuccessResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }
    }

    private sealed class OidResponse
    {
        [JsonPropertyName("value")]
        public JsonElement Value { get; set; }
    }

    private sealed class OemCodeResponse
    {
        [JsonPropertyName("oem_code")]
        public string? OemCode { get; set; }
    }

    private sealed class VersionsResponse
    {
        [JsonPropertyName("versions")]
        public List<string>? Versions { get; set; }
    }
}
