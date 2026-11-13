using Microsoft.Extensions.Options;

namespace ControlId.Dashboard;

/// <summary>
/// Ao iniciar, aponta o Monitor do equipamento para este dashboard e carrega os usuários; depois consulta o equipamento
/// de tempos em tempos para saber se ele está online. Se o equipamento voltar de uma queda, configura o Monitor de novo.
/// </summary>
public sealed class DeviceWorker(DeviceService device, DashboardState state, IOptions<DashboardOptions> options, ILogger<DeviceWorker> logger)
    : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var ready = false;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, options.Value.PollSeconds)));

        do
        {
            try
            {
                if (!ready)
                {
                    await device.ConfigureMonitorAsync(stoppingToken);
                    await device.RefreshUsersAsync(stoppingToken);
                    logger.LogInformation(
                        "Monitor do equipamento apontado para {Host}:{Port}.", options.Value.MonitorHostname, options.Value.MonitorPort);
                    ready = true;
                }

                await device.ProbeAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested && ex is ControlIdException or HttpRequestException or TaskCanceledException)
            {
                // Registra só a queda (e a primeira falha), não uma linha a cada consulta.
                if (ready || state.Snapshot().Problem is null)
                {
                    logger.LogWarning("Equipamento indisponível: {Message}", ex.Message);
                }

                ready = false;
                state.SetOffline(ex.Message);
            }
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
