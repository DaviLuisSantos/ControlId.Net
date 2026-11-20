using System.Net.Http.Json;
using ControlId.Access;
using ControlId.Rep;
using ControlId.Simulator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace ControlId.Net.IntegrationTests;

/// <summary>Um simulador próprio por teste, numa porta livre: os testes rodam em paralelo sem dividir estado.</summary>
internal sealed class RunningSimulator : IAsyncDisposable
{
    private readonly WebApplication _app;

    private RunningSimulator(WebApplication app, Uri baseAddress)
    {
        _app = app;
        BaseAddress = baseAddress;
        Control = new HttpClient { BaseAddress = baseAddress };
    }

    public Uri BaseAddress { get; }

    /// <summary>Cliente HTTP para as rotas <c>/simulator/*</c>.</summary>
    public HttpClient Control { get; }

    /// <param name="configure">Ajusta as opções em código.</param>
    /// <param name="extraArgs">Argumentos de linha de comando extras (ex.: <c>--DisabledCommands=set_system_time</c>).</param>
    public static async Task<RunningSimulator> StartAsync(Action<SimulatorOptions>? configure = null, params string[] extraArgs)
    {
        var app = SimulatorApp.Build(["--urls", "http://127.0.0.1:0", "--Logging:LogLevel:Default", "Warning", .. extraArgs], configure);
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.GetRequiredFeature<IServerAddressesFeature>().Addresses.First();
        return new RunningSimulator(app, new Uri(address));
    }

    /// <summary>Sobe um simulador do REP iDClass (<c>Simulator:Kind=Rep</c>).</summary>
    public static Task<RunningSimulator> StartRepAsync(Action<SimulatorOptions>? configure = null) =>
        StartAsync(o =>
        {
            o.Kind = SimulatorOptions.KindRep;
            configure?.Invoke(o);
        });

    public RepClient CreateRepClient(RepMode mode = RepMode.Portaria671, string login = "admin", string password = "admin") =>
        new(new ControlIdDeviceOptions { BaseAddress = BaseAddress, Login = login, Password = password, Timeout = TimeSpan.FromSeconds(10) }, mode);

    public AccessClient CreateClient(string login = "admin", string password = "admin") =>
        new(new ControlIdDeviceOptions { BaseAddress = BaseAddress, Login = login, Password = password, Timeout = TimeSpan.FromSeconds(10) });

    /// <summary>Uma pessoa se identifica no equipamento.</summary>
    public async Task<long> IdentifyAsync(long? userId = null, long? cardValue = null)
    {
        using var response = await Control.PostAsJsonAsync("/simulator/identify", new { userId, cardValue });
        response.EnsureSuccessStatusCode();
        var log = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        return log.GetProperty("id").GetInt64();
    }

    /// <summary>Quantas requisições o simulador recebeu do comando (sem o <c>.fcgi</c>), pelo contador de <c>/simulator/state</c>.</summary>
    public async Task<long> RequestCountAsync(string command)
    {
        var state = await Control.GetFromJsonAsync<System.Text.Json.JsonElement>("/simulator/state");
        return state.GetProperty("requests").TryGetProperty(command, out var count) ? count.GetInt64() : 0;
    }

    /// <summary>Se o <c>general.online</c> está ligado no simulador.</summary>
    public async Task<bool> IsOnlineAsync()
    {
        var state = await Control.GetFromJsonAsync<System.Text.Json.JsonElement>("/simulator/state");
        return state.GetProperty("online").GetBoolean();
    }

    public async Task ExpireSessionsAsync()
    {
        using var response = await Control.PostAsync("/simulator/expire-sessions", null);
        response.EnsureSuccessStatusCode();
    }

    public async ValueTask DisposeAsync()
    {
        Control.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
