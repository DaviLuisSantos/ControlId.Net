using System;
using System.Threading;
using System.Threading.Tasks;

namespace ControlId.Access;

public partial interface IAccessClient
{
    /// <summary>
    /// <c>remote_camera_calibration.fcgi</c>: abre a tela de calibração e inicia a calibração da câmera (V6).
    /// Retorna assim que o comando é enviado; acompanhe com <see cref="GetCameraCalibrationStatusAsync"/> ou <see cref="WaitCameraCalibrationAsync"/>.
    /// </summary>
    /// <param name="timeoutSeconds">Tempo limite em segundos, de 10 a 600; <c>null</c> deixa o padrão do equipamento (60).</param>
    /// <param name="cancellationToken">Cancelamento.</param>
    Task StartCameraCalibrationAsync(int? timeoutSeconds = null, CancellationToken cancellationToken = default);

    /// <summary><c>cancel_remote_camera_calibration.fcgi</c>: interrompe a calibração iniciada pela API e fecha a tela (V6). Sem efeito se a tela não estiver aberta.</summary>
    Task CancelCameraCalibrationAsync(CancellationToken cancellationToken = default);

    /// <summary><c>camera_calibration_status.fcgi</c>: se a câmera está calibrada e se há calibração em andamento (V6).</summary>
    Task<CameraCalibrationStatus> GetCameraCalibrationStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>Sonda <c>camera_calibration_status.fcgi</c> até a calibração deixar de estar em andamento (V6).</summary>
    /// <param name="pollInterval">Intervalo entre as sondagens; <c>null</c> usa 1 segundo.</param>
    /// <param name="cancellationToken">Cancela a espera (a calibração no equipamento continua).</param>
    /// <returns>O último estado lido.</returns>
    Task<CameraCalibrationStatus> WaitCameraCalibrationAsync(TimeSpan? pollInterval = null, CancellationToken cancellationToken = default);
}
