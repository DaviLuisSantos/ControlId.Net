using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ControlId.Access;

public sealed partial class AccessClient
{
    // O equipamento recusa esta senha; é um valor de fábrica público na documentação, então pode aparecer na mensagem.
    private const string ReservedUpdateModePassword = "001818";

    private const int UpdateModePasswordLength = 6;

    /// <inheritdoc/>
    public Task SetMasterPasswordAsync(string password, CancellationToken cancellationToken = default)
    {
        if (password is null)
        {
            throw new ArgumentNullException(nameof(password));
        }

        if (password.Length == 0)
        {
            throw new ArgumentException("Informe a senha mestra.", nameof(password));
        }

        return _transport.PostRawAsync("master_password.fcgi", new Dictionary<string, object> { ["password"] = password }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<MasterPasswordInfo> GetMasterPasswordInfoAsync(CancellationToken cancellationToken = default) =>
        _transport.PostAsync<MasterPasswordInfo>("master_password.fcgi", new Dictionary<string, object>(), cancellationToken);

    /// <inheritdoc/>
    public Task SetUpdateModePasswordAsync(string password, CancellationToken cancellationToken = default)
    {
        if (password is null)
        {
            throw new ArgumentNullException(nameof(password));
        }

        // A mensagem nunca inclui o valor recebido.
        if (!IsValidUpdateModePassword(password))
        {
            throw new ArgumentException(
                $"A senha do modo de update deve ter exatamente {UpdateModePasswordLength} dígitos numéricos e não pode ser {ReservedUpdateModePassword}.",
                nameof(password));
        }

        return _transport.PostRawAsync("recovery_password.fcgi", new Dictionary<string, object> { ["password"] = password }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task RemoveUpdateModePasswordAsync(CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync("recovery_password.fcgi", new Dictionary<string, object> { ["enabled"] = false }, cancellationToken);

    /// <inheritdoc/>
    public async Task<bool> GetUpdateModePasswordStatusAsync(CancellationToken cancellationToken = default)
    {
        var response = await _transport.PostAsync<RecoveryPasswordResponse>(
            "recovery_password.fcgi", new Dictionary<string, object>(), cancellationToken).ConfigureAwait(false);
        return response.Enabled;
    }

    private static bool IsValidUpdateModePassword(string password)
    {
        if (password.Length != UpdateModePasswordLength || string.Equals(password, ReservedUpdateModePassword, StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var c in password)
        {
            if (c < '0' || c > '9')
            {
                return false;
            }
        }

        return true;
    }

    private sealed class RecoveryPasswordResponse
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }
    }
}
