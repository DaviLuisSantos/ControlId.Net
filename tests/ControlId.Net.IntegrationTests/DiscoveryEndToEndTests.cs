using System.Net;
using System.Net.Sockets;
using ControlId.Discovery;

namespace ControlId.Net.IntegrationTests;

/// <summary>Descoberta de equipamentos contra simuladores reais e uma porta fechada.</summary>
public sealed class DiscoveryEndToEndTests
{
    [Fact]
    public async Task Scan_finds_three_simulators_and_reports_the_closed_port()
    {
        await using var first = await RunningSimulator.StartAsync(o => o.Serial = "SIM-A");
        await using var second = await RunningSimulator.StartAsync(o => o.Serial = "SIM-B");
        await using var third = await RunningSimulator.StartAsync(o => o.Serial = "SIM-C");
        var closed = new Uri($"http://127.0.0.1:{FreePort()}/");

        var found = await ControlIdDiscovery.ScanAsync(
            new[] { first.BaseAddress, second.BaseAddress, closed, third.BaseAddress },
            Array.Empty<DiscoveryCredential>(),
            new DiscoveryOptions { TryFactoryDefaults = true, ConnectTimeout = TimeSpan.FromSeconds(10), Timeout = TimeSpan.FromSeconds(15) });

        Assert.Equal(4, found.Count);
        var devices = found.Where(d => d.Status == DiscoveryStatus.Found).ToList();
        Assert.Equal(new[] { "SIM-A", "SIM-B", "SIM-C" }, devices.Select(d => d.Serial).OrderBy(s => s).ToArray());
        Assert.All(devices, d => Assert.Equal("admin", d.CredentialLogin));
        Assert.Equal(DiscoveryStatus.PortClosed, found.Single(d => d.Address == closed).Status);
        Assert.Equal(first.BaseAddress, found[0].Address);
        Assert.Equal(closed, found[2].Address);
    }

    [Fact]
    public async Task Scan_reports_a_rejected_credential()
    {
        await using var sim = await RunningSimulator.StartAsync();

        var found = await ControlIdDiscovery.ScanAsync(
            new[] { sim.BaseAddress },
            new[] { new DiscoveryCredential("admin", "senha-errada") });

        Assert.Equal(DiscoveryStatus.CredentialRejected, Assert.Single(found).Status);
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
