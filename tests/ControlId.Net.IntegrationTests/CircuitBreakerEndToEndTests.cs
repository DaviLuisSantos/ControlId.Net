using System.Net;
using System.Net.Sockets;
using ControlId.Access;
using ControlId.Simulator;

namespace ControlId.Net.IntegrationTests;

/// <summary>O circuito abre com o equipamento fora do ar e fecha quando ele volta.</summary>
public sealed class CircuitBreakerEndToEndTests
{
    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task Opens_while_the_device_is_down_and_closes_when_it_comes_back()
    {
        var port = FreePort();
        using var device = new AccessClient(new ControlIdDeviceOptions
        {
            BaseAddress = new Uri($"http://127.0.0.1:{port}"),
            Login = "admin",
            Password = "admin",
            Timeout = TimeSpan.FromSeconds(5),
            CircuitBreakerFailureThreshold = 2,
            CircuitBreakerBreakDuration = TimeSpan.FromSeconds(1),
        });
        Assert.Equal(ControlIdCircuitState.Closed, device.CircuitState);

        // Equipamento parado: duas falhas de rede abrem o circuito.
        await Assert.ThrowsAnyAsync<Exception>(() => device.GetSystemInformationAsync());
        await Assert.ThrowsAnyAsync<Exception>(() => device.GetSystemInformationAsync());
        Assert.Equal(ControlIdCircuitState.Open, device.CircuitState);

        var rejected = await Assert.ThrowsAsync<ControlIdCircuitOpenException>(() => device.GetSystemInformationAsync());
        Assert.True(rejected.RetryAfter > TimeSpan.Zero);

        // O equipamento volta na mesma porta; passado o tempo de abertura, a sonda fecha o circuito.
        var app = SimulatorApp.Build(["--urls", $"http://127.0.0.1:{port}", "--Logging:LogLevel:Default", "Warning"], null);
        await app.StartAsync();
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(1.2));
            var info = await device.GetSystemInformationAsync();
            Assert.NotNull(info);
            Assert.Equal(ControlIdCircuitState.Closed, device.CircuitState);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }
}
