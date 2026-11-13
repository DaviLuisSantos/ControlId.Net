using System.Net;
using System.Net.Http.Json;
using ControlId.Access;
using ControlId.Access.Monitoring;
using ControlId.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ControlId.Dashboard;

/// <summary>Fala com o equipamento pelo <see cref="IAccessClient"/>: Monitor, usuários, porta e a identificação do simulador.</summary>
public sealed class DeviceService(
    IControlIdClientFactory devices,
    IHttpClientFactory httpClientFactory,
    DashboardState state,
    IOptions<DashboardOptions> options)
{
    /// <summary>Aponta o Monitor do equipamento para este dashboard.</summary>
    public Task ConfigureMonitorAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        return Client.ConfigureMonitorAsync(
            new MonitorConfiguration
            {
                Hostname = settings.MonitorHostname,
                Port = settings.MonitorPort,
                Path = "api/notifications",
                AliveInterval = TimeSpan.FromSeconds(10),
            },
            cancellationToken);
    }

    /// <summary>Consulta o equipamento (e conta como "online" se responder).</summary>
    public async Task ProbeAsync(CancellationToken cancellationToken)
    {
        var info = await Client.GetSystemInformationAsync(cancellationToken);
        state.SetOnline($"{info.Serial} · firmware {info.Version}");
    }

    /// <summary>Recarrega os nomes dos usuários.</summary>
    public async Task RefreshUsersAsync(CancellationToken cancellationToken)
    {
        var users = await Client.GetUsersAsync(cancellationToken);
        state.SetUsers(users
            .Where(u => u.Id is not null)
            .Select(u => KeyValuePair.Create(u.Id!.Value, u.Name ?? $"Usuário {u.Id}")));
    }

    /// <summary>Abre a porta.</summary>
    public Task OpenDoorAsync(int door, CancellationToken cancellationToken) => Client.OpenDoorAsync(door, cancellationToken);

    /// <summary>
    /// Simula uma pessoa diante do equipamento (<c>POST /simulator/identify</c>, que só o simulador tem). Informe o usuário
    /// ou, para provocar um acesso negado, um cartão que ninguém tem.
    /// </summary>
    public async Task IdentifyAsync(long? userId, long? cardValue, CancellationToken cancellationToken)
    {
        var http = httpClientFactory.CreateClient(nameof(DeviceService));
        using var response = await http.PostAsJsonAsync("simulator/identify", new { userId, cardValue }, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException("Este equipamento não é o simulador (não tem /simulator/identify).");
        }

        response.EnsureSuccessStatusCode();
    }

    private IAccessClient Client => devices.GetAccessClient(DashboardOptions.DeviceName);
}
