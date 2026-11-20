using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ControlId.Access;

public sealed partial class AccessClient
{
    private const int DualSidedSecretLength = 64;

    private static readonly TimeSpan SecBoxDefaultPollInterval = TimeSpan.FromSeconds(1);

    /// <inheritdoc/>
    public async Task<bool> DeviceHasSecBoxAsync(CancellationToken cancellationToken = default) =>
        (await GetJsonAsync<SecBoxHasSecBoxResponse>("device_has_secbox.fcgi", cancellationToken).ConfigureAwait(false)).HasSecBox;

    /// <inheritdoc/>
    public async Task<bool> IsSecBoxActiveAsync(CancellationToken cancellationToken = default) =>
        (await GetJsonAsync<SecBoxIsActiveResponse>("secbox_is_active.fcgi", cancellationToken).ConfigureAwait(false)).IsActive;

    /// <inheritdoc/>
    public async Task<string> GetSecBoxSerialNumberAsync(CancellationToken cancellationToken = default) =>
        (await GetJsonAsync<SecBoxSerialResponse>("secbox_serial_number.fcgi", cancellationToken).ConfigureAwait(false)).Serial ?? string.Empty;

    /// <inheritdoc/>
    public async Task<int> GetSecBoxFirmwareVersionAsync(CancellationToken cancellationToken = default) =>
        (await GetJsonAsync<SecBoxFirmwareVersionResponse>("update_secbox_firmware_version.fcgi", cancellationToken).ConfigureAwait(false)).FirmwareVersion;

    /// <inheritdoc/>
    public async Task<bool> UpdateSecBoxFirmwareAsync(CancellationToken cancellationToken = default) =>
        (await _transport.PostAsync<SecBoxFirmwareUpdatedResponse>("update_secbox_firmware.fcgi", null, cancellationToken).ConfigureAwait(false)).IsUpdated;

    /// <inheritdoc/>
    public Task<SecBoxFirmwareUpdateStatus> GetSecBoxFirmwareUpdateStatusAsync(CancellationToken cancellationToken = default) =>
        GetJsonAsync<SecBoxFirmwareUpdateStatus>("update_secbox_firmware_status.fcgi", cancellationToken);

    /// <inheritdoc/>
    public async Task<SecBoxFirmwareUpdateStatus> WaitSecBoxFirmwareUpdateAsync(TimeSpan? pollInterval = null, CancellationToken cancellationToken = default)
    {
        var interval = pollInterval ?? SecBoxDefaultPollInterval;
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(pollInterval), "O intervalo de consulta deve ser maior que zero.");
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var status = await GetSecBoxFirmwareUpdateStatusAsync(cancellationToken).ConfigureAwait(false);
            if (status.IsFinished)
            {
                return status;
            }

            await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public Task<SecBoxKeyChangeResult> ChangeSecBoxCryptoKeyToDefaultAsync(DataLossConfirmation confirmation, CancellationToken cancellationToken = default)
    {
        DataLoss.Require(confirmation, nameof(ChangeSecBoxCryptoKeyToDefaultAsync));
        return _transport.PostAsync<SecBoxKeyChangeResult>("change_crypto_key_default.fcgi", null, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SecBoxKeyChangeResult> ChangeSecBoxCryptoKeyToRandomAsync(DataLossConfirmation confirmation, CancellationToken cancellationToken = default)
    {
        DataLoss.Require(confirmation, nameof(ChangeSecBoxCryptoKeyToRandomAsync));
        return _transport.PostAsync<SecBoxKeyChangeResult>("change_crypto_key_random.fcgi", null, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SecBoxKeyChangeResult> ResetSecBoxCryptoKeyAsync(DataLossConfirmation confirmation, CancellationToken cancellationToken = default)
    {
        DataLoss.Require(confirmation, nameof(ResetSecBoxCryptoKeyAsync));
        return _transport.PostAsync<SecBoxKeyChangeResult>("reset_crypto_key.fcgi", null, cancellationToken);
    }

    /// <inheritdoc/>
    public Task SetDualSidedSecretAsync(string secret, CancellationToken cancellationToken = default)
    {
        if (secret is null)
        {
            throw new ArgumentNullException(nameof(secret));
        }

        // A mensagem nunca inclui o valor recebido.
        if (!IsValidDualSidedSecret(secret))
        {
            throw new ArgumentException(
                $"O segredo do dual sided deve ter exatamente {DualSidedSecretLength} caracteres hexadecimais (32 bytes).",
                nameof(secret));
        }

        return _transport.PostRawAsync("set_dual_sided_secret.fcgi", new Dictionary<string, object> { ["secret"] = secret }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<DualSidedStatus> GetDualSidedStatusAsync(CancellationToken cancellationToken = default) =>
        _transport.PostAsync<DualSidedStatus>("get_dual_sided_status.fcgi", null, cancellationToken);

    private static bool IsValidDualSidedSecret(string secret)
    {
        if (secret.Length != DualSidedSecretLength)
        {
            return false;
        }

        foreach (var c in secret)
        {
            var hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
            if (!hex)
            {
                return false;
            }
        }

        return true;
    }

    private sealed class SecBoxHasSecBoxResponse
    {
        [JsonPropertyName("has_secbox")]
        public bool HasSecBox { get; set; }
    }

    private sealed class SecBoxIsActiveResponse
    {
        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; }
    }

    private sealed class SecBoxSerialResponse
    {
        [JsonPropertyName("serial")]
        public string? Serial { get; set; }
    }

    private sealed class SecBoxFirmwareVersionResponse
    {
        [JsonPropertyName("fw_version")]
        public int FirmwareVersion { get; set; }
    }

    private sealed class SecBoxFirmwareUpdatedResponse
    {
        [JsonPropertyName("isUpdated")]
        public bool IsUpdated { get; set; }
    }
}
