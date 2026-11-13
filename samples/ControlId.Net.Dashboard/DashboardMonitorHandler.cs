using ControlId.Access;
using ControlId.Access.Monitoring;
using ControlId.AspNetCore;

namespace ControlId.Dashboard;

/// <summary>Recebe o que o equipamento notifica e atualiza o <see cref="DashboardState"/>.</summary>
public sealed class DashboardMonitorHandler(DashboardState state, DeviceService device, ILogger<DashboardMonitorHandler> logger)
    : IMonitorNotificationHandler
{
    /// <inheritdoc/>
    public async Task HandleAsync(MonitorNotification notification, CancellationToken cancellationToken)
    {
        state.NotificationReceived();

        switch (notification)
        {
            case DaoNotification dao:
                foreach (var log in dao.GetInsertedAccessLogs())
                {
                    await AddAccessAsync(log, cancellationToken);
                }

                break;
            case DoorNotification { Door: { } door }:
                state.SetDoor((int)door.Id, door.Open);
                break;
        }
    }

    private async Task AddAccessAsync(AccessLog log, CancellationToken cancellationToken)
    {
        var userId = log.UserId is > 0 ? log.UserId : null;
        string? name = null;
        if (userId is { } id)
        {
            name = state.FindUserName(id);
            if (name is null)
            {
                // Usuário cadastrado depois da última carga: recarrega uma vez.
                try
                {
                    await device.RefreshUsersAsync(cancellationToken);
                    name = state.FindUserName(id);
                }
                catch (Exception ex) when (ex is ControlIdException or HttpRequestException)
                {
                    logger.LogWarning("Não foi possível carregar os usuários do equipamento: {Message}", ex.Message);
                }
            }
        }

        var time = log.Time > 0 ? DateTimeOffset.FromUnixTimeSeconds(log.Time).ToLocalTime() : DateTimeOffset.Now;
        state.AddEvent(new AccessEntry(time, userId, name ?? (userId is null ? "Não identificado" : $"Usuário {userId}"), log.EventType, log.PortalId));
    }
}
