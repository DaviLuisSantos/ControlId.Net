using System.Net.Http.Json;
using ControlId.Access;
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

    public static async Task<RunningSimulator> StartAsync(Action<SimulatorOptions>? configure = null)
    {
        var app = SimulatorApp.Build(["--urls", "http://127.0.0.1:0", "--Logging:LogLevel:Default", "Warning"], configure);
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.GetRequiredFeature<IServerAddressesFeature>().Addresses.First();
        return new RunningSimulator(app, new Uri(address));
    }

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
