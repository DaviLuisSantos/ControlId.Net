using System.Threading;
using System.Threading.Tasks;
using ControlId.Access.Monitoring;

namespace ControlId.AspNetCore;

/// <summary>
/// Recebe as notificações do Monitor mapeadas por <c>MapControlIdMonitor</c>. Registre com
/// <c>services.AddControlIdMonitor&lt;THandler&gt;()</c>; pode haver vários, chamados na ordem de registro.
/// <para>
/// O equipamento espera a resposta HTTP, então mantenha o processamento curto (ou jogue numa fila/<c>Channel</c>).
/// Se o handler lançar exceção, o equipamento recebe 500.
/// </para>
/// </summary>
public interface IMonitorNotificationHandler
{
    /// <summary>Trata uma notificação. Use <c>switch</c> em <paramref name="notification"/> para cada tipo.</summary>
    Task HandleAsync(MonitorNotification notification, CancellationToken cancellationToken);
}
