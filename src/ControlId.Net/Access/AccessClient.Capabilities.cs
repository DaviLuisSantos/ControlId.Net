using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ControlId.Internal;

namespace ControlId.Access;

public sealed partial class AccessClient
{
    /// <inheritdoc/>
    public async Task<DeviceCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        var info = await _transport.PostAsync<CapabilitiesProbe>("system_information.fcgi", null, cancellationToken).ConfigureAwait(false);
        var unsupported = FirmwareCapabilityMatrix.UnsupportedFor(info.Version);

        _transport.Firmware = info.Version;
        foreach (var command in unsupported)
        {
            _transport.MarkUnsupported(command);
        }

        return new DeviceCapabilities(info.Model, info.Version, unsupported);
    }

    private sealed class CapabilitiesProbe
    {
        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }
    }
}
