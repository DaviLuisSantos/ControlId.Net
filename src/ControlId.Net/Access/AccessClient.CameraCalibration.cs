using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace ControlId.Access;

public sealed partial class AccessClient
{
    private const int CameraCalibrationMinTimeoutSeconds = 10;

    private const int CameraCalibrationMaxTimeoutSeconds = 600;

    private static readonly TimeSpan CameraCalibrationDefaultPollInterval = TimeSpan.FromSeconds(1);

    /// <inheritdoc/>
    public Task StartCameraCalibrationAsync(int? timeoutSeconds = null, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object>();
        if (timeoutSeconds is { } timeout)
        {
            if (timeout < CameraCalibrationMinTimeoutSeconds || timeout > CameraCalibrationMaxTimeoutSeconds)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(timeoutSeconds),
                    timeout.ToString(CultureInfo.InvariantCulture),
                    $"O tempo limite precisa estar entre {CameraCalibrationMinTimeoutSeconds} e {CameraCalibrationMaxTimeoutSeconds} segundos.");
            }

            body["timeout"] = timeout;
        }

        return _transport.PostRawAsync("remote_camera_calibration.fcgi", body, cancellationToken);
    }

    /// <inheritdoc/>
    public Task CancelCameraCalibrationAsync(CancellationToken cancellationToken = default) =>
        _transport.PostRawAsync("cancel_remote_camera_calibration.fcgi", null, cancellationToken);

    /// <inheritdoc/>
    public Task<CameraCalibrationStatus> GetCameraCalibrationStatusAsync(CancellationToken cancellationToken = default) =>
        _transport.PostAsync<CameraCalibrationStatus>("camera_calibration_status.fcgi", null, cancellationToken);

    /// <inheritdoc/>
    public async Task<CameraCalibrationStatus> WaitCameraCalibrationAsync(TimeSpan? pollInterval = null, CancellationToken cancellationToken = default)
    {
        var interval = pollInterval ?? CameraCalibrationDefaultPollInterval;
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(pollInterval), "O intervalo de sondagem precisa ser positivo.");
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var status = await GetCameraCalibrationStatusAsync(cancellationToken).ConfigureAwait(false);
            if (!status.Running)
            {
                return status;
            }

            await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
        }
    }
}
